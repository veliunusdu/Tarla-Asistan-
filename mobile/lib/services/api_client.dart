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
  static const _timeout = Duration(seconds: 35);

  // -------------------------------------------------------------------------
  // Public API
  // -------------------------------------------------------------------------

  Future<Map<String, dynamic>> getJson(
    String endpoint, {
    Map<String, dynamic>? queryParameters,
    bool requiresAuth = true,
  }) async {
    final response = await _send(
      'GET',
      endpoint,
      queryParameters: queryParameters,
      requiresAuth: requiresAuth,
    );
    return _decodeObject(response);
  }

  /// Fetches an endpoint whose JSON response is an array.
  Future<List<dynamic>> getJsonList(
    String endpoint, {
    Map<String, dynamic>? queryParameters,
  }) async {
    final response = await _send(
      'GET',
      endpoint,
      queryParameters: queryParameters,
    );
    return _decodeList(response);
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

  Future<Map<String, dynamic>> postMultipart(
    String endpoint, {
    Map<String, String>? fields,
    List<ApiMultipartFile>? files,
  }) async {
    final response = await _sendMultipart(
      'POST',
      endpoint,
      fields: fields,
      files: files,
    );
    return _decodeObject(response);
  }

  Future<void> delete(String endpoint) async {
    await _send('DELETE', endpoint);
  }

  Future<Map<String, String>> getAuthHeaders() async {
    final token = await _idTokenProvider();
    if (token != null && token.isNotEmpty) {
      return {'Authorization': 'Bearer $token'};
    }
    return {};
  }

  Future<void> sendQueued({
    required String method,
    required String endpoint,
    required Map<String, dynamic> body,
  }) async {
    await _send(method, endpoint, body: body);
  }

  void close() => _http.close();

  Stream<String> postStream(
    String endpoint, {
    Map<String, dynamic>? jsonBody,
    Map<String, String>? fields,
    List<ApiMultipartFile>? files,
    void Function(String conversationId)? onConversationId,
  }) async* {
    final baseUrl = AppConfig.apiBaseUrl.replaceFirst(RegExp(r'/+$'), '');
    final normalizedEndpoint = endpoint.replaceFirst(RegExp(r'^/+'), '');
    final uri = Uri.parse('$baseUrl/$normalizedEndpoint');

    Future<http.StreamedResponse> sendWithToken(String token) async {
      http.BaseRequest request;
      if (files != null && files.isNotEmpty) {
        final multipart = http.MultipartRequest('POST', uri)
          ..headers.addAll({
            'Accept': 'text/event-stream',
            'Authorization': 'Bearer $token',
          });
        if (fields != null) multipart.fields.addAll(fields);
        for (final file in files) {
          multipart.files.add(file.toMultipartFile());
        }
        request = multipart;
      } else {
        final req = http.Request('POST', uri)
          ..headers.addAll({
            'Accept': 'text/event-stream',
            'Content-Type': 'application/json; charset=utf-8',
            'Authorization': 'Bearer $token',
          });
        if (jsonBody != null) {
          req.body = jsonEncode(jsonBody);
        }
        request = req;
      }

      try {
        return await _http.send(request).timeout(_timeout);
      } on TimeoutException {
        throw const ApiException(
          'Bağlantı zaman aşımına uğradı. Lütfen tekrar deneyin.',
          retryable: true,
        );
      } on http.ClientException {
        throw const ApiException(
          'Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edin.',
          retryable: true,
        );
      }
    }

    var token = await _idTokenProvider();
    if (token == null || token.isEmpty) {
      throw const ApiException(
        'Devam etmek için tekrar giriş yapın.',
        statusCode: 401,
      );
    }

    var streamed = await sendWithToken(token);
    if (streamed.statusCode == 401) {
      // A retry must create a new HTTP request because streams are single-use.
      await streamed.stream.drain();
      token = await _forceRefreshTokenProvider();
      if (token == null || token.isEmpty) {
        throw const ApiException(
          'Oturumunuz sona erdi. Tekrar giriş yapın.',
          statusCode: 401,
        );
      }
      streamed = await sendWithToken(token);
    }

    if (streamed.statusCode != 200) {
      final body = await streamed.stream.bytesToString();
      try {
        final decoded = jsonDecode(body);
        if (decoded is Map && decoded['detail'] is String) {
          throw ApiException(decoded['detail'] as String, statusCode: streamed.statusCode);
        }
      } catch (e) {
        if (e is ApiException) rethrow;
      }
      throw ApiException('Sunucu hatası: ${streamed.statusCode}', statusCode: streamed.statusCode);
    }

    await for (final line in streamed.stream.toStringStream().transform(const LineSplitter())) {
      final trimmed = line.trim();
      if (!trimmed.startsWith('data:')) continue;
      final payload = trimmed.substring('data:'.length).trim();
      if (payload == '[DONE]') break;
      if (payload.isEmpty) continue;

      try {
        final decoded = jsonDecode(payload);
        if (decoded is Map<String, dynamic>) {
          final convId = decoded['conversation_id']?.toString();
          if (convId != null && convId.isNotEmpty && onConversationId != null) {
            onConversationId(convId);
          }
          final content = decoded['content']?.toString();
          if (content != null && content.isNotEmpty) {
            yield content;
          }
        }
      } catch (_) {}
    }
  }

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
    Map<String, dynamic>? queryParameters,
    bool requiresAuth = true,
  }) async {
    String? token;
    try {
      token = await _idTokenProvider();
    } catch (_) {
      token = null;
    }

    if (requiresAuth && (token == null || token.isEmpty)) {
      throw const ApiException(
        'Devam etmek için tekrar giriş yapın.',
        statusCode: 401,
      );
    }

    final response = await _execute(
      method,
      endpoint,
      token: token ?? '',
      body: body,
      queryParameters: queryParameters,
    );

    if (requiresAuth && response.statusCode == 401) {
      String? freshToken;
      try {
        freshToken = await _forceRefreshTokenProvider();
      } catch (_) {
        freshToken = null;
      }
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
        queryParameters: queryParameters,
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

  Future<http.Response> _sendMultipart(
    String method,
    String endpoint, {
    Map<String, String>? fields,
    List<ApiMultipartFile>? files,
  }) async {
    final token = await _idTokenProvider();
    if (token == null || token.isEmpty) {
      throw const ApiException(
        'Devam etmek için tekrar giriş yapın.',
        statusCode: 401,
      );
    }

    final response = await _executeMultipart(
      method,
      endpoint,
      token: token,
      fields: fields,
      files: files,
    );

    if (response.statusCode == 401) {
      final freshToken = await _forceRefreshTokenProvider();
      if (freshToken == null || freshToken.isEmpty) {
        throw const ApiException(
          'Oturumunuz sona erdi. Tekrar giriş yapın.',
          statusCode: 401,
        );
      }
      final retried = await _executeMultipart(
        method,
        endpoint,
        token: freshToken,
        fields: fields,
        files: files,
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

  Future<http.Response> _executeMultipart(
    String method,
    String endpoint, {
    required String token,
    Map<String, String>? fields,
    List<ApiMultipartFile>? files,
  }) async {
    final baseUrl = AppConfig.apiBaseUrl.replaceFirst(RegExp(r'/+$'), '');
    final normalizedEndpoint = endpoint.replaceFirst(RegExp(r'^/+'), '');
    final uri = Uri.parse('$baseUrl/$normalizedEndpoint');
    final request = http.MultipartRequest(method, uri)
      ..headers.addAll({
        'Accept': 'application/json',
        'Authorization': 'Bearer $token',
      });
    if (fields != null) request.fields.addAll(fields);
    if (files != null) {
      for (final file in files) {
        request.files.add(file.toMultipartFile());
      }
    }
    try {
      final streamed = await _http.send(request).timeout(_timeout);
      return await http.Response.fromStream(streamed);
    } on TimeoutException {
      throw const ApiException(
        'Bağlantı zaman aşımına uğradı. Lütfen tekrar deneyin.',
        retryable: true,
      );
    } on http.ClientException {
      throw const ApiException(
        'Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edin.',
        retryable: true,
      );
    }
  }

  /// Sends a single HTTP request without retry or token-refresh logic.
  Future<http.Response> _execute(
    String method,
    String endpoint, {
    required String token,
    Map<String, dynamic>? body,
    Map<String, dynamic>? queryParameters,
  }) async {
    final baseUrl = AppConfig.apiBaseUrl.replaceFirst(RegExp(r'/+$'), '');
    final normalizedEndpoint = endpoint.replaceFirst(RegExp(r'^/+'), '');
    var uri = Uri.parse('$baseUrl/$normalizedEndpoint');
    if (queryParameters != null && queryParameters.isNotEmpty) {
      final combinedQuery = <String, dynamic>{
        ...uri.queryParameters,
        ...queryParameters,
      };
      uri = uri.replace(queryParameters: combinedQuery);
    }
    final request = http.Request(method, uri)
      ..headers.addAll({
        'Accept': 'application/json',
        'Content-Type': 'application/json',
        if (token.isNotEmpty) 'Authorization': 'Bearer $token',
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

  List<dynamic> _decodeList(http.Response response) {
    try {
      final value = jsonDecode(response.body);
      if (value is List<dynamic>) return value;
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

class ApiMultipartFile {
  const ApiMultipartFile({
    required this.field,
    required this.bytes,
    this.filename,
    this.contentType,
  });

  final String field;
  final List<int> bytes;
  final String? filename;
  final String? contentType;

  http.MultipartFile toMultipartFile() => http.MultipartFile.fromBytes(
        field,
        bytes,
        filename: filename,
        contentType: contentType != null ? http.MediaType.parse(contentType!) : null,
      );
}
