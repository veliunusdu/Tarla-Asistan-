# Render Production Deployment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deploy the .NET API and Next.js panel from `main` to always-on Render web services backed by Supabase, Firebase, R2, and DeepSeek.

**Architecture:** A Render Blueprint defines two Docker web services in Singapore: the API and the web panel. Render passes the API service's generated external URL to the web build and the web service's generated external URL to API CORS, avoiding hard-coded `onrender.com` values. Secrets stay in Render, while the Firebase Admin JSON is supplied as a Render secret file.

**Tech Stack:** Render Blueprints, Docker, ASP.NET Core 8, EF Core/Npgsql, Next.js 16, Supabase PostgreSQL, Firebase Auth/FCM, Cloudflare R2, DeepSeek.

**Spec:** `docs/superpowers/specs/2026-08-30-render-production-deployment-design.md`

## Global Constraints

- Deploy only from the `main` branch.
- Use Render region `singapore`, matching the Supabase pooler's Asia-Pacific location.
- Use paid `0.5c-512mb` instances for both web services; Render Free instances sleep after inactivity and are not production-safe.
- Store no secrets in Git, `render.yaml`, Docker build arguments, browser-visible variables, or logs.
- Rotate the Supabase password, R2 key pair, DeepSeek key, and Firebase Admin service account before entering production values.
- Keep the current local Docker Compose workflow unchanged.
- Do not stage unrelated mobile generated files or `web/tsconfig.tsbuildinfo`.

---

## File Structure

- Create: `render.yaml` - Render Blueprint for the API and web services.
- Create: `backend/scripts/start-render.sh` - binds the API to Render's assigned `PORT` at runtime.
- Modify: `backend/Dockerfile` - installs and executes the Render startup script.
- Modify: `backend/src/TarlaAsistani.Infrastructure/DependencyInjection.cs` - accepts the conventional `DATABASE_URL` secret in addition to .NET connection-string configuration.
- Modify: `web/lib/api.ts` - derives the `/api/v1` URL from a public API origin supplied by Render.
- Create: `backend/tests/TarlaAsistani.UnitTests/DependencyInjectionTests.cs` - proves `DATABASE_URL` config creates a valid PostgreSQL provider setup.
- Create: `docs/RENDER_DEPLOYMENT.md` - operator checklist with exact Render Dashboard, Firebase, Supabase, and test steps.

## Task 1: Accept A Standard Database URL Secret

**Files:**
- Modify: `backend/src/TarlaAsistani.Infrastructure/DependencyInjection.cs:17-19`
- Create: `backend/tests/TarlaAsistani.UnitTests/DependencyInjectionTests.cs`

**Interfaces:**
- Consumes: `DATABASE_URL` as either `postgres://` or `postgresql://`.
- Produces: `ApplicationDbContext` configured with the normalized Npgsql connection string.

- [ ] **Step 1: Write the failing configuration test**

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TarlaAsistani.Infrastructure;
using TarlaAsistani.Infrastructure.Persistence;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_UsesDatabaseUrlWhenDefaultConnectionIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DATABASE_URL"] = "postgresql://tarla:secret@db.example.com:5432/tarla"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Contains("Host=db.example.com", db.Database.GetConnectionString());
        Assert.Contains("Database=tarla", db.Database.GetConnectionString());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/TarlaAsistani.UnitTests --no-restore --filter FullyQualifiedName~DependencyInjectionTests --nologo`

Expected: FAIL because `AddInfrastructure` reads only `ConnectionStrings:DefaultConnection`.

- [ ] **Step 3: Add the fallback before URI normalization**

```csharp
var connectionString = NormalizeConnectionString(
    configuration.GetConnectionString("DefaultConnection")
    ?? configuration["DATABASE_URL"]
    ?? Environment.GetEnvironmentVariable("DATABASE_URL"));
