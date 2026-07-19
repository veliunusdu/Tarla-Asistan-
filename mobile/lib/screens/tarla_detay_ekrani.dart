import 'dart:ui';
import 'package:flutter/material.dart';
import '../models/tarla.dart';
import '../models/faaliyet.dart';
import '../services/database_helper.dart';
// ÖNEMLİ: Dosya adının 'faaliyet_ekleme_ekrani.dart' olduğundan emin ol
import 'faaliyet_ekleme_ekrani.dart';

class TarlaDetayEkrani extends StatefulWidget {
  final Tarla tarla;
  const TarlaDetayEkrani({super.key, required this.tarla});

  @override
  State<TarlaDetayEkrani> createState() => _TarlaDetayEkraniState();
}

class _TarlaDetayEkraniState extends State<TarlaDetayEkrani> {
  late Future<List<Faaliyet>> _faaliyetler;

  @override
  void initState() {
    super.initState();
    _loadFaaliyetler();
  }

  void _loadFaaliyetler() {
    setState(() {
      _faaliyetler = DatabaseHelper.instance.getFaaliyetler(widget.tarla.id);
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      extendBodyBehindAppBar: true,
      appBar: AppBar(
        title: Text(widget.tarla.name, style: const TextStyle(color: Colors.white)),
        backgroundColor: Colors.transparent,
        elevation: 0,
        iconTheme: const IconThemeData(color: Colors.white),
      ),
      body: Container(
        decoration: BoxDecoration(
          gradient: LinearGradient(
            colors: [Colors.green.shade800, Colors.green.shade400],
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
          ),
        ),
        child: Column(
          children: [
            const SizedBox(height: 120),
            _buildGlassCard(
              child: ListTile(
                title: Text("Ürün: ${widget.tarla.cropType}", style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                subtitle: Text("Boyut: ${widget.tarla.size} dönüm", style: const TextStyle(color: Colors.white70)),
                trailing: const Icon(Icons.grass, color: Colors.white),
              ),
            ),
            const Padding(
              padding: EdgeInsets.all(16.0),
              child: Align(alignment: Alignment.centerLeft, child: Text("Geçmiş Faaliyetler", style: TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold))),
            ),
            Expanded(
              child: FutureBuilder<List<Faaliyet>>(
                future: _faaliyetler,
                builder: (context, snapshot) {
                  if (snapshot.connectionState == ConnectionState.waiting) return const Center(child: CircularProgressIndicator(color: Colors.white));
                  if (!snapshot.hasData || snapshot.data!.isEmpty) return const Center(child: Text("Henüz faaliyet yok.", style: TextStyle(color: Colors.white54)));
                  
                  final faaliyetler = snapshot.data!;
                  return ListView.builder(
                    padding: const EdgeInsets.symmetric(horizontal: 16),
                    itemCount: faaliyetler.length,
                    itemBuilder: (context, index) {
                      final f = faaliyetler[index];
                      return Dismissible(
                        key: Key(f.id.toString()), 
                        background: Container(
                          color: Colors.red.withValues(alpha: 0.6),
                          alignment: Alignment.centerRight,
                          padding: const EdgeInsets.only(right: 20),
                          child: const Icon(Icons.delete, color: Colors.white),
                        ),
                        onDismissed: (direction) async {
                          await DatabaseHelper.instance.deleteFaaliyet(f.id);
                          _loadFaaliyetler();
                        },
                        child: Container(
                          margin: const EdgeInsets.only(bottom: 10),
                          child: _buildGlassCard(
                            child: ListTile(
                              leading: const Icon(Icons.history, color: Colors.white),
                              title: Text(f.type, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                              subtitle: Text(f.note ?? "", style: const TextStyle(color: Colors.white70)),
                              trailing: Text(f.timestamp.toString().split(' ')[0], style: const TextStyle(color: Colors.white54)),
                            ),
                          ),
                        ),
                      );
                    },
                  );
                },
              ),
            ),
          ],
        ),
      ),
      floatingActionButton: FloatingActionButton(
        backgroundColor: Colors.white,
        foregroundColor: Colors.green.shade800,
        onPressed: () async {
          // Navigasyon kısmı:
          final result = await Navigator.push(
            context,
            MaterialPageRoute(
             builder: (context) => FaaliyetEklemeEkrani(tarlaId: int.parse(widget.tarla.id.toString())),            ),
          );
          // Ekrandan geri dönüldüğünde (kayıt yapıldıysa) listeyi yenile
          if (result == true) _loadFaaliyetler();
        },
        child: const Icon(Icons.add),
      ),
    );
  }

  Widget _buildGlassCard({required Widget child}) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(20),
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: 10, sigmaY: 10),
        child: Container(
          decoration: BoxDecoration(
            color: Colors.white.withValues(alpha: 0.2),
            borderRadius: BorderRadius.circular(20),
            border: Border.all(color: Colors.white.withValues(alpha: 0.3)),
          ),
          child: child,
        ),
      ),
    );
  }
}