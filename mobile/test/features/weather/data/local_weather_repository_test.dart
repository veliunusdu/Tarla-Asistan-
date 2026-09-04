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

    test('cacheWeather updates existing record for same farmId', () async {
      const initialSummary = WeatherSummary(temperature: 20, description: 'Bulutlu');
      await repo.cacheWeather(farmId: 'farm-1', weather: initialSummary);

      const updatedSummary = WeatherSummary(temperature: 25, description: 'Açık');
      await repo.cacheWeather(farmId: 'farm-1', weather: updatedSummary);

      final cached = await repo.getCachedWeather(farmId: 'farm-1');
      expect(cached, isNotNull);
      expect(cached!.temperature, equals(25));
      expect(cached.description, equals('Açık'));
    });
  });
}