```

- [ ] **Step 4: Run focused and complete verification**

Run: `dotnet test backend/tests/TarlaAsistani.UnitTests --no-restore --filter FullyQualifiedName~DependencyInjectionTests --nologo`

Expected: PASS with an Npgsql connection string containing the supplied host and database.

Run: `dotnet test backend/TarlaAsistani.slnx --no-restore --nologo`

Expected: all unit and integration tests pass.

- [ ] **Step 5: Commit**

```bash
git add backend/src/TarlaAsistani.Infrastructure/DependencyInjection.cs backend/tests/TarlaAsistani.UnitTests/DependencyInjectionTests.cs
git commit -m "feat: support DATABASE_URL configuration"
```

## Task 2: Make The API Docker Image Render-Port Compatible

**Files:**
- Create: `backend/scripts/start-render.sh`
- Modify: `backend/Dockerfile:25-26`

**Interfaces:**
- Consumes: Render-provided `PORT`, or no `PORT` in local Docker.
- Produces: an API process bound to `0.0.0.0:$PORT`, defaulting to `8080` locally.

- [ ] **Step 1: Create the runtime start script**

```sh
#!/bin/sh
set -eu

api_port="${PORT:-8080}"
exec dotnet TarlaAsistani.API.dll --urls "http://0.0.0.0:${api_port}"
```

- [ ] **Step 2: Update the final Docker stage**

```dockerfile
COPY --from=build /app/publish .
COPY scripts/start-render.sh /app/start-render.sh
RUN chmod +x /app/start-render.sh
ENTRYPOINT ["/app/start-render.sh"]
```

Keep `EXPOSE 8080`; it remains the local default port.

- [ ] **Step 3: Build and verify the image**

Run: `docker build -t tarla-asistani-api-render ./backend`

Expected: successful Docker build with no secret copied into the image.

Run: `docker run --rm -d --name tarla-api-port-test -e PORT=18080 -e AUTO_MIGRATE=false -e ConnectionStrings__DefaultConnection="Host=host.docker.internal;Port=5432;Database=tarla_asistani;Username=tarla;Password=tarla-local" -p 18080:18080 tarla-asistani-api-render`

Run: `curl -f http://localhost:18080/health/live`

Expected: HTTP `200` and `{"status":"ok"}`.

- [ ] **Step 4: Stop the test container and commit**

Run: `docker stop tarla-api-port-test`

Expected: the named temporary test container is removed by `--rm`.

```bash
git add backend/Dockerfile backend/scripts/start-render.sh
git commit -m "fix: bind API to Render port"
```

## Task 3: Add The Render Blueprint And Dynamic Web API Origin

**Files:**
- Create: `render.yaml`
- Modify: `web/lib/api.ts:8-10`

**Interfaces:**
- Consumes: `RENDER_EXTERNAL_URL` from the API service at web build time.
- Produces: `NEXT_PUBLIC_API_ORIGIN`, which the web client converts to `<origin>/api/v1`.
- Consumes: `RENDER_EXTERNAL_URL` from the web service at API runtime.
- Produces: `Cors__AllowedOrigins__0` without a manually copied `onrender.com` URL.

- [ ] **Step 1: Extend the web API URL resolution**

```ts
const API_ORIGIN = process.env.NEXT_PUBLIC_API_ORIGIN;
const API_URL =
  process.env.NEXT_PUBLIC_API_URL ??
  (API_ORIGIN ? `${API_ORIGIN}/api/v1` : "http://localhost:8000/api/v1");
```

- [ ] **Step 2: Create the Blueprint without secret values**

