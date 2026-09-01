import 'firestore_user_profile_service.dart';

class PostLoginInitializationStatus {
  const PostLoginInitializationStatus({
    this.profileFailure,
    this.syncFailure,
    this.notificationFailure,
  });

  final Object? profileFailure;
  final Object? syncFailure;
  final Object? notificationFailure;
}

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
  PostLoginInitializationStatus _status = const PostLoginInitializationStatus();

  PostLoginInitializationStatus get status => _status;

  Future<void> initialize({
    required String uid,
    required String? phoneNumber,
    String? email,
  }) {
    final inFlight = _inFlight;
    if (inFlight != null && _inFlightUid == uid) return inFlight;

    late final Future<void> start;
    start = _run(uid: uid, phoneNumber: phoneNumber, email: email).whenComplete(
      () {
        if (identical(_inFlight, start)) {
          _inFlight = null;
          _inFlightUid = null;
        }
      },
    );
    _inFlightUid = uid;
    _inFlight = start;
    return start;
  }

  Future<void> _run({
    required String uid,
    required String? phoneNumber,
    String? email,
  }) async {
    Object? profileFailure;
    Object? syncFailure;
    Object? notificationFailure;

    try {
      await _profileProvisioner.ensureProfile(
        uid: uid,
        phoneNumber: phoneNumber,
        email: email,
      );
    } catch (error) {
      profileFailure = error;
    }

    try {
      await _initializeSync();
    } catch (error) {
      syncFailure = error;
    }

    try {
      await _initializeNotifications();
    } catch (error) {
      notificationFailure = error;
    }

    _status = PostLoginInitializationStatus(
      profileFailure: profileFailure,
      syncFailure: syncFailure,
      notificationFailure: notificationFailure,
    );
  }
}
