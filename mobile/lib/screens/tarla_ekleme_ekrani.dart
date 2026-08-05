import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../models/tarla.dart';
import '../services/api_client.dart';
import '../services/database_helper.dart';

class TarlaEklemeEkrani extends StatefulWidget {
  const TarlaEklemeEkrani({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  State<TarlaEklemeEkrani> createState() => _TarlaEklemeEkraniState();
}

class _TarlaEklemeEkraniState extends State<TarlaEklemeEkrani> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _sizeController = TextEditingController();
  final _latitudeController = TextEditingController();
  final _longitudeController = TextEditingController();
  String _cropType = 'WHEAT';
  bool _loading = false;
  String? _error;

  @override
  void dispose() {
    _nameController.dispose();
    _sizeController.dispose();
    _latitudeController.dispose();
    _longitudeController.dispose();
    super.dispose();
  }

  Future<void> _kaydet() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final response = await widget.apiClient.postJson('/farms', {
        'name': _nameController.text.trim(),
        'latitude': _number(_latitudeController.text),
        'longitude': _number(_longitudeController.text),
        'size_in_hectares': _number(_sizeController.text),
        'irrigation_method': 'OTHER',
        'crop_type': _cropType,
        'planted_at': DateTime.now().toIso8601String().split('T').first,
      });
      final farmJson = response['farm'];
      if (farmJson is! Map) {
        throw const ApiException('Tarla kaydedildi ancak cevap okunamadı.');
      }
      await DatabaseHelper.instance.insertTarla(
        Tarla.fromApi(Map<String, dynamic>.from(farmJson)),
      );
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  double _number(String value) => double.parse(value.replaceAll(',', '.'));

  String? _requiredNumber(String? value, double min, double max) {
    final parsed = double.tryParse((value ?? '').replaceAll(',', '.'));
    if (parsed == null || parsed < min || parsed > max) {
      return '$min ile $max arasında bir değer girin.';
    }
    return null;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Yeni tarla ekle')),
      body: SafeArea(
        child: Form(
          key: _formKey,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              const Text(
                'Tarla eklemek için bağlantı gerekir. Faaliyet kayıtları daha sonra çevrimdışı yapılabilir.',
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _nameController,
                enabled: !_loading,
                textInputAction: TextInputAction.next,
                decoration: const InputDecoration(labelText: 'Tarla adı'),
                validator: (value) => (value ?? '').trim().length >= 2
                    ? null
                    : 'En az 2 karakter girin.',
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _sizeController,
                enabled: !_loading,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                inputFormatters: [
                  FilteringTextInputFormatter.allow(RegExp(r'[0-9,.]')),
                ],
                decoration: const InputDecoration(labelText: 'Alan (hektar)'),
                validator: (value) => _requiredNumber(value, 0.01, 100000),
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                initialValue: _cropType,
                decoration: const InputDecoration(labelText: 'Ürün'),
                items:
                    const {
                          'WHEAT': 'Buğday',
                          'BARLEY': 'Arpa',
                          'CORN': 'Mısır',
                          'SUNFLOWER': 'Ayçiçeği',
                          'TOMATO': 'Domates',
                        }.entries
                        .map(
                          (entry) => DropdownMenuItem(
                            value: entry.key,
                            child: Text(entry.value),
                          ),
                        )
                        .toList(),
                onChanged: _loading
                    ? null
                    : (value) => setState(() => _cropType = value ?? _cropType),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _latitudeController,
                enabled: !_loading,
                keyboardType: const TextInputType.numberWithOptions(
                  signed: true,
                  decimal: true,
                ),
                decoration: const InputDecoration(
                  labelText: 'Enlem',
                  hintText: '38.7312',
                ),
                validator: (value) => _requiredNumber(value, -90, 90),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _longitudeController,
                enabled: !_loading,
                keyboardType: const TextInputType.numberWithOptions(
                  signed: true,
                  decimal: true,
                ),
                decoration: const InputDecoration(
                  labelText: 'Boylam',
                  hintText: '35.4787',
                ),
                validator: (value) => _requiredNumber(value, -180, 180),
              ),
              if (_error != null) ...[
                const SizedBox(height: 16),
                Semantics(
                  liveRegion: true,
                  child: Text(
                    _error!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ),
              ],
              const SizedBox(height: 24),
              FilledButton.icon(
                onPressed: _loading ? null : _kaydet,
                icon: _loading
                    ? const SizedBox.square(
                        dimension: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.save_outlined),
                label: const Text('Tarlayı kaydet'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
