import 'package:flutter/material.dart';
import '../models/tarla.dart';
import '../services/database_helper.dart';
import 'tarla_ekleme_ekrani.dart'; 
import 'tarla_detay_ekrani.dart'; 

class TarlaListesiEkrani extends StatefulWidget {
  const TarlaListesiEkrani({super.key});

  @override
  State<TarlaListesiEkrani> createState() => _TarlaListesiEkraniState();
}

class _TarlaListesiEkraniState extends State<TarlaListesiEkrani> {
  Future<List<Tarla>> _tarlalar = DatabaseHelper.instance.getTarlalar();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.green.shade50,
      appBar: AppBar(
        title: const Text("Tarlalarım"),
        backgroundColor: Colors.green.shade700,
      ),
      body: FutureBuilder<List<Tarla>>(
        future: _tarlalar,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          } else if (snapshot.hasError) {
            return Center(child: Text("Hata oluştu: ${snapshot.error}"));
          } else if (!snapshot.hasData || snapshot.data!.isEmpty) {
            return const Center(child: Text("Henüz hiç tarla eklemedin!"));
          }

          final tarlalar = snapshot.data!;
          return ListView.builder(
            padding: const EdgeInsets.all(12),
            itemCount: tarlalar.length,
            itemBuilder: (context, index) {
              final tarla = tarlalar[index];
              return Card(
                // UYARI GİDERİLDİ: withOpacity yerine withValues kullanıldı
                color: Colors.white.withValues(alpha: 0.8),
                elevation: 2,
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
                child: ListTile(
                  leading: Icon(Icons.grass, color: Colors.green.shade800),
                  title: Text(tarla.name, style: const TextStyle(fontWeight: FontWeight.bold)),
                  subtitle: Text("${tarla.cropType} • ${tarla.size} dönüm"),
                  onTap: () async {
                    await Navigator.push(
                      context,
                      MaterialPageRoute(
                        builder: (context) => TarlaDetayEkrani(tarla: tarla),
                      ),
                    );
                    setState(() {
                      _tarlalar = DatabaseHelper.instance.getTarlalar();
                    });
                  },
                ),
              );
            },
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        backgroundColor: Colors.green.shade700,
        child: const Icon(Icons.add),
        onPressed: () async {
          final result = await Navigator.push(
            context,
            MaterialPageRoute(builder: (context) => const TarlaEklemeEkrani()),
          );
          
          if (result == true) {
            setState(() {
              _tarlalar = DatabaseHelper.instance.getTarlalar();
            });
          }
        },
      ),
    );
  }
}