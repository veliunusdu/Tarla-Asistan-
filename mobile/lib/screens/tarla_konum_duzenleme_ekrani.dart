import 'package:flutter/material.dart';

import '../app/theme/app_colors.dart';
import '../app/theme/app_spacing.dart';
import '../features/fields/data/tarla_location_repository.dart';
import '../features/location/data/geolocator_location_service.dart';
import '../features/location/data/location_service.dart';
import '../features/location/domain/tarla_location.dart';
import '../features/location/presentation/field_location_picker_screen.dart';
import '../models/tarla.dart';
import 'tarla_ekleme_ekrani.dart';

class TarlaKonumDuzenlemeEkrani extends StatefulWidget {
  const TarlaKonumDuzenlemeEkrani({
    super.key,
    required this.tarla,
    required this.repository,
    this.locationService,
    this.locationPicker,
  });

  final Tarla tarla;
  final TarlaLocationRepository repository;
  final LocationService? locationService;
  final FieldLocationPicker? locationPicker;

  @override
  State<TarlaKonumDuzenlemeEkrani> createState() =>
      _TarlaKonumDuzenlemeEkraniState();
}

class _TarlaKonumDuzenlemeEkraniState extends State<TarlaKonumDuzenlemeEkrani> {
  TarlaLocation? _selectedLocation;
  bool _konumAliniyor = false;
  bool _kaydediliyor = false;

  @override
  void initState() {
    super.initState();
    if (widget.tarla.latitude != null && widget.tarla.longitude != null) {
      _selectedLocation = TarlaLocation(
        latitude: widget.tarla.latitude!,
        longitude: widget.tarla.longitude!,
      );
    }
  }

  void _snackBar(String mesaj) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(mesaj)));
  }

  Future<void> _konumumuKullan() async {
    setState(() => _konumAliniyor = true);
    try {
      final location =
          await (widget.locationService ?? GeolocatorLocationService())
              .getCurrentLocation();
      if (mounted) setState(() => _selectedLocation = location);
    } on LocationServiceDisabledException {
      _snackBar('Konum servislerini açıp tekrar deneyin.');
    } on LocationPermissionPermanentlyDeniedException {
      _snackBar('Konum iznini ayarlardan açın veya haritadan seçin.');
    } on LocationPermissionDeniedException {
      _snackBar('Konum izni verilmedi. Haritadan konum seçebilirsiniz.');
    } on LocationUnavailableException {
      _snackBar('Konum alınamadı. Haritadan konum seçebilirsiniz.');
    } finally {
      if (mounted) setState(() => _konumAliniyor = false);
    }
  }

  Future<void> _haritadaSec() async {
    final picker =
        widget.locationPicker ??
        (BuildContext context, TarlaLocation? initial) =>
            Navigator.of(context).push<TarlaLocation>(
              MaterialPageRoute(
                builder: (_) =>
                    FieldLocationPickerScreen(initialLocation: initial),
              ),
            );
    final location = await picker(context, _selectedLocation);
    if (location != null && mounted) {
      setState(() => _selectedLocation = location);
    }
  }

  Future<void> _kaydet() async {
    if (_selectedLocation == null) return;

    setState(() => _kaydediliyor = true);
    try {
      await widget.repository.updateTarlaLocation(
        widget.tarla.id,
        _selectedLocation!,
      );
      if (mounted) {
        Navigator.pop(context, true);
      }
    } catch (_) {
      if (mounted) {
        _snackBar('Konum güncellenirken bir hata oluştu.');
      }
    } finally {
      if (mounted) setState(() => _kaydediliyor = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Tarla Konumu Ekle')),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                widget.tarla.name,
                style: theme.textTheme.headlineSmall,
              ),
              const SizedBox(height: AppSpacing.sm),
              Text(
                'Hava durumu bilgilerini alabilmek için bu tarlanın konumunu belirleyin.',
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: AppSpacing.lg),
              InputDecorator(
                decoration: const InputDecoration(
                  labelText: 'Tarla konumu',
                  prefixIcon: Icon(Icons.location_on),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      _selectedLocation == null
                          ? 'Konum seçilmedi'
                          : '${_selectedLocation!.latitude.toStringAsFixed(5)}, ${_selectedLocation!.longitude.toStringAsFixed(5)}',
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: _selectedLocation == null
                            ? AppColors.textSecondary
                            : AppColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    Wrap(
                      spacing: AppSpacing.sm,
                      runSpacing: AppSpacing.xs,
                      children: [
                        OutlinedButton(
                          onPressed: _konumAliniyor || _kaydediliyor
                              ? null
                              : _konumumuKullan,
                          child: Text(
                            _konumAliniyor
                                ? 'Konum alınıyor...'
                                : 'Konumumu kullan',
                          ),
                        ),
                        OutlinedButton(
                          onPressed: _kaydediliyor ? null : _haritadaSec,
                          child: Text(
                            _selectedLocation == null
                                ? 'Haritada seç'
                                : 'Haritada değiştir',
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const Spacer(),
              ElevatedButton(
                onPressed: _selectedLocation == null || _kaydediliyor
                    ? null
                    : _kaydet,
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
