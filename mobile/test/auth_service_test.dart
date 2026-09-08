import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/services/auth_service.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test(
    'offline resume requires a session belonging to the same user',
    () async {
      SharedPreferences.setMockInitialValues({});
      final service = AuthService(
        httpClient: MockClient(
          (_) async => http.Response(
            jsonEncode({'access_token': 'access', 'refresh_token': 'refresh'}),
            200,
          ),
        ),
      );
      expect(await service.canResumeOffline('a'), isFalse);
      await service.authenticateWithFirebase('id-token', firebaseUid: 'a');
      expect(await service.canResumeOffline('a'), isTrue);
      expect(await service.canResumeOffline('b'), isFalse);
      await service.logout();
      expect(await service.canResumeOffline('a'), isFalse);
      service.close();
    },
  );

  test('legacy unbound session cannot be resumed by a new user', () async {
    SharedPreferences.setMockInitialValues({'access_token': 'legacy'});
    final service = AuthService();
    expect(await service.canResumeOffline('b'), isFalse);
    service.close();
  });

  test('exchanges a Firebase token and stores the backend session', () async {
    SharedPreferences.setMockInitialValues({});
    late Map<String, dynamic> body;
    final service = AuthService(
      httpClient: MockClient((request) async {
        body = jsonDecode(request.body) as Map<String, dynamic>;
        return http.Response(
          jsonEncode({
            'access_token': 'backend-access',
            'refresh_token': 'backend-refresh',
          }),
          200,
        );
      }),
    );

    final accessToken = await service.authenticateWithFirebase('firebase-id');

    expect(body, {'id_token': 'firebase-id'});
    expect(accessToken, 'backend-access');
    expect(await service.currentAccessToken(), 'backend-access');
    service.close();
  });
}
