import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/services/firestore_user_profile_service.dart';
import 'package:mobile/services/post_login_initializer.dart';

void main() {
  test(
    'runs profile, sync, and notifications once in order for concurrent login events',
    () async {
      final events = <String>[];
      final profileGate = Completer<void>();
      final profile = _FakeProfileProvisioner(() async {
        events.add('profile');
        await profileGate.future;
      });
      final initializer = PostLoginInitializer(
        profileProvisioner: profile,
        initializeSync: () async => events.add('sync'),
        initializeNotifications: () async => events.add('notifications'),
      );

      final first = initializer.initialize(
        uid: 'uid-1',
        phoneNumber: '+905551112233',
      );
      final second = initializer.initialize(
        uid: 'uid-1',
        phoneNumber: '+905551112233',
      );
      await Future<void>.delayed(Duration.zero);

      expect(profile.calls, 1);
      expect(events, ['profile']);
      profileGate.complete();
      await Future.wait([first, second]);
      expect(events, ['profile', 'sync', 'notifications']);
    },
  );
}

class _FakeProfileProvisioner implements UserProfileProvisioner {
  _FakeProfileProvisioner(this.callback);

  final Future<void> Function() callback;
  int calls = 0;

  @override
  Future<void> ensureProfile({
    required String uid,
    required String? phoneNumber,
  }) async {
    calls += 1;
    await callback();
  }
}
