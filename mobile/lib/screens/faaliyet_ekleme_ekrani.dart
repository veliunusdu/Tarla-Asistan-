import 'package:flutter/material.dart';
import 'package:speech_to_text/speech_to_text.dart';
import 'package:uuid/uuid.dart';

import '../app/theme/app_colors.dart';
import '../app/theme/app_spacing.dart';
import '../features/activities/data/faaliyet_repository.dart';
import '../features/activities/data/local_faaliyet_repository.dart';
import '../models/faaliyet.dart';

const List<String> _faaliyetTurleri = [
  'Sulama',
  'Gübreleme',
  'İlaçlama',
  'Hasat',
  'Ekim',
  'Budama',
  'Diğer',
];

const List<String> _trAylar = [
  'Oca',
  'Şub',
  'Mar',
  'Nis',
  'May',
  'Haz',
  'Tem',
  'Ağu',
  'Eyl',
  'Eki',
  'Kas',
  'Ara',
];

String _tarihStr(DateTime dt) =>
    '${dt.day} ${_trAylar[dt.month - 1]} ${dt.year}';

class FaaliyetEklemeEkrani extends StatefulWidget {
  const FaaliyetEklemeEkrani({
    super.key,
    required this.tarlaId,
    FaaliyetRepository? faaliyetRepository,
    @visibleForTesting this.initialIsCompleted = true,
    @visibleForTesting this.initialSelectedDate,
  }) : _repo = faaliyetRepository ?? const LocalFaaliyetRepository();

  final String tarlaId;
  final FaaliyetRepository _repo;

  @visibleForTesting
  FaaliyetRepository get repositoryForTesting => _repo;

  /// Only used in tests to pre-set state and avoid date picker interaction.
  @visibleForTesting
  final bool initialIsCompleted;

  /// Only used in tests to pre-set a date.
  @visibleForTesting
  final DateTime? initialSelectedDate;

  @override
  State<FaaliyetEklemeEkrani> createState() => _FaaliyetEklemeEkraniState();
}

class _FaaliyetEklemeEkraniState extends State<FaaliyetEklemeEkrani> {
  final _formKey = GlobalKey<FormState>();
  final _noteController = TextEditingController();
  final _speechToText = SpeechToText();

  String? _secilenTur;
  DateTime? _secilenTarih;
  bool _isCompleted = true;
  bool _isListening = false;
  bool _kaydediliyor = false;

  @override
  void initState() {
    super.initState();
    _isCompleted = widget.initialIsCompleted;
    _secilenTarih = widget.initialSelectedDate;
    _initSpeech();
  }

  @override
  void dispose() {
    _noteController.dispose();
    _speechToText.stop();
    super.dispose();
  }

  Future<void> _initSpeech() async {
    await _speechToText.initialize();
  }

  void _listen() async {
    if (!_isListening) {
      final available = await _speechToText.initialize();
      if (available) {
        setState(() => _isListening = true);
        _speechToText.listen(
          onResult: (result) {
            setState(() => _noteController.text = result.recognizedWords);
          },
        );
      }
    } else {
      setState(() => _isListening = false);
      _speechToText.stop();
    }
  }

  Future<void> _tarihSec() async {
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);

    final DateTime firstDate;
    final DateTime lastDate;
    final DateTime initialDate;

    if (_isCompleted) {
      // Tamamlandı: yalnızca geçmiş ya da bugün seçilebilir
      firstDate = DateTime(2000);
      lastDate = today;
      initialDate = _secilenTarih ?? today;
    } else {
      // Planlandı: yalnızca bugün ya da gelecek seçilebilir
      firstDate = today;
      lastDate = DateTime(now.year + 5);
      initialDate = (_secilenTarih != null && !_secilenTarih!.isBefore(today))
          ? _secilenTarih!
          : today;
    }

    final secilen = await showDatePicker(
      context: context,
      initialDate: initialDate,
      firstDate: firstDate,
      lastDate: lastDate,
      locale: const Locale('tr'),
    );

