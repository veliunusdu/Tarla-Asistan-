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
  }) : _http = httpClient ?? http.Client(),
       _idTokenProvider =
           idTokenProvider ??
           (() async {
             final user = FirebaseAuth.instance.currentUser;
             return user == null ? null : user.getIdToken();
           });

  final http.Client _http;
  final Future<String?> Function() _idTokenProvider;
  static const _timeout = Duration(seconds: 12);

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

  Future<void> sendQueued({
    required String method,
    required String endpoint,
    required Map<String, dynamic> body,
  }) async {
    await _send(method, endpoint, body: body);
  }

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
    final uri = Uri.parse('${AppConfig.apiBaseUrl}$endpoint');
    final headers = <String, String>{
      'Accept': 'application/json',
      'Content-Type': 'application/json',
      'Authorization': 'Bearer $token',
    };
    try {
      final request = http.Request(method, uri)..headers.addAll(headers);
      if (body != null) request.body = jsonEncode(body);
      final streamed = await _http.send(request).timeout(_timeout);
      final response = await http.Response.fromStream(streamed);
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw ApiException(
          _errorMessage(response),
          statusCode: response.statusCode,
          retryable: response.statusCode >= 500 || response.statusCode == 429,
        );
      }
      return response;
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

  Map<String, dynamic> _decodeObject(http.Response response) {
    if (response.body.isEmpty) return <String, dynamic>{};
    final value = jsonDecode(response.body);
    if (value is Map<String, dynamic>) return value;
    throw const ApiException('Sunucudan beklenmeyen bir cevap alındı.');
  }

  String _errorMessage(http.Response response) {
    try {
      final value = jsonDecode(response.body);
      if (value is Map && value['detail'] is String) {
        return value['detail'] as String;
      }
    } on FormatException {
      // A safe generic message is returned below.
    }
    if (response.statusCode == 401) {
      return 'Oturumunuz sona erdi. Tekrar giriş yapın.';
    }
    if (response.statusCode == 403) return 'Bu işlem için yetkiniz bulunmuyor.';
    return 'İşlem tamamlanamadı (${response.statusCode}).';
  }

  void close() => _http.close();
}
