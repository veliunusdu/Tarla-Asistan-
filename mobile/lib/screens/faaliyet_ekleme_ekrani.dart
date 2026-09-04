import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:speech_to_text/speech_to_text.dart';
import 'package:uuid/uuid.dart';

import '../app/theme/app_colors.dart';
import '../app/theme/app_spacing.dart';
import '../features/activities/data/faaliyet_repository.dart';
import '../features/activities/data/local_faaliyet_repository.dart';
import '../models/faaliyet.dart';
import '../shared/utils/date_formatter.dart';

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
  final _isTuruController = TextEditingController();
  final _noteController = TextEditingController();
  final _speechToText = SpeechToText();

  late IsDurumu _durum;
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
    _isTuruController.dispose();
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
      final isTuru = _isTuruController.text.trim();

      if (_durum == IsDurumu.planla) {
        final gorev = Faaliyet(
          id: const Uuid().v4(),
          tarlaId: widget.tarlaId,
          type: isTuru,
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
        final faaliyet = Faaliyet(
          id: const Uuid().v4(),
          tarlaId: widget.tarlaId,
          type: isTuru,
          note: _noteController.text.trim(),
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

              // ── İş türü (serbest metin girişi) ────────────────────────────
              TextFormField(
                controller: _isTuruController,
                enabled: !_kaydediliyor,
                maxLength: 150,
                maxLengthEnforcement: MaxLengthEnforcement.none,
                textCapitalization: TextCapitalization.sentences,
                decoration: const InputDecoration(
                  labelText: 'İş türü',
                  hintText: 'Yaptığınız işi yazın (örn: Damla sulama, Çapa)',
                  prefixIcon: Icon(Icons.agriculture),
                ),
                validator: (v) {
                  final trimmed = v?.trim() ?? '';
                  if (trimmed.isEmpty) {
                    return 'Lütfen yapılan veya planlanan işi yazın.';
                  }
                  if (trimmed.length > 150) {
                    return 'İş türü en fazla 150 karakter olabilir.';
                  }
                  return null;
                },
              ),

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
