import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'ozet_ekrani.dart';

class OnboardingEkrani extends StatefulWidget {
  const OnboardingEkrani({super.key});

  @override
  State<OnboardingEkrani> createState() => _OnboardingEkraniState();
}

class _OnboardingEkraniState extends State<OnboardingEkrani> {
  final PageController _controller = PageController();
  int _currentPage = 0; // Hangi sayfada olduğumuzu tutan değişken

  final List<Map<String, String>> _pages = [
    {"title": "Tarım Asistanı'na Hoş Geldin!", "desc": "Tarlalarını yönetmek artık çok daha kolay ve modern."},
    {"title": "Tarlalarını Takip Et", "desc": "Ektiğin ürünleri, alanları ve tüm faaliyetlerini tek bir yerden kontrol et."},
    {"title": "Verimliliği Artır", "desc": "Hava durumu verileriyle işlerini planla, verimini en üst seviyeye çıkar."}
  ];

  void _finishOnboarding() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool('isFirstRun', false);
    if (!mounted) return;
    Navigator.pushReplacement(context, MaterialPageRoute(builder: (_) => const OzetEkrani()));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Container(
        decoration: BoxDecoration(
          gradient: LinearGradient(
            colors: [Colors.green.shade800, Colors.green.shade400],
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
          ),
        ),
        child: Column(
          children: [
            Expanded(
              child: PageView.builder(
                controller: _controller,
                itemCount: _pages.length,
                onPageChanged: (index) {
                  setState(() {
                    _currentPage = index;
                  });
                },
                itemBuilder: (context, index) {
                  return Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Icon(Icons.grass, size: 100, color: Colors.white),
                      const SizedBox(height: 40),
                      Text(_pages[index]['title']!, style: const TextStyle(fontSize: 26, fontWeight: FontWeight.bold, color: Colors.white)),
                      Padding(
                        padding: const EdgeInsets.all(30.0),
                        child: Text(_pages[index]['desc']!, textAlign: TextAlign.center, style: const TextStyle(fontSize: 16, color: Colors.white70)),
                      ),
                    ],
                  );
                },
              ),
            ),
            
            // Buton Alanı
            Padding(
              padding: const EdgeInsets.only(bottom: 50.0),
              child: ElevatedButton(
                style: ElevatedButton.styleFrom(
                  backgroundColor: Colors.white,
                  foregroundColor: Colors.green.shade800,
                  padding: const EdgeInsets.symmetric(horizontal: 40, vertical: 15),
                ),
                onPressed: () {
                  if (_currentPage == _pages.length - 1) {
                    _finishOnboarding(); // Son sayfadaysa bitir
                  } else {
                    _controller.nextPage(duration: const Duration(milliseconds: 300), curve: Curves.easeInOut); // Değilse ilerle
                  }
                },
                child: Text(_currentPage == _pages.length - 1 ? "Hemen Başla" : "İlerle"),
              ),
            ),
          ],
        ),
      ),
    );
  }
}