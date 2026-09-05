import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:image_picker/image_picker.dart';
import 'package:connectivity_plus/connectivity_plus.dart';

import '../../../app/theme/app_colors.dart';
import '../../../app/theme/app_spacing.dart';
import '../../../features/ai_assistant/data/image_picker_service.dart';
import '../../../features/ai_assistant/data/voice_input_service.dart';
import '../../../features/fields/data/tarla_repository.dart';
import '../../../models/tarla.dart';
import '../../../services/api_client.dart';
import '../../../services/database_helper.dart';
import '../data/case_repository.dart';
import '../domain/models/case_category.dart';
import '../domain/models/create_case_input.dart';

const Color _primaryLight = Color(0xFFE8F5E9);

/// Verilen ürün türü ve vaka kategorisine göre otomatik, deterministik ve
/// backend kurallarına uyumlu bir vaka başlığı üretir.
///
/// Format: `{Ürün} • {Kategori}`
/// [cropType] boş veya null ise kategoriye özel güvenli fallback üretilir.
/// Başlık hiçbir durumda 160 karakteri aşmaz.
String buildAutomaticCaseTitle({
  String? cropType,
  required CaseCategory category,
}) {
  final cleanCrop = cropType?.trim() ?? '';
  final categoryLabel = category.displayName;

  final String rawTitle;
  if (cleanCrop.isNotEmpty) {
    rawTitle = '$cleanCrop • $categoryLabel';
  } else {
    rawTitle = switch (category) {
      CaseCategory.disease => 'Hastalık Bildirimi',
      CaseCategory.pest => 'Zararlı Bildirimi',
      CaseCategory.irrigation => 'Sulama Sorunu',
      CaseCategory.nutrition => 'Besleme / Gübre Sorunu',
      CaseCategory.weather => 'Hava Koşulları Bildirimi',
      CaseCategory.other => 'Sorun Bildirimi',
    };
  }

  if (rawTitle.length <= 160) {
    return rawTitle;
  }

  // 160 karakter sınırını aşmaması için ürün adını güvenle sınırla
  if (cleanCrop.isNotEmpty) {
    final suffix = ' • $categoryLabel';
    final maxCropLen = 160 - suffix.length;
    if (maxCropLen > 0) {
      var truncatedCrop = cleanCrop.substring(0, maxCropLen).trim();
      final lastSpace = truncatedCrop.lastIndexOf(' ');
      if (lastSpace > 0 && lastSpace >= maxCropLen - 10) {
        truncatedCrop = truncatedCrop.substring(0, lastSpace).trim();
      }
      return '$truncatedCrop$suffix';
    }
  }

  return rawTitle.substring(0, 160).trim();
}

class SorunBildirEkrani extends StatefulWidget {
  const SorunBildirEkrani({
    super.key,
    this.initialTarla,
    this.initialTarlaId,
    required this.caseRepository,
    required this.tarlaRepository,
    this.imagePickerService,
    this.voiceInputService,
    this.connectivity,
    this.descriptionFocusNode,
  });

  /// Opsiyonel başlangıç tarla nesnesi.
  final Tarla? initialTarla;

  /// Opsiyonel başlangıç tarla kimliği (initialTarla verilmemişse kullanılır).
  final String? initialTarlaId;

  final CaseRepository caseRepository;
  final TarlaRepository tarlaRepository;
  final ImagePickerService? imagePickerService;
  final VoiceInputService? voiceInputService;
  final Connectivity? connectivity;
  final FocusNode? descriptionFocusNode;

  @override
  State<SorunBildirEkrani> createState() => _SorunBildirEkraniState();
}

class _SorunBildirEkraniState extends State<SorunBildirEkrani> {
  final _formKey = GlobalKey<FormState>();
  final _descriptionController = TextEditingController();
  late final FocusNode _descriptionFocusNode;
  late final bool _ownsDescriptionFocusNode;

  late final ImagePickerService _pickerService;
  late final VoiceInputService _voiceService;
  late final bool _ownsVoiceService;
  late final Connectivity _connectivity;

  List<Tarla> _tarlalar = [];
  String? _selectedTarlaId;
  CaseCategory _selectedCategory = CaseCategory.disease;
  PickedImageData? _selectedImage;
  bool _loadingFarms = true;
  bool _isSubmitting = false;

