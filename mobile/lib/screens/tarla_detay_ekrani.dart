import 'dart:ui';
import 'package:flutter/material.dart';
import '../models/tarla.dart';
import '../models/faaliyet.dart';
import '../services/database_helper.dart';
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
    // DefaultTabController ile 2 sekme oluşturuyoruz
    return DefaultTabController(
      length: 2,
      child: Scaffold(
        extendBodyBehindAppBar: true,
        appBar: AppBar(
          title: Text(widget.tarla.name, style: const TextStyle(color: Colors.white)),
          backgroundColor: Colors.transparent,
          elevation: 0,
          iconTheme: const IconThemeData(color: Colors.white),
          bottom: const TabBar(
            indicatorColor: Colors.white,
            labelColor: Colors.white,
            unselectedLabelColor: Colors.white60,
            tabs: [
              Tab(text: "Geçmiş", icon: Icon(Icons.history)),
              Tab(text: "Yapılacak", icon: Icon(Icons.event_note)),
            ],
          ),
        ),
        body: Container(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              colors: [Colors.green.shade800, Colors.green.shade400],
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
            ),
          ),
          child: FutureBuilder<List<Faaliyet>>(
            future: _faaliyetler,
            builder: (context, snapshot) {
              if (snapshot.connectionState == ConnectionState.waiting) {
                return const Center(child: CircularProgressIndicator(color: Colors.white));
              }
              
              final allFaaliyetler = snapshot.data ?? [];
              // Geçmiş ve Yapılacakları filtreliyoruz
              final gecmisler = allFaaliyetler.where((f) => f.isCompleted).toList();
              final yapilacaklar = allFaaliyetler.where((f) => !f.isCompleted).toList();

              return TabBarView(
                children: [
                  _buildList(gecmisler, true),
                  _buildList(yapilacaklar, false),
                ],
              );
            },
          ),
        ),
        floatingActionButton: FloatingActionButton(
          backgroundColor: Colors.white,
          foregroundColor: Colors.green.shade800,
          onPressed: () async {
            final result = await Navigator.push(
              context,
              MaterialPageRoute(
                builder: (context) => FaaliyetEklemeEkrani(tarlaId: widget.tarla.id),
              ),
            );
            if (result == true) _loadFaaliyetler();
          },
          child: const Icon(Icons.add),
        ),
      ),
    );
  }

  // Liste oluşturucu yardımcı metod
  Widget _buildList(List<Faaliyet> items, bool isGecmis) {
    if (items.isEmpty) {
      return Center(
        child: Text(
          isGecmis ? "Henüz kayıtlı faaliyet yok." : "Planlanan faaliyet yok.",
          style: const TextStyle(color: Colors.white54),
        ),
      );
    }

    return ListView.builder(
      padding: const EdgeInsets.only(top: 150, left: 16, right: 16),
      itemCount: items.length,
      itemBuilder: (context, index) {
        final f = items[index];
        return Container(
          margin: const EdgeInsets.only(bottom: 10),
          child: _buildGlassCard(
            child: ListTile(
              leading: Icon(isGecmis ? Icons.check_circle : Icons.schedule, color: Colors.white),
              title: Text(f.type, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
              subtitle: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(f.note, style: const TextStyle(color: Colors.white70)),
                  if (f.dueDate != null) 
                    Text("Tarih: ${f.dueDate.toString().split(' ')[0]}", style: const TextStyle(color: Colors.orangeAccent)),
                ],
              ),
              trailing: IconButton(
                icon: const Icon(Icons.delete, color: Colors.white54),
                onPressed: () async {
                  await DatabaseHelper.instance.deleteFaaliyet(f.id);
                  _loadFaaliyetler();
                },
              ),
            ),
          ),
        );
      },
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