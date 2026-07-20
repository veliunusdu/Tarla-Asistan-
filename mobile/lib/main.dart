import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'screens/ozet_ekrani.dart';
import 'screens/onboarding_ekrani.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final prefs = await SharedPreferences.getInstance();
  final isFirstRun = prefs.getBool('isFirstRun') ?? true;
  
  runApp(TarimAsistaniApp(isFirstRun: isFirstRun));
}

class TarimAsistaniApp extends StatelessWidget {
  final bool isFirstRun;
  const TarimAsistaniApp({super.key, required this.isFirstRun});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      debugShowCheckedModeBanner: false,
      home: isFirstRun ? const OnboardingEkrani() : const OzetEkrani(),
    );
  }
}