  bool _isListening = false;
  String _baseDescription = '';
  bool _disposed = false;

  Tarla? get _selectedTarla {
    if (_selectedTarlaId == null) return widget.initialTarla;
    return _tarlalar.cast<Tarla?>().firstWhere(
      (t) => t?.id == _selectedTarlaId,
      orElse: () => widget.initialTarla,
    );
  }

  String get _currentAutomaticTitle => buildAutomaticCaseTitle(
    cropType: _selectedTarla?.cropType,
    category: _selectedCategory,
  );

  @override
  void initState() {
    super.initState();
    if (widget.descriptionFocusNode != null) {
      _descriptionFocusNode = widget.descriptionFocusNode!;
      _ownsDescriptionFocusNode = false;
    } else {
      _descriptionFocusNode = FocusNode();
      _ownsDescriptionFocusNode = true;
    }
    _connectivity = widget.connectivity ?? Connectivity();
    _pickerService =
        widget.imagePickerService ?? const DefaultImagePickerService();
    if (widget.voiceInputService != null) {
      _voiceService = widget.voiceInputService!;
      _ownsVoiceService = false;
    } else {
      _voiceService = DefaultVoiceInputService();
      _ownsVoiceService = true;
    }

    _selectedTarlaId = widget.initialTarla?.id ?? widget.initialTarlaId;
    if (widget.initialTarla != null) {
      _tarlalar = [widget.initialTarla!];
    }
    _loadFarms();
  }

  @override
  void dispose() {
    _disposed = true;
    _descriptionController.dispose();
    if (_ownsDescriptionFocusNode) {
      _descriptionFocusNode.dispose();
    }
    if (_isListening) {
      try {
        _voiceService.stopListening();
      } catch (_) {}
    }
    if (_ownsVoiceService) {
      _voiceService.dispose();
    }
    super.dispose();
  }

  Future<void> _loadFarms() async {
    try {
      final list = await widget.tarlaRepository.getTarlalar();
      if (!mounted) return;
      setState(() {
        _loadingFarms = false;
        final mergedList = <Tarla>[...list];
        if (widget.initialTarla != null &&
            !mergedList.any((t) => t.id == widget.initialTarla!.id)) {
          mergedList.insert(0, widget.initialTarla!);
        }
        _tarlalar = mergedList;

        if (_selectedTarlaId == null && mergedList.isNotEmpty) {
          _selectedTarlaId = mergedList.first.id;
        } else if (_selectedTarlaId != null &&
            !mergedList.any((t) => t.id == _selectedTarlaId)) {
          if (mergedList.isNotEmpty) {
            _selectedTarlaId = mergedList.first.id;
          }
        }
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loadingFarms = false;
        if (widget.initialTarla != null) {
          _tarlalar = [widget.initialTarla!];
          _selectedTarlaId = widget.initialTarla!.id;
        }
      });
    }
  }

