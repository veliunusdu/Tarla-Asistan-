import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../../../app/theme/app_colors.dart';
import '../../../app/theme/app_spacing.dart';
import '../../../features/ai_assistant/data/image_picker_service.dart';
import '../../../features/fields/data/tarla_repository.dart';
import '../../../models/tarla.dart';
import '../../../services/api_client.dart';
import '../data/case_repository.dart';
import '../domain/models/case_category.dart';
import '../domain/models/create_case_input.dart';

const Color _primaryLight = Color(0xFFE8F5E9);

class SorunBildirEkrani extends StatefulWidget {
  const SorunBildirEkrani({
    super.key,
    this.initialTarlaId,
    required this.caseRepository,
    required this.tarlaRepository,
    this.imagePickerService,
  });

  final String? initialTarlaId;
  final CaseRepository caseRepository;
  final TarlaRepository tarlaRepository;
  final ImagePickerService? imagePickerService;

  @override
  State<SorunBildirEkrani> createState() => _SorunBildirEkraniState();
}

class _SorunBildirEkraniState extends State<SorunBildirEkrani> {
  final _formKey = GlobalKey<FormState>();
  final _titleController = TextEditingController();
  final _descriptionController = TextEditingController();

  late final ImagePickerService _pickerService;
  List<Tarla> _tarlalar = [];
  String? _selectedTarlaId;
  CaseCategory _selectedCategory = CaseCategory.disease;
  PickedImageData? _selectedImage;
  bool _loadingFarms = true;
  bool _isSubmitting = false;

  @override
  void initState() {
    super.initState();
    _pickerService = widget.imagePickerService ?? const DefaultImagePickerService();
    _selectedTarlaId = widget.initialTarlaId;
    _loadFarms();
  }

  @override
  void dispose() {
    _titleController.dispose();
    _descriptionController.dispose();
    super.dispose();
  }

