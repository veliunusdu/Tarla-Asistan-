import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/services/firebase_auth_service.dart';

class FakeFirebaseAuthGateway implements FirebaseAuthGateway {
  String? email;
  String? password;
  bool registered = false;

  @override
  Future<String?> currentIdToken() async => null;

  @override
  Future<void> register({
    required String email,
    required String password,
  }) async {
    this.email = email;
    this.password = password;
    registered = true;
  }

  @override
  Future<void> signIn({required String email, required String password}) async {
    this.email = email;
    this.password = password;
  }

  @override
  Future<void> signOut() async {}
}

void main() {
  test('signs in with the supplied email and password', () async {
    final gateway = FakeFirebaseAuthGateway();
    final service = FirebaseAuthService(gateway: gateway);

    await service.signIn(email: 'farmer@example.com', password: 'secret1');

    expect(gateway.email, 'farmer@example.com');
    expect(gateway.password, 'secret1');
    expect(gateway.registered, isFalse);
  });

  test('registers a new user with the supplied email and password', () async {
    final gateway = FakeFirebaseAuthGateway();
    final service = FirebaseAuthService(gateway: gateway);

    await service.register(email: 'farmer@example.com', password: 'secret1');

    expect(gateway.email, 'farmer@example.com');
    expect(gateway.password, 'secret1');
    expect(gateway.registered, isTrue);
  });
}
