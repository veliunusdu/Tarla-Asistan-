import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:image/image.dart' as img;

import '../../../lib/features/ai_assistant/data/image_sanitizer.dart';

void main() {
  test('re-encodes a picked image without EXIF markers and within bounds', () {
    final source = img.Image(width: 1600, height: 900);
    final encoded = Uint8List.fromList(img.encodeJpg(source, quality: 90));

    final sanitized = ImageSanitizer.sanitize(encoded);
    final decoded = img.decodeImage(sanitized.bytes);

    expect(decoded, isNotNull);
    expect(decoded!.width, lessThanOrEqualTo(1024));
    expect(decoded.height, lessThanOrEqualTo(1024));
    expect(utf8.decode(sanitized.bytes, allowMalformed: true), isNot(contains('Exif')));
    expect(sanitized.mimeType, 'image/jpeg');
    expect(sanitized.fileExtension, '.jpg');
  });

  test('rejects bytes that are not a decodable image', () {
    expect(
      () => ImageSanitizer.sanitize(Uint8List.fromList([0x4D, 0x5A, 0x00])),
      throwsA(isA<FormatException>()),
    );
  });
}
