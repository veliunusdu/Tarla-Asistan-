import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../app/theme/app_colors.dart';
import '../app/theme/app_spacing.dart';
import '../features/ai_assistant/data/ai_assistant_repository.dart';
import '../features/ai_assistant/data/unavailable_ai_assistant_repository.dart';
import '../features/ai_assistant/domain/ai_chat_message.dart';

// ---------------------------------------------------------------------------
// Örnek sorular
// ---------------------------------------------------------------------------

const List<String> _oneriler = [
  'Yapraklardaki lekeler ne olabilir?',
  'Bugün sulama yapmalı mıyım?',
  'Bu bitkide hastalık belirtisi var mı?',
];

// ---------------------------------------------------------------------------
// Ekran
// ---------------------------------------------------------------------------

class AiAsistanEkrani extends StatefulWidget {
  const AiAsistanEkrani({super.key, AiAssistantRepository? repository})
    : _repo = repository ?? const UnavailableAiAssistantRepository();

  final AiAssistantRepository _repo;

  @override
  State<AiAsistanEkrani> createState() => _AiAsistanEkraniState();
}

class _AiAsistanEkraniState extends State<AiAsistanEkrani> {
  final List<AiChatMessage> _mesajlar = [];
  final _ctrl = TextEditingController();
  final _scrollCtrl = ScrollController();
  Uint8List? _secilenFoto;
  bool _gonderiyor = false;

  @override
  void dispose() {
    _ctrl.dispose();
    _scrollCtrl.dispose();
    super.dispose();
  }

  // ---------------------------------------------------------------------------
  // Fotoğraf seçimi
  // ---------------------------------------------------------------------------

