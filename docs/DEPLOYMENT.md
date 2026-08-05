# DEPLOYMENT STRATEGY

Defines the secure, consistent, and continuous delivery (Continuous Integration & Continuous Deployment) of Tarla Asistanı to test (Staging) and live (Production) environments.

## 1. Infrastructure
- **Cloud Provider:** AWS, Google Cloud, or DigitalOcean.
- **Server Architecture:** Docker containers for backend services (Docker Compose or managed services e.g., ECS/Cloud Run).
- **Database:** Managed PostgreSQL service (with backup and high availability (HA) features enabled).
- **Static Files (Web Panel):** Served via CDN (Vercel, Netlify, or S3+CloudFront).

## 2. CI/CD Processes
**GitHub Actions** or **GitLab CI** will be used for source code management and automation.

### 2.1 Backend Deployment Flow
1. Developer pushes code to `main` or `staging` branches.
2. The CI tool runs Unit and Integration tests.
3. Lint and security analyses are performed.
4. If tests are successful, a Docker image is built.
5. The built image is pushed to the Container Registry.
6. A connection is established to the server (via SSH/Webhook) to deploy the new image.

### 2.2 Mobile App Deployment Flow
1. Code branch is merged.
2. The Flutter project is tested.
3. APK/AAB files and iOS IPA files are built automatically using Fastlane.
4. The generated files are sent to Google Play Console (Internal/Beta test channel) or TestFlight.

## 3. Database Backup and Recovery
- Daily full backups and point-in-time recovery logs (WAL) of the PostgreSQL database will be taken automatically and stored in a secure, separate location (e.g., S3 Bucket).
- In the event of a system crash, it will be configured to allow "Point-in-time recovery".

## 4. Environment Variables
- All passwords, API keys (SMS service, Weather service), and database connection strings will be kept in `.env` files and CI/CD Secrets managers, and will never be written into the source code (GitHub etc.).

## 5. Staging Runbook

The executable staging overlay, JSON logging, health/readiness checks, Prometheus
metrics, backup verification and pilot support flow are documented in
[`PILOT_OPERATIONS.md`](./PILOT_OPERATIONS.md).
