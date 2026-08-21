import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/api/api_config.dart';

void main() {
  group('ApiConfig', () {
    test('base URL sonundaki slash karakterlerini temizler', () {
      final config = ApiConfig(baseUrl: ' https://example.com/api/v1/// ');

      expect(config.baseUrl, 'https://example.com/api/v1');
    });

    test('boş base URL için anlaşılır hata üretir', () {
      expect(
        () => ApiConfig(baseUrl: '   '),
        throwsA(
          isA<StateError>().having(
            (error) => error.message,
            'message',
            contains('API_BASE_URL tanımlı değil'),
          ),
        ),
      );
    });

    test('geçersiz URL şemasını reddeder', () {
      expect(
        () => ApiConfig(baseUrl: 'ftp://example.com'),
        throwsA(isA<StateError>()),
      );
    });
  });
}
