import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/services/firebase_auth_service.dart';

class FakeFirebaseAuthGateway implements FirebaseAuthGateway {
  String? phoneNumber;
  late FutureOr<void> Function(String) onCodeSent;
  late FutureOr<void> Function() onVerificationCompleted;
  late FutureOr<void> Function(Object) onVerificationFailed;
  late FutureOr<void> Function(String) onCodeAutoRetrievalTimeout;

  @override
  Future<String> confirmCode({
    required String verificationId,
    required String smsCode,
  }) async => 'firebase-id-token';

  @override
  Future<String?> currentIdToken() async => null;

  @override
  Future<void> sendVerificationCode({
    required String phoneNumber,
    required FutureOr<void> Function(String verificationId) onCodeSent,
    required FutureOr<void> Function() onVerificationCompleted,
    required FutureOr<void> Function(Object error) onVerificationFailed,
    required FutureOr<void> Function(String verificationId)
    onCodeAutoRetrievalTimeout,
  }) async {
    this.phoneNumber = phoneNumber;
    this.onCodeSent = onCodeSent;
    this.onVerificationCompleted = onVerificationCompleted;
    this.onVerificationFailed = onVerificationFailed;
    this.onCodeAutoRetrievalTimeout = onCodeAutoRetrievalTimeout;
  }

  @override
  Future<void> signOut() async {}
}

void main() {
  test('sends the normalized phone number', () async {
    final fake = FakeFirebaseAuthGateway();
    final service = FirebaseAuthService(gateway: fake);

    final sent = service.sendCode('+905551112233');
    await fake.onCodeSent('verification-id');
    await sent;

    expect(fake.phoneNumber, '+905551112233');
  });

  test('keeps the verification ID needed to confirm the SMS code', () async {
    final fake = FakeFirebaseAuthGateway();
    final service = FirebaseAuthService(gateway: fake);

    final sent = service.sendCode('+905551112233');
    await fake.onCodeSent('verification-id');
    await sent;

    expect(service.verificationId, 'verification-id');
  });

  test('returns an ID token after code confirmation', () async {
    final service = FirebaseAuthService(gateway: FakeFirebaseAuthGateway());

    expect(await service.confirmCode('id', '123456'), 'firebase-id-token');
  });

  test('completes after automatic verification', () async {
    final fake = FakeFirebaseAuthGateway();
    final service = FirebaseAuthService(gateway: fake);

    final sent = service.sendCode('+905551112233');
    await fake.onVerificationCompleted();

    await sent;
  });

  test('surfaces automatic verification errors', () async {
    final fake = FakeFirebaseAuthGateway();
    final service = FirebaseAuthService(gateway: fake);

    final sent = service.sendCode('+905551112233');
    final error = expectLater(sent, throwsA(isA<StateError>()));
    await fake.onVerificationFailed(StateError('automatic sign-in failed'));

    await error;
  });

  test('keeps a timeout verification ID for manual confirmation', () async {
    final fake = FakeFirebaseAuthGateway();
    final service = FirebaseAuthService(gateway: fake);

    final sent = service.sendCode('+905551112233');
    await fake.onCodeAutoRetrievalTimeout('timeout-id');
    await sent;

    expect(service.verificationId, 'timeout-id');
  });
}
