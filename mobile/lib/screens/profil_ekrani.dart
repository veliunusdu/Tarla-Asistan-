import 'package:flutter/material.dart';

import '../features/cases/data/case_repository.dart';
import '../features/cases/presentation/vaka_listesi_ekrani.dart';
import '../features/fields/data/local_tarla_repository.dart';
import '../features/fields/data/tarla_repository.dart';
import '../features/profile/data/profile_repository.dart';
import '../features/profile/domain/user_profile.dart';
import '../features/tasks/services/daily_task_notification_service.dart';
import '../services/api_client.dart';
import 'bildirimler_ekrani.dart';

class ProfilEkrani extends StatefulWidget {
  const ProfilEkrani({
    super.key,
    this.repository,
    this.caseRepository,
    this.tarlaRepository,
    this.apiClient,
    this.dailyTaskNotificationService,
    this.onLogout,
  });

  final ProfileRepository? repository;
  final CaseRepository? caseRepository;
  final TarlaRepository? tarlaRepository;
  final ApiClient? apiClient;
  final DailyTaskNotificationService? dailyTaskNotificationService;
  final Future<void> Function()? onLogout;

  @override
  State<ProfilEkrani> createState() => _ProfilEkraniState();
}

class _ProfilEkraniState extends State<ProfilEkrani> {
  UserProfile? _profile;
  bool _loading = true;
  bool _saving = false;
  String? _error;
  late final DailyTaskNotificationService _dailyNotificationService;
  bool _dailyTasksNotificationEnabled = true;
  TimeOfDay _dailyReminderTime = const TimeOfDay(hour: 8, minute: 0);

  @override
  void initState() {
    super.initState();
    _dailyNotificationService =
        widget.dailyTaskNotificationService ?? DailyTaskNotificationService();
    _loadProfile();
    _loadNotificationPreferences();
  }

  Future<void> _loadNotificationPreferences() async {
    final enabled = await _dailyNotificationService.preferences
        .isDailyTasksNotificationEnabled();
    final time =
        await _dailyNotificationService.preferences.getReminderTime();
    if (mounted) {
      setState(() {
        _dailyTasksNotificationEnabled = enabled;
        _dailyReminderTime = time;
      });
    }
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

      if (enabled) {
        if (_dailyTasksNotificationEnabled) {
          await _dailyNotificationService.scheduleDailySummaryIfNeeded();
        }
      } else {
        await _dailyNotificationService.cancelDailyReminder();
      }

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
                  key: const Key('profile_notifications_switch'),
                  value: profile.notificationsEnabled,
                  onChanged: _saving ? null : _toggleNotifications,
                ),
        ),
        if (profile?.notificationsEnabled == true) ...[
          ListTile(
            contentPadding: const EdgeInsets.only(left: 32, right: 16),
            title: const Text('Günlük görev hatırlatmaları'),
            subtitle: Text(
              _dailyTasksNotificationEnabled
                  ? 'Sabahları günün önemli işlerini hatırlat'
                  : 'Kapalı',
            ),
            trailing: Switch(
              key: const Key('daily_tasks_notification_switch'),
              value: _dailyTasksNotificationEnabled,
              onChanged: (val) async {
                await _dailyNotificationService.preferences
                    .setDailyTasksNotificationEnabled(val);
                setState(() => _dailyTasksNotificationEnabled = val);
                if (val) {
                  await _dailyNotificationService.scheduleDailySummaryIfNeeded();
                } else {
                  await _dailyNotificationService.cancelDailyReminder();
                }
              },
            ),
          ),
          if (_dailyTasksNotificationEnabled)
            ListTile(
              contentPadding: const EdgeInsets.only(left: 32, right: 16),
              title: const Text('Günlük görev hatırlatma saati'),
              subtitle: Text(_dailyReminderTime.format(context)),
              trailing: const Icon(Icons.access_time),
              onTap: () async {
                final picked = await showTimePicker(
                  context: context,
                  initialTime: _dailyReminderTime,
                );
                if (picked != null) {
                  await _dailyNotificationService.preferences
                      .setReminderTime(picked);
                  setState(() => _dailyReminderTime = picked);
                  await _dailyNotificationService.rescheduleDailyReminder();
                }
              },
            ),
        ],
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
