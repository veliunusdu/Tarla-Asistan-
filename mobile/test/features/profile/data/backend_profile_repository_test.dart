import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/features/profile/data/backend_profile_repository.dart';
import 'package:mobile/services/api_client.dart';

void main() {
  test('loads the backend profile from auth/me', () async {
    late String path;
    final repository = BackendProfileRepository(
      ApiClient(
        httpClient: MockClient((request) async {
          path = request.url.path;
          return http.Response.bytes(
            utf8.encode(
              jsonEncode({
                'id': 'user-1',
                'phone_number': 'firebase-user',
                'role': 'FARMER',
                'terms_accepted': true,
                'notifications_enabled': true,
                'full_name': 'Ayşe Demir',
                'province': 'Ankara',
                'district': 'Çankaya',
              }),
            ),
            200,
            headers: const {'content-type': 'application/json; charset=utf-8'},
          );
        }),
        idTokenProvider: () async => 'backend-token',
      ),
    );

    final profile = await repository.getCurrentProfile();

    expect(path, '/api/v1/auth/me');
    expect(profile.fullName, 'Ayşe Demir');
  });
}
