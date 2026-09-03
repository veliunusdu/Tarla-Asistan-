import 'package:flutter/material.dart';

import '../../app/theme/app_colors.dart';

/// Tarla Asistanı uygulama logosu.
/// 'Tarlalarım' simgesi olan [Icons.grass] ile birebir aynı görsel kimliği taşır.
class AppLogo extends StatelessWidget {
  const AppLogo({
    super.key,
    this.size = 64,
    this.iconSize,
    this.backgroundColor = AppColors.primary,
    this.iconColor = Colors.white,
    this.borderRadius,
    this.isCircle = true,
  });

  /// Logo kapsayıcısının boyutu (en ve boy).
  final double size;

  /// İçerideki çim/başak simgesinin boyutu.
  final double? iconSize;

  /// Arka plan dolgu rengi. Null verilirse şeffaf olur.
  final Color? backgroundColor;

  /// Çim simgesinin rengi.
  final Color iconColor;

  /// Köşe yuvarlaklığı ([isCircle] false ise kullanılır).
  final BorderRadius? borderRadius;

  /// Dairesel kapsayıcı mı?
  final bool isCircle;

  @override
  Widget build(BuildContext context) {
    final effectiveIconSize = iconSize ?? (size * 0.58);

    final iconWidget = Icon(
      Icons.grass,
      size: effectiveIconSize,
      color: iconColor,
    );

    if (backgroundColor == null) {
      return SizedBox(
        width: size,
        height: size,
        child: Center(child: iconWidget),
      );
    }

    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: backgroundColor,
        shape: isCircle ? BoxShape.circle : BoxShape.rectangle,
        borderRadius: isCircle
            ? null
            : (borderRadius ?? BorderRadius.circular(size * 0.22)),
      ),
      child: Center(child: iconWidget),
    );
  }
}
