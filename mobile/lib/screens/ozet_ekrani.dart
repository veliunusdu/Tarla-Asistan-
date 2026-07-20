import 'dart:ui';
import 'package:flutter/material.dart';
import '../services/database_helper.dart';
import '../services/weather_service.dart'; // Servisi import etmeyi unutma
import 'tarla_listesi_ekrani.dart';

class OzetEkrani extends StatefulWidget {
  const OzetEkrani({super.key});

  @override
  State<OzetEkrani> createState() => _OzetEkraniState();
}

class _OzetEkraniState extends State<OzetEkrani> {
  final WeatherService _weatherService = WeatherService(); // Servisi başlat
  int _tarlaSayisi = 0;
  double _toplamDonum = 0.0;

  @override
  void initState() {
    super.initState();
    _loadVeriler();
  }

  Future<void> _loadVeriler() async {
    final sayi = await DatabaseHelper.instance.getTarlaSayisi();
    final donum = await DatabaseHelper.instance.getToplamDonum();
    if (mounted) {
      setState(() {
        _tarlaSayisi = sayi;
        _toplamDonum = donum;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Container(
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          gradient: LinearGradient(
            colors: [Colors.green.shade800, Colors.green.shade400],
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
          ),
        ),
        child: SafeArea(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text("Tarım Asistanı", style: TextStyle(fontSize: 28, fontWeight: FontWeight.bold, color: Colors.white)),
              const Text("Bugün tarlalarınızda neler oluyor?", style: TextStyle(color: Colors.white70)),
              const SizedBox(height: 30),
              
              // İstatistik Kartları
              Row(
                children: [
                  Expanded(child: _buildStatCard("Toplam Tarla", "$_tarlaSayisi")),
                  const SizedBox(width: 15),
                  Expanded(child: _buildStatCard("Toplam Alan", "${_toplamDonum.toInt()} Dönüm")),
                ],
              ),
              const SizedBox(height: 20),

              // Hava Durumu Kartı
              FutureBuilder<Map<String, dynamic>>(
                future: _weatherService.getWeather(),
                builder: (context, snapshot) {
                  if (snapshot.connectionState == ConnectionState.waiting) {
                    return _buildGlassCard(child: const ListTile(title: Text("Hava durumu yükleniyor...", style: TextStyle(color: Colors.white))));
                  }
                  if (snapshot.hasError) {
                    return const SizedBox(); // Hata oluşursa kartı gizle
                  }
                  
                  final data = snapshot.data!;
                  final temp = data['main']['temp'].round();
                  final desc = data['weather'][0]['description'];
                  final iconCode = data['weather'][0]['icon'];

                  return _buildGlassCard(
                    child: ListTile(
                      leading: Image.network(
                        'https://openweathermap.org/img/wn/$iconCode@2x.png',
                        width: 40, height: 40, color: Colors.white,
                      ),
                      title: Text("İzmir: $temp°C", style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                      subtitle: Text(desc[0].toUpperCase() + desc.substring(1), style: const TextStyle(color: Colors.white70)),
                    ),
                  );
                },
              ),

              const Spacer(),
              
              // Ana Listeye Gitme Butonu
              SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  style: ElevatedButton.styleFrom(
                    backgroundColor: Colors.white,
                    foregroundColor: Colors.green.shade800,
                    padding: const EdgeInsets.symmetric(vertical: 16),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(15)),
                  ),
                  onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const TarlaListesiEkrani())),
                  child: const Text("Tarlalarımı Görüntüle", style: TextStyle(fontWeight: FontWeight.bold)),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  // Yeniden kullanılabilir Glassmorphic kart tasarım yardımcısı
  Widget _buildGlassCard({required Widget child}) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(20),
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: 10, sigmaY: 10),
        child: Container(
          padding: const EdgeInsets.all(10),
          decoration: BoxDecoration(
            color: Colors.white.withValues(alpha: 0.2),
            borderRadius: BorderRadius.circular(20),
            border: Border.all(color: Colors.white.withValues(alpha: 0.3)),
          ),
          child: child,
        ),
      ),
    );
  }

  Widget _buildStatCard(String title, String value) {
    return _buildGlassCard(
      child: Column(
        children: [
          Text(value, style: const TextStyle(fontSize: 24, fontWeight: FontWeight.bold, color: Colors.white)),
          const SizedBox(height: 5),
          Text(title, style: const TextStyle(color: Colors.white70)),
        ],
      ),
    );
  }
}