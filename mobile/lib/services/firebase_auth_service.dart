import 'package:firebase_auth/firebase_auth.dart';

abstract class FirebaseAuthGateway {
  Future<void> signIn({required String email, required String password});
  Future<void> register({required String email, required String password});
  Future<String?> currentIdToken();
  Future<void> signOut();
}

class FirebaseAuthService {
  FirebaseAuthService({FirebaseAuthGateway? gateway})
    : _gateway = gateway ?? FirebaseAuthSdkGateway();

  final FirebaseAuthGateway _gateway;

  Future<void> signIn({required String email, required String password}) =>
      _gateway.signIn(email: email, password: password);

  Future<void> register({required String email, required String password}) =>
      _gateway.register(email: email, password: password);

  Future<String?> currentIdToken() => _gateway.currentIdToken();

  Future<void> signOut() => _gateway.signOut();
}

class FirebaseAuthSdkGateway implements FirebaseAuthGateway {
  FirebaseAuthSdkGateway({FirebaseAuth? auth}) : _providedAuth = auth;

  final FirebaseAuth? _providedAuth;

  FirebaseAuth get _auth => _providedAuth ?? FirebaseAuth.instance;

  @override
  Future<void> signIn({required String email, required String password}) async {
    await _auth.signInWithEmailAndPassword(email: email, password: password);
  }

  @override
  Future<void> register({
    required String email,
    required String password,
  }) async {
    await _auth.createUserWithEmailAndPassword(
      email: email,
      password: password,
    );
  }

  @override
  Future<String?> currentIdToken() async {
    final user = _auth.currentUser;
    return user == null ? null : user.getIdToken();
  }

  @override
  Future<void> signOut() => _auth.signOut();
}
