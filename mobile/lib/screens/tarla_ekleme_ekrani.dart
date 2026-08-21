import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../app/theme/app_colors.dart';
import '../app/theme/app_spacing.dart';
import '../features/fields/data/local_tarla_repository.dart';
import '../features/fields/data/tarla_repository.dart';
import '../models/tarla.dart';

const List<String> _trAylar = [
  'Ocak',
  'Şubat',
  'Mart',
  'Nisan',
  'Mayıs',
  'Haziran',
  'Temmuz',
  'Ağustos',
  'Eylül',
  'Ekim',
  'Kasım',
  'Aralık',
];

String _formatTarih(DateTime dt) =>
    '${dt.day} ${_trAylar[dt.month - 1]} ${dt.year}';

/// PRD §7.4: Desteklenen ürünler listesi.
const List<String> _desteklenenUrunler = [
  'Buğday',
  'Arpa',
  'Mısır',
  'Ayçiçeği',
  'Domates',
];

class TarlaEklemeEkrani extends StatefulWidget {
  const TarlaEklemeEkrani({super.key, TarlaRepository? repository})
    : _repository = repository ?? const LocalTarlaRepository();

  final TarlaRepository _repository;

  @override
  State<TarlaEklemeEkrani> createState() => _TarlaEklemeEkraniState();
}

