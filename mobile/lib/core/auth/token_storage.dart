import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class TokenStorage {
  TokenStorage({FlutterSecureStorage? storage})
    : _storage = storage ?? const FlutterSecureStorage();

  static const _accessTokenKey = 'access_token';
  static const _refreshTokenKey = 'refresh_token';

  final FlutterSecureStorage _storage;

  Future<void> saveAccessToken(String token) {
    return _storage.write(key: _accessTokenKey, value: token);
  }

  Future<String?> readAccessToken() {
    return _storage.read(key: _accessTokenKey);
  }

  Future<void> saveRefreshToken(String token) {
    return _storage.write(key: _refreshTokenKey, value: token);
  }

  Future<String?> readRefreshToken() {
    return _storage.read(key: _refreshTokenKey);
  }

  Future<void> clearTokens() async {
    await Future.wait([
      _storage.delete(key: _accessTokenKey),
      _storage.delete(key: _refreshTokenKey),
    ]);
  }
}