  Future<void> _pickPhotoFrom(ImageSource source) async {
    if (!mounted) return;
    try {
      final picked = await _pickerService.pickImage(source: source);
      if (picked != null && mounted) {
        setState(() => _selectedImage = picked);
      }
    } on FormatException catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(error.message)));
      }
    }
  }

  Future<void> _pickPhoto() async {
    final source = await showModalBottomSheet<ImageSource>(
      context: context,
      builder: (ctx) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.camera_alt),
              title: const Text('Kameradan Çek'),
              onTap: () => Navigator.pop(ctx, ImageSource.camera),
            ),
            ListTile(
              leading: const Icon(Icons.photo_library),
              title: const Text('Galeriden Seç'),
              onTap: () => Navigator.pop(ctx, ImageSource.gallery),
            ),
          ],
        ),
      ),
    );

    if (source == null || !mounted) return;
    await _pickPhotoFrom(source);
  }

  Future<void> _showTarlaSelectorModal() async {
    if (_tarlalar.isEmpty) return;
    final selected = await showModalBottomSheet<String>(
      context: context,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (ctx) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 12),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Padding(
                padding: EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                child: Text(
                  'Tarla Seçin',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                ),
              ),
              const Divider(),
              Flexible(
                child: ListView.builder(
                  shrinkWrap: true,
                  itemCount: _tarlalar.length,
                  itemBuilder: (ctx, i) {
                    final t = _tarlalar[i];
                    final isSelected = t.id == _selectedTarlaId;
                    final subInfo =
                        (t.cropType != null && t.cropType!.trim().isNotEmpty)
                        ? ' • ${t.cropType}'
                        : '';
                    return ListTile(
                      leading: Icon(
                        Icons.grass,
                        color: isSelected
                            ? AppColors.primary
                            : AppColors.textSecondary,
                      ),
                      title: Text(
                        '${t.name}$subInfo',
                        style: TextStyle(
                          fontWeight: isSelected
                              ? FontWeight.bold
                              : FontWeight.normal,
                        ),
                      ),
                      trailing: isSelected
                          ? const Icon(Icons.check, color: AppColors.primary)
                          : null,
                      onTap: () => Navigator.pop(ctx, t.id),
                    );
                  },
                ),
              ),
            ],
          ),
        ),
      ),
    );

    if (selected != null && mounted) {
      setState(() => _selectedTarlaId = selected);
    }
  }

  Future<void> _toggleVoiceInput() async {
    if (_isListening) {
      await _stopListening();
    } else {
      await _startListening();
    }
  }

  Future<bool> _isOffline() async {
    try {
      final connectivity = await _connectivity.checkConnectivity();
      return connectivity.isEmpty ||
          connectivity.every((item) => item == ConnectivityResult.none);
    } catch (_) {
      // Test/desktop platforms may not expose a connectivity plugin.
      return false;
    }
  }

  void _openKeyboardFallback(String message) {
    if (_disposed || !mounted) return;
    _descriptionFocusNode.requestFocus();
    SystemChannels.textInput.invokeMethod<void>('TextInput.show');

    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text(message),
          duration: const Duration(seconds: 4),
        ),
      );
  }

  Future<void> _startListening() async {
    if (_isListening || _isSubmitting) return;

    final offline = await _isOffline();
    if (offline) {
      _openKeyboardFallback(
        'İnternet yok. Klavyenizdeki mikrofonu kullanarak konuşabilirsiniz.',
      );
      return;
    }

    _baseDescription = _descriptionController.text.trim();

    try {
      final started = await _voiceService.startListening(
        onDevice: false,
        onResult: (result) {
          if (_disposed || !mounted) return;
          final words = result.recognizedWords.trim();
          if (words.isEmpty) return;

          final newText = _baseDescription.isEmpty
              ? words
              : '$_baseDescription $words';

          setState(() {
            _descriptionController.text = newText;
            _descriptionController.selection = TextSelection.fromPosition(
              TextPosition(offset: newText.length),
            );
          });
        },
        onError: (error) {
          if (_disposed || !mounted) return;
          setState(() => _isListening = false);

          if (error.type == VoiceInputErrorType.network ||
              error.type == VoiceInputErrorType.offlineRecognitionUnavailable) {
            _openKeyboardFallback(
              'Sesle yazma kullanılamadı. Klavyenizdeki mikrofonu deneyebilirsiniz.',
            );
            return;
          }

          final String msg;
          switch (error.type) {
            case VoiceInputErrorType.permissionDenied:
              msg = 'Mikrofon izni olmadan sesli anlatım kullanılamıyor.';
              break;
            case VoiceInputErrorType.unavailable:
              msg = 'Bu cihazda ses tanıma özelliği kullanılamıyor.';
              break;
            default:
              msg = 'Ses tanıma başlatılamadı. Lütfen tekrar deneyin.';
              break;
          }
          ScaffoldMessenger.of(context)
            ..hideCurrentSnackBar()
            ..showSnackBar(
              SnackBar(content: Text(msg), duration: const Duration(seconds: 4)),
            );
        },
        onListeningChanged: (isListening) {
          if (_disposed || !mounted) return;
          setState(() => _isListening = isListening);
        },
      );

      if (!started && !_disposed && mounted) {
        setState(() => _isListening = false);
      }
    } catch (_) {
      if (!_disposed && mounted) {
        setState(() => _isListening = false);
      }
    }
  }

  Future<void> _stopListening() async {
    try {
      await _voiceService.stopListening();
    } catch (_) {}
    if (!_disposed && mounted) {
      setState(() => _isListening = false);
    }
  }

  Future<void> _submit() async {
    if (_isSubmitting) return;

    if (_isListening) {
      await _stopListening();
    }

    if (!mounted) return;

    if (_selectedTarlaId == null || _selectedTarlaId!.isEmpty) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          const SnackBar(content: Text('Lütfen bir tarla seçin.')),
        );
      return;
    }

    final description = _descriptionController.text.trim();

    if (description.length < 2) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          const SnackBar(
            content: Text('Lütfen sorununuzu en az 2 karakter ile açıklayın.'),
          ),
        );
      return;
    }

    final autoTitle = _currentAutomaticTitle;

    setState(() => _isSubmitting = true);

    try {
      final result = await widget.caseRepository.createCase(
        CreateCaseInput(
          farmId: _selectedTarlaId!,
          category: _selectedCategory,
          title: autoTitle,
          description: description,
          imageBytes: _selectedImage?.bytes,
          imageFileName: _selectedImage?.name,
        ),
      );

      if (!mounted) return;
      final queued = result.startsWith('pending:');
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            backgroundColor: AppColors.primary,
            content: Text(
              queued
                  ? 'Sorununuz kaydedildi. İnternet bağlantısı geldiğinde uzmana gönderilecek.'
                  : 'Sorununuz uzmana gönderildi.',
            ),
          ),
        );
      Navigator.of(context).pop(true);
    } catch (e) {
      if (!mounted) return;
      final errorMessage = e is ApiException
          ? e.message
          : 'Bir sorun oluştu. Lütfen bağlantınızı kontrol edip tekrar deneyin.';
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            backgroundColor: AppColors.error,
            content: Text(errorMessage),
          ),
        );
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  Widget _buildSubmitButton() {
    return FilledButton(
      key: const Key('btn_submit_case'),
      style: FilledButton.styleFrom(
        minimumSize: const Size(double.infinity, 50),
        padding: const EdgeInsets.symmetric(vertical: 14),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      ),
      onPressed: _isSubmitting ? null : _submit,
      child: _isSubmitting
          ? const Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                SizedBox(
                  height: 20,
                  width: 20,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: Colors.white,
                  ),
                ),
                SizedBox(width: 10),
                Text(
                  'Gönderiliyor...',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                ),
              ],
            )
          : const Text(
              'Uzmana Gönder',
              style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
            ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Sorun Bildir')),
      body: _loadingFarms
          ? const Center(child: CircularProgressIndicator())
          : SingleChildScrollView(
              padding: const EdgeInsets.all(AppSpacing.md),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    // 1. HANGİ TARLA? (Kompakt Alan)
                    if (_tarlalar.isNotEmpty)
                      Card(
                        elevation: 0,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(12),
                          side: BorderSide(
                            color: AppColors.primary.withValues(alpha: 0.2),
                          ),
                        ),
                        color: _primaryLight.withValues(alpha: 0.5),
                        child: InkWell(
                          borderRadius: BorderRadius.circular(12),
                          onTap: _showTarlaSelectorModal,
                          child: Padding(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 14,
                              vertical: 10,
                            ),
                            child: Row(
                              children: [
                                const Icon(
                                  Icons.location_on,
                                  color: AppColors.primary,
                                  size: 20,
                                ),
                                const SizedBox(width: 8),
                                Expanded(
                                  child: Text(
                                    '${_selectedTarla?.name ?? 'Tarla Seçilmedi'}${_selectedTarla?.cropType != null && _selectedTarla!.cropType!.trim().isNotEmpty ? ' • ${_selectedTarla!.cropType}' : ''}',
                                    key: const Key('txt_compact_tarla_info'),
                                    style: const TextStyle(
                                      fontWeight: FontWeight.bold,
                                      fontSize: 14,
                                      color: AppColors.onSurface,
                                    ),
                                    maxLines: 2,
                                    overflow: TextOverflow.ellipsis,
                                  ),
                                ),
                                const SizedBox(width: 8),
                                TextButton(
                                  key: const Key('btn_change_tarla'),
                                  onPressed: _showTarlaSelectorModal,
                                  style: TextButton.styleFrom(
                                    visualDensity: VisualDensity.compact,
                                    padding: const EdgeInsets.symmetric(
                                      horizontal: 8,
                                      vertical: 4,
                                    ),
                                  ),
                                  child: const Text(
                                    'Değiştir',
                                    style: TextStyle(
                                      fontWeight: FontWeight.w600,
                                      color: AppColors.primary,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                      )
                    else if (widget.initialTarla != null)
                      Card(
                        margin: EdgeInsets.zero,
                        color: AppColors.surface,
                        child: ListTile(
                          leading: const Icon(
                            Icons.location_on,
                            color: AppColors.primary,
                          ),
                          title: const Text('İlgili Tarla'),
                          subtitle: Text(
                            '${widget.initialTarla!.name}${(widget.initialTarla!.cropType != null && widget.initialTarla!.cropType!.isNotEmpty) ? ' • ${widget.initialTarla!.cropType}' : ''}',
                            style: const TextStyle(fontWeight: FontWeight.bold),
                          ),
                        ),
                      )
                    else if (widget.initialTarlaId == null)
                      const Card(
                        color: AppColors.surface,
                        child: Padding(
                          padding: EdgeInsets.all(12),
                          child: Row(
                            children: [
                              Icon(
                                Icons.info_outline,
                                color: AppColors.warning,
                              ),
                              SizedBox(width: 8),
                              Expanded(
                                child: Text(
                                  'Kayıtlı tarla bulunamadı. Sorun bildirmek için önce bir tarla eklemelisiniz.',
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    const SizedBox(height: AppSpacing.md),

                    // 2. FOTOĞRAF EKLE (Birinci Ana Aksiyon)
                    Card(
                      elevation: 0,
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                        side: BorderSide(
                          color: _selectedImage != null
                              ? AppColors.primary.withValues(alpha: 0.3)
                              : AppColors.textDisabled.withValues(alpha: 0.3),
                        ),
                      ),
                      color: AppColors.surface,
                      child: Padding(
                        padding: const EdgeInsets.all(AppSpacing.md),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            if (_selectedImage != null) ...[
                              ClipRRect(
                                borderRadius: BorderRadius.circular(8),
                                child: Image.memory(
                                  _selectedImage!.bytes,
                                  height: 200,
                                  width: double.infinity,
                                  fit: BoxFit.cover,
                                ),
                              ),
                              const SizedBox(height: AppSpacing.sm),
                              Wrap(
                                alignment: WrapAlignment.spaceEvenly,
                                spacing: 8,
                                runSpacing: 4,
                                children: [
                                  Semantics(
                                    label: 'Fotoğrafı değiştir',
                                    button: true,
                                    child: OutlinedButton.icon(
                                      key: const Key('btn_change_photo'),
                                      icon: const Icon(Icons.sync, size: 18),
                                      label: const Text('Fotoğrafı Değiştir'),
                                      onPressed: _pickPhoto,
                                    ),
                                  ),
                                  Semantics(
                                    label: 'Fotoğrafı kaldır',
                                    button: true,
                                    child: TextButton.icon(
                                      key: const Key('btn_remove_photo'),
                                      icon: const Icon(
                                        Icons.delete_outline,
                                        size: 18,
                                        color: AppColors.error,
                                      ),
                                      label: const Text(
                                        'Fotoğrafı Kaldır',
                                        style: TextStyle(
                                          color: AppColors.error,
                                        ),
                                      ),
                                      onPressed: () =>
                                          setState(() => _selectedImage = null),
                                    ),
                                  ),
                                ],
                              ),
                            ] else ...[
                              Row(
                                children: [
                                  Container(
                                    padding: const EdgeInsets.all(10),
                                    decoration: BoxDecoration(
                                      color: _primaryLight,
                                      borderRadius: BorderRadius.circular(10),
                                    ),
                                    child: const Icon(
                                      Icons.camera_alt,
                                      color: AppColors.primary,
                                      size: 24,
                                    ),
                                  ),
                                  const SizedBox(width: AppSpacing.md),
                                  const Expanded(
                                    child: Column(
                                      crossAxisAlignment:
                                          CrossAxisAlignment.start,
                                      children: [
                                        Text(
                                          'Sorunun fotoğrafını ekleyin',
                                          style: TextStyle(
                                            fontWeight: FontWeight.bold,
                                            fontSize: 15,
                                          ),
                                        ),
                                        SizedBox(height: 2),
                                        Text(
                                          'Fotoğraf eklerseniz uzman sorunu daha hızlı değerlendirebilir.',
                                          style: TextStyle(
                                            color: AppColors.textSecondary,
                                            fontSize: 12,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                ],
                              ),
                              const SizedBox(height: AppSpacing.md),
                              LayoutBuilder(
                                builder: (context, constraints) {
                                  final isVeryCompact =
                                      constraints.maxWidth < 280;
                                  if (isVeryCompact) {
                                    return Column(
                                      crossAxisAlignment:
                                          CrossAxisAlignment.stretch,
                                      children: [
                                        Semantics(
                                          label: 'Fotoğraf çek',
                                          button: true,
                                          child: OutlinedButton.icon(
                                            key: const Key('btn_pick_camera'),
                                            icon: const Icon(
                                              Icons.camera_alt,
                                              size: 18,
                                            ),
                                            label: const Text('Fotoğraf Çek'),
                                            onPressed: () => _pickPhotoFrom(
                                              ImageSource.camera,
                                            ),
                                          ),
                                        ),
                                        const SizedBox(height: 8),
                                        Semantics(
                                          label: 'Galeriden seç',
                                          button: true,
                                          child: OutlinedButton.icon(
                                            key: const Key('btn_pick_gallery'),
                                            icon: const Icon(
                                              Icons.photo_library,
                                              size: 18,
                                            ),
                                            label: const Text('Galeriden Seç'),
                                            onPressed: () => _pickPhotoFrom(
                                              ImageSource.gallery,
                                            ),
                                          ),
                                        ),
                                      ],
                                    );
                                  }
                                  return Row(
                                    children: [
                                      Expanded(
                                        child: Semantics(
                                          label: 'Fotoğraf çek',
                                          button: true,
                                          child: OutlinedButton.icon(
                                            key: const Key('btn_pick_camera'),
                                            icon: const Icon(
                                              Icons.camera_alt,
                                              size: 18,
                                            ),
                                            label: const Text(
                                              'Fotoğraf Çek',
                                              overflow: TextOverflow.ellipsis,
                                            ),
                                            onPressed: () => _pickPhotoFrom(
                                              ImageSource.camera,
                                            ),
                                          ),
                                        ),
                                      ),
                                      const SizedBox(width: AppSpacing.sm),
                                      Expanded(
                                        child: Semantics(
                                          label: 'Galeriden seç',
                                          button: true,
                                          child: OutlinedButton.icon(
                                            key: const Key('btn_pick_gallery'),
                                            icon: const Icon(
                                              Icons.photo_library,
                                              size: 18,
                                            ),
                                            label: const Text(
                                              'Galeriden Seç',
                                              overflow: TextOverflow.ellipsis,
                                            ),
                                            onPressed: () => _pickPhotoFrom(
                                              ImageSource.gallery,
                                            ),
                                          ),
                                        ),
                                      ),
                                    ],
                                  );
                                },
                              ),
                            ],
                          ],
                        ),
                      ),
                    ),
                    const SizedBox(height: AppSpacing.md),

                    // 3. SORUNU ANLAT (İkinci Ana Aksiyon)
                    const Text(
                      'Sorunu Anlatın',
                      style: TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 16,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.xs),

                    // Sesle Anlat Butonu veya Dinleme Banner'ı
                    if (!_isListening)
                      SizedBox(
                        width: double.infinity,
                        child: OutlinedButton.icon(
                          key: const Key('btn_voice_input'),
                          onPressed: _isSubmitting ? null : _toggleVoiceInput,
                          icon: const Icon(
                            Icons.mic,
                            size: 22,
                            color: AppColors.primary,
                          ),
                          label: const Text(
                            '🎙️ Konuşarak Anlat',
                            style: TextStyle(
                              color: AppColors.primary,
                              fontWeight: FontWeight.bold,
                              fontSize: 15,
                            ),
                          ),
                          style: OutlinedButton.styleFrom(
                            padding: const EdgeInsets.symmetric(vertical: 12),
                            side: const BorderSide(
                              color: AppColors.primary,
                              width: 1.5,
                            ),
                            backgroundColor: _primaryLight.withValues(
                              alpha: 0.3,
                            ),
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(10),
                            ),
                          ),
                        ),
                      )
                    else
                      Container(
                        key: const Key('voice_listening_banner'),
                        padding: const EdgeInsets.symmetric(
                          horizontal: 14,
                          vertical: 10,
                        ),
                        decoration: BoxDecoration(
                          color: AppColors.error.withValues(alpha: 0.08),
                          borderRadius: BorderRadius.circular(10),
                          border: Border.all(
                            color: AppColors.error.withValues(alpha: 0.5),
                            width: 1.5,
                          ),
                        ),
                        child: Row(
                          children: [
                            const SizedBox(
                              width: 16,
                              height: 16,
                              child: CircularProgressIndicator(
                                strokeWidth: 2.5,
                                color: AppColors.error,
                              ),
                            ),
                            const SizedBox(width: AppSpacing.sm),
                            Expanded(
                              child: const Text(
                                '🎙️ Dinliyorum... Konuşabilirsiniz',
                                style: TextStyle(
                                  color: AppColors.error,
                                  fontWeight: FontWeight.bold,
                                  fontSize: 14,
                                ),
                              ),
                            ),
                            TextButton.icon(
                              key: const Key('btn_stop_listening'),
                              onPressed: _toggleVoiceInput,
                              icon: const Icon(
                                Icons.stop,
                                size: 18,
                                color: AppColors.error,
                              ),
                              label: const Text(
                                'Durdur',
                                style: TextStyle(
                                  color: AppColors.error,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              style: TextButton.styleFrom(
                                visualDensity: VisualDensity.compact,
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 8,
                                  vertical: 4,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    const SizedBox(height: AppSpacing.xs),

                    // Açıklama TextField
                    TextField(
                      key: const Key('field_description'),
                      controller: _descriptionController,
                      focusNode: _descriptionFocusNode,
                      maxLines: 4,
                      maxLength: 2000,
                      decoration: InputDecoration(
                        hintText:
                            'Ne zaman başladı? Nasıl görünüyor? Konuşarak veya yazarak anlatın.',
                        hintStyle: const TextStyle(
                          color: AppColors.textDisabled,
                          fontSize: 13,
                        ),
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(10),
                        ),
                        contentPadding: const EdgeInsets.all(12),
                      ),
                    ),
                    const SizedBox(height: AppSpacing.sm),

                    // 4. KATEGORİ (İkincil / Geri Planda)
                    const Text(
                      'Sorun türü',
                      style: TextStyle(
                        fontWeight: FontWeight.w600,
                        fontSize: 14,
                        color: AppColors.textSecondary,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Wrap(
                      spacing: 8,
                      runSpacing: 4,
                      children: CaseCategory.values.map((cat) {
                        final selected = _selectedCategory == cat;
                        return ChoiceChip(
                          label: Text(cat.displayName),
                          selected: selected,
                          selectedColor: _primaryLight,
                          onSelected: (val) {
                            if (val) setState(() => _selectedCategory = cat);
                          },
                        );
                      }).toList(),
                    ),
                    const SizedBox(height: AppSpacing.sm),

                    // Otomatik Başlık Özeti (Sade İkincil Bilgi)
                    Container(
                      key: const Key('badge_auto_title'),
                      padding: const EdgeInsets.symmetric(
                        horizontal: 10,
                        vertical: 6,
                      ),
                      decoration: BoxDecoration(
                        color: AppColors.surface,
                        borderRadius: BorderRadius.circular(6),
                        border: Border.all(
                          color: AppColors.textDisabled.withValues(alpha: 0.2),
                        ),
                      ),
                      child: Row(
                        children: [
                          const Icon(
                            Icons.bookmark_outline,
                            size: 14,
                            color: AppColors.textDisabled,
                          ),
                          const SizedBox(width: 6),
                          const Text(
                            'Vaka Başlığı: ',
                            style: TextStyle(
                              fontSize: 12,
                              color: AppColors.textSecondary,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                          Expanded(
                            child: Text(
                              _currentAutomaticTitle,
                              key: const Key('txt_auto_title'),
                              style: const TextStyle(
                                fontSize: 12,
                                fontWeight: FontWeight.bold,
                                color: AppColors.textSecondary,
                              ),
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
      bottomNavigationBar: _loadingFarms
          ? null
          : SafeArea(
              top: false,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(
                  AppSpacing.md,
                  AppSpacing.sm,
                  AppSpacing.md,
                  AppSpacing.sm,
                ),
                child: _buildSubmitButton(),
              ),
            ),
    );
  }
}