```yaml
services:
  - type: web
    name: tarla-asistani-api
    runtime: docker
    region: singapore
    plan: 0.5c-512mb
    branch: main
    autoDeploy: true
    dockerContext: ./backend
    dockerfilePath: ./backend/Dockerfile
    healthCheckPath: /health/ready
    envVars:
      - key: ASPNETCORE_ENVIRONMENT
        value: Production
      - key: DATABASE_URL
        sync: false
      - key: JWT_SECRET
        generateValue: true
      - key: Cors__AllowedOrigins__0
        fromService:
          name: tarla-asistani-web
          type: web
          envVarKey: RENDER_EXTERNAL_URL
      - key: MEDIA_STORAGE_PROVIDER
        value: r2
      - key: R2_ACCOUNT_ID
        sync: false
      - key: R2_BUCKET
        sync: false
      - key: R2_ACCESS_KEY_ID
        sync: false
      - key: R2_SECRET_ACCESS_KEY
        sync: false
      - key: AI_CHAT_PROVIDER
        value: deepseek
      - key: DEEPSEEK_API_KEY
        sync: false
      - key: DEEPSEEK_BASE_URL
        value: https://api.deepseek.com
      - key: DEEPSEEK_MODEL
        value: deepseek-chat
      - key: FIREBASE_AUTH_ENABLED
        value: "true"
      - key: FIREBASE_PROJECT_ID
        value: demo2-c4265
      - key: Firebase__CredentialsPath
        value: /etc/secrets/firebase-service-account.json
      - key: PUBLIC_API_ORIGIN
        fromService:
          name: tarla-asistani-api
          type: web
          envVarKey: RENDER_EXTERNAL_URL

  - type: web
    name: tarla-asistani-web
    runtime: docker
    region: singapore
    plan: 0.5c-512mb
    branch: main
    autoDeploy: true
    dockerContext: ./web
    dockerfilePath: ./web/Dockerfile
    healthCheckPath: /login
    envVars:
      - key: NEXT_PUBLIC_API_ORIGIN
        fromService:
          name: tarla-asistani-api
          type: web
          envVarKey: PUBLIC_API_ORIGIN
```

- [ ] **Step 3: Validate code and the Blueprint**

Run: `pnpm --dir web run lint`

Run: `pnpm --dir web run build`

Run: `docker compose config --quiet`

Expected: all three commands exit with code `0`.

Run: `render blueprints validate render.yaml`

Expected: validation succeeds without an unknown field or unresolved service reference.

- [ ] **Step 4: Commit**

```bash
git add render.yaml web/lib/api.ts
git commit -m "feat: add Render production blueprint"
```

## Task 4: Write The Production Operator Checklist

**Files:**
- Create: `docs/RENDER_DEPLOYMENT.md`

**Interfaces:**
- Consumes: the `render.yaml` Blueprint and rotated credentials.
- Produces: a repeatable, non-secret deployment and rollback procedure.

- [ ] **Step 1: Document credential rotation**

Include this exact pre-deploy order:

```text
1. Reset the Supabase database password and obtain a new pooled PostgreSQL URL.
2. Delete the exposed R2 access key pair and create a replacement restricted to tarla-asistani-media.
3. Revoke the exposed DeepSeek API key and create a replacement key.
4. Create a new Firebase Admin service account key and delete the old key from Firebase.
5. Update only local ignored .env files and the Render secret entry form with the new values.
```

- [ ] **Step 2: Document Render and Firebase configuration**

List the API secret keys `DATABASE_URL`, `R2_ACCOUNT_ID`, `R2_BUCKET`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, and `DEEPSEEK_API_KEY`. Document the Firebase Secret File name `firebase-service-account.json` and its mounted path `/etc/secrets/firebase-service-account.json`.

Document these Firebase Console actions:

```text
Authentication > Sign-in method > Phone: enable.
Authentication > Settings > Authorized domains: add the Render web hostname without https://.
Cloud Messaging: verify Android package com.tarlaasistani.pilot.
Project settings: register release SHA-1 and SHA-256 fingerprints for the Android signing key.
```

- [ ] **Step 3: Document deployment and rollback**

```text
Render Dashboard > New > Blueprint > select the GitHub repository > Apply.
Render API service > Environment > Secret Files > add firebase-service-account.json.
Render API service > Manual Deploy > Deploy latest commit.
Render web service > Manual Deploy > Deploy latest commit.
Rollback: Render service > Events > select the last healthy deploy > Roll back.
```

- [ ] **Step 4: Commit**

```bash
git add docs/RENDER_DEPLOYMENT.md
git commit -m "docs: add Render production runbook"
```

## Task 5: Provision Render And Enter Rotated Secrets

**Files:**
- Modify at runtime: Render project environment only.
- Modify at runtime: Firebase Console and Supabase Dashboard only.

**Interfaces:**
- Consumes: `render.yaml`, rotated provider credentials, and the Firebase service-account JSON.
- Produces: two Render services with the correct production configuration.

- [ ] **Step 1: Push only approved production commits**

Run: `git status --short`

