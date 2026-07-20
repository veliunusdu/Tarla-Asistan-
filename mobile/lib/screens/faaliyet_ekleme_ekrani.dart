import 'package:flutter/material.dart';
import 'package:speech_to_text/speech_to_text.dart';
// Modellerin ve Servislerin doğru import edildiğinden emin ol (yolunu kontrol et)
import '../models/faaliyet.dart'; 
import '../services/database_helper.dart';

class FaaliyetEklemeEkrani extends StatefulWidget {
  final String tarlaId;

  const FaaliyetEklemeEkrani({super.key, required this.tarlaId});

  @override
  State<FaaliyetEklemeEkrani> createState() => _FaaliyetEklemeEkraniState();
}

class _FaaliyetEklemeEkraniState extends State<FaaliyetEklemeEkrani> {
  final TextEditingController _faaliyetController = TextEditingController();
  final SpeechToText _speechToText = SpeechToText();
  bool _isListening = false;

  @override
  void initState() {
    super.initState();
    _initSpeech();
  }

  void _initSpeech() async {
    await _speechToText.initialize();
  }

  void _listen() async {
    if (!_isListening) {
      bool available = await _speechToText.initialize();
      if (available) {
        setState(() => _isListening = true);
        _speechToText.listen(
          onResult: (result) {
            setState(() {
              _faaliyetController.text = result.recognizedWords;
            });
          },
        );
      }
    } else {
      setState(() => _isListening = false);
      _speechToText.stop();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text("Faaliyet Ekle")),
      body: Padding(
        padding: const EdgeInsets.all(20.0),
        child: Column(
          children: [
            TextField(
              controller: _faaliyetController,
              maxLength: 200,
              decoration: InputDecoration(
                labelText: "Faaliyet Detayı",
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(15)),
                suffixIcon: IconButton(
                  icon: Icon(_isListening ? Icons.mic : Icons.mic_none),
                  color: _isListening ? Colors.red : Colors.green.shade800,
                  onPressed: _listen,
                ),
              ),
            ),
            const SizedBox(height: 20),
            ElevatedButton(
              onPressed: () async {
                // 1. Kontrol: Boş kayıt yapmasın
                if (_faaliyetController.text.isEmpty) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text("Lütfen bir faaliyet girin!")),
                  );
                  return;
                }

                // 2. Faaliyet nesnesini oluştur
                final yeniFaaliyet = Faaliyet(
                  id: DateTime.now().millisecondsSinceEpoch.toString(), // Benzersiz ID
                  tarlaId: widget.tarlaId,
                  type: _faaliyetController.text, // TextField'daki veriyi alıyoruz
                  note: "", 
                  timestamp: DateTime.now(),
                  isCompleted: true, // "Geçmiş" listesinde görünmesi için true yapıyoruz
                );

                // 3. VERİTABANINA KAYDET
                await DatabaseHelper.instance.insertFaaliyet(yeniFaaliyet);

                // 4. Ekrana bilgi ver ve geri dön
                if (mounted) {
                  Navigator.pop(context, true); // 'true' değeri, TarlaDetayEkrani'ne verinin yenilenmesi gerektiğini söyler
                }
              },
              child: const Text("Kaydet"),
            )
          ],
        ),
      ),
    );
  }
}