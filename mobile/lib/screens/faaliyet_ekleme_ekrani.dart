import 'package:flutter/material.dart';
import 'package:speech_to_text/speech_to_text.dart';
import 'package:uuid/uuid.dart';

import '../models/faaliyet.dart';
import '../services/database_helper.dart';
import '../services/sync_service.dart';

class FaaliyetEklemeEkrani extends StatefulWidget {
  const FaaliyetEklemeEkrani({
    super.key,
    required this.tarlaId,
    required this.syncService,
  });

  final String tarlaId;
  final SyncService syncService;

  @override
  State<FaaliyetEklemeEkrani> createState() => _FaaliyetEklemeEkraniState();
}

class _FaaliyetEklemeEkraniState extends State<FaaliyetEklemeEkrani> {
  final _formKey = GlobalKey<FormState>();
  final _faaliyetController = TextEditingController();
  final _speechToText = SpeechToText();
  bool _isListening = false;
  bool _saving = false;

  @override
  void dispose() {
    _speechToText.stop();
    _faaliyetController.dispose();
    super.dispose();
  }

  Future<void> _listen() async {
    if (_isListening) {
      await _speechToText.stop();
      if (mounted) setState(() => _isListening = false);
      return;
    }
    final available = await _speechToText.initialize(
      onError: (_) {
        if (mounted) setState(() => _isListening = false);
      },
    );
    if (!available) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Sesli giriş bu cihazda kullanılamıyor.'),
          ),
        );
      }
      return;
    }
    setState(() => _isListening = true);
    await _speechToText.listen(
      listenOptions: SpeechListenOptions(localeId: 'tr_TR'),
      onResult: (result) {
        _faaliyetController.text = result.recognizedWords;
        if (mounted && result.finalResult) setState(() => _isListening = false);
      },
    );
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _saving = true);
    final activity = Faaliyet(
      id: const Uuid().v4(),
      tarlaId: widget.tarlaId,
      type: _faaliyetController.text.trim(),
      note: '',
      timestamp: DateTime.now(),
      isCompleted: true,
    );
    await DatabaseHelper.instance.insertFaaliyetWithSync(activity);
    await widget.syncService.syncNow();
    if (mounted) Navigator.pop(context, true);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Faaliyet ekle')),
      body: SafeArea(
        child: Form(
          key: _formKey,
          child: ListView(
            padding: const EdgeInsets.all(20),
            children: [
              const Text(
                'Bağlantınız olmasa da kayıt cihazda korunur ve internet geldiğinde otomatik gönderilir.',
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _faaliyetController,
                enabled: !_saving,
                minLines: 3,
                maxLines: 6,
                maxLength: 200,
                textCapitalization: TextCapitalization.sentences,
                decoration: InputDecoration(
                  labelText: 'Faaliyet detayı',
                  hintText: 'Örnek: Kuzey parselde damlama sulama yapıldı.',
                  suffixIcon: IconButton(
                    tooltip: _isListening ? 'Dinlemeyi durdur' : 'Sesle yaz',
                    icon: Icon(_isListening ? Icons.mic : Icons.mic_none),
                    color: _isListening
                        ? Theme.of(context).colorScheme.error
                        : Theme.of(context).colorScheme.primary,
                    onPressed: _saving ? null : _listen,
                  ),
                ),
                validator: (value) => (value ?? '').trim().length >= 2
                    ? null
                    : 'Faaliyet detayını yazın veya sesle ekleyin.',
              ),
              const SizedBox(height: 20),
              FilledButton.icon(
                onPressed: _saving ? null : _save,
                icon: _saving
                    ? const SizedBox.square(
                        dimension: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.save_outlined),
                label: const Text('Kaydet'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
