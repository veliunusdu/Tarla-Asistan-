import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../app/theme/app_colors.dart';
import '../app/theme/app_spacing.dart';
import '../features/ai_assistant/data/ai_assistant_repository.dart';
import '../features/ai_assistant/data/image_picker_service.dart';
import '../features/ai_assistant/data/unavailable_ai_assistant_repository.dart';
import '../features/ai_assistant/data/voice_input_service.dart';
import '../features/ai_assistant/data/voice_output_service.dart';
import '../features/ai_assistant/data/voice_assistant_preferences.dart';
import '../features/ai_assistant/domain/ai_chat_message.dart';
import '../services/api_client.dart';

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
  const AiAsistanEkrani({
    super.key,
    AiAssistantRepository? repository,
    ImagePickerService? imagePickerService,
    VoiceInputService? voiceInputService,
    VoiceOutputService? voiceOutputService,
    VoiceAssistantPreferences? preferences,
    this.fieldId,
  }) : _repo = repository ?? const UnavailableAiAssistantRepository(),
       _imagePickerService =
           imagePickerService ?? const DefaultImagePickerService(),
       _injectedVoiceService = voiceInputService,
       _injectedVoiceOutputService = voiceOutputService,
       _injectedPreferences = preferences;

  final AiAssistantRepository _repo;
  final ImagePickerService _imagePickerService;
  final VoiceInputService? _injectedVoiceService;
  final VoiceOutputService? _injectedVoiceOutputService;
  final VoiceAssistantPreferences? _injectedPreferences;
  final String? fieldId;

  @override
  State<AiAsistanEkrani> createState() => _AiAsistanEkraniState();
}

class _AiAsistanEkraniState extends State<AiAsistanEkrani> {
  final List<AiChatMessage> _mesajlar = [];
  final _ctrl = TextEditingController();
  final _scrollCtrl = ScrollController();
  late final VoiceInputService _voiceService;
  late final bool _ownsVoiceService;
  late final VoiceOutputService _voiceOutputService;
  late final bool _ownsVoiceOutputService;
  late final VoiceAssistantPreferences _preferences;

  PickedImageData? _secilenFoto;
  String? _conversationId;
  bool _gonderiyor = false;
  bool _isListening = false;
  bool _isDisposed = false;
  String _voiceBaseText = '';
  int? _speakingMessageIndex;
  bool _voiceOutputInitialized = false;
  int _ttsSession = 0;
  bool _voiceResponsesEnabled = false;

  @override
  void initState() {
    super.initState();
    if (widget._injectedVoiceService != null) {
      _voiceService = widget._injectedVoiceService!;
      _ownsVoiceService = false;
    } else {
      _voiceService = DefaultVoiceInputService();
      _ownsVoiceService = true;
    }

    if (widget._injectedVoiceOutputService != null) {
      _voiceOutputService = widget._injectedVoiceOutputService!;
      _ownsVoiceOutputService = false;
    } else {
      _voiceOutputService = DefaultVoiceOutputService();
      _ownsVoiceOutputService = true;
    }

    _preferences =
        widget._injectedPreferences ??
        const SharedPreferencesVoiceAssistantPreferences();
    _loadVoicePreferences();

    _voiceOutputService.onSpeakingChanged = (speaking) {
      if (_isDisposed || !mounted) return;
      if (!speaking) {
        setState(() => _speakingMessageIndex = null);
      }
    };

    _voiceOutputService.onError = (error) {
      if (_isDisposed || !mounted) return;
      setState(() => _speakingMessageIndex = null);
      _hataGosterTts(error);
    };
  }

  @override
  void dispose() {
    _isDisposed = true;
    _ctrl.dispose();
    _scrollCtrl.dispose();
    if (_isListening) {
      _isListening = false;
      try {
        _voiceService.stopListening();
      } catch (_) {}
    }
    if (_speakingMessageIndex != null || _voiceOutputService.isSpeaking) {
      try {
        _voiceOutputService.stop();
      } catch (_) {}
    }
    if (_ownsVoiceService) {
      _voiceService.dispose();
    }
    if (_ownsVoiceOutputService) {
      _voiceOutputService.dispose();
    }
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

    final picked = await widget._imagePickerService.pickImage(source: kaynak);
    if (picked == null || !mounted) return;
    setState(() => _secilenFoto = picked);
  }

  // ---------------------------------------------------------------------------
  // Mesaj gönderme
  // ---------------------------------------------------------------------------

