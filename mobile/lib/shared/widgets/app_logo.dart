import 'package:flutter/material.dart';

/// Tarla Asistanı uygulama logosu.
class AppLogo extends StatelessWidget {
  const AppLogo({
    super.key,
    this.size = 64,
    this.semanticLabel = 'Tarla Asistanı logosu',
  });

  static const assetPath =
      'assets/branding/concepts/concept_c_white.png';

  final double size;
  final String semanticLabel;

  @override
  Widget build(BuildContext context) => Semantics(
    image: true,
    label: semanticLabel,
    child: Image.asset(
      assetPath,
      width: size,
      height: size,
      fit: BoxFit.contain,
      filterQuality: FilterQuality.high,
    ),
  );
}
