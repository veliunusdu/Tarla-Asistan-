import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/api/api_exception.dart';

void main() {
  group('ApiException', () {
    test('detail alanındaki mesajı okur', () {
      final exception = ApiException.fromResponse(
        statusCode: 422,
        responseBody: '{"detail":"Telefon numarası geçersiz."}',
      );

      expect(exception.message, 'Telefon numarası geçersiz.');
    });

    test('message alanındaki mesajı okur', () {
      final exception = ApiException.fromResponse(
        statusCode: 409,
        responseBody: '{"message":"Kayıt zaten mevcut."}',
      );

      expect(exception.message, 'Kayıt zaten mevcut.');
    });

    test('geçersiz gövdede status koduna uygun güvenli mesaj döndürür', () {
      final exception = ApiException.fromResponse(
        statusCode: 401,
        responseBody: '<html>Unauthorized</html>',
      );

      expect(exception.message, 'Oturumunuz geçersiz veya süresi dolmuş.');
      expect(exception.responseBody, '<html>Unauthorized</html>');
    });
  });
}
