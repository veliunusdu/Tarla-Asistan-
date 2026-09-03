# Mobile Case Creation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable farmers to report field issues/cases with category, photo, and voice/text description from the mobile app to the backend support case system.

**Architecture:** Feature-first module `mobile/lib/features/cases/` containing domain models (`CaseCategory`, `CreateCaseInput`), repository layer (`CaseRepository`, `BackendCaseRepository`) that uploads media to `/api/v1/media` and creates cases at `/api/v1/cases`, and UI screen `SorunBildirEkrani` integrated into field detail and home screens.

**Tech Stack:** Flutter, Dart, `ApiClient`, `ImagePickerService`, `flutter_test`.

## Global Constraints

- Follow Clean Architecture and existing feature-first project structure (`features/cases`).
- Follow TDD: every step starts with a failing test and ends with passing tests.
- High contrast and large touch targets (min 48dp) suitable for outdoor farming conditions.
- Handle network and validation errors gracefully without losing farmer input.
- Maintain backward compatibility with existing `TarlaDetayEkrani` and `AnaSayfaEkrani` signatures.

---

### Task 1: Domain Models (`CaseCategory` & `CreateCaseInput`)

**Files:**
- Create: `mobile/lib/features/cases/domain/models/case_category.dart`
- Create: `mobile/lib/features/cases/domain/models/create_case_input.dart`
- Test: `mobile/test/features/cases/domain/models/case_category_test.dart`

**Interfaces:**
- Consumes: None
- Produces:
  ```dart
  enum CaseCategory { disease, pest, irrigation, nutrition, weather, other }
  extension CaseCategoryX on CaseCategory {
    String get displayName;
    String get backendValue;
  }

  class CreateCaseInput {
    const CreateCaseInput({
      required this.farmId,
      required this.category,
      required this.title,
      required this.description,
      this.imageBytes,
      this.imageFileName,
    });
    final String farmId;
    final CaseCategory category;
    final String title;
    final String description;
    final List<int>? imageBytes;
    final String? imageFileName;
  }
  ```

- [ ] **Step 1: Write failing test for `CaseCategory` and `CreateCaseInput`**

```dart
// mobile/test/features/cases/domain/models/case_category_test.dart
import 'package:flutter_test/flutter_test.dart';
import 'package:tarla_asistani/features/cases/domain/models/case_category.dart';
import 'package:tarla_asistani/features/cases/domain/models/create_case_input.dart';

void main() {
  group('CaseCategory', () {
    test('enum values map correctly to display names and backend strings', () {
      expect(CaseCategory.disease.displayName, 'Hastalık');
      expect(CaseCategory.disease.backendValue, 'Disease');

      expect(CaseCategory.pest.displayName, 'Zararlı');
      expect(CaseCategory.pest.backendValue, 'Pest');

      expect(CaseCategory.irrigation.displayName, 'Sulama');
      expect(CaseCategory.irrigation.backendValue, 'Irrigation');

      expect(CaseCategory.nutrition.displayName, 'Besleme / Gübre');
      expect(CaseCategory.nutrition.backendValue, 'Nutrition');

      expect(CaseCategory.weather.displayName, 'Hava Koşulları');
      expect(CaseCategory.weather.backendValue, 'Weather');

      expect(CaseCategory.other.displayName, 'Diğer');
      expect(CaseCategory.other.backendValue, 'Other');
    });
  });

  group('CreateCaseInput', () {
    test('instantiates with required and optional properties', () {
      const input = CreateCaseInput(
        farmId: 'farm-123',
        category: CaseCategory.disease,
        title: 'Yaprak Lekesi',
        description: 'Alt yapraklarda sarı lekeler oluştu.',
        imageBytes: [1, 2, 3],
        imageFileName: 'foto.jpg',
      );

      expect(input.farmId, 'farm-123');
      expect(input.category, CaseCategory.disease);
      expect(input.title, 'Yaprak Lekesi');
      expect(input.description, 'Alt yapraklarda sarı lekeler oluştu.');
      expect(input.imageBytes, [1, 2, 3]);
      expect(input.imageFileName, 'foto.jpg');
    });
  });
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd mobile && flutter test test/features/cases/domain/models/case_category_test.dart`
Expected: FAIL with compilation error (target files do not exist).

- [ ] **Step 3: Implement `case_category.dart` and `create_case_input.dart`**