  Future<void> _fotoCek() async {
    final kaynak = await showModalBottomSheet<ImageSource>(
      context: context,
      builder: (ctx) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.camera_alt),
              title: const Text('Kameradan Çek'),
              onTap: () => Navigator.pop(ctx, ImageSource.camera),
            ),
            ListTile(
              leading: const Icon(Icons.photo_library),
              title: const Text('Galeriden Seç'),
              onTap: () => Navigator.pop(ctx, ImageSource.gallery),
            ),
            ListTile(
              leading: const Icon(Icons.cancel_outlined),
              title: const Text('İptal'),
              onTap: () => Navigator.pop(ctx),
            ),
          ],
        ),
      ),
    );

    if (kaynak == null || !mounted) return;

    final picker = ImagePicker();
    final xFile = await picker.pickImage(
      source: kaynak,
      imageQuality: 70,
      maxWidth: 1024,
      maxHeight: 1024,
    );

    if (xFile == null || !mounted) return;
    final bytes = await xFile.readAsBytes();
    if (!mounted) return;
    setState(() => _secilenFoto = bytes);
  }

  // ---------------------------------------------------------------------------
  // Mesaj gönderme
  // ---------------------------------------------------------------------------

  Future<void> _gonder() async {
    final metin = _ctrl.text.trim();
    if (metin.isEmpty && _secilenFoto == null) return;
    if (_gonderiyor) return;

    final fotoKopya = _secilenFoto;
    final metinKopya = metin;

    setState(() {
      _mesajlar.add(
        AiChatMessage(
          text: metinKopya,
          isUser: true,
          photo: fotoKopya,
          timestamp: DateTime.now(),
        ),
      );
      _ctrl.clear();
      _secilenFoto = null;
      _gonderiyor = true;
    });

    _scrollToBottom();

    try {
      final cevap = await widget._repo.sendMessage(
        message: metinKopya,
        photo: fotoKopya,
        history: List.unmodifiable(_mesajlar),
      );

      if (!mounted) return;

      setState(() {
        _mesajlar.add(
          AiChatMessage(text: cevap, isUser: false, timestamp: DateTime.now()),
        );
      });

      _scrollToBottom();
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('AI Asistan bağlantısı henüz yapılandırılmadı.'),
        ),
      );
    } finally {
      if (mounted) setState(() => _gonderiyor = false);
    }
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scrollCtrl.hasClients) {
        _scrollCtrl.animateTo(
          _scrollCtrl.position.maxScrollExtent,
          duration: const Duration(milliseconds: 300),
          curve: Curves.easeOut,
        );
      }
    });
  }

  void _onOneriSec(String metin) {
    setState(() => _ctrl.text = metin);
  }

  // ---------------------------------------------------------------------------
  // Build
  // ---------------------------------------------------------------------------

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('AI Tarla Asistanı')),
      body: Column(
        children: [
          Expanded(
            child: _mesajlar.isEmpty
                ? _BosKonusma(onOneri: _onOneriSec)
                : _MesajListesi(
                    mesajlar: _mesajlar,
                    gonderiyor: _gonderiyor,
                    scrollCtrl: _scrollCtrl,
                  ),
          ),
          _GirisBolumu(
            ctrl: _ctrl,
            secilenFoto: _secilenFoto,
            onFotoKaldir: () => setState(() => _secilenFoto = null),
            onFotoEkle: _fotoCek,
            onGonder: _gonder,
            gonderiyor: _gonderiyor,
          ),
        ],
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Boş konuşma durumu
// ---------------------------------------------------------------------------

class _BosKonusma extends StatelessWidget {
  const _BosKonusma({required this.onOneri});

  final ValueChanged<String> onOneri;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return SingleChildScrollView(
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const SizedBox(height: AppSpacing.md),
          Icon(
            Icons.smart_toy_outlined,
            size: 56,
            color: theme.colorScheme.primary,
          ),
          const SizedBox(height: AppSpacing.md),
          Text(
            'AI Tarla Asistanı',
            style: theme.textTheme.headlineSmall?.copyWith(
              fontWeight: FontWeight.w600,
            ),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: AppSpacing.sm),
          Text(
            'Fotoğraf ekleyerek bitkinle ilgili soru sorabilir veya doğrudan yazışabilirsin.',
            style: theme.textTheme.bodyMedium?.copyWith(
              color: AppColors.textSecondary,
            ),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: AppSpacing.lg),
          Text(
            'Örnek sorular:',
            style: theme.textTheme.titleSmall?.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: AppSpacing.sm),
          ..._oneriler.map(
            (o) => Padding(
              padding: const EdgeInsets.only(bottom: AppSpacing.sm),
              child: OutlinedButton(
                onPressed: () => onOneri(o),
                child: Text(
                  o,
                  textAlign: TextAlign.center,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Mesaj listesi
// ---------------------------------------------------------------------------

class _MesajListesi extends StatelessWidget {
  const _MesajListesi({
    required this.mesajlar,
    required this.gonderiyor,
    required this.scrollCtrl,
  });

  final List<AiChatMessage> mesajlar;
  final bool gonderiyor;
  final ScrollController scrollCtrl;

  @override
  Widget build(BuildContext context) {
    return ListView.builder(
      controller: scrollCtrl,
      padding: const EdgeInsets.all(AppSpacing.md),
      itemCount: mesajlar.length + (gonderiyor ? 1 : 0),
      itemBuilder: (context, i) {
        if (i == mesajlar.length) {
          return const _YuklenigorGostergesi();
        }
        return _MesajBubble(mesaj: mesajlar[i]);
      },
    );
  }
}

class _YuklenigorGostergesi extends StatelessWidget {
  const _YuklenigorGostergesi();

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: Alignment.centerLeft,
      child: Container(
        margin: const EdgeInsets.only(bottom: AppSpacing.sm),
        padding: const EdgeInsets.symmetric(
          horizontal: AppSpacing.md,
          vertical: AppSpacing.sm,
        ),
        decoration: BoxDecoration(
          color: Theme.of(context).colorScheme.surfaceContainerHighest,
          borderRadius: BorderRadius.circular(12),
        ),
        child: const SizedBox(
          width: 20,
          height: 20,
          child: CircularProgressIndicator(strokeWidth: 2),
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Mesaj baloncuğu
// ---------------------------------------------------------------------------

class _MesajBubble extends StatelessWidget {
  const _MesajBubble({required this.mesaj});

  final AiChatMessage mesaj;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final isUser = mesaj.isUser;

    return Align(
      alignment: isUser ? Alignment.centerRight : Alignment.centerLeft,
      child: ConstrainedBox(
        constraints: BoxConstraints(
          maxWidth: MediaQuery.of(context).size.width * 0.75,
        ),
        child: Container(
          margin: const EdgeInsets.only(bottom: AppSpacing.sm),
          padding: const EdgeInsets.all(AppSpacing.sm),
          decoration: BoxDecoration(
            color: isUser
                ? AppColors.primary.withValues(alpha: 0.12)
                : theme.colorScheme.surfaceContainerHighest,
            borderRadius: BorderRadius.only(
              topLeft: const Radius.circular(12),
              topRight: const Radius.circular(12),
              bottomLeft: Radius.circular(isUser ? 12 : 2),
              bottomRight: Radius.circular(isUser ? 2 : 12),
            ),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              if (mesaj.photo != null) ...[
                ClipRRect(
                  borderRadius: BorderRadius.circular(8),
                  child: Image.memory(
                    mesaj.photo!,
                    width: double.infinity,
                    height: 150,
                    fit: BoxFit.cover,
                  ),
                ),
                if (mesaj.text.isNotEmpty)
                  const SizedBox(height: AppSpacing.xs),
              ],
              if (mesaj.text.isNotEmpty)
                Text(mesaj.text, style: theme.textTheme.bodyMedium),
            ],
          ),
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Giriş bölümü
// ---------------------------------------------------------------------------

class _GirisBolumu extends StatelessWidget {
  const _GirisBolumu({
    required this.ctrl,
    required this.secilenFoto,
    required this.onFotoKaldir,
    required this.onFotoEkle,
    required this.onGonder,
    required this.gonderiyor,
  });

  final TextEditingController ctrl;
  final Uint8List? secilenFoto;
  final VoidCallback onFotoKaldir;
  final VoidCallback onFotoEkle;
  final VoidCallback onGonder;
  final bool gonderiyor;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return SafeArea(
      top: false,
      child: Container(
        decoration: BoxDecoration(
          color: theme.colorScheme.surface,
          border: Border(top: BorderSide(color: theme.dividerColor)),
        ),
        padding: const EdgeInsets.all(AppSpacing.sm),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Fotoğraf önizleme
            if (secilenFoto != null) ...[
              _FotoOnizleme(foto: secilenFoto!, onKaldir: onFotoKaldir),
              const SizedBox(height: AppSpacing.sm),
            ],
            // Giriş satırı
            Row(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                // Fotoğraf ekleme butonu
                IconButton(
                  icon: const Icon(Icons.add_photo_alternate_outlined),
                  onPressed: gonderiyor ? null : onFotoEkle,
                  tooltip: 'Fotoğraf Ekle',
                ),
                // Metin alanı
                Expanded(
                  child: TextField(
                    controller: ctrl,
                    enabled: !gonderiyor,
                    minLines: 1,
                    maxLines: 4,
                    textCapitalization: TextCapitalization.sentences,
                    decoration: const InputDecoration(
                      hintText: 'Sorunu veya sorununu anlat...',
                    ),
                  ),
                ),
                const SizedBox(width: AppSpacing.xs),
                // Gönder butonu
                IconButton.filled(
                  icon: gonderiyor
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Colors.white,
                          ),
                        )
                      : const Icon(Icons.send),
                  onPressed: gonderiyor ? null : onGonder,
                  tooltip: 'Gönder',
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Fotoğraf önizleme
// ---------------------------------------------------------------------------

class _FotoOnizleme extends StatelessWidget {
  const _FotoOnizleme({required this.foto, required this.onKaldir});

  final Uint8List foto;
  final VoidCallback onKaldir;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 72,
      height: 72,
      child: Stack(
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: Image.memory(foto, width: 64, height: 64, fit: BoxFit.cover),
          ),
          Positioned(
            top: 0,
            right: 0,
            child: GestureDetector(
              onTap: onKaldir,
              child: Container(
                width: 20,
                height: 20,
                decoration: const BoxDecoration(
                  color: AppColors.error,
                  shape: BoxShape.circle,
                ),
                child: const Icon(Icons.close, size: 14, color: Colors.white),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
