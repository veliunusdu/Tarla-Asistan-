import 'firestore_user_profile_service.dart';

class PostLoginInitializer {
  PostLoginInitializer({
    required UserProfileProvisioner profileProvisioner,
    required Future<void> Function() initializeSync,
    required Future<void> Function() initializeNotifications,
  }) : _profileProvisioner = profileProvisioner,
       _initializeSync = initializeSync,
       _initializeNotifications = initializeNotifications;

  final UserProfileProvisioner _profileProvisioner;
  final Future<void> Function() _initializeSync;
  final Future<void> Function() _initializeNotifications;
  String? _inFlightUid;
  Future<void>? _inFlight;

  Future<void> initialize({required String uid, required String? phoneNumber}) {
    final inFlight = _inFlight;
    if (inFlight != null && _inFlightUid == uid) return inFlight;

    late final Future<void> start;
    start = _run(uid: uid, phoneNumber: phoneNumber).whenComplete(() {
      if (identical(_inFlight, start)) {
        _inFlight = null;
        _inFlightUid = null;
      }
    });
    _inFlightUid = uid;
    _inFlight = start;
    return start;
  }

  Future<void> _run({required String uid, required String? phoneNumber}) async {
    await _profileProvisioner.ensureProfile(uid: uid, phoneNumber: phoneNumber);
    await _initializeSync();
    await _initializeNotifications();
  }
}
