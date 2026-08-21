import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;

import '../auth/token_storage.dart';
import 'api_config.dart';
import 'api_exception.dart';

class ApiClient {
  ApiClient({
    required this.config,
    this.tokenStorage,
    http.Client? httpClient,
    this.timeout = const Duration(seconds: 15),
  }) : _httpClient = httpClient ?? http.Client();

  final ApiConfig config;
  final TokenStorage? tokenStorage;
  final http.Client _httpClient;
  final Duration timeout;

  Future<Object?> get(String endpoint, {Map<String, String>? queryParameters}) {
    return _request('GET', endpoint, queryParameters: queryParameters);
  }

  Future<Object?> post(String endpoint, {Object? body}) {
    return _request('POST', endpoint, body: body);
  }

  Future<Object?> put(String endpoint, {Object? body}) {
    return _request('PUT', endpoint, body: body);
  }

  Future<Object?> patch(String endpoint, {Object? body}) {
    return _request('PATCH', endpoint, body: body);
  }

  Future<Object?> delete(String endpoint, {Object? body}) {
    return _request('DELETE', endpoint, body: body);
  }

  Future<Object?> _request(
    String method,
    String endpoint, {
    Object? body,
    Map<String, String>? queryParameters,
  }) async {
    try {
      final token = await tokenStorage?.readAccessToken();
      final request = http.Request(
        method,
        _buildUri(endpoint, queryParameters),
      );

      request.headers.addAll({
        'Accept': 'application/json',
        'Content-Type': 'application/json',
        if (token != null && token.isNotEmpty) 'Authorization': 'Bearer $token',
      });

      if (body != null) {
        request.body = jsonEncode(body);
      }

      final response = await (() async {
        final streamedResponse = await _httpClient.send(request);
        return http.Response.fromStream(streamedResponse);
      })().timeout(timeout);

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw ApiException.fromResponse(
          statusCode: response.statusCode,
          responseBody: response.body,
        );
      }

      if (response.statusCode == 204 || response.body.trim().isEmpty) {
        return null;
      }

      return jsonDecode(response.body);
    } on ApiException {
      rethrow;
    } on TimeoutException {
      throw const ApiException(
        statusCode: null,
        message: 'İstek zaman aşımına uğradı. Lütfen tekrar deneyin.',
      );
    } on SocketException {
      throw const ApiException(
        statusCode: null,
        message: 'İnternet bağlantınızı kontrol edip tekrar deneyin.',
      );
    } on http.ClientException {
      throw const ApiException(
        statusCode: null,
        message: 'Sunucuya ulaşılamadı. Lütfen tekrar deneyin.',
      );
    } on FormatException {
      throw const ApiException(
        statusCode: null,
        message: 'Sunucudan geçersiz bir yanıt alındı.',
      );
    }
  }

  Uri _buildUri(String endpoint, Map<String, String>? queryParameters) {
    final normalizedEndpoint = endpoint.trim().replaceFirst(RegExp(r'^/+'), '');
    final uri = Uri.parse('${config.baseUrl}/$normalizedEndpoint');

    if (queryParameters == null || queryParameters.isEmpty) {
      return uri;
    }

    return uri.replace(
      queryParameters: {...uri.queryParameters, ...queryParameters},
    );
  }

  void close() {
    _httpClient.close();
  }
}