    if (secilen != null) {
      setState(() => _secilenTarih = secilen);
    }
  }

  Future<void> _kaydet() async {
    if (_kaydediliyor) return;
    if (!_formKey.currentState!.validate()) return;

    if (_secilenTarih == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            _isCompleted
                ? 'Lütfen gerçekleşme tarihini seçin.'
                : 'Lütfen planlanan tarihi seçin.',
          ),
        ),
      );
      return;
    }

    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    if (_isCompleted && _secilenTarih!.isAfter(today)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Gerçekleşme tarihi gelecek bir tarih olamaz.'),
        ),
      );
      return;
    }
    if (!_isCompleted && _secilenTarih!.isBefore(today)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Planlanan tarih geçmiş bir tarih olamaz.'),
        ),
      );
      return;
    }

    setState(() => _kaydediliyor = true);

    final faaliyet = Faaliyet(
      id: const Uuid().v4(),
      tarlaId: widget.tarlaId,
      type: _secilenTur!,
      note: _noteController.text.trim(),
      timestamp: _isCompleted ? _secilenTarih! : DateTime.now(),
      dueDate: _isCompleted ? null : _secilenTarih!,
      isCompleted: _isCompleted,
    );

    try {
      if (_isCompleted) {
        await widget._repo.addFaaliyet(faaliyet);
      } else {
        final repository = widget._repo;
        if (repository is! PlanliGorevRepository) {
          throw StateError('Planlı görev kaydı desteklenmiyor.');
        }
        await (repository as PlanliGorevRepository).addPlanliGorev(faaliyet);
      }
      if (mounted) Navigator.pop(context, true);
    } catch (_) {
      if (mounted) {
        setState(() => _kaydediliyor = false);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              _isCompleted
                  ? 'Faaliyet kaydedilemedi. Lütfen tekrar deneyin.'
                  : 'Planlı görev kaydedilemedi. Lütfen tekrar deneyin.',
            ),
          ),
        );
      }
    }
  }

  // ---------------------------------------------------------------------------
  // Build
  // ---------------------------------------------------------------------------

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Faaliyet Ekle')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // ── Faaliyet türü ──────────────────────────────────────────────
              DropdownButtonFormField<String>(
                initialValue: _secilenTur,
                decoration: const InputDecoration(
                  labelText: 'Faaliyet Türü',
                  prefixIcon: Icon(Icons.agriculture),
                ),
                items: _faaliyetTurleri
                    .map((t) => DropdownMenuItem(value: t, child: Text(t)))
                    .toList(),
                onChanged: _kaydediliyor
                    ? null
                    : (v) => setState(() => _secilenTur = v),
                validator: (v) =>
                    v == null ? 'Lütfen bir faaliyet türü seçin.' : null,
              ),

              const SizedBox(height: AppSpacing.md),

              // ── Faaliyet Açıklaması (sesli giriş destekli) ─────────────────
              TextFormField(
                controller: _noteController,
                enabled: !_kaydediliyor,
                maxLength: 200,
                maxLines: 3,
                decoration: InputDecoration(
                  labelText: 'Faaliyet Açıklaması',
                  prefixIcon: const Icon(Icons.notes),
                  suffixIcon: IconButton(
                    icon: Icon(_isListening ? Icons.mic : Icons.mic_none),
                    color: _isListening ? AppColors.error : AppColors.primary,
                    onPressed: _kaydediliyor ? null : _listen,
                  ),
                ),
                validator: (v) {
                  final text = v?.trim() ?? '';
                  if (text.length < 2) {
                    return 'Faaliyet açıklaması en az 2 karakter olmalıdır.';
                  }
                  return null;
                },
              ),

              const SizedBox(height: AppSpacing.md),

              // ── Durum seçimi ──────────────────────────────────────────────
              Text('Durum', style: theme.textTheme.labelLarge),
              const SizedBox(height: AppSpacing.xs),
              SegmentedButton<bool>(
                segments: const [
                  ButtonSegment(
                    value: false,
                    label: Text('Planlandı'),
                    icon: Icon(Icons.event_note),
                  ),
                  ButtonSegment(
                    value: true,
                    label: Text('Tamamlandı'),
                    icon: Icon(Icons.check_circle_outline),
                  ),
                ],
                selected: {_isCompleted},
                onSelectionChanged: _kaydediliyor
                    ? null
                    : (Set<bool> s) => setState(() {
                        _isCompleted = s.first;
                        _secilenTarih = null;
                      }),
              ),

              const SizedBox(height: AppSpacing.md),

              // ── Tarih seçimi ──────────────────────────────────────────────
              InkWell(
                onTap: _kaydediliyor ? null : _tarihSec,
                borderRadius: BorderRadius.circular(12),
                child: InputDecorator(
                  decoration: InputDecoration(
                    labelText: _isCompleted
                        ? 'Gerçekleşme Tarihi'
                        : 'Planlanan Tarih',
                    prefixIcon: const Icon(Icons.calendar_today),
                  ),
                  child: Text(
                    _secilenTarih != null
                        ? _tarihStr(_secilenTarih!)
                        : 'Tarih seçin',
                    style: theme.textTheme.bodyLarge?.copyWith(
                      color: _secilenTarih != null
                          ? AppColors.textPrimary
                          : AppColors.textDisabled,
                    ),
                  ),
                ),
              ),

              const SizedBox(height: AppSpacing.xl),

              // ── Kaydet ────────────────────────────────────────────────────
              ElevatedButton(
                onPressed: _kaydediliyor ? null : _kaydet,
                child: _kaydediliyor
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Text('Kaydet'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
