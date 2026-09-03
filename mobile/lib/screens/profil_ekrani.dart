import 'package:flutter/material.dart';

import '../features/cases/data/case_repository.dart';
import '../features/cases/presentation/vaka_listesi_ekrani.dart';
import '../features/fields/data/local_tarla_repository.dart';
import '../features/fields/data/tarla_repository.dart';
import '../features/profile/data/profile_repository.dart';
import '../features/profile/domain/user_profile.dart';
import '../services/api_client.dart';
import 'bildirimler_ekrani.dart';

class ProfilEkrani extends StatefulWidget {
  const ProfilEkrani({
    super.key,
    this.repository,
    this.caseRepository,
    this.tarlaRepository,
    this.apiClient,
    this.onLogout,
  });

  final ProfileRepository? repository;
  final CaseRepository? caseRepository;
  final TarlaRepository? tarlaRepository;
  final ApiClient? apiClient;
  final Future<void> Function()? onLogout;

  @override
  State<ProfilEkrani> createState() => _ProfilEkraniState();
}

class _ProfilEkraniState extends State<ProfilEkrani> {
  UserProfile? _profile;
  bool _loading = true;
  bool _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadProfile();
  }

  Future<void> _loadProfile() async {
    if (widget.repository == null) {
      if (mounted) {
        setState(() {
          _loading = false;
        });
      }
      return;
    }

    if (mounted) {
      setState(() {
        _loading = true;
        _error = null;
      });
    }

    try {
      final profile = await widget.repository!.getCurrentProfile();
      if (mounted) {
        setState(() {
          _profile = profile;
          _loading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _error = 'Profil bilgileri yüklenemedi.';
          _loading = false;
        });
      }
    }
  }

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
    if (confirmed == true) {
      try {
        await widget.onLogout?.call();
      } catch (e) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('Çıkış yapılırken hata: $e')),
          );
        }
      }
    }
  }

  Future<void> _toggleNotifications(bool enabled) async {
    final currentProfile = _profile;
    if (currentProfile == null || widget.repository == null || _saving) return;

    setState(() {
      _saving = true;
    });

    try {
      final updated = await widget.repository!.updateProfile(
        UserProfileUpdate(
          fullName: currentProfile.fullName ?? '',
          province: currentProfile.province ?? '',
          district: currentProfile.district ?? '',
          termsAccepted: currentProfile.termsAccepted,
          notificationsEnabled: enabled,
        ),
      );

      if (mounted) {
        setState(() {
          _profile = updated;
          _saving = false;
        });

        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              enabled ? 'Bildirimler açıldı.' : 'Bildirimler kapatıldı.',
            ),
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _saving = false;
        });

        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Bildirim tercihi kaydedilemedi: $e'),
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Profil ve Ayarlar')),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_error != null) {
      return Center(
        child: FilledButton(
          onPressed: _loadProfile,
          child: const Text('Tekrar dene'),
        ),
      );
    }

    final profile = _profile;
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
          trailing: profile == null || widget.repository == null
              ? null
              : Switch(
                  value: profile.notificationsEnabled,
                  onChanged: _saving ? null : _toggleNotifications,
                ),
        ),
        if (widget.apiClient != null)
          ListTile(
            leading: const Icon(Icons.notifications_none_outlined),
            title: const Text('Bildirim Merkezi'),
            subtitle: const Text('Görev, uzman yanıtı ve hava uyarıları'),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => Navigator.push(
              context,
              MaterialPageRoute(
                builder: (_) => BildirimlerEkrani(apiClient: widget.apiClient!),
              ),
            ),
          ),
        if (widget.caseRepository != null) ...[
          const Divider(),
          ListTile(
            leading: const Icon(Icons.forum_outlined),
            title: const Text('Sorun Bildirimlerim'),
            subtitle:
                const Text('Ziraat mühendisi ile mesajlaşmalar ve vakalar'),
            trailing: const Icon(Icons.chevron_right),
            onTap: () {
              Navigator.push(
                context,
                MaterialPageRoute(
                  builder: (_) => VakaListesiEkrani(
                    caseRepository: widget.caseRepository!,
                    tarlaRepository:
                        widget.tarlaRepository ?? const LocalTarlaRepository(),
                  ),
                ),
              );
            },
          ),
        ],
        const SizedBox(height: 24),
        FilledButton.tonalIcon(
          onPressed: widget.onLogout == null ? null : _confirmLogout,
          icon: const Icon(Icons.logout),
          label: const Text('Çıkış yap'),
        ),
      ],
    );
  }
}
