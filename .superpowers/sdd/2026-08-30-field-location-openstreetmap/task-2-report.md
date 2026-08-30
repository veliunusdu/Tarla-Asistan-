# Task 2 Report: Single-use GPS Location Service

## Scope

Implemented Task 2 only in the specified feature worktree. The production
changes are confined to `mobile/lib/features/location`, and the test change is
confined to `mobile/test/features/location/data`.

## Implementation

- Added immutable `TarlaLocation` with latitude and longitude coordinates.
- Added the `LocationService` contract with `getCurrentLocation()`.
- Added a typed location-unavailable exception hierarchy:
  `LocationServiceDisabledException`, `LocationPermissionDeniedException`,
  and `LocationPermissionPermanentlyDeniedException`.
- Added `GeolocatorLocationService`, which checks whether device location is
  enabled, checks permission, requests permission only when initially denied,
  and returns the current coordinate pair.
- Added a narrow `LocationPlatformAdapter` boundary so service behavior can be
  tested without platform channels. The concrete adapter delegates to
  `geolocator`.
- Current-position requests use `LocationAccuracy.high` with a 15-second time
  limit. Platform service-disabled and timeout failures map to typed
  location-unavailable errors.

## Test-Driven Development

The test suite was created before production files existed. Its initial run
failed because the Task 2 imports, service, and exception types were absent.
After the minimal implementation was added, the focused suite passed.

Covered behavior:

- Device location service disabled.
- Permission denied after requesting permission.
- Permission permanently denied.
- Successful coordinate conversion and the required accuracy/time-limit
  settings.

## Verification

- `flutter test test/features/location/data/geolocator_location_service_test.dart`
  passed: 4 tests, 0 failures.
- `flutter analyze lib/features/location test/features/location/data/geolocator_location_service_test.dart`
  could not complete because the local Dart analysis server exited while
  parsing a malformed LSP message. This occurred before source analysis and
  was reproduced on retry.
