import 'package:flutter/material.dart';
import 'package:mobile/app/theme/app_theme.dart';

/// Minimal MaterialApp sarmalayıcı — tema ve scaffold sağlar.
class TestApp extends StatelessWidget {
  const TestApp({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      theme: AppTheme.light,
      home: Scaffold(body: child),
    );
  }
}
