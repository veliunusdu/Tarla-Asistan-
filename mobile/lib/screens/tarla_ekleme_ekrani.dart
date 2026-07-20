import 'package:flutter/material.dart';
import '../models/tarla.dart';
import '../services/database_helper.dart';

class TarlaEklemeEkrani extends StatefulWidget {
  const TarlaEklemeEkrani({super.key});

  @override
  State<TarlaEklemeEkrani> createState() => _TarlaEklemeEkraniState();
}

class _TarlaEklemeEkraniState extends State<TarlaEklemeEkrani> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _sizeController = TextEditingController();
  final _cropController = TextEditingController();

  Future<void> _kaydet() async {
    if (_formKey.currentState!.validate()) {
      final yeniTarla = Tarla(
        id: DateTime.now().toString(), // Basit bir benzersiz ID
        name: _nameController.text,
        latitude: 0.0, // İleride GPS ile dolduracağız
        longitude: 0.0,
        size: double.parse(_sizeController.text),
        cropType: _cropController.text,
        plantingDate: DateTime.now(),
      );

      await DatabaseHelper.instance.insertTarla(yeniTarla);
      if (mounted) Navigator.pop(context, true); // Ekranı kapat ve listeyi yenile
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text("Yeni Tarla Ekle")),
      body: Padding(
        padding: const EdgeInsets.all(16.0),
        child: Form(
          key: _formKey,
          child: Column(
            children: [
              TextFormField(controller: _nameController, decoration: const InputDecoration(labelText: "Tarla Adı")),
              TextFormField(controller: _sizeController, decoration: const InputDecoration(labelText: "Büyüklük (Dönüm)")),
              TextFormField(controller: _cropController, decoration: const InputDecoration(labelText: "Ürün Türü")),
              const SizedBox(height: 20),
              ElevatedButton(onPressed: _kaydet, child: const Text("Kaydet")),
            ],
          ),
        ),
      ),
    );
  }
}