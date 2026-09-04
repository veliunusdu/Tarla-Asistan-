import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/weather/domain/weather_summary.dart';

void main() {
  group('WeatherRisk', () {
    test('instantiates with required fields and defaults', () {
      const risk = WeatherRisk(
        riskType: 'FROST',
        severity: 'CRITICAL',
        message: 'Don riski var.',
      );

      expect(risk.riskType, 'FROST');
      expect(risk.severity, 'CRITICAL');
      expect(risk.message, 'Don riski var.');
      expect(risk.startsAt, isNull);
      expect(risk.endsAt, isNull);
      expect(risk.suggestedAction, isNull);
      expect(risk.isCritical, isTrue);
      expect(risk.isHigh, isFalse);
    });

    test('parses from json with valid fields', () {
      final json = {
        'riskType': 'STRONG_WIND',
        'severity': 'HIGH',
        'startsAt': '2026-09-05T06:00:00Z',
        'endsAt': '2026-09-05T18:00:00Z',
        'message': 'Kuvvetli rüzgâr bekleniyor.',
        'suggestedAction': 'İlaçlamayı erteleyin.',
      };

      final risk = WeatherRisk.fromJson(json);

      expect(risk.riskType, 'STRONG_WIND');
      expect(risk.severity, 'HIGH');
      expect(risk.startsAt, DateTime.parse('2026-09-05T06:00:00Z'));
      expect(risk.endsAt, DateTime.parse('2026-09-05T18:00:00Z'));
      expect(risk.message, 'Kuvvetli rüzgâr bekleniyor.');
      expect(risk.suggestedAction, 'İlaçlamayı erteleyin.');
      expect(risk.isHigh, isTrue);
      expect(risk.isCritical, isFalse);
    });

    test('handles malformed optional DateTime in WeatherRisk gracefully', () {
      final json = {
        'riskType': 'HEAVY_RAIN',
        'severity': 'HIGH',
        'startsAt': 'not-a-valid-date',
        'endsAt': 'invalid-timestamp-xyz',
        'message': 'Yoğun yağış.',
      };

      final risk = WeatherRisk.fromJson(json);

      expect(risk.riskType, 'HEAVY_RAIN');
      expect(risk.severity, 'HIGH');
      expect(risk.startsAt, isNull);
      expect(risk.endsAt, isNull);
      expect(risk.message, 'Yoğun yağış.');
    });

    test('handles missing or null fields gracefully in fromJson', () {
      final risk = WeatherRisk.fromJson({});

      expect(risk.riskType, '');
      expect(risk.severity, 'LOW');
      expect(risk.message, '');
      expect(risk.startsAt, isNull);
      expect(risk.endsAt, isNull);
      expect(risk.suggestedAction, isNull);
    });

    test('serializes to json', () {
      final risk = WeatherRisk(
        riskType: 'HEAVY_RAIN',
        severity: 'HIGH',
        startsAt: DateTime.parse('2026-09-05T12:00:00Z'),
        endsAt: DateTime.parse('2026-09-05T15:00:00Z'),
        message: 'Aşırı yağış.',
        suggestedAction: 'Drenajı kontrol edin.',
      );

      final json = risk.toJson();

      expect(json['riskType'], 'HEAVY_RAIN');
      expect(json['severity'], 'HIGH');
      expect(json['startsAt'], '2026-09-05T12:00:00.000Z');
      expect(json['endsAt'], '2026-09-05T15:00:00.000Z');
      expect(json['message'], 'Aşırı yağış.');
      expect(json['suggestedAction'], 'Drenajı kontrol edin.');
    });
  });

  group('WeatherSummary', () {
    test('maintains backwards compatibility with existing 2-parameter constructor', () {
      const summary = WeatherSummary(
        temperature: 24,
        description: 'Güneşli',
      );

      expect(summary.temperature, 24);
      expect(summary.description, 'Güneşli');
      expect(summary.hasTemperature, isTrue);
      expect(summary.condition, isNull);
      expect(summary.feelsLike, isNull);
      expect(summary.humidity, isNull);
      expect(summary.windSpeed, isNull);
      expect(summary.windGust, isNull);
      expect(summary.minTemperature, isNull);
      expect(summary.maxTemperature, isNull);
      expect(summary.precipitationProbability, isNull);
      expect(summary.precipitationAmount, isNull);
      expect(summary.risks, isEmpty);
      expect(summary.isStale, isFalse);
      expect(summary.staleReason, isNull);
      expect(summary.hasRisks, isFalse);
    });

    test('temperature gerçek 0°C doğru şekilde temsil edilir', () {
      const summary = WeatherSummary(
        temperature: 0,
        description: 'Dondurucu',
      );

      expect(summary.temperature, 0);
      expect(summary.hasTemperature, isTrue);
      expect(summary.temperature == 0, isTrue);
      expect(summary.temperature == null, isFalse);
    });

    test('temperature eksik olduğunda null olarak temsil edilir', () {
      const summary = WeatherSummary(
        temperature: null,
        description: 'Veri yok',
      );

      expect(summary.temperature, isNull);
      expect(summary.hasTemperature, isFalse);
      expect(summary.temperature == 0, isFalse);
    });

    test('eksik sıcaklık gerçek 0°C ile aynı sonucu üretmez', () {
      const missing = WeatherSummary(
        temperature: null,
        description: 'Eksik veri',
      );
      const zero = WeatherSummary(
        temperature: 0,
        description: 'Sıfır derece',
      );

      expect(missing.temperature, isNot(equals(zero.temperature)));
      expect(missing.hasTemperature, isFalse);
      expect(zero.hasTemperature, isTrue);
      expect(missing.temperature == 0, isFalse);
      expect(zero.temperature == 0, isTrue);
    });

    test('decimal sıcaklık hassasiyeti kaybolmadan korunur (double/num)', () {
      const summary = WeatherSummary(
        temperature: 23.6,
        description: 'Ilık',
      );

      expect(summary.temperature, 23.6);
      expect(summary.temperature, isNot(equals(24)));
      expect(summary.hasTemperature, isTrue);
    });

    test('nullable feelsLike alanı desteklenir', () {
      const withNull = WeatherSummary(
        description: 'Açık',
        feelsLike: null,
      );
      expect(withNull.feelsLike, isNull);

      const withVal = WeatherSummary(
        description: 'Açık',
        feelsLike: 25.4,
      );
      expect(withVal.feelsLike, 25.4);
    });

    test('nullable windGust alanı desteklenir', () {
      const withNull = WeatherSummary(
        description: 'Rüzgârlı',
        windGust: null,
      );
      expect(withNull.windGust, isNull);

      const withVal = WeatherSummary(
        description: 'Rüzgârlı',
        windGust: 32.8,
      );
      expect(withVal.windGust, 32.8);
    });

    test('risks boş olduğunda güvenli davranır', () {
      const summary = WeatherSummary(
        description: 'Normal',
        risks: [],
      );

      expect(summary.risks, isEmpty);
      expect(summary.hasRisks, isFalse);
      expect(summary.hasCriticalRisks, isFalse);
    });

    test('risks dolu olduğunda riskleri ve risk seviyesini doğru verir', () {
      final summary = WeatherSummary(
        description: 'Kritik durum',
        risks: [
          const WeatherRisk(
            riskType: 'FROST',
            severity: 'CRITICAL',
            message: 'Don riski.',
          ),
          const WeatherRisk(
            riskType: 'STRONG_WIND',
            severity: 'HIGH',
            message: 'Fırtına.',
          ),
        ],
      );

      expect(summary.risks, hasLength(2));
      expect(summary.hasRisks, isTrue);
      expect(summary.hasCriticalRisks, isTrue);
      expect(summary.risks.first.isCritical, isTrue);
      expect(summary.risks.last.isHigh, isTrue);
    });

    test('current null, daily null, points boş envelope durumunda güvenli çalışır', () {
      final summary = WeatherSummary.fromJson({
        'current': null,
        'daily': null,
        'points': [],
        'risks': [],
      });

      expect(summary.temperature, isNull);
      expect(summary.hasTemperature, isFalse);
      expect(summary.feelsLike, isNull);
      expect(summary.minTemperature, isNull);
      expect(summary.maxTemperature, isNull);
      expect(summary.precipitationProbability, isNull);
      expect(summary.precipitationAmount, isNull);
      expect(summary.risks, isEmpty);
      expect(summary.isStale, isFalse);
      expect(summary.temperature == 0, isFalse);
    });

    test('malformed optional DateTime stringleri güvenle null yapar', () {
      final summary = WeatherSummary.fromJson({
        'observedAt': 'corrupted-datetime-string',
        'fetchedAt': 'not-a-real-date-string',
      });

      expect(summary.observedAt, isNull);
      expect(summary.fetchedAt, isNull);
    });

    test('flat json serialization roundtrip preserves all types and values', () {
      final original = WeatherSummary(
        temperature: 22.5,
        description: 'Açık hava',
        condition: 'Açık',
        feelsLike: 21.3,
        humidity: 45.0,
        windSpeed: 14.2,
        windGust: 22.0,
        minTemperature: 12.1,
        maxTemperature: 26.8,
        precipitationProbability: 10.0,
        precipitationAmount: 0.0,
        isStale: false,
        staleReason: null,
        weatherCode: 1,
        observedAt: DateTime.parse('2026-09-04T12:00:00Z'),
        fetchedAt: DateTime.parse('2026-09-04T12:05:00Z'),
        risks: [
          WeatherRisk(
            riskType: 'FROST',
            severity: 'CRITICAL',
            startsAt: DateTime.parse('2026-09-05T03:00:00Z'),
            endsAt: DateTime.parse('2026-09-05T06:00:00Z'),
            message: 'Don tehlikesi.',
            suggestedAction: 'Örtü altı önlem alın.',
          ),
        ],
      );

      final json = original.toJson();
      final restored = WeatherSummary.fromJson(json);

      expect(restored.temperature, 22.5);
      expect(restored.description, 'Açık hava');
      expect(restored.condition, 'Açık');
      expect(restored.feelsLike, 21.3);
      expect(restored.humidity, 45.0);
      expect(restored.windSpeed, 14.2);
      expect(restored.windGust, 22.0);
      expect(restored.minTemperature, 12.1);
      expect(restored.maxTemperature, 26.8);
      expect(restored.precipitationProbability, 10.0);
      expect(restored.precipitationAmount, 0.0);
      expect(restored.risks.length, 1);
      expect(restored.risks.first.riskType, 'FROST');
      expect(restored.risks.first.severity, 'CRITICAL');
      expect(restored.isStale, isFalse);
    });

    test('copyWith produces updated clone with decimal and nullable support', () {
      const initial = WeatherSummary(
        temperature: 20,
        description: 'Bulutlu',
      );

      final updated = initial.copyWith(
        temperature: 22.7,
        feelsLike: 21.5,
        isStale: true,
      );

      expect(updated.temperature, 22.7);
      expect(updated.description, 'Bulutlu');
      expect(updated.feelsLike, 21.5);
      expect(updated.isStale, isTrue);
      expect(initial.temperature, 20);
      expect(initial.isStale, isFalse);
    });
  });
}