```dart
// mobile/lib/features/cases/domain/models/case_category.dart
enum CaseCategory {
  disease,
  pest,
  irrigation,
  nutrition,
  weather,
  other,
}

extension CaseCategoryX on CaseCategory {
  String get displayName => switch (this) {
    CaseCategory.disease => 'Hastalık',
    CaseCategory.pest => 'Zararlı',
    CaseCategory.irrigation => 'Sulama',
    CaseCategory.nutrition => 'Besleme / Gübre',
    CaseCategory.weather => 'Hava Koşulları',
    CaseCategory.other => 'Diğer',
  };

  String get backendValue => switch (this) {
    CaseCategory.disease => 'Disease',
    CaseCategory.pest => 'Pest',
    CaseCategory.irrigation => 'Irrigation',
    CaseCategory.nutrition => 'Nutrition',
    CaseCategory.weather => 'Weather',
    CaseCategory.other => 'Other',
  };
}
```

```dart
// mobile/lib/features/cases/domain/models/create_case_input.dart
import 'case_category.dart';

class CreateCaseInput {
  const CreateCaseInput({
    required this.farmId,
    required this.category,
    required this.title,
    required this.description,
    this.imageBytes,
    this.imageFileName,
  });

  final String farmId;
  final CaseCategory category;
  final String title;
  final String description;
  final List<int>? imageBytes;
  final String? imageFileName;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd mobile && flutter test test/features/cases/domain/models/case_category_test.dart`
Expected: PASS (All tests passed).

- [ ] **Step 5: Commit**

```bash
git add mobile/lib/features/cases/domain/ mobile/test/features/cases/domain/
git commit -m "feat(cases): add CaseCategory and CreateCaseInput domain models"
```

---

### Task 2: Data Layer (`CaseRepository` & `BackendCaseRepository`)

**Files:**
- Create: `mobile/lib/features/cases/data/case_repository.dart`
- Create: `mobile/lib/features/cases/data/backend_case_repository.dart`
- Test: `mobile/test/features/cases/data/backend_case_repository_test.dart`

**Interfaces:**
- Consumes: `ApiClient`, `CreateCaseInput`, `CaseCategory`
- Produces:
  ```dart
  abstract interface class CaseRepository {
    Future<String> createCase(CreateCaseInput input);
  }
  class BackendCaseRepository implements CaseRepository {
    const BackendCaseRepository({required ApiClient apiClient});
  }
  ```

- [ ] **Step 1: Write failing test for `BackendCaseRepository`**

```dart
// mobile/test/features/cases/data/backend_case_repository_test.dart
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:tarla_asistani/features/cases/data/backend_case_repository.dart';
import 'package:tarla_asistani/features/cases/data/case_repository.dart';
import 'package:tarla_asistani/features/cases/domain/models/case_category.dart';
import 'package:tarla_asistani/features/cases/domain/models/create_case_input.dart';
import 'package:tarla_asistani/services/api_client.dart';

class MockHttpClient extends http.BaseClient {
  MockHttpClient(this._handler);
  final Future<http.Response> Function(http.BaseRequest request) _handler;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final response = await _handler(request);
    return http.StreamedResponse(
      Stream.value(response.bodyBytes),
      response.statusCode,
      headers: response.headers,
    );
  }
}

void main() {
  group('BackendCaseRepository', () {
    test('createCase without media calls /cases directly and returns id', () async {
      final requests = <http.BaseRequest>[];
      final client = ApiClient(
        httpClient: MockHttpClient((req) async {
          requests.add(req);
          if (req.url.path.endsWith('/cases')) {
            return http.Response(
              '{"id":"case-uuid-1","title":"Test","status":"Open"}',
              201,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }
          return http.Response('{"detail":"not found"}', 404);
        }),
        idTokenProvider: () async => 'dummy-token',
      );

      final CaseRepository repository = BackendCaseRepository(apiClient: client);

      final resultId = await repository.createCase(
        const CreateCaseInput(
          farmId: 'farm-1',
          category: CaseCategory.disease,
          title: 'Pas Hastalığı',
          description: 'Yapraklarda sarı pas lekeleri görüldü.',
        ),
      );

      expect(resultId, 'case-uuid-1');
      expect(requests.length, 1);
      expect(requests.first.url.path, endsWith('/cases'));
    });

    test('createCase with media calls /media first then /cases with mediaIds', () async {
      final requests = <http.BaseRequest>[];
      final client = ApiClient(
        httpClient: MockHttpClient((req) async {
          requests.add(req);
          if (req.url.path.endsWith('/media')) {
            return http.Response(
              '{"id":"media-uuid-1","url":"https://example.com/media-1"}',
              201,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }
          if (req.url.path.endsWith('/cases')) {
            return http.Response(
              '{"id":"case-uuid-2","title":"Test with media"}',
              201,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }
          return http.Response('{"detail":"not found"}', 404);
        }),
        idTokenProvider: () async => 'dummy-token',
      );

      final repository = BackendCaseRepository(apiClient: client);

      final resultId = await repository.createCase(
        const CreateCaseInput(
          farmId: 'farm-1',
          category: CaseCategory.pest,
          title: 'Yeşil Kurt',
          description: 'Meyvelerde delikler var.',
          imageBytes: [1, 2, 3, 4],
          imageFileName: 'kurt.jpg',
        ),
      );

      expect(resultId, 'case-uuid-2');
      expect(requests.length, 2);
      expect(requests[0].url.path, endsWith('/media'));
      expect(requests[1].url.path, endsWith('/cases'));
    });
  });
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd mobile && flutter test test/features/cases/data/backend_case_repository_test.dart`
Expected: FAIL (files not found).

