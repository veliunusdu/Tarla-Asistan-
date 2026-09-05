import 'dart:typed_data';

import 'package:image_picker/image_picker.dart';

import 'image_sanitizer.dart';

class PickedImageData {
  const PickedImageData({
    required this.bytes,
    required this.name,
    this.mimeType,
  });

  final Uint8List bytes;
  final String name;
  final String? mimeType;
}

abstract class ImagePickerService {
  Future<PickedImageData?> pickImage({required ImageSource source});
}

class DefaultImagePickerService implements ImagePickerService {
  const DefaultImagePickerService({ImagePicker? picker}) : _picker = picker;

  final ImagePicker? _picker;

  @override
  Future<PickedImageData?> pickImage({required ImageSource source}) async {
    final picker = _picker ?? ImagePicker();
    final file = await picker.pickImage(
      source: source,
      imageQuality: 70,
      maxWidth: 1024,
      maxHeight: 1024,
    );
    if (file == null) return null;
    final bytes = await file.readAsBytes();
    final sanitized = ImageSanitizer.sanitize(bytes);
    final originalName = file.name.replaceFirst(RegExp(r'\.[^.]+$'), '');
    return PickedImageData(
      bytes: sanitized.bytes,
      name: '${originalName.isEmpty ? 'image' : originalName}${sanitized.fileExtension}',
      mimeType: sanitized.mimeType,
    );
  }
}
