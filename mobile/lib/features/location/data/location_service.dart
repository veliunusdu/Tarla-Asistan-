import 'package:mobile/features/location/domain/tarla_location.dart';

abstract interface class LocationService {
  Future<TarlaLocation> getCurrentLocation();
}

class LocationUnavailableException implements Exception {
  const LocationUnavailableException();
}

final class LocationServiceDisabledException
    extends LocationUnavailableException {
  const LocationServiceDisabledException();
}

final class LocationPermissionDeniedException
    extends LocationUnavailableException {
  const LocationPermissionDeniedException();
}

final class LocationPermissionPermanentlyDeniedException
    extends LocationUnavailableException {
  const LocationPermissionPermanentlyDeniedException();
}
