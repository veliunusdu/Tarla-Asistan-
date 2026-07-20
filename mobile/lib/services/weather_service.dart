import 'dart:convert';
import 'package:http/http.dart' as http;

class WeatherService {
  // Buraya OpenWeatherMap'ten aldığın API anahtarını yapıştır
  final String apiKey = "37590b3bb69698c40110ec7f94605cfe";
  final String city = "Izmir"; // Varsayılan şehir

  Future<Map<String, dynamic>> getWeather() async {
    final url = Uri.parse(
        'https://api.openweathermap.org/data/2.5/weather?q=$city&appid=$apiKey&units=metric&lang=tr');

    final response = await http.get(url);

    if (response.statusCode == 200) {
      return json.decode(response.body);
    } else {
      throw Exception('Hava durumu verisi alınamadı');
    }
  }
}