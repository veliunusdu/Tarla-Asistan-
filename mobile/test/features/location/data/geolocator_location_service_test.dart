import 'package:flutter_test/flutter_test.dart';
import 'package:geolocator/geolocator.dart'
    show LocationAccuracy, LocationPermission;
import 'package:mobile/features/location/data/geolocator_location_service.dart';
import 'package:mobile/features/location/data/location_service.dart';

void main() {
  group('GeolocatorLocationService', () {
    test('throws when location services are disabled', () async {
      final service = GeolocatorLocationService(
        platform: FakeLocationPlatform(serviceEnabled: false),
      );

      await expectLater(
        service.getCurrentLocation(),
        throwsA(isA<LocationServiceDisabledException>()),
      );
    });

    test(
      'throws when location permission is denied after requesting it',
      () async {
        final service = GeolocatorLocationService(
          platform: FakeLocationPlatform(
            permission: LocationPermission.denied,
            requestedPermission: LocationPermission.denied,
          ),
        );

        await expectLater(
          service.getCurrentLocation(),
          throwsA(isA<LocationPermissionDeniedException>()),
        );
      },
    );

    test('throws when location permission is permanently denied', () async {
      final service = GeolocatorLocationService(
        platform: FakeLocationPlatform(
          permission: LocationPermission.deniedForever,
        ),
      );

      await expectLater(
        service.getCurrentLocation(),
        throwsA(isA<LocationPermissionPermanentlyDeniedException>()),
      );
    });

    test(
      'returns the current coordinates using a high accuracy 15-second request',
      () async {
        final platform = FakeLocationPlatform(
          position: const PlatformLocation(latitude: 37.872, longitude: 32.493),
        );
        final service = GeolocatorLocationService(platform: platform);

        final location = await service.getCurrentLocation();

        expect(location.latitude, 37.872);
        expect(location.longitude, 32.493);
        expect(platform.requestedAccuracy, LocationAccuracy.high);
        expect(platform.requestedTimeLimit, const Duration(seconds: 15));
      },
    );
  });
}

class FakeLocationPlatform implements LocationPlatformAdapter {
  FakeLocationPlatform({
    this.serviceEnabled = true,
    this.permission = LocationPermission.whileInUse,
    this.requestedPermission = LocationPermission.whileInUse,
    this.position = const PlatformLocation(latitude: 0, longitude: 0),
  });

  final bool serviceEnabled;
  final LocationPermission permission;
  final LocationPermission requestedPermission;
  final PlatformLocation position;
  LocationAccuracy? requestedAccuracy;
  Duration? requestedTimeLimit;

  @override
  Future<bool> isLocationServiceEnabled() async => serviceEnabled;

  @override
  Future<LocationPermission> checkPermission() async => permission;

  @override
  Future<LocationPermission> requestPermission() async => requestedPermission;

  @override
  Future<PlatformLocation> getCurrentPosition({
    required LocationAccuracy accuracy,
    required Duration timeLimit,
  }) async {
    requestedAccuracy = accuracy;
    requestedTimeLimit = timeLimit;
    return position;
  }
}
