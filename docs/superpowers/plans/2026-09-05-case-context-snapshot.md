# Case Context Snapshot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist a backend-generated farm context snapshot when a support case is created and show it in the agronomist web case detail without changing the mobile create request.

**Architecture:** Add a nullable one-to-one `CaseContextSnapshot` owned by `SupportCase`. A scoped builder reads the authorized farm, active crop period, fresh weather snapshot, and latest typed activities during case creation, then persists the snapshot in the same EF unit of work. Case detail loads the immutable snapshot; case lists remain unchanged.

**Tech Stack:** .NET 8, EF Core/Npgsql, MediatR, PostgreSQL/Supabase, Next.js/React/TypeScript, existing test suites.

**Spec:** User-approved Case Context Snapshot implementation prompt in `C:/Users/veliu/.codex/attachments/5c43354c-3e6c-4c3f-bb27-904fa2a95841/pasted-text.txt`.

## Global Constraints

- Mobile `POST /api/v1/cases` request fields and behavior remain unchanged.
- Context is generated only by the backend from `farmId`; client crop/weather/activity data is never trusted.
- Missing crop, activity, or weather data must not block case creation.
- Stale weather must not be presented as current context.
- `GET /cases/{id}` may include context; `GET /cases` must remain lightweight.
- Existing case authorization applies to context; no public context endpoint is added.
- Existing cases receive no fake/backfilled context.
- No AI, offline queue, notification, task-engine, audio, multi-photo, or GPS changes.

### Task 1: Map current case and web contracts

**Files:** Read-only inspection of existing Case, Farm, CropPeriod, WeatherSnapshot, Activity, EF mapping, API client, and web case detail files.

- [ ] Record actual status/type/freshness fields and existing growing-day convention.
- [ ] Identify the current detail authorization query and web response types.
- [ ] Confirm the weather freshness rule before writing tests.

### Task 2: Add failing backend snapshot tests

**Files:**
- Create/modify: `backend/tests/TarlaAsistani.UnitTests/Features/Cases/CaseContextSnapshotBuilderTests.cs`
- Modify: `backend/tests/TarlaAsistani.UnitTests/Features/Cases/CaseCommandHandlerTests.cs`
- Modify: `backend/tests/TarlaAsistani.IntegrationTests/CaseEndpointsIntegrationTests.cs`

- [ ] Add tests for active crop, crop name, planted date, growing day, typed latest activities, fresh weather, captured timestamps, and partial context.
- [ ] Add tests proving no weather/activity/crop does not fail creation.
- [ ] Add immutable snapshot tests after farm/crop/weather/activity changes.
- [ ] Add farmer/agronomist authorization, old context-less case, unchanged POST contract, detail response, lightweight list, and cascade deletion tests.
- [ ] Run focused tests and confirm they fail for missing snapshot behavior rather than test setup errors.

### Task 3: Implement domain model and EF migration

**Files:**
- Create: `backend/src/TarlaAsistani.Domain/Entities/CaseContextSnapshot.cs`
- Modify: `backend/src/TarlaAsistani.Domain/Entities/SupportCase.cs`
- Modify: `backend/src/TarlaAsistani.Infrastructure/Persistence/ApplicationDbContext.cs`
- Create: `backend/src/TarlaAsistani.Infrastructure/Migrations/<timestamp>_AddCaseContextSnapshot.cs`
- Create/modify: corresponding migration designer and model snapshot

- [ ] Add nullable context fields plus required `Id`, `CaseId`, and `CapturedAtUtc`.
- [ ] Configure a unique `CaseId` index/constraint and cascade delete from SupportCase.
- [ ] Keep existing case rows unchanged and make optional fields nullable.
- [ ] Generate the migration through EF tooling and verify both Up and Down.
- [ ] Run migration/model tests.

### Task 4: Implement server-side context builder

**Files:**
- Create: `backend/src/TarlaAsistani.Application/Features/Cases/Services/ICaseContextSnapshotBuilder.cs`
- Create: `backend/src/TarlaAsistani.Application/Features/Cases/Services/CaseContextSnapshotBuilder.cs`
- Modify: `backend/src/TarlaAsistani.Application/Common/Interfaces/IApplicationDbContext.cs` only if required for the new set
- Modify: `backend/src/TarlaAsistani.Infrastructure/DependencyInjection.cs`

- [ ] Query the already-authorized farm by `farmId`.
- [ ] Select the current active crop period using the existing status/selection convention.
- [ ] Calculate growing day with the existing project convention and preserve null when planted date is unavailable.
- [ ] Select only the nearest acceptable weather snapshot and preserve `WeatherObservedAtUtc`; stale data becomes null or explicit stale metadata according to the verified existing rule.
- [ ] Query latest irrigation, fertilization, and spraying by typed `ActivityType`, farm, confirmed/non-archived status, and occurred timestamp without loading history into memory.
- [ ] Return a partial snapshot on optional lookup failures and log safely without user-sensitive payloads.

### Task 5: Enrich case creation transactionally

**Files:**
- Modify: `backend/src/TarlaAsistani.Application/Features/Cases/Commands/CreateCaseCommandHandler.cs`
- Modify: `backend/src/TarlaAsistani.Application/Features/Cases/DTOs/CaseDtos.cs`
- Modify: `backend/src/TarlaAsistani.Application/Features/Cases/Queries/GetCaseByIdQueryHandler.cs`

- [ ] Build context after farm authorization and before the existing `SaveChangesAsync` unit of work.
- [ ] Attach one snapshot to the new case and keep idempotent replay loading context.
- [ ] Include context only in `CaseDetailDto`; keep `CaseSummaryDto` and list query unchanged.
- [ ] Ensure context-less legacy cases serialize as `context: null`.
- [ ] Verify GET detail uses the existing farmer/agronomist authorization path.

### Task 6: Add expert web context presentation

**Files:**
- Modify: `web/lib/api.ts`
- Modify: `web/app/dashboard/cases/[caseId]/page.tsx`
- Modify: `web/app/globals.css`
- Create/modify: web tests using the repository’s existing test setup

- [ ] Add optional typed context parsing while preserving unknown/absent-field tolerance.
- [ ] Render a `Tarla Bağlamı` section only when context has displayable fields.
- [ ] Render partial context without null/undefined/invalid-date text.
- [ ] Format temperature, humidity, weather timestamp, planted/growing day, and recent activity timestamps using existing panel conventions.
- [ ] Keep long farm/crop names within the existing responsive layout.

### Task 7: Full verification and audit

**Files:** No production file changes unless verification exposes a directly related defect.

- [ ] Run backend unit tests, integration tests, build, and migration/model checks.
- [ ] Run web lint, test, and production build.
- [ ] Run mobile case parsing tests, full `flutter test`, `dart analyze`, and Android debug build.
- [ ] Run `git diff --check` and audit that no mobile POST contract, case list query, task engine, or unrelated API behavior changed.
- [ ] Report remaining analyzer warnings separately from failures.