Expected: only intentional deployment files are staged or committed; mobile generated files and `web/tsconfig.tsbuildinfo` remain unstaged.

Run: `git push origin main`

Expected: Render can read the latest `main` commit.

- [ ] **Step 2: Create the Blueprint in Render**

In Render Dashboard, choose `New > Blueprint`, connect the GitHub repository, select `render.yaml`, and provide the six `sync: false` values from the freshly rotated credentials.

Expected: services named `tarla-asistani-api` and `tarla-asistani-web` are created in Singapore.

- [ ] **Step 3: Upload the Firebase secret file**

In `tarla-asistani-api > Environment > Secret Files`, add a secret file named `firebase-service-account.json` containing the newly created Firebase Admin JSON.

Expected: a redeploy is triggered and the file is available at `/etc/secrets/firebase-service-account.json`.

- [ ] **Step 4: Verify PostGIS and API migrations**

In Supabase SQL Editor, run:

```sql
select extname from pg_extension where extname = 'postgis';
```

Expected: exactly one row containing `postgis`.

Open API deployment logs and confirm `Database migrations applied successfully.` appears once.

- [ ] **Step 5: Verify no runtime secret entered Git**

Run: `git status --short`

Expected: Render, Firebase, and Supabase actions do not create tracked secret files.

## Task 6: Run Production Acceptance Tests And Enable Automatic Delivery

**Files:**
- Modify at runtime: Render service settings only.
- Modify at runtime: Firebase Console authorized-domain list only.

**Interfaces:**
- Consumes: deployed API and web URLs, Firebase phone authentication, R2 credentials, DeepSeek key, and a physical Android device.
- Produces: evidence that every production dependency works over HTTPS.

- [ ] **Step 1: Check API and web readiness**

Run: `curl -f https://<api-render-host>/health/ready`

Expected: HTTP `200` with `{"status":"ok","database":"ok"}`.

Open `https://<web-render-host>/login`.

Expected: login loads over HTTPS without browser CORS errors.

- [ ] **Step 2: Test Firebase phone sign-in**

Use a real test phone number, request an SMS code, submit it, and open a protected page.

Expected: Firebase returns an ID token, API login succeeds, and protected farm or case data is readable.

- [ ] **Step 3: Test R2 and DeepSeek**

Create a case with a small PNG through the deployed application and load the media as the same authorized user.

Expected: upload returns `201`, media is readable, and logs contain no credential values.

Send `Buğdayda sarı lekeler için ilk neyi kontrol etmeliyim?` through deployed AI chat.

Expected: HTTP `200`, non-empty Turkish reply, and a saved conversation identifier.

- [ ] **Step 4: Test FCM from a physical release Android build**

Run:

```bash
flutter build apk --release --dart-define=API_BASE_URL=https://<api-render-host>/api/v1
```

Install the APK, sign in with Firebase Phone Auth, grant notification permission, and trigger a case or task notification.

Expected: the device registers at `/notifications/devices`, receives one FCM notification, and opens the target screen when tapped.

- [ ] **Step 5: Verify automatic deploy and record release evidence**

Confirm both Render services have Auto-Deploy enabled for `main`. Push a documentation-only commit and inspect Events.

Expected: both services rebuild, API health succeeds, and web continues using the API origin.

Record the web URL, API URL, commit SHA, deployment time, health response, and test results in the release record. Do not record secrets, OTPs, Firebase ID tokens, phone numbers, signed R2 URLs, or database URLs.

## Plan Self-Review

- Spec coverage: Tasks 1-3 implement Render configuration, dynamic service URLs, port binding, and Supabase configuration. Task 4 captures safe operations. Task 5 covers provider provisioning and migration verification. Task 6 covers Firebase, R2, DeepSeek, FCM, HTTPS, and automatic deploy acceptance.
- Placeholder scan: every implementation step has concrete code or an exact operational action. Runtime host markers occur only where Render generates the public hostname during provisioning.
- Type consistency: `DATABASE_URL`, `NEXT_PUBLIC_API_ORIGIN`, `PUBLIC_API_ORIGIN`, `Cors__AllowedOrigins__0`, and `/etc/secrets/firebase-service-account.json` use the same names across code, Blueprint, and runtime instructions.
