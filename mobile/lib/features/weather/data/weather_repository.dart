import '../domain/weather_summary.dart';

abstract interface class WeatherRepository {
  Future<WeatherSummary> getWeather();
}
