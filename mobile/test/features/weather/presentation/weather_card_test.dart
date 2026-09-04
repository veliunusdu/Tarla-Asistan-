import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_colors.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/weather/domain/weather_summary.dart';
import 'package:mobile/features/weather/presentation/weather_presentation_helper.dart';
import 'package:mobile/features/weather/presentation/widgets/weather_card.dart';

Widget _wrap(Widget child, {double width = 400}) => MaterialApp(
      theme: AppTheme.light,
      home: Scaffold(
        body: Center(
          child: SizedBox(
            width: width,
            child: child,
          ),
        ),
      ),
    );

void main() {
  group('WeatherPresentationHelper', () {
    test('formatTemperature: formats decimal, integer, and zero without redundant .0', () {
      expect(WeatherPresentationHelper.formatTemperature(23.6), '23.6°C');
      expect(WeatherPresentationHelper.formatTemperature(23.0), '23°C');
      expect(WeatherPresentationHelper.formatTemperature(23), '23°C');
      expect(WeatherPresentationHelper.formatTemperature(0), '0°C');
      expect(WeatherPresentationHelper.formatTemperature(0.0), '0°C');
      expect(WeatherPresentationHelper.formatTemperature(null), isNull);
    });

    test('formatFeelsLike: formats feels like temperature', () {
      expect(WeatherPresentationHelper.formatFeelsLike(22.8), 'Hissedilen 22.8°C');
      expect(WeatherPresentationHelper.formatFeelsLike(22.0), 'Hissedilen 22°C');
      expect(WeatherPresentationHelper.formatFeelsLike(null), isNull);
    });

    test('formatHumidity: formats humidity percentage', () {
      expect(WeatherPresentationHelper.formatHumidity(45.0), 'Nem %45');
      expect(WeatherPresentationHelper.formatHumidity(0.0), 'Nem %0');
      expect(WeatherPresentationHelper.formatHumidity(null), isNull);
    });

    test('formatWind: formats wind and gust', () {
      expect(WeatherPresentationHelper.formatWind(12.4), 'Rüzgâr 12.4 km/sa');
      expect(WeatherPresentationHelper.formatWind(12.0), 'Rüzgâr 12 km/sa');
      expect(
        WeatherPresentationHelper.formatWind(12.4, 18.2),
        'Rüzgâr 12.4 km/sa · Hamle 18.2',
      );
      expect(WeatherPresentationHelper.formatWind(null), isNull);
    });

    test('formatMinMax: formats daily min/max combinations', () {
      expect(
        WeatherPresentationHelper.formatMinMax(14.2, 28.5),
        'Bugün 14.2° / 28.5°',
      );
      expect(
        WeatherPresentationHelper.formatMinMax(14.0, 28.0),
        'Bugün 14° / 28°',
      );
      expect(WeatherPresentationHelper.formatMinMax(null, 28.5), 'Maks. 28.5°C');
      expect(WeatherPresentationHelper.formatMinMax(14.2, null), 'Min. 14.2°C');
      expect(WeatherPresentationHelper.formatMinMax(null, null), isNull);
    });

    test('formatPrecipitation: preserves true 0% value', () {
      expect(WeatherPresentationHelper.formatPrecipitation(20.0), 'Yağış %20');
      expect(WeatherPresentationHelper.formatPrecipitation(0.0), 'Yağış %0');
      expect(WeatherPresentationHelper.formatPrecipitation(0), 'Yağış %0');
      expect(WeatherPresentationHelper.formatPrecipitation(null), isNull);
    });
  });

  group('WeatherCard 12 UI Presentation Scenarios', () {
    testWidgets('1. Full weather: all metrics appear accurately', (tester) async {
      const summary = WeatherSummary(
        temperature: 23.6,
        description: 'Parçalı bulutlu',
        feelsLike: 22.8,
        humidity: 45.0,
        windSpeed: 12.4,
        minTemperature: 14.2,
        maxTemperature: 28.5,
        precipitationProbability: 20.0,
        weatherCode: 2,
      );

      await tester.pumpWidget(_wrap(const WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.text('23.6°C'), findsOneWidget);
      expect(find.text('Parçalı bulutlu'), findsOneWidget);
      expect(find.text('Hissedilen 22.8°C'), findsOneWidget);
      expect(find.text('Nem %45'), findsOneWidget);
      expect(find.text('Rüzgâr 12.4 km/sa'), findsOneWidget);
      expect(find.text('Bugün 14.2° / 28.5°'), findsOneWidget);
      expect(find.text('Yağış %20'), findsOneWidget);
    });

    testWidgets('2. temperature = 0: "0°C" is displayed', (tester) async {
      const summary = WeatherSummary(
        temperature: 0,
        description: 'Dondurucu soğuk',
        weatherCode: 0,
      );

      await tester.pumpWidget(_wrap(const WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.text('0°C'), findsOneWidget);
      expect(find.text('Dondurucu soğuk'), findsOneWidget);
    });

    testWidgets('3. temperature = null: "0°C" is NOT displayed, "Hava verisi yok" is shown', (tester) async {
      const summary = WeatherSummary(
        temperature: null,
        description: 'Bilinmeyen durum',
      );

      await tester.pumpWidget(_wrap(const WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.text('0°C'), findsNothing);
      expect(find.text('0.0°C'), findsNothing);
      expect(find.text('—°C'), findsNothing);
      expect(find.text('Hava verisi yok'), findsOneWidget);
    });

    testWidgets('4. Optional fields null: no crash, no overflow, and literal "null" does not appear', (tester) async {
      const summary = WeatherSummary(
        temperature: 21.0,
        description: 'Açık',
        feelsLike: null,
        humidity: null,
        windSpeed: null,
        windGust: null,
        minTemperature: null,
        maxTemperature: null,
        precipitationProbability: null,
      );

      await tester.pumpWidget(_wrap(const WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.text('21°C'), findsOneWidget);
      expect(find.text('Açık'), findsOneWidget);
      expect(find.textContaining('null'), findsNothing);
      expect(tester.takeException(), isNull);
    });

    testWidgets('5. precipitationProbability = 0: true meteorological %0 is displayed', (tester) async {
      const summary = WeatherSummary(
        temperature: 25.0,
        description: 'Güneşli',
        precipitationProbability: 0,
      );

      await tester.pumpWidget(_wrap(const WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.text('Yağış %0'), findsOneWidget);
    });

    testWidgets('6. isStale = false: stale message is not displayed', (tester) async {
      const summary = WeatherSummary(
        temperature: 24.0,
        description: 'Açık',
        isStale: false,
      );

      await tester.pumpWidget(_wrap(const WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.text('Son güncel hava verisi gösteriliyor'), findsNothing);
    });

    testWidgets('7. isStale = true: meteorological description is preserved and stale subtext is displayed', (tester) async {
      const summary = WeatherSummary(
        temperature: 24.0,
        description: 'Parçalı Bulutlu',
        isStale: true,
        staleReason: 'Cache used',
      );

      await tester.pumpWidget(_wrap(const WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.text('Parçalı Bulutlu'), findsOneWidget);
      expect(find.text('Son güncel hava verisi gösteriliyor'), findsOneWidget);
    });

    testWidgets('8. Weather code sunny: maps to wb_sunny_outlined icon', (tester) async {
      const summary = WeatherSummary(
        temperature: 28.0,
        description: 'Açık',
        weatherCode: 0,
      );

      await tester.pumpWidget(_wrap(const WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.byIcon(Icons.wb_sunny_outlined), findsOneWidget);
    });

    testWidgets('9. Weather code rain: maps to water_drop_outlined icon', (tester) async {
      const summary = WeatherSummary(
        temperature: 15.0,
        description: 'Yağmurlu',
        weatherCode: 63,
      );

      await tester.pumpWidget(_wrap(const WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.byIcon(Icons.water_drop_outlined), findsOneWidget);
    });

    testWidgets('10. Weather code thunderstorm: maps to thunderstorm_outlined icon', (tester) async {
      const summary = WeatherSummary(
        temperature: 19.0,
        description: 'Gök Gürültülü Fırtına',
        weatherCode: 95,
      );

      await tester.pumpWidget(_wrap(const WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.byIcon(Icons.thunderstorm_outlined), findsOneWidget);
    });

    testWidgets('11. Very long condition string does not trigger RenderFlex overflow on small screen', (tester) async {
      const summary = WeatherSummary(
        temperature: 18.4,
        description:
            'Gök Gürültülü Sağanak Yağışlı ve Şiddetli Rüzgarlı Fırtına Uyarısı Bulunmaktadır',
        feelsLike: 17.2,
        humidity: 88.0,
        windSpeed: 42.5,
        windGust: 65.0,
        minTemperature: 12.0,
        maxTemperature: 21.0,
        precipitationProbability: 95.0,
        weatherCode: 95,
      );

      // Render on a narrow screen with large accessibility text scale factor
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light,
          home: MediaQuery(
            data: const MediaQueryData(
              size: Size(320, 600),
              textScaler: TextScaler.linear(1.4),
            ),
            child: const Scaffold(
              body: Center(
                child: SizedBox(
                  width: 300,
                  child: WeatherCard(
                    weather: summary,
                    tarlaName: 'Çok Uzun İsimli Doğu Anadolu Yayla Tarlası Parsel No 452',
                  ),
                ),
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });

    testWidgets('12. Farm association: displays farm name when provided', (tester) async {
      const summary = WeatherSummary(
        temperature: 23.6,
        description: 'Parçalı Bulutlu',
      );

      await tester.pumpWidget(
        _wrap(
          const WeatherCard(
            weather: summary,
            tarlaName: 'Yeniköy Tarlası',
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Yeniköy Tarlası'), findsOneWidget);
      expect(find.byIcon(Icons.location_on_outlined), findsOneWidget);
      expect(find.text('23.6°C'), findsOneWidget);
      expect(find.text('Parçalı Bulutlu'), findsOneWidget);
    });
  });

  group('WeatherCard 12 WeatherRisk Presentation Scenarios', () {
    testWidgets('1. Empty risk list: risk alert area is not rendered', (tester) async {
      const summary = WeatherSummary(
        temperature: 24.0,
        description: 'Açık',
        risks: [],
      );

      await tester.pumpWidget(_wrap(const WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.text('Don Riski'), findsNothing);
      expect(find.text('Kuvvetli Rüzgâr'), findsNothing);
      expect(find.text('Yoğun Yağış'), findsNothing);
      expect(find.text('Hava Uyarısı'), findsNothing);
      expect(find.textContaining('diğer uyarı'), findsNothing);
      expect(find.textContaining('Öneri:'), findsNothing);
    });

    testWidgets('2. Single risk (FROST, CRITICAL): shows title, critical badge, message and suggestion', (tester) async {
      final risk = WeatherRisk(
        riskType: 'FROST',
        severity: 'CRITICAL',
        startsAt: DateTime(2026, 9, 5, 3, 0),
        endsAt: DateTime(2026, 9, 5, 6, 0),
        message: 'Önümüzdeki 24 saatte don riski görülebilir.',
        suggestedAction: 'Hassas ürünleri kontrol edin.',
      );
      final summary = WeatherSummary(
        temperature: 1.0,
        description: 'Soğuk',
        risks: [risk],
      );

      await tester.pumpWidget(_wrap(WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.text('Don Riski'), findsOneWidget);
      expect(find.text('Kritik'), findsOneWidget);
      expect(find.text('Önümüzdeki 24 saatte don riski görülebilir.'), findsOneWidget);
      expect(find.text('Öneri: Hassas ürünleri kontrol edin.'), findsOneWidget);
      expect(find.byIcon(Icons.ac_unit), findsWidgets);
    });

    testWidgets('3. Multiple risks priority sorting: CRITICAL is above HIGH, domain list is not mutated', (tester) async {
      const highRisk = WeatherRisk(
        riskType: 'STRONG_WIND',
        severity: 'HIGH',
        message: 'Kuvvetli rüzgâr uyarısı',
      );
      const criticalRisk = WeatherRisk(
        riskType: 'FROST',
        severity: 'CRITICAL',
        message: 'Don tehlikesi',
      );
      // Domain model has HIGH first
      const summary = WeatherSummary(
        temperature: 5.0,
        description: 'Rüzgarlı',
        risks: [highRisk, criticalRisk],
      );

      await tester.pumpWidget(_wrap(const WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      final frostY = tester.getTopLeft(find.text('Don Riski')).dy;
      final windY = tester.getTopLeft(find.text('Kuvvetli Rüzgâr')).dy;

      // CRITICAL (Don Riski) should be displayed above HIGH (Kuvvetli Rüzgâr)
      expect(frostY, lessThan(windY));

      // Domain list order must remain intact (not mutated in-place)
      expect(summary.risks.first.riskType, 'STRONG_WIND');
      expect(summary.risks.last.riskType, 'FROST');
    });

    testWidgets('4. 3+ risks scenario: top 2 are displayed and "+N diğer uyarı" is shown', (tester) async {
      const r1 = WeatherRisk(riskType: 'FROST', severity: 'CRITICAL', message: 'Don riski');
      const r2 = WeatherRisk(riskType: 'STRONG_WIND', severity: 'HIGH', message: 'Rüzgâr uyarısı');
      const r3 = WeatherRisk(riskType: 'HEAVY_RAIN', severity: 'MEDIUM', message: 'Yağmur');
      const r4 = WeatherRisk(riskType: 'UNKNOWN_RISK', severity: 'LOW', message: 'Düşük risk');
      const summary = WeatherSummary(
        temperature: 10.0,
        description: 'Değişken',
        risks: [r1, r2, r3, r4],
      );

      await tester.pumpWidget(_wrap(const WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.text('Don Riski'), findsOneWidget);
      expect(find.text('Kuvvetli Rüzgâr'), findsOneWidget);
      expect(find.text('Yoğun Yağış'), findsNothing);
      expect(find.text('+2 diğer uyarı'), findsOneWidget);
    });

    testWidgets('5. Unknown risk_type fallback: "Hava Uyarısı" and warning_amber_rounded icon', (tester) async {
      const risk = WeatherRisk(
        riskType: 'CUSTOM_UNKNOWN_RISK',
        severity: 'MEDIUM',
        message: 'Bilinmeyen meteorolojik durum',
      );
      const summary = WeatherSummary(
        temperature: 15.0,
        description: 'Bulutlu',
        risks: [risk],
      );

      await tester.pumpWidget(_wrap(const WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.text('Hava Uyarısı'), findsOneWidget);
      expect(find.byIcon(Icons.warning_amber_rounded), findsOneWidget);
      expect(find.text('Orta'), findsOneWidget);
    });

    testWidgets('6. Timing: startsAt and endsAt formatted properly in local time', (tester) async {
      final start = DateTime(2026, 9, 5, 3, 0);
      final end = DateTime(2026, 9, 5, 6, 0);

      expect(WeatherPresentationHelper.formatRiskTiming(start, end), '03:00–06:00');

      final risk = WeatherRisk(
        riskType: 'FROST',
        severity: 'CRITICAL',
        startsAt: start,
        endsAt: end,
        message: 'Gece don bekleniyor.',
      );
      final summary = WeatherSummary(
        temperature: 0.0,
        description: 'Soğuk',
        risks: [risk],
      );

      await tester.pumpWidget(_wrap(WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.text('03:00–06:00'), findsOneWidget);
    });

    testWidgets('7. suggestedAction null or blank: neither "Öneri" nor "null" appears', (tester) async {
      const riskNull = WeatherRisk(
        riskType: 'STRONG_WIND',
        severity: 'HIGH',
        message: 'Rüzgâr şiddetli esecek.',
        suggestedAction: null,
      );
      const riskEmpty = WeatherRisk(
        riskType: 'FROST',
        severity: 'CRITICAL',
        message: 'Sıcaklık düşecek.',
        suggestedAction: '   ',
      );
      const summary = WeatherSummary(
        temperature: 4.0,
        description: 'Soğuk',
        risks: [riskNull, riskEmpty],
      );

      await tester.pumpWidget(_wrap(const WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.textContaining('Öneri'), findsNothing);
      expect(find.textContaining('null'), findsNothing);
    });

    testWidgets('8. suggestedAction provided: "Öneri: ..." is displayed', (tester) async {
      const risk = WeatherRisk(
        riskType: 'STRONG_WIND',
        severity: 'HIGH',
        message: 'Kuvvetli rüzgâr bekleniyor.',
        suggestedAction: 'İlaçlama planını erteleyin.',
      );
      const summary = WeatherSummary(
        temperature: 18.0,
        description: 'Rüzgarlı',
        risks: [risk],
      );

      await tester.pumpWidget(_wrap(const WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.text('Öneri: İlaçlama planını erteleyin.'), findsOneWidget);
    });

    testWidgets('9. WeatherPresentationHelper: unknown weather icon is neutral (not sunny) with secondary color', (tester) async {
      expect(WeatherPresentationHelper.getWeatherIcon(null, null), Icons.cloud_queue);
      expect(WeatherPresentationHelper.getWeatherIcon(999, 'Bilinmeyen hava'), Icons.cloud_queue);
      expect(WeatherPresentationHelper.getWeatherIcon(null, null), isNot(Icons.wb_sunny_outlined));
      expect(WeatherPresentationHelper.getWeatherIconColor(null, null), AppColors.textSecondary);
    });

    testWidgets('10. Severity presentation: verifies labels and colors for all severity levels', (tester) async {
      expect(WeatherPresentationHelper.getRiskSeverityLabel('CRITICAL'), 'Kritik');
      expect(WeatherPresentationHelper.getRiskSeverityColor('CRITICAL'), AppColors.error);

      expect(WeatherPresentationHelper.getRiskSeverityLabel('HIGH'), 'Yüksek');
      expect(WeatherPresentationHelper.getRiskSeverityColor('HIGH'), AppColors.warning);

      expect(WeatherPresentationHelper.getRiskSeverityLabel('MEDIUM'), 'Orta');
      expect(WeatherPresentationHelper.getRiskSeverityColor('MEDIUM'), const Color(0xFFED6C02));

      expect(WeatherPresentationHelper.getRiskSeverityLabel('LOW'), 'Düşük');
      expect(WeatherPresentationHelper.getRiskSeverityLabel('UNKNOWN'), 'Bilgi');
    });

    testWidgets('11. Small screen (320px) & large text scaling: no RenderFlex overflow', (tester) async {
      final risk = WeatherRisk(
        riskType: 'FROST',
        severity: 'CRITICAL',
        startsAt: DateTime(2026, 9, 5, 23, 0),
        endsAt: DateTime(2026, 9, 6, 6, 0),
        message: 'Önümüzdeki 24 saat boyunca bölgede çok kuvvetli zirai don riski oluşabilir.',
        suggestedAction: 'Hassas ürünleri yerinde kontrol edin ve uygun koruma tedbirlerini gecikmeden değerlendirin.',
      );
      final summary = WeatherSummary(
        temperature: -2.5,
        description: 'Zirai Don Tehlikesi ve Buzlanma',
        risks: [risk],
      );

      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light,
          home: MediaQuery(
            data: const MediaQueryData(
              size: Size(320, 600),
              textScaler: TextScaler.linear(1.8),
            ),
            child: Scaffold(
              body: SingleChildScrollView(
                child: Center(
                  child: SizedBox(
                    width: 300,
                    child: WeatherCard(
                      weather: summary,
                      tarlaName: 'Uzun İsimli Çiftlik Parseli No 12',
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });

    testWidgets('12. isStale = true with risks: renders risks properly and stale notice is not duplicated', (tester) async {
      const risk = WeatherRisk(
        riskType: 'STRONG_WIND',
        severity: 'HIGH',
        message: 'Kuvvetli rüzgâr uyarısı',
        suggestedAction: 'Tedbir alın.',
      );
      const summary = WeatherSummary(
        temperature: 19.0,
        description: 'Rüzgarlı',
        isStale: true,
        staleReason: 'Cache used',
        risks: [risk],
      );

      await tester.pumpWidget(_wrap(const WeatherCard(weather: summary)));
      await tester.pumpAndSettle();

      expect(find.text('Kuvvetli Rüzgâr'), findsOneWidget);
      expect(find.text('Yüksek'), findsOneWidget);
      expect(find.text('Kuvvetli rüzgâr uyarısı'), findsOneWidget);
      expect(find.text('Öneri: Tedbir alın.'), findsOneWidget);

      // Consolidated single notice at card bottom
      expect(find.text('Son güncel hava verisi gösteriliyor'), findsOneWidget);
    });
  });
}

