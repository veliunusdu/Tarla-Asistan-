import 'dart:async';

import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:fake_cloud_firestore/fake_cloud_firestore.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/services/firestore_farm_repository.dart';
import 'package:mobile/services/firestore_user_profile_service.dart';

void main() {
  test('provisions the exact farmer profile with server timestamps', () async {
    final store = _RecordingUserProfileStore();
    final service = FirestoreUserProfileService(store: store);

    await service.ensureProfile(uid: 'uid-1', phoneNumber: '+905551112233');

    expect(store.path, 'users/uid-1');
    expect(
      store.data.keys,
      unorderedEquals(FirestoreUserProfileContract.profileFields),
    );
    expect(store.data['phoneNumber'], '+905551112233');
    expect(store.data['role'], 'FARMER');
    expect(store.data['notificationsEnabled'], isTrue);
    expect(store.data['createdAt'], isA<FirestoreServerTimestamp>());
    expect(store.data['updatedAt'], isA<FirestoreServerTimestamp>());
  });

  test(
    'production adapter selects the named database and preserves an existing profile',
    () async {
      final firestore = FakeFirebaseFirestore();
      final existing = <String, Object?>{
        'phoneNumber': '+905551112233',
        'role': 'FARMER',
        'notificationsEnabled': false,
        'createdAt': Timestamp.fromDate(DateTime.utc(2026, 8, 1)),
        'updatedAt': Timestamp.fromDate(DateTime.utc(2026, 8, 2)),
      };
      await firestore.doc('users/uid-1').set(existing);
      final factory = _RecordingInstanceFactory(firestore);
      final service = FirestoreUserProfileService(
        store: FirebaseFirestoreUserProfileStore(instanceFactory: factory),
      );

      await service.ensureProfile(uid: 'uid-1', phoneNumber: '+905559999999');

      expect(factory.databaseId, 'tarla-asistani');
      expect((await firestore.doc('users/uid-1').get()).data(), existing);
    },
  );

  test('returns a safe Turkish error when profile provisioning fails', () async {
    final service = FirestoreUserProfileService(
      store: _RecordingUserProfileStore(error: StateError('secret detail')),
    );

    await expectLater(
      service.ensureProfile(uid: 'uid-1', phoneNumber: '+905551112233'),
      throwsA(
        isA<UserProfileInitializationException>().having(
          (error) => error.message,
          'message',
          'Hesabınız hazırlanamadı. Bağlantınızı kontrol edip tekrar deneyin.',
        ),
      ),
    );
  });
}

class _RecordingInstanceFactory implements FirebaseFirestoreInstanceFactory {
  _RecordingInstanceFactory(this.firestore);

  final FirebaseFirestore firestore;
  String? databaseId;

  @override
  FirebaseFirestore instanceFor({required String databaseId}) {
    this.databaseId = databaseId;
    return firestore;
  }
}

class _RecordingUserProfileStore implements FirestoreUserProfileStore {
  _RecordingUserProfileStore({this.error});

  final Object? error;
  String? path;
  Map<String, Object?> data = {};

  @override
  Future<void> createIfAbsent(String path, Map<String, Object?> data) async {
    if (error != null) throw error!;
    this.path = path;
    this.data = Map.of(data);
  }
}
