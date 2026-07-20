# CHANGELOG

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
