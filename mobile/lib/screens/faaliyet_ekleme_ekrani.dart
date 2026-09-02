import 'package:flutter/material.dart';
import 'package:speech_to_text/speech_to_text.dart';
import 'package:uuid/uuid.dart';

import '../app/theme/app_colors.dart';
import '../app/theme/app_spacing.dart';
import '../features/activities/data/faaliyet_repository.dart';
import '../features/activities/data/local_faaliyet_repository.dart';
import '../models/faaliyet.dart';
import '../shared/utils/date_formatter.dart';

const List<String> _faaliyetTurleri = [
  'Sulama',
  'Gübreleme',
  'İlaçlama',
  'Hasat',
  'Ekim / Dikim',
  'Budama',
  'Toprak İşleme',
  'Diğer',
];

enum IsDurumu { planla, yapildi }

class FaaliyetEklemeEkrani extends StatefulWidget {
  const FaaliyetEklemeEkrani({
    super.key,
    required this.tarlaId,
    FaaliyetRepository? faaliyetRepository,
    this.initialSelectedDate,
    this.initialIsCompleted = true,
  }) : _repo = faaliyetRepository ?? const LocalFaaliyetRepository();

  final String tarlaId;
  final FaaliyetRepository _repo;

  @visibleForTesting
  FaaliyetRepository get repositoryForTesting => _repo;

  /// Optional initial date to pre-populate.
  final DateTime? initialSelectedDate;

  /// Pre-set mode (true = yapildi, false = planla). Defaults to true.
  final bool initialIsCompleted;

  @override
  State<FaaliyetEklemeEkrani> createState() => _FaaliyetEklemeEkraniState();
}

class _FaaliyetEklemeEkraniState extends State<FaaliyetEklemeEkrani> {
  final _formKey = GlobalKey<FormState>();
  final _noteController = TextEditingController();
  final _customTitleController = TextEditingController();
  final _speechToText = SpeechToText();

  late IsDurumu _durum;
  String? _secilenTur;
  DateTime? _secilenTarih;
  bool _isListening = false;
  bool _kaydediliyor = false;

  @override
  void initState() {
    super.initState();
    _durum = widget.initialIsCompleted ? IsDurumu.yapildi : IsDurumu.planla;
    _secilenTarih = widget.initialSelectedDate;
    _initSpeech();
  }