class _TarlaEklemeEkraniState extends State<TarlaEklemeEkrani> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _sizeController = TextEditingController();

  String? _secilenUrun;
  DateTime? _secilenTarih;
  bool _kaydediliyor = false;

  @override
  void dispose() {
    _nameController.dispose();
    _sizeController.dispose();
    super.dispose();
  }

  // ---------------------------------------------------------------------------
  // Doğrulama yardımcıları
  // ---------------------------------------------------------------------------

  String? _validateAd(String? value) {
    final trimmed = value?.trim() ?? '';
    if (trimmed.isEmpty) {
      return 'Tarla adı boş bırakılamaz.';
    }
    if (trimmed.length > 120) {
      return 'Tarla adı en fazla 120 karakter olabilir.';
    }
    return null;
  }

  String? _validateBoyut(String? value) {
    final normalized = (value ?? '').trim().replaceAll(',', '.');
    if (normalized.isEmpty) {
      return 'Büyüklük boş bırakılamaz.';
    }
    final parsed = double.tryParse(normalized);
    if (parsed == null) {
      return 'Geçerli bir sayı girin.';
    }
    if (parsed <= 0) {
      return "Büyüklük 0'dan büyük olmalıdır.";
    }
    return null;
  }

  // ---------------------------------------------------------------------------
  // Tarih seçici
  // ---------------------------------------------------------------------------

  Future<void> _tarihSec() async {
    final bugun = DateTime.now();
    final secilen = await showDatePicker(
      context: context,
      initialDate: _secilenTarih ?? bugun,
      firstDate: DateTime(2000),
      lastDate: bugun,
      helpText: 'Ekim tarihini seçin',
      cancelText: 'İptal',
      confirmText: 'Tamam',
    );
    if (secilen != null) {
      setState(() => _secilenTarih = secilen);
    }
  }

  // ---------------------------------------------------------------------------
  // Kaydetme
  // ---------------------------------------------------------------------------

  Future<void> _kaydet() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    if (_secilenUrun == null) {
      _snackBar('Lütfen bir ürün seçin.');
      return;
    }
    if (_secilenTarih == null) {
      _snackBar('Lütfen ekim tarihini seçin.');
      return;
    }

    setState(() => _kaydediliyor = true);

    final boyut = double.parse(
      _sizeController.text.trim().replaceAll(',', '.'),
    );

    final yeniTarla = Tarla(
      id: DateTime.now().millisecondsSinceEpoch.toString(),
      name: _nameController.text.trim(),
      // TODO(location): Koordinatlar şu an 0.0 — harita/GPS desteği
      // bir sonraki adımda eklenecek.
      latitude: 0.0,
      longitude: 0.0,
      size: boyut,
      cropType: _secilenUrun!,
      plantingDate: _secilenTarih!,
    );

    try {
      await widget._repository.addTarla(yeniTarla);
      if (!mounted) return;
      Navigator.pop(context, true);
    } catch (_) {
      if (!mounted) return;
      setState(() => _kaydediliyor = false);
      _snackBar('Tarla kaydedilemedi. Lütfen tekrar deneyin.');
    }
  }

  void _snackBar(String mesaj) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(mesaj)));
  }

  // ---------------------------------------------------------------------------
  // UI
  // ---------------------------------------------------------------------------

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Yeni Tarla Ekle')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Tarla adı
                TextFormField(
                  controller: _nameController,
                  textCapitalization: TextCapitalization.sentences,
                  decoration: const InputDecoration(
                    labelText: 'Tarla Adı',
                    prefixIcon: Icon(Icons.agriculture),
                  ),
                  validator: _validateAd,
                ),
                const SizedBox(height: AppSpacing.md),

                // Büyüklük
                TextFormField(
                  controller: _sizeController,
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  inputFormatters: [
                    FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]')),
                  ],
                  decoration: const InputDecoration(
                    labelText: 'Büyüklük (Dönüm)',
                    prefixIcon: Icon(Icons.straighten),
                  ),
                  validator: _validateBoyut,
                ),
                const SizedBox(height: AppSpacing.md),

                // Ürün seçimi (PRD §7.4 desteklenen ürünler)
                DropdownButtonFormField<String>(
                  initialValue: _secilenUrun,
                  decoration: const InputDecoration(
                    labelText: 'Ürün',
                    prefixIcon: Icon(Icons.grass),
                  ),
                  items: _desteklenenUrunler
                      .map((u) => DropdownMenuItem(value: u, child: Text(u)))
                      .toList(),
                  onChanged: _kaydediliyor
                      ? null
                      : (v) => setState(() => _secilenUrun = v),
                  validator: (v) => v == null ? 'Lütfen bir ürün seçin.' : null,
                ),
                const SizedBox(height: AppSpacing.md),

                // Ekim tarihi (PRD §7.4: gelecek tarih kabul edilmez)
                InkWell(
                  onTap: _kaydediliyor ? null : _tarihSec,
                  borderRadius: BorderRadius.circular(12),
                  child: InputDecorator(
                    decoration: const InputDecoration(
                      labelText: 'Ekim Tarihi',
                      prefixIcon: Icon(Icons.calendar_today),
                    ),
                    child: Text(
                      _secilenTarih != null
                          ? _formatTarih(_secilenTarih!)
                          : 'Tarih seçin',
                      style: theme.textTheme.bodyLarge?.copyWith(
                        color: _secilenTarih != null
                            ? AppColors.textPrimary
                            : AppColors.textDisabled,
                      ),
                    ),
                  ),
                ),
                const SizedBox(height: AppSpacing.sm),

                // Konum bilgisi (PRD §7.3 — sonraki adım)
                Container(
                  padding: const EdgeInsets.all(AppSpacing.sm),
                  decoration: BoxDecoration(
                    color: AppColors.primary.withValues(alpha: 0.07),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Row(
                    children: [
                      Icon(
                        Icons.info_outline,
                        size: 18,
                        color: AppColors.primary,
                      ),
                      const SizedBox(width: AppSpacing.sm),
                      Expanded(
                        child: Text(
                          'Konum seçimi sonraki adımda eklenecek.',
                          style: theme.textTheme.bodyMedium?.copyWith(
                            color: AppColors.primary,
                            fontSize: 14,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: AppSpacing.xl),

                // Kaydet butonu
                ElevatedButton(
                  onPressed: _kaydediliyor ? null : _kaydet,
                  child: _kaydediliyor
                      ? const SizedBox(
                          height: 20,
                          width: 20,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Colors.white,
                          ),
                        )
                      : const Text('Kaydet'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
