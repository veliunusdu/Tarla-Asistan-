import 'dart:async';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/services/api_client.dart';
import 'package:mobile/services/user_error_message.dart';

void main() {
  test('authorization rejection never allows offline session fallback', () {
    for (final code in [401, 403]) {
      expect(
        isTemporaryConnectionFailure(
          ApiException('error', statusCode: code, retryable: true),
        ),
        isFalse,
      );
    }
    expect(isTemporaryConnectionFailure(TimeoutException('timeout')), isTrue);
    expect(
      isTemporaryConnectionFailure(
        const ApiException('network', retryable: true),
      ),
      isTrue,
    );
    expect(
      isTemporaryConnectionFailure(StateError('invalid session')),
      isFalse,
    );
  });
  test(
    'weather errors distinguish authorization, service failure and timeout',
    () {
      expect(
        userErrorMessage(const ApiException('error', statusCode: 401)),
        contains('giriş'),
      );
      expect(
        userErrorMessage(const ApiException('error', statusCode: 403)),
        contains('yetkiniz'),
      );
      expect(
        userErrorMessage(const ApiException('error', statusCode: 503)),
        contains('geçici'),
      );
      expect(
        userErrorMessage(TimeoutException('timeout')),
        contains('zaman aşımı'),
      );
    },
  );
}