  @override
  void dispose() {
    _noteController.dispose();
    _customTitleController.dispose();
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

  void _gecerliTarihiAyarla() {
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    if (_secilenTarih == null) return;
    if (_durum == IsDurumu.planla && _secilenTarih!.isBefore(today)) {
      _secilenTarih = today;
    } else if (_durum == IsDurumu.yapildi && _secilenTarih!.isAfter(today)) {
      _secilenTarih = today;
    }
  }

  Future<void> _tarihSec() async {
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);

    final DateTime firstDate;
    final DateTime lastDate;
    final DateTime initialDate;

    if (_durum == IsDurumu.planla) {
      firstDate = today;
      lastDate = DateTime(2100);
      initialDate = (_secilenTarih != null && !_secilenTarih!.isBefore(today))
          ? _secilenTarih!
          : today;
    } else {
      firstDate = DateTime(2000);
      lastDate = today;
      initialDate = (_secilenTarih != null && !_secilenTarih!.isAfter(today))
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

    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);

    if (_secilenTarih == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            _durum == IsDurumu.planla
                ? 'Lütfen planlanan tarihi seçin.'
                : 'Lütfen gerçekleşme tarihini seçin.',
          ),
        ),
      );
      return;
    }

    if (_durum == IsDurumu.planla && _secilenTarih!.isBefore(today)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Planlanan tarih geçmiş bir tarih olamaz.'),
        ),
      );
      return;
    }

    if (_durum == IsDurumu.yapildi && _secilenTarih!.isAfter(today)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Gerçekleşme tarihi gelecek bir tarih olamaz.'),
        ),
      );
      return;
    }

    setState(() => _kaydediliyor = true);

    try {
      if (_durum == IsDurumu.planla) {
        final title = _secilenTur == 'Diğer'
            ? _customTitleController.text.trim()
            : _secilenTur!;
        final gorev = Faaliyet(
          id: const Uuid().v4(),
          tarlaId: widget.tarlaId,
          type: title,
          note: _noteController.text.trim(),
          timestamp: DateTime.now(),
          dueDate: _secilenTarih!,
          isCompleted: false,
        );

        final repository = widget._repo;
        if (repository is! PlanliGorevRepository) {
          throw StateError('İş planlama kaydı desteklenmiyor.');
        }
        await (repository as PlanliGorevRepository).addPlanliGorev(gorev);
      } else {
        final noteText = _secilenTur == 'Diğer'
            ? (_noteController.text.trim().isEmpty
                ? _customTitleController.text.trim()
                : '${_customTitleController.text.trim()} - ${_noteController.text.trim()}')
            : _noteController.text.trim();

        final faaliyet = Faaliyet(
          id: const Uuid().v4(),
          tarlaId: widget.tarlaId,
          type: _secilenTur!,
          note: noteText,
          timestamp: _secilenTarih!,
          dueDate: null,
          isCompleted: true,
        );

        await widget._repo.addFaaliyet(faaliyet);
      }

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              _durum == IsDurumu.planla ? 'İş planlandı.' : 'İş kaydedildi.',
            ),
          ),
        );
        Navigator.pop(context, true);
      }
    } catch (_) {
      if (mounted) {
        setState(() => _kaydediliyor = false);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              _durum == IsDurumu.planla
                  ? 'İş planlanamadı. Lütfen tekrar deneyin.'
                  : 'İş kaydedilemedi. Lütfen tekrar deneyin.',
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
      appBar: AppBar(title: const Text('İş Ekle')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // ── İş durumu seçimi ──────────────────────────────────────────
              Text('Bu işi ne yapıyorsun?', style: theme.textTheme.labelLarge),
              const SizedBox(height: AppSpacing.xs),
              SegmentedButton<IsDurumu>(
                segments: const [
                  ButtonSegment(
                    value: IsDurumu.planla,
                    label: Text('Planla'),
                    icon: Icon(Icons.event_note),
                  ),
                  ButtonSegment(
                    value: IsDurumu.yapildi,
                    label: Text('Yapıldı'),
                    icon: Icon(Icons.check_circle_outline),
                  ),
                ],
                selected: {_durum},
                onSelectionChanged: _kaydediliyor
                    ? null
                    : (Set<IsDurumu> s) {
                        final yeniDurum = s.first;
                        if (yeniDurum != _durum) {
                          setState(() {
                            _durum = yeniDurum;
                            _gecerliTarihiAyarla();
                          });
                        }
                      },
              ),

              const SizedBox(height: AppSpacing.md),

              // ── İş türü ───────────────────────────────────────────────────
              DropdownButtonFormField<String>(
                initialValue: _secilenTur,
                decoration: const InputDecoration(
                  labelText: 'İş türü',
                  prefixIcon: Icon(Icons.agriculture),
                ),
                items: _faaliyetTurleri
                    .map((t) => DropdownMenuItem(value: t, child: Text(t)))
                    .toList(),
                onChanged: _kaydediliyor
                    ? null
                    : (v) => setState(() => _secilenTur = v),
                validator: (v) =>
                    v == null ? 'Lütfen bir iş türü seçin.' : null,
              ),

              if (_secilenTur == 'Diğer') ...[
                const SizedBox(height: AppSpacing.md),
                TextFormField(
                  controller: _customTitleController,
                  enabled: !_kaydediliyor,
                  decoration: const InputDecoration(
                    labelText: 'İş adı',
                    hintText: 'Örn: Budama temizliği',
                    prefixIcon: Icon(Icons.edit_note),
                  ),
                  validator: (v) {
                    if (_secilenTur == 'Diğer' &&
                        (v == null || v.trim().isEmpty)) {
                      return 'Lütfen iş adını girin.';
                    }
                    return null;
                  },
                ),
              ],

              const SizedBox(height: AppSpacing.md),

              // ── Tarih seçimi ──────────────────────────────────────────────
              InkWell(
                onTap: _kaydediliyor ? null : _tarihSec,
                borderRadius: BorderRadius.circular(12),
                child: InputDecorator(
                  decoration: InputDecoration(
                    labelText: _durum == IsDurumu.planla
                        ? 'Planlanan tarih'
                        : 'Yapıldığı tarih',
                    prefixIcon: const Icon(Icons.calendar_today),
                  ),
                  child: Text(
                    _secilenTarih != null
                        ? formatTarih(_secilenTarih!)
                        : 'Tarih seçin',
                    style: theme.textTheme.bodyLarge?.copyWith(
                      color: _secilenTarih != null
                          ? AppColors.textPrimary
                          : AppColors.textDisabled,
                    ),
                  ),
                ),
              ),

              const SizedBox(height: AppSpacing.md),

              // ── Not (sesli giriş destekli, opsiyonel) ──────────────────────
              TextFormField(
                controller: _noteController,
                enabled: !_kaydediliyor,
                maxLength: 200,
                maxLines: 3,
                decoration: InputDecoration(
                  labelText: 'Not',
                  prefixIcon: const Icon(Icons.notes),
                  suffixIcon: IconButton(
                    icon: Icon(_isListening ? Icons.mic : Icons.mic_none),
                    color: _isListening ? AppColors.error : AppColors.primary,
                    onPressed: _kaydediliyor ? null : _listen,
                  ),
                ),
              ),

              const SizedBox(height: AppSpacing.xl),

              // ── Kaydet / Planla Butonu ────────────────────────────────────
              ElevatedButton(
                onPressed: _kaydediliyor ? null : _kaydet,
                child: _kaydediliyor
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : Text(
                        _durum == IsDurumu.planla
                            ? 'İşi Planla'
                            : 'İşi Kaydet',
                      ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
