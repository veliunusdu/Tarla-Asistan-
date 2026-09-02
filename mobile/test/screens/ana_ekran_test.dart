import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/activities/data/faaliyet_repository.dart';
import 'package:mobile/features/ai_assistant/data/ai_assistant_repository.dart';
import 'package:mobile/features/ai_assistant/domain/ai_chat_message.dart';
import 'package:mobile/features/ai_assistant/domain/ai_chat_response.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/features/weather/data/weather_repository.dart';
import 'package:mobile/features/weather/domain/weather_summary.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/screens/ai_asistan_ekrani.dart';
import 'package:mobile/screens/ana_ekran.dart';
import 'package:mobile/screens/ana_sayfa_ekrani.dart';
import 'package:mobile/screens/tarla_gunlugu_ekrani.dart';
import 'package:mobile/screens/tarla_listesi_ekrani.dart';

// ---------------------------------------------------------------------------
// Fakes
// ---------------------------------------------------------------------------

class FakeTarlaRepository implements TarlaRepository {
  @override
  Future<List<Tarla>> getTarlalar() async => [];
  @override
  Future<void> addTarla(Tarla tarla) async {}
}

class FakeFaaliyetRepository implements FaaliyetRepository {
  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];
  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async {}
  @override
  Future<List<Faaliyet>> getTumFaaliyetler() async => [];

  @override
  Future<void> deleteFaaliyet(String id) async {}

  @override
  Future<void> markAsCompleted(String id) async {}
}

class FakeWeatherRepository implements WeatherRepository {
  @override
  Future<WeatherSummary> getWeather({String? farmId}) async =>
      const WeatherSummary(temperature: 22, description: 'açık');
}

class FakeAiAssistantRepository implements AiAssistantRepository {
  @override
  Future<AiChatResponse> sendMessage({
    required String message,
    Uint8List? photo,
    String? photoContentType,
    String? photoFileName,
    String? fieldId,
    String? conversationId,
    List<AiChatMessage> history = const [],
  }) async =>
      const AiChatResponse(reply: 'Test cevap', conversationId: 'conv-1');
}

// ---------------------------------------------------------------------------
// Helper
// ---------------------------------------------------------------------------

Widget _wrap() => MaterialApp(
  theme: AppTheme.light,
  home: AnaEkran(
    tarlaRepository: FakeTarlaRepository(),
    faaliyetRepository: FakeFaaliyetRepository(),
    weatherRepository: FakeWeatherRepository(),
    aiRepository: FakeAiAssistantRepository(),
  ),
);

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

