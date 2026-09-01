import 'package:flutter/material.dart';

class ProfilEkrani extends StatelessWidget {
  const ProfilEkrani({super.key, this.onLogout});

  final Future<void> Function()? onLogout;

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Profil ve Ayarlar')),
    body: ListView(
      padding: const EdgeInsets.all(16),
      children: [
        const ListTile(
          leading: CircleAvatar(child: Icon(Icons.person)),
          title: Text('Hesabım'),
          subtitle: Text('Profil bilgileri yükleniyor…'),
        ),
        const Divider(),
        const ListTile(
          leading: Icon(Icons.notifications_outlined),
          title: Text('Bildirimler'),
          subtitle: Text('Bildirim tercihleriniz burada görünür.'),
        ),
        const SizedBox(height: 24),
        FilledButton.tonalIcon(
          onPressed: onLogout == null ? null : () => onLogout!(),
          icon: const Icon(Icons.logout),
          label: const Text('Çıkış yap'),
        ),
      ],
    ),
  );
}