- [ ] **Step 3: Implement `case_repository.dart` and `backend_case_repository.dart`**

```dart
// mobile/lib/features/cases/data/case_repository.dart
import '../domain/models/create_case_input.dart';

abstract interface class CaseRepository {
  Future<String> createCase(CreateCaseInput input);
}
```

```dart
// mobile/lib/features/cases/data/backend_case_repository.dart
import 'package:uuid/uuid.dart';

import '../../../services/api_client.dart';
import '../domain/models/case_category.dart';
import '../domain/models/create_case_input.dart';
import 'case_repository.dart';

class BackendCaseRepository implements CaseRepository {
  const BackendCaseRepository({
    required ApiClient apiClient,
    Uuid uuid = const Uuid(),
  })  : _api = apiClient,
        _uuid = uuid;

  final ApiClient _api;
  final Uuid _uuid;

  @override
  Future<String> createCase(CreateCaseInput input) async {
    List<String>? mediaIds;

    if (input.imageBytes != null && input.imageBytes!.isNotEmpty) {
      final fileName = input.imageFileName ?? 'case_image.jpg';
      final mediaResponse = await _api.postMultipart(
        '/media',
        files: [
          ApiMultipartFile(
            field: 'file',
            bytes: input.imageBytes!,
            filename: fileName,
            contentType: 'image/jpeg',
          ),
        ],
      );
      final mediaId = mediaResponse['id']?.toString();
      if (mediaId != null && mediaId.isNotEmpty) {
        mediaIds = [mediaId];
      }
    }

    final payload = <String, dynamic>{
      'farm_id': input.farmId,
      'category': input.category.backendValue,
      'title': input.title.trim(),
      'description': input.description.trim(),
      'media_ids': mediaIds,
      'client_operation_id': _uuid.v4(),
    };

    final response = await _api.postJson('/cases', payload);
    final caseId = response['id']?.toString();
    if (caseId == null || caseId.isEmpty) {
      throw const ApiException('Vaka oluşturuldu ancak yanıt kimliği alınamadı.');
    }
    return caseId;
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd mobile && flutter test test/features/cases/data/backend_case_repository_test.dart`
Expected: PASS (All tests passed).

- [ ] **Step 5: Commit**

```bash
git add mobile/lib/features/cases/data/ mobile/test/features/cases/data/
git commit -m "feat(cases): implement CaseRepository and BackendCaseRepository"
```

---

### Task 3: Presentation Layer (`SorunBildirEkrani`)

**Files:**
- Create: `mobile/lib/features/cases/presentation/sorun_bildir_ekrani.dart`
- Test: `mobile/test/features/cases/presentation/sorun_bildir_ekrani_test.dart`

**Interfaces:**
- Consumes: `CaseRepository`, `TarlaRepository`, `Tarla`, `ImagePickerService`
- Produces:
  ```dart
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
  }
  ```

- [ ] **Step 1: Write failing widget test for `SorunBildirEkrani`**

