# Sprint 5 Pilot Operations

## Staging release

1. Copy `.env.example` to a secret-managed staging environment and replace every
   placeholder secret.
2. Start the stack with `docker compose -f compose.yaml -f compose.staging.yaml up -d --build`.
3. Run migrations with `docker compose exec backend alembic upgrade head`.
4. Verify `/health/live`, `/health/ready`, `/metrics`, OTP login, one task flow,
   one offline replay and one expert response before admitting pilot users.

`PUSH_PROVIDER=noop` keeps a complete notification inbox without sending external
traffic. Set it to `http` only after configuring the HTTPS gateway URL and bearer
token. The gateway must return `message_id` or `name`.

## Monitoring and incident response

- Scrape `/metrics` every 30 seconds and alert when readiness fails for two
  consecutive checks or the 5xx rate exceeds 2% for five minutes.
- Application logs are one-line JSON. Search by `request_id`; the same value is
  returned in the `X-Request-ID` response header and unexpected-error body.
- Keep push delivery below the pilot acceptance threshold under review using
  `GET /api/v1/pilot/metrics` and inspect failed records in `notifications`.
- Do not log JWTs, OTPs, device tokens, feedback text, or uploaded media.

## Backup and recovery

Run `scripts/postgres-backup.sh` daily with `DATABASE_URL`, an encrypted
`BACKUP_DIR`, and `BACKUP_RETENTION_DAYS`. Copy the result to a separate encrypted
storage account. Weekly, restore the newest dump into an isolated database with
`pg_restore --clean --if-exists --no-owner --dbname "$RESTORE_DATABASE_URL" file.dump`
and execute the API integration tests against it.

## Pilot cohort and feedback

- Cohort: 30–50 farmers and 2–3 agronomists; record explicit consent and a support contact.
- Weekly: farmer submits `WEEKLY_CHECKIN` with a 1–5 rating and comment.
- False weather alert: submit `FALSE_ALERT` linked to the generated weather task.
- Agronomist reviews items using the feedback list/update endpoints.
- Review active farmers, task completion, false-alert rate, average expert response,
  push delivery, feedback volume and rating each week via the metrics endpoint.
- Pilot exit: resolve critical incidents, document unresolved feedback, export the
  weekly metrics, revoke test device tokens and decide go/no-go with product owner.
