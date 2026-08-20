import 'dart:async';

import 'package:firebase_auth/firebase_auth.dart';

abstract class FirebaseAuthGateway {
  Future<void> sendVerificationCode({
    required String phoneNumber,
    required FutureOr<void> Function(String verificationId) onCodeSent,
    required FutureOr<void> Function() onVerificationCompleted,
    required FutureOr<void> Function(Object error) onVerificationFailed,
    required FutureOr<void> Function(String verificationId)
    onCodeAutoRetrievalTimeout,
  });

  Future<String> confirmCode({
    required String verificationId,
    required String smsCode,
  });

  Future<String?> currentIdToken();

  Future<void> signOut();
}

class FirebaseAuthService {
  FirebaseAuthService({FirebaseAuthGateway? gateway})
    : _gateway = gateway ?? FirebaseAuthSdkGateway();

  final FirebaseAuthGateway _gateway;
  String? verificationId;

  Future<void> sendCode(String phone) {
    final completer = Completer<void>();
    return _gateway
        .sendVerificationCode(
          phoneNumber: phone,
          onCodeSent: (verificationId) {
            this.verificationId = verificationId;
            if (!completer.isCompleted) completer.complete();
          },
          onVerificationCompleted: () {
            if (!completer.isCompleted) completer.complete();
          },
          onVerificationFailed: (error) {
            if (!completer.isCompleted) completer.completeError(error);
          },
          onCodeAutoRetrievalTimeout: (verificationId) {
            this.verificationId = verificationId;
            if (!completer.isCompleted) completer.complete();
          },
        )
        .then((_) => completer.future);
  }

  Future<String> confirmCode(String verificationId, String smsCode) =>
      _gateway.confirmCode(verificationId: verificationId, smsCode: smsCode);

  Future<String?> currentIdToken() => _gateway.currentIdToken();

  Future<void> signOut() => _gateway.signOut();
}

class FirebaseAuthSdkGateway implements FirebaseAuthGateway {
  FirebaseAuthSdkGateway({FirebaseAuth? auth}) : _providedAuth = auth;

  final FirebaseAuth? _providedAuth;

  FirebaseAuth get _auth => _providedAuth ?? FirebaseAuth.instance;

  @override
  Future<void> sendVerificationCode({
    required String phoneNumber,
    required FutureOr<void> Function(String verificationId) onCodeSent,
    required FutureOr<void> Function() onVerificationCompleted,
    required FutureOr<void> Function(Object error) onVerificationFailed,
    required FutureOr<void> Function(String verificationId)
    onCodeAutoRetrievalTimeout,
  }) {
    return _auth.verifyPhoneNumber(
      phoneNumber: phoneNumber,
      verificationCompleted: (credential) async {
        try {
          await _auth.signInWithCredential(credential);
          await onVerificationCompleted();
        } catch (error) {
          await onVerificationFailed(FirebasePhoneAuthException(error));
        }
      },
      verificationFailed: onVerificationFailed,
      codeSent: (verificationId, _) => onCodeSent(verificationId),
      codeAutoRetrievalTimeout: onCodeAutoRetrievalTimeout,
    );
  }

  @override
  Future<String> confirmCode({
    required String verificationId,
    required String smsCode,
  }) async {
    final credential = PhoneAuthProvider.credential(
      verificationId: verificationId,
      smsCode: smsCode,
    );
    final user = (await _auth.signInWithCredential(credential)).user;
    final idToken = await user?.getIdToken();
    if (idToken == null || idToken.isEmpty) {
      throw StateError('Firebase kimlik belirteci alınamadı.');
    }
    return idToken;
  }

  @override
  Future<String?> currentIdToken() async {
    final user = _auth.currentUser;
    return user == null ? null : user.getIdToken();
  }

  @override
  Future<void> signOut() => _auth.signOut();
}

class FirebasePhoneAuthException implements Exception {
  const FirebasePhoneAuthException(this.cause);

  final Object cause;

  @override
  String toString() => 'Telefon doğrulaması tamamlanamadı: $cause';
}
