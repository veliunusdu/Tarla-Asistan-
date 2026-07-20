# CHANGELOG

## 2026-07-20 - Sprint 3 (Veli)
- Added expert-created and automatically generated daily farm tasks.
- Added task priority, reason, confidence, status transitions and overdue
  handling.
- Limited the main daily list to three tasks while keeping critical weather
  alerts separate.
- Added database-backed task deduplication and idempotent task completion.
- Added manual and voice-draft activity records, confirmation, edit history,
  archive and restore flows.
- Added a combined farm journal for confirmed activities and completed tasks.
- Added migration `20260720_0004` and backend test coverage.

## 2026-07-20 - Sprint 2 (Veli)
- Added owner-scoped farm create, list, detail, update and soft-archive APIs.
- Added duplicate farm-name warnings and input validation.
- Added crop/production-period history with a single-active-period safeguard.
- Added replaceable Open-Meteo adapter, stored fallback snapshots and stale data
  indicators.
- Added frost, strong-wind and heavy-rain rules with cautious action text.
- Added migration `20260720_0003` and backend test coverage.

All notable changes to the Tarla Asistanı project will be documented in this file in chronological order. The format is based on [Keep a Changelog](https://keepachangelog.com/).

## [Unreleased]

### Added
- Created the project documentation structure (`docs/` folder).
- Added `PROJECT_OVERVIEW.md`, `PRODUCT_REQUIREMENTS.md`, and `TECHNICAL_DOCUMENTATION.md`.
- Created `DECISIONS.md` for architectural decisions.
- Added `AGRICULTURAL_RULES.md` defining agricultural limits and prioritization.
- Defined `DATABASE.md` containing the database schema for the MVP.
- Prepared `API_DOCUMENTATION.md` explaining basic API endpoints.
- Wrote `DESIGN.md` establishing interface principles.
- Documented security measures (`SECURITY.md`), test strategy (`TESTING_STRATEGY.md`), and deployment plan (`DEPLOYMENT.md`).
- Created `BACKLOG.md` for tracking tasks.

### Changed
- No changes yet.

### Fixed
- No bug fixes yet.

### Removed
- No features removed yet.
