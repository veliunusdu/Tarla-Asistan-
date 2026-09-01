import 'package:flutter/material.dart';

class ProfilEkrani extends StatelessWidget {
  const ProfilEkrani({super.key, this.onLogout});

  final Future<void> Function()? onLogout;

  Future<void> _confirmLogout(BuildContext context) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Çıkış yapmak istiyor musunuz?'),
        content: const Text('Bu cihazdaki oturumunuz kapatılacak.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Vazgeç'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Çıkış yap'),
          ),
        ],
      ),
    );
    if (confirmed == true) await onLogout?.call();
  }

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
          onPressed: onLogout == null ? null : () => _confirmLogout(context),
          icon: const Icon(Icons.logout),
          label: const Text('Çıkış yap'),
        ),
      ],
    ),
  );
}