```dart
// mobile/test/features/cases/presentation/sorun_bildir_ekrani_test.dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tarla_asistani/features/ai_assistant/data/image_picker_service.dart';
import 'package:tarla_asistani/features/cases/data/case_repository.dart';
import 'package:tarla_asistani/features/cases/domain/models/case_category.dart';
import 'package:tarla_asistani/features/cases/domain/models/create_case_input.dart';
import 'package:tarla_asistani/features/cases/presentation/sorun_bildir_ekrani.dart';
import 'package:tarla_asistani/features/fields/data/tarla_repository.dart';
import 'package:tarla_asistani/models/tarla.dart';

class MockCaseRepository implements CaseRepository {
  CreateCaseInput? lastInput;
  @override
  Future<String> createCase(CreateCaseInput input) async {
    lastInput = input;
    return 'created-case-id';
  }
}

class MockTarlaRepository implements TarlaRepository {
  MockTarlaRepository(this.tarlalar);
  final List<Tarla> tarlalar;

  @override
  Future<List<Tarla>> getTarlalar() async => tarlalar;

  @override
  Future<void> addTarla(Tarla tarla) async {}

  @override
  Future<void> deleteTarla(String id) async {}
}

void main() {
  final sampleTarla = Tarla(
    id: 'tarla-1',
    name: 'Büyük Tarla',
    area: 10.0,
    cropType: 'Domates',
    plantingDate: DateTime(2026, 4, 1),
  );

  testWidgets('renders all essential form fields', (tester) async {
    final caseRepo = MockCaseRepository();
    final tarlaRepo = MockTarlaRepository([sampleTarla]);

    await tester.pumpWidget(
      MaterialApp(
        home: SorunBildirEkrani(
          initialTarlaId: 'tarla-1',
          caseRepository: caseRepo,
          tarlaRepository: tarlaRepo,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Sorun Bildir'), findsWidgets);
    expect(find.text('Hastalık'), findsOneWidget);
    expect(find.text('Zararlı'), findsOneWidget);
    expect(find.text('Uzmana Gönder'), findsOneWidget);
    expect(find.byType(TextField), findsNWidgets(2)); // Title + Description
  });

  testWidgets('submits form successfully and calls repository', (tester) async {
    final caseRepo = MockCaseRepository();
    final tarlaRepo = MockTarlaRepository([sampleTarla]);

    await tester.pumpWidget(
      MaterialApp(
        home: SorunBildirEkrani(
          initialTarlaId: 'tarla-1',
          caseRepository: caseRepo,
          tarlaRepository: tarlaRepo,
        ),
      ),
    );
    await tester.pumpAndSettle();

    // Select category
    await tester.tap(find.text('Hastalık'));
    await tester.pump();

    // Enter title & description
    await tester.enterText(find.byType(TextField).at(0), 'Unlu Bit');
    await tester.enterText(find.byType(TextField).at(1), 'Gövdede beyaz unlanma var.');
    await tester.pump();

    // Tap submit
    await tester.tap(find.text('Uzmana Gönder'));
    await tester.pumpAndSettle();

    expect(caseRepo.lastInput, isNotNull);
    expect(caseRepo.lastInput!.title, 'Unlu Bit');
    expect(caseRepo.lastInput!.category, CaseCategory.disease);
    expect(caseRepo.lastInput!.farmId, 'tarla-1');
  });
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd mobile && flutter test test/features/cases/presentation/sorun_bildir_ekrani_test.dart`
Expected: FAIL (screen not found).

- [ ] **Step 3: Implement `sorun_bildir_ekrani.dart`**

```dart
// mobile/lib/features/cases/presentation/sorun_bildir_ekrani.dart
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../../../app/theme/app_colors.dart';
import '../../../app/theme/app_spacing.dart';
import '../../../features/ai_assistant/data/image_picker_service.dart';
import '../../../features/fields/data/tarla_repository.dart';
import '../../../models/tarla.dart';
import '../data/case_repository.dart';
import '../domain/models/case_category.dart';
import '../domain/models/create_case_input.dart';

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
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Lütfen bir tarla seçin.')),
      );
      return;
    }

    final title = _titleController.text.trim();
    final description = _descriptionController.text.trim();

    if (title.length < 2) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Lütfen en az 2 karakterli bir başlık girin.')),
      );
      return;
    }

    if (description.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Lütfen sorununuzu açıklayın.')),
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
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          backgroundColor: AppColors.primary,
          content: Text('Sorun bildiriminiz ziraat mühendisine iletildi.'),
        ),
      );
      Navigator.of(context).pop(true);
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          backgroundColor: AppColors.error,
          content: Text('Gönderilemedi: $e'),
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
              padding: const EdgeInsets.all(AppSpacing.screenPadding),
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
                      ),
                    const SizedBox(height: AppSpacing.elementGap),

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
                                  backgroundColor: AppColors.primaryLight,
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
                    const SizedBox(height: AppSpacing.elementGap),

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
                          selectedColor: AppColors.primaryLight,
                          onSelected: (val) {
                            if (val) setState(() => _selectedCategory = cat);
                          },
                        );
                      }).toList(),
                    ),
                    const SizedBox(height: AppSpacing.elementGap),

                    // Başlık
                    TextField(
                      controller: _titleController,
                      maxLength: 160,
                      decoration: const InputDecoration(
                        labelText: 'Sorun Başlığı',
                        hintText: 'Örn: Yapraklarda beyaz lekeler',
                      ),
                    ),
                    const SizedBox(height: AppSpacing.elementGap),

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
                    const SizedBox(height: AppSpacing.cardPadding),

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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd mobile && flutter test test/features/cases/presentation/sorun_bildir_ekrani_test.dart`
Expected: PASS (All tests passed).