  Future<void> _loadFarms() async {
    try {
      final list = await widget.tarlaRepository.getTarlalar();
      if (!mounted) return;
      setState(() {
        _tarlalar = list;
        _loadingFarms = false;
        if (_selectedTarlaId == null && list.isNotEmpty) {
          _selectedTarlaId = list.first.id;
        } else if (_selectedTarlaId != null &&
            !list.any((t) => t.id == _selectedTarlaId) &&
            list.isNotEmpty) {
          _selectedTarlaId = list.first.id;
        }
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _loadingFarms = false);
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
    final picked = await _pickerService.pickImage(source: source);
    if (picked != null && mounted) {
      setState(() => _selectedImage = picked);
    }
  }

  Future<void> _submit() async {
    if (_isSubmitting) return;
    if (_selectedTarlaId == null || _selectedTarlaId!.isEmpty) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          const SnackBar(content: Text('Lütfen bir tarla seçin.')),
        );
      return;
    }

    final title = _titleController.text.trim();
    final description = _descriptionController.text.trim();

    if (title.length < 2) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          const SnackBar(content: Text('Lütfen en az 2 karakterli bir başlık girin.')),
        );
      return;
    }

    if (description.length < 2) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          const SnackBar(content: Text('Lütfen sorununuzu en az 2 karakter ile açıklayın.')),
        );
      return;
    }

    setState(() => _isSubmitting = true);

    try {
      await widget.caseRepository.createCase(
        CreateCaseInput(
          farmId: _selectedTarlaId!,
          category: _selectedCategory,
          title: title,
          description: description,
          imageBytes: _selectedImage?.bytes,
          imageFileName: _selectedImage?.name,
        ),
      );

      if (!mounted) return;
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          const SnackBar(
            backgroundColor: AppColors.primary,
            content: Text('Sorun bildiriminiz ziraat mühendisine iletildi.'),
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

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Sorun Bildir'),
      ),
      body: _loadingFarms
          ? const Center(child: CircularProgressIndicator())
          : SingleChildScrollView(
              padding: const EdgeInsets.all(AppSpacing.md),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    // Tarla Seçimi
                    if (_tarlalar.isNotEmpty)
                      DropdownButtonFormField<String>(
                        value: _selectedTarlaId,
                        decoration: const InputDecoration(
                          labelText: 'İlgili Tarla',
                          prefixIcon: Icon(Icons.grass),
                        ),
                        items: _tarlalar.map((t) {
                          return DropdownMenuItem(
                            value: t.id,
                            child: Text(t.name),
                          );
                        }).toList(),
                        onChanged: widget.initialTarlaId != null
                            ? null
                            : (val) => setState(() => _selectedTarlaId = val),
                      )
                    else if (widget.initialTarlaId == null)
                      const Card(
                        color: AppColors.surface,
                        child: Padding(
                          padding: EdgeInsets.all(12),
                          child: Row(
                            children: [
                              Icon(Icons.info_outline, color: AppColors.warning),
                              SizedBox(width: 8),
                              Expanded(
                                child: Text('Kayıtlı tarla bulunamadı. Sorun bildirmek için önce bir tarla eklemelisiniz.'),
                              ),
                            ],
                          ),
                        ),
                      ),
                    const SizedBox(height: AppSpacing.md),

                    // Fotoğraf Alanı
                    Card(
                      child: Padding(
                        padding: const EdgeInsets.all(12),
                        child: Column(
                          children: [
                            if (_selectedImage != null) ...[
                              ClipRRect(
                                borderRadius: BorderRadius.circular(8),
                                child: Image.memory(
                                  _selectedImage!.bytes,
                                  height: 180,
                                  width: double.infinity,
                                  fit: BoxFit.cover,
                                ),
                              ),
                              const SizedBox(height: 8),
                              TextButton.icon(
                                icon: const Icon(Icons.delete_outline, color: AppColors.error),
                                label: const Text('Fotoğrafı Kaldır', style: TextStyle(color: AppColors.error)),
                                onPressed: () => setState(() => _selectedImage = null),
                              ),
                            ] else ...[
                              ListTile(
                                leading: const CircleAvatar(
                                  backgroundColor: _primaryLight,
                                  child: Icon(Icons.camera_alt, color: AppColors.primary),
                                ),
                                title: const Text('Sorunun Fotoğrafını Çek'),
                                subtitle: const Text('Ziraat mühendisinin teşhisini hızlandırır'),
                                trailing: const Icon(Icons.chevron_right),
                                onTap: _pickPhoto,
                              ),
                            ],
                          ],
                        ),
                      ),
                    ),
                    const SizedBox(height: AppSpacing.md),

                    // Kategori Çipleri
                    const Text(
                      'Sorun Kategorisi',
                      style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
                    ),
                    const SizedBox(height: 8),
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
                    const SizedBox(height: AppSpacing.md),

                    // Başlık
                    TextField(
                      controller: _titleController,
                      maxLength: 160,
                      decoration: const InputDecoration(
                        labelText: 'Sorun Başlığı',
                        hintText: 'Örn: Yapraklarda beyaz lekeler',
                      ),
                    ),
                    const SizedBox(height: AppSpacing.md),

                    // Açıklama
                    TextField(
                      controller: _descriptionController,
                      maxLines: 4,
                      maxLength: 2000,
                      decoration: const InputDecoration(
                        labelText: 'Detaylı Açıklama',
                        hintText: 'Ne zaman başladı? Ne kadar alana yayıldı? Açıklayınız.',
                      ),
                    ),
                    const SizedBox(height: AppSpacing.md),

                    // Gönder Butonu
                    FilledButton(
                      style: FilledButton.styleFrom(
                        padding: const EdgeInsets.symmetric(vertical: 16),
                      ),
                      onPressed: _isSubmitting ? null : _submit,
                      child: _isSubmitting
                          ? const SizedBox(
                              height: 20,
                              width: 20,
                              child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                            )
                          : const Text(
                              'Uzmana Gönder',
                              style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                            ),
                    ),
                  ],
                ),
              ),
            ),
    );
  }
}
