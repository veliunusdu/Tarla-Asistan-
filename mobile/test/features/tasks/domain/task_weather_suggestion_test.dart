import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/tasks/domain/farm_task.dart';
import 'package:mobile/features/tasks/domain/task_weather_suggestion.dart';

void main() {
  group('TaskWeatherSuggestion & FarmTask JSON Parsing', () {
    test('FarmTask_FromJson_WithWeatherPostponeSuggestion_ParsesAllFields', () {
      final json = <String, dynamic>{
        'id': 'task-100',
        'farm_id': 'farm-1',
        'title': 'İlaçlama Görevi',
        'description': 'Mısır tarlasında ilaçlama',
        'reason': 'Zararlı tespiti',
        'priority': 'High',
        'status': 'New',
        'source': 'CropCalendar',
        'confidence': 'High',
        'due_date': '2026-09-07',
        'expert_review_recommended': false,
        'weather_postpone_suggestion': {
          'advisory_id': 'adv-guid-1234',
          'risk_level': 'Warning',
          'reason': 'Yüksek Rüzgâr',
          'reasons': [
            'Rüzgâr hızı 28 km/s (maks 15 km/s)',
            'Sürüklenme ve hedef dışı yayılma riski yüksek',
          ],
          'suggested_action': 'İlaçlamayı rüzgârın sakin olduğu güne erteleyin.',
          'recommended_date': '2026-09-08',
          'evaluated_at_utc': '2026-09-06T10:00:00Z',
          'weather_fetched_at_utc': '2026-09-06T09:30:00Z',
          'is_weather_stale': false,
          'stale_reason': null,
          'can_apply': true,
        },
      };

      final task = FarmTask.fromJson(json);

      expect(task.weatherPostponeSuggestion, isNotNull);
      final suggestion = task.weatherPostponeSuggestion!;

      expect(suggestion.advisoryId, 'adv-guid-1234');
      expect(suggestion.riskLevel, 'Warning');
      expect(suggestion.reason, 'Yüksek Rüzgâr');
      expect(suggestion.reasons, hasLength(2));
      expect(suggestion.reasons[0], 'Rüzgâr hızı 28 km/s (maks 15 km/s)');
      expect(suggestion.reasons[1], 'Sürüklenme ve hedef dışı yayılma riski yüksek');
      expect(suggestion.suggestedAction, 'İlaçlamayı rüzgârın sakin olduğu güne erteleyin.');
      expect(suggestion.recommendedDate, DateTime(2026, 9, 8));
      expect(suggestion.evaluatedAtUtc, DateTime.utc(2026, 9, 6, 10, 0, 0));
      expect(suggestion.weatherFetchedAtUtc, DateTime.utc(2026, 9, 6, 9, 30, 0));
      expect(suggestion.isWeatherStale, isFalse);
      expect(suggestion.staleReason, isNull);
      expect(suggestion.canApply, isTrue);
    });

    test('FarmTask_FromJson_WithoutWeatherSuggestion_ReturnsNull', () {
      final json = <String, dynamic>{
        'id': 'task-101',
        'farm_id': 'farm-1',
        'title': 'Sulama Görevi',
        'description': 'Sulama talimatı',
        'reason': 'Toprak nemi azaldı',
        'priority': 'Medium',
        'status': 'New',
        'source': 'System',
        'confidence': 'Medium',
        'due_date': '2026-09-07',
        // weather_postpone_suggestion property missing
      };

      final task = FarmTask.fromJson(json);
      expect(task.weatherPostponeSuggestion, isNull);
    });

    test('FarmTask_FromJson_WithNullWeatherSuggestion_ReturnsNull', () {
      final json = <String, dynamic>{
        'id': 'task-102',
        'farm_id': 'farm-1',
        'title': 'Gözlem Görevi',
        'description': 'Tarla kontrolü',
        'reason': 'Rutin kontrol',
        'priority': 'Low',
        'status': 'New',
        'source': 'Manual',
        'confidence': 'High',
        'due_date': '2026-09-07',
        'weather_postpone_suggestion': null,
      };

      final task = FarmTask.fromJson(json);
      expect(task.weatherPostponeSuggestion, isNull);
    });

    test('TaskWeatherSuggestion_WithNullableFields_ParsesSafely', () {
      final json = <String, dynamic>{
        'advisory_id': null,
        'risk_level': 'Info',
        'reason': 'Belirsiz hava koşulları',
        // reasons missing
        'suggested_action': 'Hava tahminini takip edin',
        'recommended_date': null,
        'evaluated_at_utc': '2026-09-06T12:00:00Z',
        'weather_fetched_at_utc': null,
        'is_weather_stale': true,
        'stale_reason': null,
        'can_apply': false,
      };

      final suggestion = TaskWeatherSuggestion.fromJson(json);

      expect(suggestion.advisoryId, isNull);
      expect(suggestion.riskLevel, 'Info');
      expect(suggestion.reason, 'Belirsiz hava koşulları');
      expect(suggestion.reasons, isEmpty);
      expect(suggestion.suggestedAction, 'Hava tahminini takip edin');
      expect(suggestion.recommendedDate, isNull);
      expect(suggestion.evaluatedAtUtc, DateTime.utc(2026, 9, 6, 12, 0, 0));
      expect(suggestion.weatherFetchedAtUtc, isNull);
      expect(suggestion.isWeatherStale, isTrue);
      expect(suggestion.staleReason, isNull);
      expect(suggestion.canApply, isFalse);
    });

    test('TaskWeatherSuggestion_RecommendedDate_DoesNotShiftCalendarDay', () {
      // Direct YYYY-MM-DD string
      final json1 = <String, dynamic>{
        'risk_level': 'Warning',
        'reason': 'Test',
        'suggested_action': 'Test action',
        'recommended_date': '2026-09-08',
        'evaluated_at_utc': '2026-09-06T10:00:00Z',
        'is_weather_stale': false,
        'can_apply': true,
      };
      final parsed1 = TaskWeatherSuggestion.fromJson(json1);
      expect(parsed1.recommendedDate, isNotNull);
      expect(parsed1.recommendedDate!.year, 2026);
      expect(parsed1.recommendedDate!.month, 9);
      expect(parsed1.recommendedDate!.day, 8);

      // ISO string with UTC timezone marker (e.g. 2026-09-08T00:00:00Z)
      // Must not shift to September 7 even in negative UTC offset timezones
      final json2 = <String, dynamic>{
        'risk_level': 'Warning',
        'reason': 'Test',
        'suggested_action': 'Test action',
        'recommended_date': '2026-09-08T00:00:00Z',
        'evaluated_at_utc': '2026-09-06T10:00:00Z',
        'is_weather_stale': false,
        'can_apply': true,
      };
      final parsed2 = TaskWeatherSuggestion.fromJson(json2);
      expect(parsed2.recommendedDate, isNotNull);
      expect(parsed2.recommendedDate!.year, 2026);
      expect(parsed2.recommendedDate!.month, 9);
      expect(parsed2.recommendedDate!.day, 8);
    });

    test('TaskWeatherSuggestion_WithUnknownRiskLevel_ParsesSafelyWithoutCrash', () {
      final json = <String, dynamic>{
        'risk_level': 'EXTREME_UNFORESEEN_WEATHER_RISK',
        'reason': 'Bilinmeyen meteorolojik risk',
        'suggested_action': 'Dikkatli olun',
        'evaluated_at_utc': '2026-09-06T10:00:00Z',
        'is_weather_stale': false,
        'can_apply': false,
      };

      final suggestion = TaskWeatherSuggestion.fromJson(json);
      expect(suggestion.riskLevel, 'EXTREME_UNFORESEEN_WEATHER_RISK');
    });

    test('TaskWeatherSuggestion_RoundtripSerialization_PreservesAllFields', () {
      final original = TaskWeatherSuggestion(
        advisoryId: 'adv-guid-999',
        riskLevel: 'Critical',
        reason: 'Şiddetli Fırtına',
        reasons: ['Rüzgar > 50 km/s', 'Dolu riski'],
        suggestedAction: 'Tüm tarla işlerini iptal edin',
        recommendedDate: DateTime(2026, 9, 10),
        evaluatedAtUtc: DateTime.utc(2026, 9, 6, 8, 0, 0),
        weatherFetchedAtUtc: DateTime.utc(2026, 9, 6, 7, 45, 0),
        isWeatherStale: false,
        staleReason: null,
        canApply: true,
      );

      final jsonMap = original.toJson();
      final parsed = TaskWeatherSuggestion.fromJson(jsonMap);

      expect(parsed, equals(original));
      expect(parsed.recommendedDate, DateTime(2026, 9, 10));
      expect(parsed.reasons, equals(['Rüzgar > 50 km/s', 'Dolu riski']));
    });
  });
}
