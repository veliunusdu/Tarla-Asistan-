import 'dart:convert';

class ApiException implements Exception {
  const ApiException({
    required this.statusCode,
    required this.message,
    this.responseBody,
  });

  factory ApiException.fromResponse({
    required int statusCode,
    String? responseBody,
  }) {
    return ApiException(
      statusCode: statusCode,
      message:
          _extractBackendMessage(responseBody) ?? _messageForStatus(statusCode),
      responseBody: responseBody,
    );
  }

  final int? statusCode;
  final String message;
  final String? responseBody;

  static String? _extractBackendMessage(String? responseBody) {
    if (responseBody == null || responseBody.trim().isEmpty) {
      return null;
    }

    try {
      final decoded = jsonDecode(responseBody);
      if (decoded is! Map<String, dynamic>) {
        return null;
      }

      for (final key in const ['detail', 'message']) {
        final value = decoded[key];
        if (value is String && value.trim().isNotEmpty) {
          return value.trim();
        }
      }
    } on FormatException {
      return null;
    }

    return null;
  }

  static String _messageForStatus(int statusCode) {
    if (statusCode >= 500) {
      return 'Sunucuda bir sorun oluştu. Lütfen daha sonra tekrar deneyin.';
    }

    return switch (statusCode) {
      400 => 'Gönderilen bilgiler geçersiz.',
      401 => 'Oturumunuz geçersiz veya süresi dolmuş.',
      403 => 'Bu işlem için yetkiniz bulunmuyor.',
      404 => 'İstenen içerik bulunamadı.',
      409 => 'İşlem mevcut bir kayıtla çakışıyor.',
      422 => 'Gönderilen bilgiler doğrulanamadı.',
      429 => 'Çok fazla istek gönderildi. Lütfen daha sonra tekrar deneyin.',
      _ => 'İstek tamamlanamadı. Lütfen tekrar deneyin.',
    };
  }

  @override
  String toString() => 'ApiException(statusCode: $statusCode)';
}
