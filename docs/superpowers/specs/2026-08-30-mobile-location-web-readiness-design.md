# Mobile Location and Web Readiness Design

## Goal

Make location-backed farm creation discoverable and reliably persisted in the backend, then make the web experience honest and useful for the roles it exposes.

## Findings

### Mobile

- `TarlaEklemeEkrani` already has GPS and OpenStreetMap controls, but they appear after crop and planting-date fields, near the bottom of a scrollable form.
- `TarlaGunluguEkrani` opens `const TarlaEklemeEkrani()` from its empty state. That drops the injected backend repository, so the new field is stored only in SQLite.
- Weather deliberately selects a backend field with both coordinates. A local-only or locationless field therefore produces the location-required result.

### Web

- `/login` is an agronomist-only console.
- `/register` auto-creates a Firebase-backed `FARMER`, but `/farmer` is only a handoff page. There is no farmer web workspace for farms, weather, AI, media, or activities.
- Registration calls `updateProfile` but does not force-refresh the Firebase ID token before backend registration, so the entered name may not reach the backend profile.
- The checked-in web environment example sets Firebase variables to empty values. Empty strings override the defaults and break local Firebase configuration.
- The web package has type checking but no test runner.
- New Firebase accounts are always `FARMER`. Agronomist Firebase linking requires an operator approval, but there is no operator web interface or documented operational runbook.

## Product Decision

Mobile remains the primary farmer experience for this release. The web stays an agronomist console. Farmer registration remains an account-creation bridge and must clearly hand users to mobile after success. A full farmer web portal needs a separate product design.

## Acceptance Criteria

1. Every add-field route receives the injected backend repository.
2. The form presents an explicit location section before Save with GPS, map, and skip options.
3. A coordinate-backed field survives restart and returns weather; a locationless field gets a clear location action.
4. Registration persists the entered display name or shows a recoverable error.
5. Web copy clearly distinguishes farmer registration from agronomist sign-in.
6. Local web startup works from the documented environment and registration is automatically tested.
7. The pilot operator has a documented, verified process for provisioning an agronomist account without self-assigning a role.
