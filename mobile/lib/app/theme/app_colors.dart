import 'package:flutter/material.dart';

abstract final class AppColors {
  // Primary — crop green (#2E7D32, DESIGN.md §2)
  static const primary = Color(0xFF2E7D32);
  static const onPrimary = Colors.white;

  // Secondary — earth/harvest brown (#8D6E63, DESIGN.md §2)
  static const secondary = Color(0xFF8D6E63);
  static const onSecondary = Colors.white;

  // Background — white, high-contrast for sunlight use (DESIGN.md §2)
  static const background = Color(0xFFF9FAF9);
  static const onBackground = Color(0xFF1A1C1A);

  // Surface
  static const surface = Colors.white;
  static const onSurface = Color(0xFF1A1C1A);

  // Status
  static const success = Color(0xFF2E7D32);
  static const warning = Color(0xFFF57C00);
  static const error = Color(0xFFD32F2F);
  static const onError = Colors.white;

  // Text helpers
  static const textPrimary = Color(0xFF1A1C1A);
  static const textSecondary = Color(0xFF5A6059);
  static const textDisabled = Color(0xFF9AA099);
}