- [ ] **Step 5: Commit**

```bash
git add mobile/lib/features/cases/presentation/ mobile/test/features/cases/presentation/
git commit -m "feat(cases): implement SorunBildirEkrani presentation screen"
```

---

### Task 4: Integrate Entry Points in Field Detail and Home Screen

**Files:**
- Modify: `mobile/lib/screens/tarla_detay_ekrani.dart`
- Modify: `mobile/lib/screens/ana_sayfa_ekrani.dart`
- Modify: `mobile/lib/screens/ana_ekran.dart`
- Test: `mobile/test/screens/tarla_detay_case_entry_test.dart`

**Interfaces:**
- Consumes: `SorunBildirEkrani`, `CaseRepository`, `BackendCaseRepository`
- Produces: Visual entry button on `TarlaDetayEkrani` and `AnaSayfaEkrani` to open `SorunBildirEkrani`.

- [ ] **Step 1: Write failing test for `TarlaDetayEkrani` entry button**

```dart
// mobile/test/screens/tarla_detay_case_entry_test.dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tarla_asistani/features/activities/data/local_faaliyet_repository.dart';
import 'package:tarla_asistani/features/cases/data/case_repository.dart';
import 'package:tarla_asistani/features/cases/domain/models/create_case_input.dart';
import 'package:tarla_asistani/models/tarla.dart';
import 'package:tarla_asistani/screens/tarla_detay_ekrani.dart';

class DummyCaseRepo implements CaseRepository {
  @override
  Future<String> createCase(CreateCaseInput input) async => 'case-id';
}

void main() {
  testWidgets('TarlaDetayEkrani shows Sorun Bildir action button', (tester) async {
    final tarla = Tarla(
      id: 'tarla-42',
      name: 'Zeytinlik',
      area: 5.0,
      cropType: 'Zeytin',
      plantingDate: DateTime(2025, 1, 1),
    );

    await tester.pumpWidget(
      MaterialApp(
        home: TarlaDetayEkrani(
          tarla: tarla,
          faaliyetRepository: const LocalFaaliyetRepository(),
          caseRepository: DummyCaseRepo(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Sorun Bildir'), findsOneWidget);
  });
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd mobile && flutter test test/screens/tarla_detay_case_entry_test.dart`
Expected: FAIL (named parameter `caseRepository` not defined or button not found).

- [ ] **Step 3: Update `TarlaDetayEkrani` to accept `CaseRepository` and render "Sorun Bildir" button**

In `mobile/lib/screens/tarla_detay_ekrani.dart`:
Add optional `caseRepository` parameter and a prominent button in the header card that navigates to `SorunBildirEkrani`:

```dart
// Add import:
// import '../features/cases/data/case_repository.dart';
// import '../features/cases/presentation/sorun_bildir_ekrani.dart';
```

In `TarlaDetayEkrani` constructor:
```dart
final CaseRepository? caseRepository;
```

In header card / actions area of `TarlaDetayEkrani`:
```dart
OutlinedButton.icon(
  icon: const Icon(Icons.report_problem_outlined, color: AppColors.accent),
  label: const Text('Sorun Bildir / Uzmana Danış'),
  onPressed: () {
    if (widget.caseRepository != null) {
      Navigator.push(
        context,
        MaterialPageRoute(
          builder: (_) => SorunBildirEkrani(
            initialTarlaId: widget.tarla.id,
            caseRepository: widget.caseRepository!,
            tarlaRepository: widget.tarlaRepository ?? LocalTarlaRepository(),
          ),
        ),
      );
    }
  },
),
```

- [ ] **Step 4: Update `AnaEkran` and `AnaSayfaEkrani` to pass repository and wire home action**

Pass `CaseRepository` from `AnaEkran` (or instantiate `BackendCaseRepository(apiClient: ...)` if `ApiClient` is available).

- [ ] **Step 5: Run all test suites to verify passing**

Run:
```bash
cd mobile && flutter test test/features/cases/ test/screens/tarla_detay_case_entry_test.dart
```
Expected: PASS (All tests pass).

- [ ] **Step 6: Commit**

```bash
git add mobile/lib/ mobile/test/
git commit -m "feat(cases): wire SorunBildirEkrani to TarlaDetayEkrani and AnaEkran"
```
