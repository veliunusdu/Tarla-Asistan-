import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/services/api_client.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test('gets a fresh Firebase ID token for every authorized request', () async {
    var tokenCalls = 0;
    final httpClient = MockClient((request) async {
      expect(
        request.headers['Authorization'],
        'Bearer fresh-token-$tokenCalls',
      );
      return http.Response(jsonEncode({'id': 'task-1'}), 200);
    });
    final api = ApiClient(
      httpClient: httpClient,
      idTokenProvider: () async => 'fresh-token-${++tokenCalls}',
    );

    final firstTask = await api.getJson('/tasks/task-1');
    final secondTask = await api.getJson('/tasks/task-1');

    expect(firstTask['id'], 'task-1');
    expect(secondTask['id'], 'task-1');
    expect(tokenCalls, 2);
    api.close();
  });

  test('does not send a request without a Firebase ID token', () async {
    var requestSent = false;
    final api = ApiClient(
      httpClient: MockClient((request) async {
        requestSent = true;
        return http.Response('{}', 200);
      }),
      idTokenProvider: () async => null,
    );

    await expectLater(
      api.getJson('/tasks/task-1'),
      throwsA(isA<ApiException>()),
    );

    expect(requestSent, isFalse);
    api.close();
  });
}
