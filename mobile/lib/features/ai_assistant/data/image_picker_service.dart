import 'dart:typed_data';

import 'package:image_picker/image_picker.dart';

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
    return PickedImageData(
      bytes: bytes,
      name: file.name,
      mimeType: file.mimeType,
    );
  }
}
