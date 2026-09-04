import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/weather/data/local_weather_repository.dart';
import 'package:mobile/features/weather/domain/weather_summary.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  setUpAll(() {
    sqfliteFfiInit();
    databaseFactory = databaseFactoryFfi;
  });

  group('LocalWeatherRepository', () {
    late Database db;
    late LocalWeatherRepository repo;

    setUp(() async {
      db = await databaseFactoryFfi.openDatabase(inMemoryDatabasePath);
      repo = LocalWeatherRepository(databaseProvider: () async => db);
    });

    tearDown(() async {
      await db.close();
    });

    test('cacheWeather and getCachedWeather roundtrips data correctly', () async {
      final initial = await repo.getCachedWeather(farmId: 'farm-1');
      expect(initial, isNull);

      const summary = WeatherSummary(temperature: 24, description: 'Güneşli');
      await repo.cacheWeather(farmId: 'farm-1', weather: summary);

      final cached = await repo.getCachedWeather(farmId: 'farm-1');
      expect(cached, isNotNull);
      expect(cached!.temperature, equals(24));
      expect(cached.description, equals('Güneşli'));
    });

    test('cacheWeather and getCachedWeather with null farmId uses default key', () async {
      final initial = await repo.getCachedWeather();
      expect(initial, isNull);

      const summary = WeatherSummary(temperature: 18, description: 'Parçalı Bulutlu');
      await repo.cacheWeather(weather: summary);

      final cached = await repo.getCachedWeather();
      expect(cached, isNotNull);
      expect(cached!.temperature, equals(18));
      expect(cached.description, equals('Parçalı Bulutlu'));
    });

    test('cacheWeather and getCachedWeather preserves full extended WeatherSummary with precision and risks', () async {
      final summary = WeatherSummary(
        temperature: 23.6,
        description: 'Parçalı Bulutlu',
        condition: 'Parçalı Bulutlu',
        feelsLike: 22.8,
        humidity: 45.0,
        windSpeed: 12.4,
        windGust: 18.2,
        minTemperature: 14.2,
        maxTemperature: 28.5,
        precipitationProbability: 15.0,
        precipitationAmount: 0.5,
        weatherCode: 2,
        risks: [
          WeatherRisk(
            riskType: 'FROST',
            severity: 'CRITICAL',
            startsAt: DateTime.utc(2026, 9, 5, 3),
            endsAt: DateTime.utc(2026, 9, 5, 6),
            message: 'Don riski',
            suggestedAction: 'Örtü serin',
          ),
        ],
      );

      await repo.cacheWeather(farmId: 'farm-full', weather: summary);
      final cached = await repo.getCachedWeather(farmId: 'farm-full');

      expect(cached, isNotNull);
      expect(cached!.temperature, equals(23.6));
      expect(cached.description, equals('Parçalı Bulutlu'));
      expect(cached.condition, equals('Parçalı Bulutlu'));
      expect(cached.feelsLike, equals(22.8));
      expect(cached.humidity, equals(45.0));
      expect(cached.windSpeed, equals(12.4));
      expect(cached.windGust, equals(18.2));
      expect(cached.minTemperature, equals(14.2));
      expect(cached.maxTemperature, equals(28.5));
      expect(cached.precipitationProbability, equals(15.0));
      expect(cached.precipitationAmount, equals(0.5));
      expect(cached.weatherCode, equals(2));
      expect(cached.risks.length, equals(1));
      expect(cached.risks.first.riskType, equals('FROST'));
      expect(cached.risks.first.suggestedAction, equals('Örtü serin'));
    });

    test('cacheWeather and getCachedWeather preserves null temperature safely', () async {
      const summary = WeatherSummary(
        temperature: null,
        description: 'Sadece Tahmin',
        minTemperature: 10.0,
        maxTemperature: 20.0,
      );

      await repo.cacheWeather(farmId: 'farm-null-temp', weather: summary);
      final cached = await repo.getCachedWeather(farmId: 'farm-null-temp');

      expect(cached, isNotNull);
      expect(cached!.temperature, isNull);
      expect(cached.description, equals('Sadece Tahmin'));
      expect(cached.minTemperature, equals(10.0));
    });

    test('getCachedWeather falls back to legacy row columns when payload_json is null', () async {
      // Direct raw insert simulating legacy database row
      await db.execute('''
        CREATE TABLE IF NOT EXISTS weather_cache (
          farm_id TEXT PRIMARY KEY,
          temperature INTEGER,
          description TEXT NOT NULL,
          updated_at_utc TEXT NOT NULL
        )
      ''');
      await db.insert(
        'weather_cache',
        {
          'farm_id': 'legacy-farm',
          'temperature': 21,
          'description': 'Eski Önbellek',
          'updated_at_utc': DateTime.now().toUtc().toIso8601String(),
        },
      );

      final cached = await repo.getCachedWeather(farmId: 'legacy-farm');
      expect(cached, isNotNull);
      expect(cached!.temperature, equals(21));
      expect(cached.description, equals('Eski Önbellek'));
    });
  });
}
