# Render Production Deployment

## Prerequisites

The Render Blueprint is [render.yaml](../render.yaml). It creates two public
services in Singapore:

- tarla-asistani-api
- tarla-asistani-web

Both services use paid 0.5c-512mb instances. Do not use Render Free for a
production release because inactive services sleep.

## Rotate Credentials First

Rotate every credential that was previously shared outside a secret manager,
then invalidate the old credential:

1. Reset the Supabase database password and copy a new pooled PostgreSQL URL.
2. Delete the old R2 access key pair and create a replacement restricted to
   the tarla-asistani-media bucket.
3. Revoke the old DeepSeek API key and create a replacement key.
4. Create a new Firebase Admin service account key and delete the old key.
5. Update only ignored local environment files and the Render dashboard with
   the new values.

Never commit, paste into source code, or add these values to render.yaml.

## Create The Render Services

1. In Render Dashboard, select New > Blueprint.
2. Connect the GitHub repository and select render.yaml from main.
3. Provide the values requested for:
   - DATABASE_URL
   - R2_ACCOUNT_ID
   - R2_BUCKET
   - R2_ACCESS_KEY_ID
   - R2_SECRET_ACCESS_KEY
   - DEEPSEEK_API_KEY
4. Let Render generate JWT_SECRET.
5. In tarla-asistani-api > Environment > Secret Files, create
   firebase-service-account.json and paste the newly generated Firebase Admin
   JSON. Render mounts it at /etc/secrets/firebase-service-account.json.
6. Deploy the API, then deploy the web service.

The API logs must contain Database migrations applied successfully. Its
/health/ready endpoint must return a 200 response with database status ok.

## Firebase Configuration

In Firebase project demo2-c4265:

1. Enable Authentication > Sign-in method > Phone.
2. Under Authentication > Settings > Authorized domains, add the Render web
   hostname without https://.
3. Under Cloud Messaging, verify Android package
   com.tarlaasistani.pilot.
4. Add the release signing key SHA-1 and SHA-256 fingerprints in project
   settings before building the Android release APK.

## Verify The Release

1. Open https://<api-host>/health/ready; expect HTTP 200.
2. Open https://<web-host>/login; expect no browser CORS errors.
3. Request and verify a real Firebase phone SMS code, then open a protected
   page.
4. Create a case with a small PNG and open the uploaded media.
5. Send an AI chat request and verify a non-empty Turkish response.
6. Build and install the Android release:

    flutter build apk --release --dart-define=API_BASE_URL=https://<api-host>/api/v1

7. Sign in on the device, grant notification permission, register the FCM
   device token, and trigger one case or task notification.

Record the web URL, API URL, commit SHA, deployment time, health response, and
test results in the release record. Do not record credentials, OTPs, Firebase
tokens, phone numbers, database URLs, or signed media URLs.

## Rollback

For either Render service, open Events, select the last healthy deploy, and
choose Roll back. Verify /health/ready and the web login page after the
rollback completes.
