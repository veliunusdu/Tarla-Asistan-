import '../domain/weather_summary.dart';
import 'weather_repository.dart';

/// Backend weather endpoint'i aktif edilene kadar kullanılan yer tutucu.
///
/// Backend `/api/v1/farms/{farm_id}/weather` endpoint'i mevcuttur ve
/// JWT kimlik doğrulaması gerektirir. Mobil tarafta tarla seçimi ve
/// ApiClient entegrasyonu tamamlandıktan sonra bu sınıfın yerine
/// [BackendWeatherRepository] geçirilmelidir.
class UnavailableWeatherRepository implements WeatherRepository {
  const UnavailableWeatherRepository();

  @override
  Future<WeatherSummary> getWeather({String? farmId}) async {
    throw Exception('Hava durumu şu anda alınamıyor.');
  }
}
