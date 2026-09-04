import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/screens/bildirimler_ekrani.dart';
import 'package:mobile/services/api_client.dart';

void main() {
  testWidgets('lists persistent notifications from the backend inbox', (tester) async {
    final client = ApiClient(
      httpClient: MockClient((request) async {
        expect(request.url.path, '/api/v1/notifications');
        return http.Response(
          '{"items":[{"id":"notice-1","title":"Yeni uzman göreviniz var","body":"İlaçlama planını inceleyin","deepLink":"tarla-asistani://farms/farm-1/tasks/task-1","data":"{\\"farm_id\\":\\"farm-1\\",\\"task_id\\":\\"task-1\\"}","readAtUtc":null}]}',
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        );
      }),
      idTokenProvider: () async => 'token',
    );

    await tester.pumpWidget(MaterialApp(home: BildirimlerEkrani(apiClient: client)));
    await tester.pumpAndSettle();

    expect(find.text('Bildirim Merkezi'), findsOneWidget);
    expect(find.text('Yeni uzman göreviniz var'), findsOneWidget);
    expect(find.text('İlaçlama planını inceleyin'), findsOneWidget);
  });
}