  Future<void> _gonder() async {
    if (_isListening) {
      await _durdurSesliGiris();
    }
    if (_speakingMessageIndex != null || _voiceOutputService.isSpeaking) {
      await _durdurSesliCikti();
    }
    final metin = _ctrl.text.trim();
    if (metin.isEmpty) {
      if (_secilenFoto != null) {
        _hataMesajiGoster(
          'Lütfen fotoğrafla ilgili bir soru veya açıklama yazın.',
        );
      }
      return;
    }
    if (_gonderiyor) return;

    final fotoKopya = _secilenFoto;
    final metinKopya = metin;

    setState(() {
      _mesajlar.add(
        AiChatMessage(
          text: metinKopya,
          isUser: true,
          photo: fotoKopya?.bytes,
          timestamp: DateTime.now(),
        ),
      );
      _ctrl.clear();
      _secilenFoto = null;
      _gonderiyor = true;
    });

    _scrollToBottom();

    var streamSuccess = false;

    try {
      final stream = widget._repo.streamMessage(
        message: metinKopya,
        photo: fotoKopya?.bytes,
        photoContentType: fotoKopya?.mimeType,
        photoFileName: fotoKopya?.name,
        fieldId: widget.fieldId,
        conversationId: _conversationId,
        history: List.unmodifiable(_mesajlar.take(_mesajlar.length - 1)),
        onConversationId: (id) {
          if (mounted && id.isNotEmpty) {
            _conversationId = id;
          }
        },
      );

      var assistantText = '';
      var hasAssistantMessage = false;

      await for (final chunk in stream) {
        if (!mounted) return;
        assistantText += chunk;
        setState(() {
          if (!hasAssistantMessage) {
            _mesajlar.add(
              AiChatMessage(
                text: assistantText,
                isUser: false,
                timestamp: DateTime.now(),
              ),
            );
            hasAssistantMessage = true;
          } else {
            _mesajlar[_mesajlar.length - 1] = AiChatMessage(
              text: assistantText,
              isUser: false,
              timestamp: DateTime.now(),
            );
          }
        });
        _scrollToBottom();
      }
      streamSuccess = hasAssistantMessage && assistantText.trim().isNotEmpty;
    } catch (e) {
      if (!mounted) return;
      setState(() {
        if (_mesajlar.isNotEmpty && _mesajlar.last.isUser) {
          _mesajlar.removeLast();
        }
        _ctrl.text = metinKopya;
        _secilenFoto = fotoKopya;
      });
      final errorMsg = e is ApiException
          ? e.message
          : 'AI Asistan bağlantısında bir hata oluştu.';
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text(errorMsg)));
    } finally {
      if (mounted) setState(() => _gonderiyor = false);
    }

    // Sesli yanıtlar tercihi açıksa tamamlanmış yeni yanıtı otomatik seslendir
    if (mounted &&
        !_isDisposed &&
        streamSuccess &&
        _voiceResponsesEnabled &&
        !_isListening &&
        !_voiceService.isListening &&
        _mesajlar.isNotEmpty &&
        !_mesajlar.last.isUser) {
      await _dinleTetikle(_mesajlar.length - 1);
    }
  }

  // ---------------------------------------------------------------------------
  // Sesli giriş (Voice Input / Push-to-Talk)
  // ---------------------------------------------------------------------------

  Future<void> _sesliGirisTetikle() async {
    if (_gonderiyor) return;

    if (_isListening) {
      await _durdurSesliGiris();
      return;
    }

    if (_speakingMessageIndex != null || _voiceOutputService.isSpeaking) {
      await _durdurSesliCikti();
    }

    _voiceBaseText = _ctrl.text;

    try {
      await _voiceService.startListening(
        onResult: (result) {
          if (_isDisposed || !mounted) return;
          final recognized = result.recognizedWords.trim();
          setState(() {
            if (recognized.isEmpty) {
              _ctrl.text = _voiceBaseText;
            } else {
              final separator =
                  (_voiceBaseText.isNotEmpty && !_voiceBaseText.endsWith(' '))
                  ? ' '
                  : '';
              _ctrl.text = '$_voiceBaseText$separator$recognized';
            }
            _ctrl.selection = TextSelection.fromPosition(
              TextPosition(offset: _ctrl.text.length),
            );
          });
        },
        onError: (error) {
          if (_isDisposed || !mounted) return;
          setState(() => _isListening = false);
          _hataGoster(error);
        },
        onListeningChanged: (listening) {
          if (_isDisposed || !mounted) return;
          setState(() => _isListening = listening);
        },
      );
      if (!_isDisposed && mounted) {
        setState(() => _isListening = true);
      }
    } catch (_) {
      if (!_isDisposed && mounted) {
        setState(() => _isListening = false);
        _hataMesajiGoster('Dinleme başlatılamadı. Lütfen tekrar deneyin.');
      }
    }
  }

  Future<void> _durdurSesliGiris() async {
    try {
      await _voiceService.stopListening();
    } catch (_) {}
    if (!_isDisposed && mounted) {
      setState(() => _isListening = false);
    }
  }

  void _hataGoster(VoiceInputException error) {
    final String mesaj;
    switch (error.type) {
      case VoiceInputErrorType.permissionDenied:
        mesaj =
            'Mikrofon izni verilmedi. Sesli giriş için mikrofon erişimine izin verin.';
        break;
      case VoiceInputErrorType.unavailable:
        mesaj = 'Bu cihazda konuşma tanıma kullanılamıyor.';
        break;
      case VoiceInputErrorType.initializeFailed:
        mesaj = 'Sesli giriş başlatılamadı.';
        break;
      case VoiceInputErrorType.startListeningFailed:
        mesaj = 'Dinleme başlatılamadı. Lütfen tekrar deneyin.';
        break;
      case VoiceInputErrorType.runtimeError:
      case VoiceInputErrorType.notListening:
        mesaj = 'Ses tanıma sırasında bir sorun oluştu.';
        break;
    }
    _hataMesajiGoster(mesaj);
  }

  void _hataMesajiGoster(String mesaj) {
    if (_isDisposed || !mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(mesaj)));
  }

  // ---------------------------------------------------------------------------
  // Sesli yanıt (Voice Output / Text-to-Speech)
  // ---------------------------------------------------------------------------

  Future<void> _dinleTetikle(int index) async {
    if (_isListening || _voiceService.isListening) return;
    if (index < 0 || index >= _mesajlar.length) return;
    final mesaj = _mesajlar[index];
    if (mesaj.isUser || mesaj.text.trim().isEmpty) return;

    // Aynı mesaj zaten okunuyorsa durdur
    if (_speakingMessageIndex == index) {
      await _durdurSesliCikti();
      return;
    }

    // Başka bir mesaj okunuyorsa önce durdur
    if (_speakingMessageIndex != null || _voiceOutputService.isSpeaking) {
      await _durdurSesliCikti();
    }

    // Lazy initialize
    if (!_voiceOutputInitialized) {
      final ok = await _voiceOutputService.initialize();
      if (_isDisposed || !mounted) return;
      if (!ok) {
        _hataMesajiGoster('Sesli yanıt sistemi başlatılamadı.');
        return;
      }
      _voiceOutputInitialized = true;
    }

    final currentSession = ++_ttsSession;
    setState(() => _speakingMessageIndex = index);

    try {
      await _voiceOutputService.speak(
        mesaj.text,
        onSpeakingChanged: (speaking) {
          if (_isDisposed || !mounted) return;
          if (_ttsSession != currentSession) return;
          if (!speaking) {
            setState(() => _speakingMessageIndex = null);
          }
        },
        onError: (error) {
          if (_isDisposed || !mounted) return;
          if (_ttsSession != currentSession) return;
          setState(() => _speakingMessageIndex = null);
          _hataGosterTts(error);
        },
      );
    } catch (_) {
      if (_isDisposed || !mounted) return;
      if (_ttsSession == currentSession) {
        setState(() => _speakingMessageIndex = null);
        _hataMesajiGoster('Yanıt seslendirilemedi. Lütfen tekrar deneyin.');
      }
    }
  }

  Future<void> _durdurSesliCikti() async {
    try {
      await _voiceOutputService.stop();
    } catch (_) {}
    if (!_isDisposed && mounted) {
      setState(() => _speakingMessageIndex = null);
    }
  }

  void _hataGosterTts(VoiceOutputException error) {
    final String mesaj;
    switch (error.type) {
      case VoiceOutputErrorType.unavailable:
        mesaj = 'Bu cihazda sesli yanıt kullanılamıyor.';
        break;
      case VoiceOutputErrorType.initializeFailed:
        mesaj = 'Sesli yanıt sistemi başlatılamadı.';
        break;
      case VoiceOutputErrorType.speakFailed:
        mesaj = 'Yanıt seslendirilemedi. Lütfen tekrar deneyin.';
        break;
      case VoiceOutputErrorType.stopFailed:
        mesaj = 'Seslendirme durdurulurken bir sorun oluştu.';
        break;
      case VoiceOutputErrorType.runtimeError:
        mesaj = 'Sesli yanıt sırasında bir sorun oluştu.';
        break;
    }
    _hataMesajiGoster(mesaj);
  }

  // ---------------------------------------------------------------------------
  // Sesli yanıt tercihleri (Voice Responses Preferences)
  // ---------------------------------------------------------------------------

  Future<void> _loadVoicePreferences() async {
    try {
      final enabled = await _preferences.getVoiceResponsesEnabled();
      if (_isDisposed || !mounted) return;
      setState(() => _voiceResponsesEnabled = enabled);
    } catch (_) {
      // Hata durumunda varsayılan false kalır
    }
  }

  Future<void> _setVoiceResponsesEnabled(bool enabled) async {
    if (_voiceResponsesEnabled == enabled) return;
    setState(() => _voiceResponsesEnabled = enabled);

    // Kapatılıyorsa ve aktif TTS varsa hemen durdur
    if (!enabled &&
        (_speakingMessageIndex != null || _voiceOutputService.isSpeaking)) {
      await _durdurSesliCikti();
    }

    try {
      await _preferences.setVoiceResponsesEnabled(enabled);
    } catch (_) {}
  }

  void _toggleVoiceResponses() {
    _setVoiceResponsesEnabled(!_voiceResponsesEnabled);
  }

  void _yeniSohbet() {
    if (_isListening) {
      _durdurSesliGiris();
    }
    if (_speakingMessageIndex != null || _voiceOutputService.isSpeaking) {
      _durdurSesliCikti();
    }
    setState(() {
      _mesajlar.clear();
      _conversationId = null;
      _secilenFoto = null;
      _ctrl.clear();
      _isListening = false;
      _voiceBaseText = '';
      _speakingMessageIndex = null;
    });
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
      appBar: AppBar(
        title: const Text('AI Tarla Asistanı'),
        actions: [
          if (_mesajlar.isNotEmpty)
            IconButton(
              icon: const Icon(Icons.refresh),
              tooltip: 'Yeni Sohbet',
              onPressed: _gonderiyor ? null : _yeniSohbet,
            ),
          PopupMenuButton<void>(
            icon: const Icon(Icons.more_vert),
            tooltip: 'Seçenekler',
            itemBuilder: (context) => [
              PopupMenuItem<void>(
                onTap: _toggleVoiceResponses,
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Expanded(
                      child: Text(
                        'Sesli yanıtlar',
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    const SizedBox(width: 8),
                    Semantics(
                      label: _voiceResponsesEnabled
                          ? 'Sesli yanıtları kapat'
                          : 'Sesli yanıtları aç',
                      child: IgnorePointer(
                        child: Switch(
                          value: _voiceResponsesEnabled,
                          materialTapTargetSize:
                              MaterialTapTargetSize.shrinkWrap,
                          onChanged: (_) {},
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ],
      ),
      body: Column(
        children: [
          Expanded(
            child: _mesajlar.isEmpty
                ? _BosKonusma(onOneri: _onOneriSec)
                : _MesajListesi(
                    mesajlar: _mesajlar,
                    gonderiyor: _gonderiyor,
                    scrollCtrl: _scrollCtrl,
                    speakingMessageIndex: _speakingMessageIndex,
                    canListen: !_isListening,
                    onDinle: _dinleTetikle,
                  ),
          ),
          _GirisBolumu(
            ctrl: _ctrl,
            secilenFoto: _secilenFoto?.bytes,
            onFotoKaldir: () => setState(() => _secilenFoto = null),
            onFotoEkle: _fotoCek,
            onGonder: _gonder,
            gonderiyor: _gonderiyor,
            isListening: _isListening,
            onSesliGiris: _sesliGirisTetikle,
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
    required this.speakingMessageIndex,
    required this.canListen,
    required this.onDinle,
  });

  final List<AiChatMessage> mesajlar;
  final bool gonderiyor;
  final ScrollController scrollCtrl;
  final int? speakingMessageIndex;
  final bool canListen;
  final ValueChanged<int> onDinle;

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
        final isStreaming = gonderiyor && i == mesajlar.length - 1;
        return _MesajBubble(
          mesaj: mesajlar[i],
          isSpeaking: speakingMessageIndex == i,
          canListen: canListen,
          isStreaming: isStreaming,
          onDinle: () => onDinle(i),
        );
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
  const _MesajBubble({
    required this.mesaj,
    this.isSpeaking = false,
    this.canListen = true,
    this.isStreaming = false,
    this.onDinle,
  });

  final AiChatMessage mesaj;
  final bool isSpeaking;
  final bool canListen;
  final bool isStreaming;
  final VoidCallback? onDinle;

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
              // AI yanıtları için Dinle / Durdur butonu
              if (!isUser && mesaj.text.isNotEmpty && !isStreaming) ...[
                const SizedBox(height: AppSpacing.xs),
                Align(
                  alignment: Alignment.centerRight,
                  child: Semantics(
                    label: isSpeaking ? 'Seslendirmeyi durdur' : 'Yanıtı dinle',
                    button: true,
                    enabled: canListen,
                    child: Tooltip(
                      message: isSpeaking
                          ? 'Seslendirmeyi durdur'
                          : 'Yanıtı dinle',
                      child: InkWell(
                        borderRadius: BorderRadius.circular(16),
                        onTap: canListen ? onDinle : null,
                        child: Padding(
                          padding: const EdgeInsets.symmetric(
                            horizontal: AppSpacing.xs,
                            vertical: 2,
                          ),
                          child: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Icon(
                                isSpeaking
                                    ? Icons.stop
                                    : Icons.volume_up_outlined,
                                size: 16,
                                color: canListen
                                    ? (isSpeaking
                                          ? theme.colorScheme.error
                                          : theme.colorScheme.primary)
                                    : theme.disabledColor,
                              ),
                              const SizedBox(width: 4),
                              Text(
                                isSpeaking ? 'Durdur' : 'Dinle',
                                style: theme.textTheme.bodySmall?.copyWith(
                                  color: canListen
                                      ? (isSpeaking
                                            ? theme.colorScheme.error
                                            : theme.colorScheme.primary)
                                      : theme.disabledColor,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
              ],
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
    required this.isListening,
    required this.onSesliGiris,
  });

  final TextEditingController ctrl;
  final Uint8List? secilenFoto;
  final VoidCallback onFotoKaldir;
  final VoidCallback onFotoEkle;
  final VoidCallback onGonder;
  final bool gonderiyor;
  final bool isListening;
  final VoidCallback onSesliGiris;

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
            // Dinleme durumu göstergesi
            if (isListening) ...[
              Padding(
                padding: const EdgeInsets.only(bottom: AppSpacing.xs),
                child: Row(
                  children: [
                    Container(
                      width: 8,
                      height: 8,
                      decoration: BoxDecoration(
                        color: theme.colorScheme.error,
                        shape: BoxShape.circle,
                      ),
                    ),
                    const SizedBox(width: AppSpacing.xs),
                    Text(
                      'Dinliyorum...',
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: theme.colorScheme.error,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                ),
              ),
            ],
            // Giriş satırı
            Row(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                // Fotoğraf ekleme butonu
                IconButton(
                  icon: const Icon(Icons.add_photo_alternate_outlined),
                  onPressed: (gonderiyor || isListening) ? null : onFotoEkle,
                  tooltip: 'Fotoğraf Ekle',
                ),
                // Metin alanı
                Expanded(
                  child: TextField(
                    controller: ctrl,
                    enabled: !gonderiyor,
                    readOnly: isListening,
                    minLines: 1,
                    maxLines: 4,
                    textCapitalization: TextCapitalization.sentences,
                    decoration: InputDecoration(
                      hintText: isListening
                          ? 'Dinleniyor... Konuşun'
                          : 'Sorunu veya sorununu anlat...',
                    ),
                  ),
                ),
                const SizedBox(width: AppSpacing.xs),
                // Mikrofon butonu (Push-to-Talk)
                Semantics(
                  label: isListening
                      ? 'Sesli girişi durdur'
                      : 'Sesli giriş başlat',
                  button: true,
                  child: IconButton(
                    icon: Icon(
                      isListening ? Icons.mic : Icons.mic_none,
                      color: isListening
                          ? theme.colorScheme.error
                          : theme.colorScheme.primary,
                    ),
                    onPressed: gonderiyor ? null : onSesliGiris,
                    tooltip: isListening
                        ? 'Sesli girişi durdur'
                        : 'Sesli giriş başlat',
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
                  onPressed: (gonderiyor || isListening) ? null : onGonder,
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
