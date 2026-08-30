import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/services/auth_service.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

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
