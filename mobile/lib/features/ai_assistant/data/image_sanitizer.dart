import 'dart:typed_data';

import 'package:image/image.dart' as img;

class SanitizedImage {
  const SanitizedImage({required this.bytes, required this.fileExtension, required this.mimeType});

  final Uint8List bytes;
  final String fileExtension;
  final String mimeType;
}

class ImageSanitizer {
  const ImageSanitizer._();

  static SanitizedImage sanitize(Uint8List input) {
    img.Image? decoded;
    try {
      decoded = img.decodeImage(input);
    } catch (_) {
      throw const FormatException('Görsel okunamadı.');
    }
    if (decoded == null) {
      throw const FormatException('Görsel okunamadı.');
    }

    // Normalize orientation before dropping metadata, then resize and encode once.
    final oriented = img.bakeOrientation(decoded);
    final longestSide = oriented.width > oriented.height ? oriented.width : oriented.height;
    final scale = longestSide > 1024 ? 1024 / longestSide : 1.0;
    final output = scale == 1.0
        ? oriented
        : img.copyResize(
            oriented,
            width: (oriented.width * scale).round(),
            height: (oriented.height * scale).round(),
          );

    return SanitizedImage(
      bytes: Uint8List.fromList(img.encodeJpg(output, quality: 70)),
      fileExtension: '.jpg',
      mimeType: 'image/jpeg',
    );
  }
}