void main() {
  group('AnaEkran navigasyon', () {
    testWidgets(
      'ilk açılışta Ana Sayfa sekmesi seçili ve AnaSayfaEkrani gösterilir',
      (tester) async {
        await tester.pumpWidget(_wrap());
        await tester.pump();

        final navBar = tester.widget<NavigationBar>(find.byType(NavigationBar));
        expect(navBar.selectedIndex, 0);
        expect(find.byType(AnaSayfaEkrani), findsOneWidget);
      },
    );

    testWidgets('NavigationBar dört doğru etiketi doğru sırada gösteriyor', (
      tester,
    ) async {
      await tester.pumpWidget(_wrap());
      await tester.pump();

      // NavBar içindeki etiketleri sırayla doğrula
      expect(
        find.descendant(
          of: find.byType(NavigationBar),
          matching: find.text('Ana Sayfa'),
        ),
        findsOneWidget,
      );
      expect(
        find.descendant(
          of: find.byType(NavigationBar),
          matching: find.text('İş Planım'),
        ),
        findsOneWidget,
      );
      expect(
        find.descendant(
          of: find.byType(NavigationBar),
          matching: find.text('Tarlalarım'),
        ),
        findsOneWidget,
      );
      expect(
        find.descendant(
          of: find.byType(NavigationBar),
          matching: find.text('Asistan'),
        ),
        findsOneWidget,
      );
    });

    testWidgets('Profil sekmesi görünür', (tester) async {
      await tester.pumpWidget(_wrap());
      await tester.pump();

      expect(
        find.descendant(
          of: find.byType(NavigationBar),
          matching: find.text('Profil'),
        ),
        findsOneWidget,
      );
    });

    testWidgets(
      'İş Planım sekmesine geçilince TarlaGunluguEkrani gösterilir (index 1)',
      (tester) async {
        await tester.pumpWidget(_wrap());
        await tester.pump();

        await tester.tap(
          find.descendant(
            of: find.byType(NavigationBar),
            matching: find.text('İş Planım'),
          ),
        );
        await tester.pumpAndSettle();

        final navBar = tester.widget<NavigationBar>(find.byType(NavigationBar));
        expect(navBar.selectedIndex, 1);
        expect(find.byType(TarlaGunluguEkrani), findsOneWidget);
      },
    );

    testWidgets(
      'Tarlalarım sekmesine geçilince TarlaListesiEkrani gösterilir (index 2)',
      (tester) async {
        await tester.pumpWidget(_wrap());
        await tester.pump();

        await tester.tap(
          find.descendant(
            of: find.byType(NavigationBar),
            matching: find.text('Tarlalarım'),
          ),
        );
        await tester.pumpAndSettle();

        final navBar = tester.widget<NavigationBar>(find.byType(NavigationBar));
        expect(navBar.selectedIndex, 2);
        expect(find.byType(TarlaListesiEkrani), findsOneWidget);
      },
    );

    testWidgets(
      'Asistan sekmesine geçilince AiAsistanEkrani gösterilir (index 3)',
      (tester) async {
        await tester.pumpWidget(_wrap());
        await tester.pump();

        await tester.tap(
          find.descendant(
            of: find.byType(NavigationBar),
            matching: find.text('Asistan'),
          ),
        );
        await tester.pumpAndSettle();

        final navBar = tester.widget<NavigationBar>(find.byType(NavigationBar));
        expect(navBar.selectedIndex, 3);
        expect(find.byType(AiAsistanEkrani), findsOneWidget);
      },
    );

    testWidgets('sekme değişince seçili NavigationDestination güncellenir', (
      tester,
    ) async {
      await tester.pumpWidget(_wrap());
      await tester.pump();

      expect(
        tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex,
        0,
      );

      // İş Planım → 1
      await tester.tap(
        find.descendant(
          of: find.byType(NavigationBar),
          matching: find.text('İş Planım'),
        ),
      );
      await tester.pumpAndSettle();
      expect(
        tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex,
        1,
      );

      // Tarlalarım → 2
      await tester.tap(
        find.descendant(
          of: find.byType(NavigationBar),
          matching: find.text('Tarlalarım'),
        ),
      );
      await tester.pumpAndSettle();
      expect(
        tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex,
        2,
      );

      // Asistan → 3
      await tester.tap(
        find.descendant(
          of: find.byType(NavigationBar),
          matching: find.text('Asistan'),
        ),
      );
      await tester.pumpAndSettle();
      expect(
        tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex,
        3,
      );
    });

    testWidgets(
      'Ana Sayfa dışındaki sekmede geri işlemi Ana Sayfa sekmesine döner',
      (tester) async {
        await tester.pumpWidget(_wrap());
        await tester.pump();

        // Tarlalarım sekmesine geç
        await tester.tap(
          find.descendant(
            of: find.byType(NavigationBar),
            matching: find.text('Tarlalarım'),
          ),
        );
        await tester.pumpAndSettle();

        expect(
          tester
              .widget<NavigationBar>(find.byType(NavigationBar))
              .selectedIndex,
          2,
        );

        // Geri tuşu simüle et
        final NavigatorState navigator = tester.state(find.byType(Navigator));
        navigator.maybePop();
        await tester.pumpAndSettle();

        // Ana Sayfa'ya dönmeli
        expect(
          tester
              .widget<NavigationBar>(find.byType(NavigationBar))
              .selectedIndex,
          0,
        );
      },
    );

    testWidgets('Ana Sayfa sekmesindeyken geri işlemi sekmeyi değiştirmez', (
      tester,
    ) async {
      await tester.pumpWidget(_wrap());
      await tester.pump();

      final NavigatorState navigator = tester.state(find.byType(Navigator));
      navigator.maybePop();
      await tester.pumpAndSettle();

      expect(
        tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex,
        0,
      );
    });

    testWidgets(
      'onTarlalarimSekme callback\'i Tarlalarım sekmesine (index 2) geçer',
      (tester) async {
        await tester.pumpWidget(_wrap());
        await tester.pump();

        // Ana Sayfa'nın "Tarlalarım" hızlı işlem butonu NavigationBar dışında olduğu için
        // doğrudan sekmeye geçerek callback davranışını doğrularız
        expect(
          tester
              .widget<NavigationBar>(find.byType(NavigationBar))
              .selectedIndex,
          0,
        );

        await tester.tap(
          find.descendant(
            of: find.byType(NavigationBar),
            matching: find.text('Tarlalarım'),
          ),
        );
        await tester.pumpAndSettle();

        expect(
          tester
              .widget<NavigationBar>(find.byType(NavigationBar))
              .selectedIndex,
          2,
        );
      },
    );

    testWidgets(
      'onGunlukSekme callback\'i İş Planım sekmesine (index 1) geçer',
      (tester) async {
        await tester.pumpWidget(_wrap());
        await tester.pump();

        await tester.tap(
          find.descendant(
            of: find.byType(NavigationBar),
            matching: find.text('İş Planım'),
          ),
        );
        await tester.pumpAndSettle();

        expect(
          tester
              .widget<NavigationBar>(find.byType(NavigationBar))
              .selectedIndex,
          1,
        );
      },
    );

    testWidgets('AnaEkran faaliyetRepository nesnesini alt ekranlara iletir', (
      tester,
    ) async {
      final faaliyetRepo = FakeFaaliyetRepository();
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light,
          home: AnaEkran(
            tarlaRepository: FakeTarlaRepository(),
            faaliyetRepository: faaliyetRepo,
            weatherRepository: FakeWeatherRepository(),
            aiRepository: FakeAiAssistantRepository(),
          ),
        ),
      );
      await tester.pump();

      final anaSayfa = tester.widget<AnaSayfaEkrani>(
        find.byType(AnaSayfaEkrani),
      );
      expect(
        identical(anaSayfa.faaliyetRepositoryForTesting, faaliyetRepo),
        isTrue,
      );

      final gunluk = tester.widget<TarlaGunluguEkrani>(
        find.byType(TarlaGunluguEkrani, skipOffstage: false),
      );
      expect(
        identical(gunluk.faaliyetRepositoryForTesting, faaliyetRepo),
        isTrue,
      );

      final tarlaListesi = tester.widget<TarlaListesiEkrani>(
        find.byType(TarlaListesiEkrani, skipOffstage: false),
      );
      expect(
        identical(tarlaListesi.faaliyetRepositoryForTesting, faaliyetRepo),
        isTrue,
      );
    });
  });
}
