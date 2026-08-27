# Critical Backend Blockers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the 4 critical compatibility blockers in `backend-dotnet` so that Web (Next.js) and Mobile (Flutter) clients can interact with the .NET backend seamlessly without code breakage.

**Architecture:** 
1. Configure System.Text.Json in `Program.cs` to use `JsonNamingPolicy.SnakeCaseLower` for properties and `JsonNamingPolicy.SnakeCaseUpper` for enums.
2. Update `JwtService.cs` to emit role claims in uppercase (`FARMER`, `AGRONOMIST`).
3. Add dual-token support (Firebase ID token fallback) to ASP.NET Core Bearer Authentication via `JwtBearerEvents.OnMessageReceived`.
4. Ensure all endpoint groups resolve user ID and role using `CurrentUserExtensions` (`httpContext.GetUserId()` and `httpContext.GetUserRole()`), making body `userId` optional and defaulting to the authenticated user.
5. Verify with unit and integration tests.

**Tech Stack:** .NET 8, ASP.NET Core Minimal APIs, MediatR, Entity Framework Core, System.Text.Json, FluentValidation, xUnit, FluentAssertions.

---

### Task 1: Configure JSON Serialization (snake_case and UPPER_SNAKE_CASE enums)

**Files:**
- Modify: `backend-dotnet/src/TarlaAsistani.API/Program.cs`
- Modify: `backend-dotnet/src/TarlaAsistani.Infrastructure/Services/JwtService.cs`
- Modify: `backend-dotnet/tests/TarlaAsistani.IntegrationTests/CustomWebApplicationFactory.cs`

- [ ] **Step 1: Update Program.cs JSON options and JWT claim formatting**
  - Set `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower`
  - Set `DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower`
  - Register `new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper)`
  - Update `JwtService.cs` so role claim is `user.Role.ToString().ToUpperInvariant()` ("FARMER" / "AGRONOMIST")

- [ ] **Step 2: Update CustomWebApplicationFactory.cs JSON options**
  - Align `CustomWebApplicationFactory.JsonOptions` with `JsonNamingPolicy.SnakeCaseLower` and `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper)`

- [ ] **Step 3: Run existing tests to verify serialization**
  - Run `dotnet test TarlaAsistani.slnx`
  - Fix any assertions that previously expected camelCase or PascalCase enums

---

### Task 2: Dual-Token Authentication Support (Firebase ID Token as Bearer)

**Files:**
- Modify: `backend-dotnet/src/TarlaAsistani.API/Program.cs`
- Test: `backend-dotnet/tests/TarlaAsistani.IntegrationTests/AuthEndpointsIntegrationTests.cs`

- [ ] **Step 1: Add JwtBearerEvents.OnMessageReceived to AddJwtBearer in Program.cs**
  - Inspect incoming `Authorization: Bearer <token>`
  - If token issuer is `https://securetoken.google.com/` or token starts with `dev_` / `mock_`:
    - Call `IFirebaseAuthService.VerifyIdTokenAsync`
    - Look up or link user in database
    - Construct `ClaimsPrincipal` with `Sub` = user.Id, `Role` = user.Role
    - Call `context.Success()`

- [ ] **Step 2: Add integration test for direct Firebase ID token authentication**
  - Add test in `AuthEndpointsIntegrationTests.cs` verifying that calling `/api/v1/auth/me` with a dev Firebase Bearer token returns 200 OK with the linked user.

- [ ] **Step 3: Run tests to verify passing**
  - Run `dotnet test TarlaAsistani.slnx`

---

### Task 3: Standardize User Context Resolution across All Endpoint Groups

**Files:**
- Modify: `backend-dotnet/src/TarlaAsistani.API/Endpoints/UserEndpoints.cs`
- Modify: `backend-dotnet/src/TarlaAsistani.API/Endpoints/FarmEndpoints.cs`
- Modify: `backend-dotnet/src/TarlaAsistani.API/Endpoints/CropPeriodEndpoints.cs`
- Modify: `backend-dotnet/src/TarlaAsistani.API/Endpoints/WeatherEndpoints.cs`
- Modify: `backend-dotnet/src/TarlaAsistani.API/Endpoints/TaskEndpoints.cs`
- Modify: `backend-dotnet/src/TarlaAsistani.API/Endpoints/ActivityEndpoints.cs`
- Modify: `backend-dotnet/src/TarlaAsistani.API/Endpoints/MediaEndpoints.cs`
- Modify: `backend-dotnet/src/TarlaAsistani.API/Endpoints/CaseEndpoints.cs`
- Modify: `backend-dotnet/src/TarlaAsistani.API/Endpoints/NotificationEndpoints.cs`
- Modify: `backend-dotnet/src/TarlaAsistani.API/Endpoints/PilotEndpoints.cs`
- Modify: `backend-dotnet/src/TarlaAsistani.API/Endpoints/AIEndpoints.cs`

- [ ] **Step 1: Update request DTOs**
  - In request DTOs where `Guid UserId` or `Guid OwnerId` is present, change to `Guid? UserId = null` / `Guid? OwnerId = null` so clients sending bodies without userId deserialize correctly.

- [ ] **Step 2: Update endpoint handlers to resolve user from `httpContext.GetUserId()`**
  - For each endpoint group:
    - Pass `HttpContext httpContext`
    - Resolve `var userId = headerUserId ?? req?.UserId ?? httpContext.GetUserId()`
    - If endpoint requires authentication and `userId == null || userId == Guid.Empty`, return `Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized)`
    - Resolve role: `var role = reqRole ?? headerRole ?? httpContext.GetUserRole() ?? UserRole.Farmer`

- [ ] **Step 3: Add integration tests verifying authenticated calls work without X-User-Id header**
  - Ensure tests verify calling protected endpoints (e.g. `/api/v1/farms`, `/api/v1/tasks`, `/api/v1/cases`) with ONLY `Authorization: Bearer <token>` succeeds.

- [ ] **Step 4: Run full test suite**
  - Run `dotnet test TarlaAsistani.slnx`
  - Verify all unit and integration tests pass.
