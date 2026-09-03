import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

import '../config/app_config.dart';
import 'api_client.dart';

class OtpRequestResult {
  const OtpRequestResult({required this.expiresIn, this.debugOtp});

  final int expiresIn;
  final String? debugOtp;
}

class AuthService {
  AuthService({http.Client? httpClient}) : _http = httpClient ?? http.Client();

  final http.Client _http;
  static const _timeout = Duration(seconds: 35);

  Future<bool> get isAuthenticated async {
    final prefs = await SharedPreferences.getInstance();
    return (prefs.getString('access_token') ?? '').isNotEmpty;
  }

  Future<OtpRequestResult> requestOtp(String phoneNumber) async {
    final response = await _post('/auth/request-otp', {
      'phone_number': phoneNumber,
    });
    return OtpRequestResult(
      expiresIn: response['expires_in'] as int? ?? 180,
      debugOtp: response['debug_otp']?.toString(),
    );
  }

  Future<void> verifyOtp(String phoneNumber, String otpCode) async {
    final response = await _post('/auth/verify-otp', {
      'phone_number': phoneNumber,
      'otp_code': otpCode,
    });
    await _saveSession(response);
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('phone_number', phoneNumber);
  }

  Future<String> authenticateWithFirebase(String idToken) async {
    final response = await _post('/auth/firebase', {'id_token': idToken});
    return _saveSession(response);
  }

  Future<String?> currentAccessToken() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString('access_token');
  }

  Future<String> _saveSession(Map<String, dynamic> response) async {
    final accessToken = response['access_token']?.toString();
    final refreshToken = response['refresh_token']?.toString();
    if (accessToken == null || refreshToken == null) {
      throw const ApiException('Giriş cevabı geçersiz. Lütfen tekrar deneyin.');
    }
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('access_token', accessToken);
    await prefs.setString('refresh_token', refreshToken);
    return accessToken;
  }

  Future<void> logout() async {
    final prefs = await SharedPreferences.getInstance();
    final refreshToken = prefs.getString('refresh_token');
    if (refreshToken != null && refreshToken.isNotEmpty) {
      try {
        await _post('/auth/logout', {'refresh_token': refreshToken});
      } catch (_) {
        // Local credentials are removed even when the network is unavailable.
      }
    }
    await prefs.remove('access_token');
    await prefs.remove('refresh_token');
  }

  Future<Map<String, dynamic>> _post(
    String endpoint,
    Map<String, dynamic> body,
  ) async {
    try {
      final response = await _http
          .post(
            Uri.parse('${AppConfig.apiBaseUrl}$endpoint'),
            headers: const {'Content-Type': 'application/json'},
            body: jsonEncode(body),
          )
          .timeout(_timeout);
      final decoded = response.body.isEmpty ? null : jsonDecode(response.body);
      if (response.statusCode < 200 || response.statusCode >= 300) {
        final detail = decoded is Map ? decoded['detail']?.toString() : null;
        throw ApiException(
          detail ?? 'Giriş işlemi tamamlanamadı (${response.statusCode}).',
          statusCode: response.statusCode,
          retryable: response.statusCode >= 500 || response.statusCode == 429,
        );
      }
      if (response.body.isEmpty) return <String, dynamic>{};
      if (decoded is Map<String, dynamic>) return decoded;
      throw const ApiException('Sunucudan beklenmeyen bir cevap alındı.');
    } on TimeoutException {
      throw const ApiException(
        'Bağlantı zaman aşımına uğradı. İnternetinizi kontrol edin.',
        retryable: true,
      );
    } on http.ClientException {
      throw const ApiException(
        'Sunucuya ulaşılamadı. İnternetinizi kontrol edin.',
        retryable: true,
      );
    } on FormatException {
      throw const ApiException('Sunucudan geçersiz bir cevap alındı.');
    }
  }

  void close() => _http.close();
}
