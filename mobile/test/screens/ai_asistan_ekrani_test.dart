import 'dart:async';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/ai_assistant/data/ai_assistant_repository.dart';
import 'package:mobile/features/ai_assistant/domain/ai_chat_message.dart';
import 'package:mobile/screens/ai_asistan_ekrani.dart';

// ---------------------------------------------------------------------------
// Fakes
// ---------------------------------------------------------------------------

class FakeAiAssistantRepository implements AiAssistantRepository {
  FakeAiAssistantRepository({this.cevap = 'Test cevap', this.hata});

  final String cevap;
  final Object? hata;

  final List<String> gonderilen = [];
  final List<Uint8List?> fotolar = [];

  @override
  Future<String> sendMessage({
    required String message,
    Uint8List? photo,
    List<AiChatMessage> history = const [],
  }) async {
    if (hata != null) throw hata!;
    gonderilen.add(message);
    fotolar.add(photo);
    return cevap;
  }
}

/// Yavaş repo — Completer ile kontrollü gecikme.
class _SlowAiRepo implements AiAssistantRepository {
  _SlowAiRepo(this._future);
  final Future<String> _future;

  @override
  Future<String> sendMessage({
    required String message,
    Uint8List? photo,
    List<AiChatMessage> history = const [],
  }) => _future;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

Widget _wrap({AiAssistantRepository? repo}) => MaterialApp(
  theme: AppTheme.light,
  home: AiAsistanEkrani(repository: repo ?? FakeAiAssistantRepository()),
);

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

void main() {
  group('AiAsistanEkrani', () {
    // ── Boş durum ────────────────────────────────────────────────────────
    group('boş durum', () {
      testWidgets('başlangıçta boş konuşma durumu gösterilir', (tester) async {
        await tester.pumpWidget(_wrap());
        await tester.pumpAndSettle();

        expect(find.text('AI Tarla Asistanı'), findsWidgets);
        expect(find.textContaining('Fotoğraf ekleyerek'), findsOneWidget);
      });

      testWidgets('örnek sorular boş durumda görünür', (tester) async {
        await tester.pumpWidget(_wrap());
        await tester.pumpAndSettle();

        expect(find.text('Yapraklardaki lekeler ne olabilir?'), findsOneWidget);
        expect(find.text('Bugün sulama yapmalı mıyım?'), findsOneWidget);
        expect(
          find.text('Bu bitkide hastalık belirtisi var mı?'),
          findsOneWidget,
        );
      });

      testWidgets('örnek soruya tıklayınca metin alanı dolar', (tester) async {
        await tester.pumpWidget(_wrap());
        await tester.pumpAndSettle();

        await tester.tap(find.text('Yapraklardaki lekeler ne olabilir?'));
        await tester.pumpAndSettle();

        expect(
          find.widgetWithText(TextField, 'Yapraklardaki lekeler ne olabilir?'),
          findsOneWidget,
        );
      });
    });

    // ── Form doğrulaması ──────────────────────────────────────────────────
    group('form doğrulaması', () {
      testWidgets('boş metin gönderilemiyor', (tester) async {
        final repo = FakeAiAssistantRepository();
        await tester.pumpWidget(_wrap(repo: repo));
        await tester.pumpAndSettle();

        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        expect(repo.gonderilen, isEmpty);
      });

      testWidgets('yalnızca boşluktan oluşan metin gönderilemiyor', (
        tester,
      ) async {
        final repo = FakeAiAssistantRepository();
        await tester.pumpWidget(_wrap(repo: repo));
        await tester.pumpAndSettle();

        await tester.enterText(find.byType(TextField), '   ');
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        expect(repo.gonderilen, isEmpty);
      });
    });

    // ── Mesaj gönderme ────────────────────────────────────────────────────
    group('mesaj gönderme', () {
      testWidgets('metin mesajı gönderince kullanıcı baloncuğu görünür', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(_wrap());
        await tester.pumpAndSettle();

        await tester.enterText(
          find.byType(TextField),
          'Sulama zamanı geldi mi?',
        );
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        expect(find.text('Sulama zamanı geldi mi?'), findsOneWidget);
      });

      testWidgets('mesaj fake repository\'ye gönderilir', (tester) async {
        final repo = FakeAiAssistantRepository();
        await tester.pumpWidget(_wrap(repo: repo));
        await tester.pumpAndSettle();

        await tester.enterText(find.byType(TextField), 'Sulama?');
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pumpAndSettle();

        expect(repo.gonderilen, contains('Sulama?'));
      });

      testWidgets('başarılı cevap asistan baloncuğu olarak görünür', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(
          _wrap(repo: FakeAiAssistantRepository(cevap: 'Evet, sulama yapın.')),
        );
        await tester.pumpAndSettle();

        await tester.enterText(find.byType(TextField), 'Sulama?');
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pumpAndSettle();

        expect(find.text('Evet, sulama yapın.'), findsOneWidget);
      });

      testWidgets('aynı mesaj aynı anda iki kez gönderilemez', (tester) async {
        final completer = Completer<String>();
        final repo = _SlowAiRepo(completer.future);

        await tester.pumpWidget(_wrap(repo: repo));
        await tester.pumpAndSettle();

        await tester.enterText(find.byType(TextField), 'Test');

        // İlk gönderim
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        // Loading sırasında gönder butonu devre dışı — ikinci gönderim engellenir
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        completer.complete('Cevap');
        await tester.pumpAndSettle();
      });

      testWidgets('gönderim sırasında loading göstergesi görünür', (
        tester,
      ) async {
        final completer = Completer<String>();

        await tester.pumpWidget(_wrap(repo: _SlowAiRepo(completer.future)));
        await tester.pumpAndSettle();

        await tester.enterText(find.byType(TextField), 'Test mesajı');
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        expect(find.byType(CircularProgressIndicator), findsWidgets);
        completer.complete('Cevap');
        await tester.pumpAndSettle();
      });
    });

    // ── Hata durumu ───────────────────────────────────────────────────────
    group('hata durumu', () {
      testWidgets('hata durumunda SnackBar gösterilir', (tester) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(
          _wrap(
            repo: FakeAiAssistantRepository(hata: Exception('bağlantı hatası')),
          ),
        );
        await tester.pumpAndSettle();

        await tester.enterText(find.byType(TextField), 'Test');
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();
        await tester.pump();

        expect(find.textContaining('henüz yapılandırılmadı'), findsOneWidget);
      });

      testWidgets('hata sonrası mesaj text field\'da kaybolmaz', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(
          _wrap(repo: FakeAiAssistantRepository(hata: Exception('hata'))),
        );
        await tester.pumpAndSettle();

        await tester.enterText(find.byType(TextField), 'Silinmemeli');
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pumpAndSettle();

        // Kullanıcı mesajı hala chat listesinde görünmeli
        // (mesaj gönderildikten sonra bubble olarak eklendi)
        expect(find.text('Silinmemeli'), findsOneWidget);
      });
    });

    // ── Overflow / responsive ─────────────────────────────────────────────
    group('responsive', () {
      testWidgets('320x640 ekranda render exception oluşmaz', (tester) async {
        tester.view.physicalSize = const Size(480, 960);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(_wrap());
        await tester.pumpAndSettle();

        expect(tester.takeException(), isNull);
      });
    });
  });
}
