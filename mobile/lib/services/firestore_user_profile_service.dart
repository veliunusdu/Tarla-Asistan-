import 'package:cloud_firestore/cloud_firestore.dart';

import 'firestore_farm_repository.dart';

abstract interface class UserProfileProvisioner {
  Future<void> ensureProfile({
    required String uid,
    required String? phoneNumber,
  });
}

abstract class FirestoreUserProfileStore {
  Future<void> createIfAbsent(String path, Map<String, Object?> data);
}

abstract final class FirestoreUserProfileContract {
  static const databaseId = 'tarla-asistani';
  static const profileFields = {
    'phoneNumber',
    'role',
    'notificationsEnabled',
    'createdAt',
    'updatedAt',
  };
}

class FirestoreUserProfileService implements UserProfileProvisioner {
  FirestoreUserProfileService({FirestoreUserProfileStore? store})
    : _store = store;

  FirestoreUserProfileStore? _store;

  FirestoreUserProfileStore get _profileStore =>
      _store ??= FirebaseFirestoreUserProfileStore();

  @override
  Future<void> ensureProfile({
    required String uid,
    required String? phoneNumber,
  }) async {
    if (phoneNumber == null ||
        !RegExp(r'^\+[1-9][0-9]{7,14}$').hasMatch(phoneNumber)) {
      throw const UserProfileInitializationException();
    }
    try {
      await _profileStore.createIfAbsent('users/$uid', {
        'phoneNumber': phoneNumber,
        'role': 'FARMER',
        'notificationsEnabled': true,
        'createdAt': const FirestoreServerTimestamp(),
        'updatedAt': const FirestoreServerTimestamp(),
      });
    } catch (_) {
      throw const UserProfileInitializationException();
    }
  }
}

class FirebaseFirestoreUserProfileStore implements FirestoreUserProfileStore {
  FirebaseFirestoreUserProfileStore({
    FirebaseFirestore? firestore,
    FirebaseFirestoreInstanceFactory? instanceFactory,
  }) : _firestore =
           firestore ??
           (instanceFactory ?? const FirebaseFirestoreSdkInstanceFactory())
               .instanceFor(
                 databaseId: FirestoreUserProfileContract.databaseId,
               );

  final FirebaseFirestore _firestore;

  @override
  Future<void> createIfAbsent(String path, Map<String, Object?> data) async {
    final reference = _firestore.doc(path);
    await _firestore.runTransaction((transaction) async {
      final existing = await transaction.get(reference);
      if (!existing.exists) {
        transaction.set(reference, FirestoreMapEncoder.encode(data));
      }
    });
  }
}

class UserProfileInitializationException implements Exception {
  const UserProfileInitializationException();

  String get message =>
      'Hesabınız hazırlanamadı. Bağlantınızı kontrol edip tekrar deneyin.';

  @override
  String toString() => message;
}
