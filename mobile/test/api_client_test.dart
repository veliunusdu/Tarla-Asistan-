import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/services/api_client.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test(
    'rotates an expired session and retries the authorized request',
    () async {
      SharedPreferences.setMockInitialValues({
        'access_token': 'expired-access',
        'refresh_token': 'valid-refresh',
      });
      var taskAttempts = 0;
      final httpClient = MockClient((request) async {
        if (request.url.path.endsWith('/auth/refresh')) {
          expect(jsonDecode(request.body), {'refresh_token': 'valid-refresh'});
          return http.Response(
            jsonEncode({
              'access_token': 'new-access',
              'refresh_token': 'new-refresh',
            }),
            200,
          );
        }
        taskAttempts += 1;
        if (taskAttempts == 1) {
          expect(request.headers['Authorization'], 'Bearer expired-access');
          return http.Response(jsonEncode({'detail': 'expired'}), 401);
        }
        expect(request.headers['Authorization'], 'Bearer new-access');
        return http.Response(jsonEncode({'id': 'task-1'}), 200);
      });
      final api = ApiClient(httpClient: httpClient);

      final task = await api.getJson('/tasks/task-1');

      expect(task['id'], 'task-1');
      expect(taskAttempts, 2);
      final prefs = await SharedPreferences.getInstance();
      expect(prefs.getString('access_token'), 'new-access');
      expect(prefs.getString('refresh_token'), 'new-refresh');
      api.close();
    },
  );
}
