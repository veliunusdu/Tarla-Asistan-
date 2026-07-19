import 'package:flutter/material.dart';
import 'package:speech_to_text/speech_to_text.dart';

class FaaliyetEklemeEkrani extends StatefulWidget {
  final int tarlaId; // Tarla ID'yi dışarıdan alıyoruz

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
      appBar: AppBar(
        title: const Text("Faaliyet Ekle"),
        backgroundColor: Colors.green.shade700,
      ),
      body: Padding(
        padding: const EdgeInsets.all(20.0),
        child: Column(
          children: [
            TextField(
              controller: _faaliyetController,
              maxLength: 200, // 200 karakter sınırı
              keyboardType: TextInputType.text, // Türkçe klavye desteği
              textCapitalization: TextCapitalization.sentences,
              decoration: InputDecoration(
                labelText: "Faaliyet Detayı",
                hintText: "Örn: Tarlaya gübre atıldı...",
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(15)),
                suffixIcon: IconButton(
                  icon: Icon(_isListening ? Icons.mic : Icons.mic_none),
                  color: _isListening ? Colors.red : Colors.green.shade800,
                  onPressed: _listen,
                ),
              ),
            ),
            const SizedBox(height: 20),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton(
                onPressed: () {
                  // Kaydetme mantığı buraya gelecek
                  print("Tarla ID: ${widget.tarlaId}, Faaliyet: ${_faaliyetController.text}");
                  Navigator.pop(context); // Ekranı kapat
                },
                style: ElevatedButton.styleFrom(padding: const EdgeInsets.all(15)),
                child: const Text("Kaydet"),
              ),
            )
          ],
        ),
      ),
    );
  }
}