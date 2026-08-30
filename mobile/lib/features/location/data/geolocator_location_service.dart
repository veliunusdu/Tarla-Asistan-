import 'dart:async';

import 'package:geolocator/geolocator.dart' as geolocator;
import 'package:mobile/features/location/data/location_service.dart';
import 'package:mobile/features/location/domain/tarla_location.dart';

final class GeolocatorLocationService implements LocationService {
  GeolocatorLocationService({LocationPlatformAdapter? platform})
    : _platform = platform ?? GeolocatorPlatformAdapter();

  final LocationPlatformAdapter _platform;

  @override
  Future<TarlaLocation> getCurrentLocation() async {
    if (!await _platform.isLocationServiceEnabled()) {
      throw const LocationServiceDisabledException();
    }

    var permission = await _platform.checkPermission();
    if (permission == geolocator.LocationPermission.denied) {
      permission = await _platform.requestPermission();
    }

    if (permission == geolocator.LocationPermission.deniedForever) {
      throw const LocationPermissionPermanentlyDeniedException();
    }
    if (permission == geolocator.LocationPermission.denied) {
      throw const LocationPermissionDeniedException();
    }

    try {
      final position = await _platform.getCurrentPosition(
        accuracy: geolocator.LocationAccuracy.high,
        timeLimit: const Duration(seconds: 15),
      );
      return TarlaLocation(
        latitude: position.latitude,
        longitude: position.longitude,
      );
    } on geolocator.LocationServiceDisabledException {
      throw const LocationServiceDisabledException();
    } on TimeoutException {
      throw const LocationUnavailableException();
    }
  }
}

abstract interface class LocationPlatformAdapter {
  Future<bool> isLocationServiceEnabled();
  Future<geolocator.LocationPermission> checkPermission();
  Future<geolocator.LocationPermission> requestPermission();
  Future<PlatformLocation> getCurrentPosition({
    required geolocator.LocationAccuracy accuracy,
    required Duration timeLimit,
  });
}

final class PlatformLocation {
  const PlatformLocation({required this.latitude, required this.longitude});

  final double latitude;
  final double longitude;
}

final class GeolocatorPlatformAdapter implements LocationPlatformAdapter {
  @override
  Future<bool> isLocationServiceEnabled() =>
      geolocator.Geolocator.isLocationServiceEnabled();

  @override
  Future<geolocator.LocationPermission> checkPermission() =>
      geolocator.Geolocator.checkPermission();

  @override
  Future<geolocator.LocationPermission> requestPermission() =>
      geolocator.Geolocator.requestPermission();

  @override
  Future<PlatformLocation> getCurrentPosition({
    required geolocator.LocationAccuracy accuracy,
    required Duration timeLimit,
  }) async {
    final position = await geolocator.Geolocator.getCurrentPosition(
      locationSettings: geolocator.LocationSettings(
        accuracy: accuracy,
        timeLimit: timeLimit,
      ),
    );
    return PlatformLocation(
      latitude: position.latitude,
      longitude: position.longitude,
    );
  }
}
