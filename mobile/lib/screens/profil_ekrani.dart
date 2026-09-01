import 'package:flutter/material.dart';

import '../features/profile/data/profile_repository.dart';
import '../features/profile/domain/user_profile.dart';

class ProfilEkrani extends StatefulWidget {
  const ProfilEkrani({super.key, this.repository, this.onLogout});

  final ProfileRepository? repository;
  final Future<void> Function()? onLogout;

  @override
  State<ProfilEkrani> createState() => _ProfilEkraniState();
}

class _ProfilEkraniState extends State<ProfilEkrani> {
  late Future<UserProfile?> _profile;

  @override
  void initState() {
    super.initState();
    _profile = _loadProfile();
  }

  Future<UserProfile?> _loadProfile() async =>
      widget.repository?.getCurrentProfile();

  Future<void> _confirmLogout() async {
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
    if (confirmed == true) await widget.onLogout?.call();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Profil ve Ayarlar')),
    body: FutureBuilder<UserProfile?>(
      future: _profile,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snapshot.hasError) {
          return Center(
            child: FilledButton(
              onPressed: () => setState(() => _profile = _loadProfile()),
              child: const Text('Tekrar dene'),
            ),
          );
        }
        final profile = snapshot.data;
        final location = [
          profile?.province,
          profile?.district,
        ].whereType<String>().where((value) => value.isNotEmpty).join(', ');
        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            ListTile(
              leading: const CircleAvatar(child: Icon(Icons.person)),
              title: Text(
                profile?.fullName?.trim().isNotEmpty == true
                    ? profile!.fullName!
                    : 'Hesabım',
              ),
              subtitle: Text(
                profile?.phoneNumber ?? 'Profil bilgileri yüklenemedi.',
              ),
            ),
            if (location.isNotEmpty)
              ListTile(
                leading: const Icon(Icons.location_on_outlined),
                title: const Text('Yaşadığınız yer'),
                subtitle: Text(location),
              ),
            const Divider(),
            ListTile(
              leading: const Icon(Icons.notifications_outlined),
              title: const Text('Bildirimler'),
              subtitle: Text(
                profile?.notificationsEnabled == true
                    ? 'Bildirimler açık'
                    : 'Bildirimler kapalı',
              ),
            ),
            const SizedBox(height: 24),
            FilledButton.tonalIcon(
              onPressed: widget.onLogout == null ? null : _confirmLogout,
              icon: const Icon(Icons.logout),
              label: const Text('Çıkış yap'),
            ),
          ],
        );
      },
    ),
  );
}
