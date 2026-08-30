import 'dart:async';
import 'dart:convert';

import 'package:firebase_auth/firebase_auth.dart';
import 'package:http/http.dart' as http;

import '../config/app_config.dart';

class ApiException implements Exception {
  const ApiException(this.message, {this.statusCode, this.retryable = false});

  final String message;
  final int? statusCode;
  final bool retryable;

  @override
  String toString() => message;
}

class ApiClient {
  ApiClient({
    http.Client? httpClient,
    Future<String?> Function()? idTokenProvider,
    Future<String?> Function()? forceRefreshTokenProvider,
  }) : _http = httpClient ?? http.Client(),
       _idTokenProvider =
           idTokenProvider ?? _defaultTokenProvider(forceRefresh: false),
       _forceRefreshTokenProvider =
           forceRefreshTokenProvider ??
           _defaultTokenProvider(forceRefresh: true);

  static Future<String?> Function() _defaultTokenProvider({
    required bool forceRefresh,
  }) => () async {
    final user = FirebaseAuth.instance.currentUser;
    return user?.getIdToken(forceRefresh);
  };

  final http.Client _http;
  final Future<String?> Function() _idTokenProvider;
  final Future<String?> Function() _forceRefreshTokenProvider;
  static const _timeout = Duration(seconds: 12);

  // -------------------------------------------------------------------------
  // Public API
  // -------------------------------------------------------------------------

  Future<Map<String, dynamic>> getJson(String endpoint) async {
    final response = await _send('GET', endpoint);
    return _decodeObject(response);
  }

  Future<Map<String, dynamic>> postJson(
    String endpoint,
    Map<String, dynamic> body,
  ) async {
    final response = await _send('POST', endpoint, body: body);
    return _decodeObject(response);
  }

  Future<Map<String, dynamic>> putJson(
    String endpoint,
    Map<String, dynamic> body,
  ) async {
    final response = await _send('PUT', endpoint, body: body);
    return _decodeObject(response);
  }

  Future<Map<String, dynamic>> patchJson(
    String endpoint,
    Map<String, dynamic> body,
  ) async {
    final response = await _send('PATCH', endpoint, body: body);
    return _decodeObject(response);
  }

  Future<void> delete(String endpoint) async {
    await _send('DELETE', endpoint);
  }

  Future<void> sendQueued({
    required String method,
    required String endpoint,
    required Map<String, dynamic> body,
  }) async {
    await _send(method, endpoint, body: body);
  }

  void close() => _http.close();

  // -------------------------------------------------------------------------
  // Internal
  // -------------------------------------------------------------------------

  /// Obtains a token, executes the request, and retries once on HTTP 401
  /// using a force-refreshed Firebase ID token. A second 401 throws immediately
  /// without further retries.
  Future<http.Response> _send(
    String method,
    String endpoint, {
    Map<String, dynamic>? body,
  }) async {
    final token = await _idTokenProvider();
    if (token == null || token.isEmpty) {
      throw const ApiException(
        'Devam etmek için tekrar giriş yapın.',
        statusCode: 401,
      );
    }

    final response = await _execute(method, endpoint, token: token, body: body);

    if (response.statusCode == 401) {
      final freshToken = await _forceRefreshTokenProvider();
      if (freshToken == null || freshToken.isEmpty) {
        throw const ApiException(
          'Oturumunuz sona erdi. Tekrar giriş yapın.',
          statusCode: 401,
        );
      }
      final retried = await _execute(
        method,
        endpoint,
        token: freshToken,
        body: body,
      );
      if (retried.statusCode == 401) {
        throw ApiException(_errorMessage(retried), statusCode: 401);
      }
      _checkStatus(retried);
      return retried;
    }

    _checkStatus(response);
    return response;
  }

  /// Sends a single HTTP request without retry or token-refresh logic.
  Future<http.Response> _execute(
    String method,
    String endpoint, {
    required String token,
    Map<String, dynamic>? body,
  }) async {
    final baseUrl = AppConfig.apiBaseUrl.replaceFirst(RegExp(r'/+$'), '');
    final normalizedEndpoint = endpoint.replaceFirst(RegExp(r'^/+'), '');
    final uri = Uri.parse('$baseUrl/$normalizedEndpoint');
    final request = http.Request(method, uri)
      ..headers.addAll({
        'Accept': 'application/json',
        'Content-Type': 'application/json',
        'Authorization': 'Bearer $token',
      });
    if (body != null) request.body = jsonEncode(body);
    try {
      final streamed = await _http.send(request).timeout(_timeout);
      return await http.Response.fromStream(streamed);
    } on TimeoutException {
      throw const ApiException(
        'Bağlantı zaman aşımına uğradı. İşlem çevrimdışı kuyruğa alındı.',
        retryable: true,
      );
    } on http.ClientException {
      throw const ApiException(
        'Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edin.',
        retryable: true,
      );
    }
  }

  void _checkStatus(http.Response response) {
    if (response.statusCode >= 200 && response.statusCode < 300) return;
    throw ApiException(
      _errorMessage(response),
      statusCode: response.statusCode,
      retryable: response.statusCode >= 500 || response.statusCode == 429,
    );
  }

  Map<String, dynamic> _decodeObject(http.Response response) {
    if (response.statusCode == 204 || response.body.trim().isEmpty) {
      return <String, dynamic>{};
    }
    try {
      final value = jsonDecode(response.body);
      if (value is Map<String, dynamic>) return value;
    } on FormatException {
      // Fall through to the generic error below.
    }
    throw const ApiException('Sunucudan beklenmeyen bir cevap alındı.');
  }

  String _errorMessage(http.Response response) {
    try {
      final value = jsonDecode(response.body);
      if (value is Map) {
        final detail = value['detail'];
        if (detail is String && detail.isNotEmpty) return detail;
        // FastAPI validation error: detail is a list of field errors.
        if (detail is List) return 'Gönderilen bilgiler doğrulanamadı.';
      }
    } on FormatException {
      // Fall through to status-based messages.
    }
    return _messageForStatus(response.statusCode);
  }

  static String _messageForStatus(int statusCode) => switch (statusCode) {
    401 => 'Oturumunuz sona erdi. Tekrar giriş yapın.',
    403 => 'Bu işlem için yetkiniz bulunmuyor.',
    404 => 'İstenen içerik bulunamadı.',
    409 => 'İşlem mevcut bir kayıtla çakışıyor.',
    413 => 'Gönderilen dosya çok büyük.',
    415 => 'Desteklenmeyen dosya türü.',
    422 => 'Gönderilen bilgiler doğrulanamadı.',
    429 => 'Çok fazla istek gönderildi. Lütfen daha sonra tekrar deneyin.',
    _ when statusCode >= 500 =>
      'Sunucuda bir sorun oluştu. Lütfen daha sonra tekrar deneyin.',
    _ => 'İşlem tamamlanamadı ($statusCode).',
  };
}
