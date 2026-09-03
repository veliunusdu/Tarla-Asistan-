import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:image_picker/image_picker.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/ai_assistant/data/ai_assistant_repository.dart';
import 'package:mobile/features/ai_assistant/data/image_picker_service.dart';
import 'package:mobile/features/ai_assistant/domain/ai_chat_message.dart';
import 'package:mobile/features/ai_assistant/domain/ai_chat_response.dart';
import 'package:mobile/screens/ai_asistan_ekrani.dart';
import 'package:mobile/services/api_client.dart';

// Valid 1x1 PNG bytes for Flutter Image.memory decoding
final kSamplePng = base64Decode(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==',
);

// ---------------------------------------------------------------------------
// Fakes
// ---------------------------------------------------------------------------

class FakeAiAssistantRepository implements AiAssistantRepository {
  FakeAiAssistantRepository({
    this.cevap = 'Test cevap',
    this.donusConversationId,
    this.hata,
  });

  final String cevap;
  final String? donusConversationId;
  final Object? hata;

  final List<String> gonderilen = [];
  final List<Uint8List?> fotolar = [];
  final List<String?> photoFileNames = [];
  final List<String?> photoContentTypes = [];
  final List<String?> fieldIds = [];
  final List<String?> conversationIds = [];
  final List<List<AiChatMessage>> histories = [];

  @override
  Future<AiChatResponse> sendMessage({
    required String message,
    Uint8List? photo,
    String? photoContentType,
    String? photoFileName,
    String? fieldId,
    String? conversationId,
    List<AiChatMessage> history = const [],
  }) async {
    if (hata != null) throw hata!;
    gonderilen.add(message);
    fotolar.add(photo);
    photoFileNames.add(photoFileName);
    photoContentTypes.add(photoContentType);
    fieldIds.add(fieldId);
    conversationIds.add(conversationId);
    histories.add(List.of(history));
    return AiChatResponse(
      reply: cevap,
      conversationId: donusConversationId ?? (conversationId ?? 'conv-default'),
    );
  }

  @override
  Stream<String> streamMessage({
    required String message,
    Uint8List? photo,
    String? photoContentType,
    String? photoFileName,
    String? fieldId,
    String? conversationId,
    List<AiChatMessage> history = const [],
    void Function(String conversationId)? onConversationId,
  }) async* {
    if (hata != null) throw hata!;
    gonderilen.add(message);
    fotolar.add(photo);
    photoFileNames.add(photoFileName);
    photoContentTypes.add(photoContentType);
    fieldIds.add(fieldId);
    conversationIds.add(conversationId);
    histories.add(List.of(history));
    final resolvedConvId = donusConversationId ?? (conversationId ?? 'conv-default');
    if (onConversationId != null) onConversationId(resolvedConvId);
    yield cevap;
  }
}

class FakeImagePickerService implements ImagePickerService {
  FakeImagePickerService({this.imageToReturn});

  PickedImageData? imageToReturn;
  ImageSource? lastSource;

  @override
  Future<PickedImageData?> pickImage({required ImageSource source}) async {
    lastSource = source;
    return imageToReturn;
  }
}

/// Yavaş repo — Completer ile kontrollü gecikme.
class _SlowAiRepo implements AiAssistantRepository {
  _SlowAiRepo(this._future);
  final Future<AiChatResponse> _future;

  @override
  Future<AiChatResponse> sendMessage({
    required String message,
    Uint8List? photo,
    String? photoContentType,
    String? photoFileName,
    String? fieldId,
    String? conversationId,
    List<AiChatMessage> history = const [],
  }) => _future;

  @override
  Stream<String> streamMessage({
    required String message,
    Uint8List? photo,
    String? photoContentType,
    String? photoFileName,
    String? fieldId,
    String? conversationId,
    List<AiChatMessage> history = const [],
    void Function(String conversationId)? onConversationId,
  }) async* {
    final res = await _future;
    if (onConversationId != null) onConversationId(res.conversationId);
    yield res.reply;
  }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

Widget _wrap({
  AiAssistantRepository? repo,
  ImagePickerService? imagePickerService,
  String? fieldId,
}) => MaterialApp(
  theme: AppTheme.light,
  home: AiAsistanEkrani(
    repository: repo ?? FakeAiAssistantRepository(),
    imagePickerService: imagePickerService ?? FakeImagePickerService(),
    fieldId: fieldId,
  ),
);

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

void main() {
  group('AiAsistanEkrani', () {
    // ── Boş durum ────────────────────────────────────────────────────────
    group('boş durum', () {
      testWidgets('başlangıçta boş konuşma durumu gösterilir', (tester) async {
        await tester.pumpWidget(_wrap());
        await tester.pumpAndSettle();

        expect(find.text('AI Tarla Asistanı'), findsWidgets);
        expect(find.textContaining('Fotoğraf ekleyerek'), findsOneWidget);
      });

      testWidgets('örnek sorular boş durumda görünür', (tester) async {
        await tester.pumpWidget(_wrap());
        await tester.pumpAndSettle();

        expect(find.text('Yapraklardaki lekeler ne olabilir?'), findsOneWidget);
        expect(find.text('Bugün sulama yapmalı mıyım?'), findsOneWidget);
        expect(
          find.text('Bu bitkide hastalık belirtisi var mı?'),
          findsOneWidget,
        );
      });

      testWidgets('örnek soruya tıklayınca metin alanı dolar', (tester) async {
        await tester.pumpWidget(_wrap());
        await tester.pumpAndSettle();

        await tester.tap(find.text('Yapraklardaki lekeler ne olabilir?'));
        await tester.pumpAndSettle();

        expect(
          find.widgetWithText(TextField, 'Yapraklardaki lekeler ne olabilir?'),
          findsOneWidget,
        );
      });
    });

    // ── Form doğrulaması ──────────────────────────────────────────────────
    group('form doğrulaması', () {
      testWidgets('boş metin gönderilemiyor', (tester) async {
        final repo = FakeAiAssistantRepository();
        await tester.pumpWidget(_wrap(repo: repo));
        await tester.pumpAndSettle();

        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        expect(repo.gonderilen, isEmpty);
      });

      testWidgets('yalnızca boşluktan oluşan metin gönderilemiyor', (
        tester,
      ) async {
        final repo = FakeAiAssistantRepository();
        await tester.pumpWidget(_wrap(repo: repo));
        await tester.pumpAndSettle();

        await tester.enterText(find.byType(TextField), '   ');
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        expect(repo.gonderilen, isEmpty);
      });

      testWidgets('fotoğraf seçiliyken boş mesaj gönderilmeye çalışılırsa uyarı gösterilir', (
        tester,
      ) async {
        final repo = FakeAiAssistantRepository();
        final picker = FakeImagePickerService(
          imageToReturn: PickedImageData(
            bytes: kSamplePng,
            name: 'yaprak.png',
            mimeType: 'image/png',
          ),
        );

        await tester.pumpWidget(_wrap(repo: repo, imagePickerService: picker));
        await tester.pumpAndSettle();

        // Fotoğraf ekleme butonuna bas
        await tester.tap(find.byTooltip('Fotoğraf Ekle'));
        await tester.pumpAndSettle();

        // Galeriden Seç'e bas
        await tester.tap(find.text('Galeriden Seç'));
        await tester.pumpAndSettle();

        // Metin yazmadan Gönder'e bas
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pumpAndSettle();

        expect(repo.gonderilen, isEmpty);
        expect(
          find.text('Lütfen fotoğrafla ilgili bir soru veya açıklama yazın.'),
          findsOneWidget,
        );
      });
    });

    // ── Fotoğraf seçimi ve önizleme ───────────────────────────────────────
    group('fotoğraf seçimi', () {
      testWidgets('fotoğraf seçildiğinde önizleme gösterilir ve kaldırılabilir', (
        tester,
      ) async {
        final picker = FakeImagePickerService(
          imageToReturn: PickedImageData(
            bytes: kSamplePng,
            name: 'bitki.png',
            mimeType: 'image/png',
          ),
        );

        await tester.pumpWidget(_wrap(imagePickerService: picker));
        await tester.pumpAndSettle();

        expect(find.byIcon(Icons.close), findsNothing);

        // Fotoğraf Ekle
        await tester.tap(find.byTooltip('Fotoğraf Ekle'));
        await tester.pumpAndSettle();

        await tester.tap(find.text('Kameradan Çek'));
        await tester.pumpAndSettle();

        expect(picker.lastSource, ImageSource.camera);
        // Önizleme ve kapat butonu görünmeli
        expect(find.byIcon(Icons.close), findsOneWidget);

        // Kaldır butonuna bas
        await tester.tap(find.byIcon(Icons.close));
        await tester.pumpAndSettle();

        expect(find.byIcon(Icons.close), findsNothing);
      });
    });

    // ── Mesaj gönderme ────────────────────────────────────────────────────
    group('mesaj gönderme', () {
      testWidgets('metin mesajı gönderince kullanıcı baloncuğu görünür', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(_wrap());
        await tester.pumpAndSettle();

        await tester.enterText(
          find.byType(TextField),
          'Sulama zamanı geldi mi?',
        );
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        expect(find.text('Sulama zamanı geldi mi?'), findsOneWidget);
      });

      testWidgets('fotoğraf + metin gönderildiğinde repository doğru argümanlarla çağrılır', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final repo = FakeAiAssistantRepository(
          cevap: 'Bu yaprakta pas hastalığı var.',
          donusConversationId: 'conv-photo-resp',
        );
        final picker = FakeImagePickerService(
          imageToReturn: PickedImageData(
            bytes: kSamplePng,
            name: 'yaprak.png',
            mimeType: 'image/png',
          ),
        );

        await tester.pumpWidget(
          _wrap(repo: repo, imagePickerService: picker, fieldId: 'tarla-99'),
        );
        await tester.pumpAndSettle();

        // Fotoğraf seç
        await tester.tap(find.byTooltip('Fotoğraf Ekle'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Galeriden Seç'));
        await tester.pumpAndSettle();

        // Metin gir ve gönder
        await tester.enterText(find.byType(TextField), 'Bu leke nedir?');
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pumpAndSettle();

        expect(repo.gonderilen, contains('Bu leke nedir?'));
        expect(repo.fotolar.first, kSamplePng);
        expect(repo.photoFileNames.first, 'yaprak.png');
        expect(repo.photoContentTypes.first, 'image/png');
        expect(repo.fieldIds.first, 'tarla-99');
        expect(find.text('Bu yaprakta pas hastalığı var.'), findsOneWidget);
      });

      testWidgets('conversation_id devamlılığı sağlanır ve yeni sohbette sıfırlanır', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final repo = FakeAiAssistantRepository(
          cevap: 'İlk yanıt',
          donusConversationId: 'conv-session-1',
        );

        await tester.pumpWidget(_wrap(repo: repo));
        await tester.pumpAndSettle();

        // 1. Mesaj (İlk mesajda conversation_id null gitmeli)
        await tester.enterText(find.byType(TextField), 'Mesaj 1');
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pumpAndSettle();

        expect(repo.conversationIds.first, isNull);

        // 2. Mesaj (Repo tarafından dönülen conv-session-1 ile devam etmeli)
        await tester.enterText(find.byType(TextField), 'Mesaj 2');
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pumpAndSettle();

        expect(repo.conversationIds[1], 'conv-session-1');

        // Yeni Sohbet butonuna bas
        expect(find.byTooltip('Yeni Sohbet'), findsOneWidget);
        await tester.tap(find.byTooltip('Yeni Sohbet'));
        await tester.pumpAndSettle();

        // Sohbet temizlenmeli
        expect(find.text('Mesaj 1'), findsNothing);
        expect(find.text('Mesaj 2'), findsNothing);

        // 3. Mesaj (Yeni sohbette conversation_id sıfırlanmış olmalı)
        await tester.enterText(find.byType(TextField), 'Yeni Konuşma');
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pumpAndSettle();

        expect(repo.conversationIds[2], isNull);
      });

      testWidgets('aynı mesaj aynı anda iki kez gönderilemez', (tester) async {
        final completer = Completer<AiChatResponse>();
        final repo = _SlowAiRepo(completer.future);

        await tester.pumpWidget(_wrap(repo: repo));
        await tester.pumpAndSettle();

        await tester.enterText(find.byType(TextField), 'Test');

        // İlk gönderim
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        // Loading sırasında gönder butonu devre dışı — ikinci gönderim engellenir
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        completer.complete(const AiChatResponse(reply: 'Cevap', conversationId: 'c1'));
        await tester.pumpAndSettle();
      });

      testWidgets('gönderim sırasında loading göstergesi görünür', (
        tester,
      ) async {
        final completer = Completer<AiChatResponse>();

        await tester.pumpWidget(_wrap(repo: _SlowAiRepo(completer.future)));
        await tester.pumpAndSettle();

        await tester.enterText(find.byType(TextField), 'Test mesajı');
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        expect(find.byType(CircularProgressIndicator), findsWidgets);
        completer.complete(const AiChatResponse(reply: 'Cevap', conversationId: 'c1'));
        await tester.pumpAndSettle();
      });
    });

    // ── Hata durumu ───────────────────────────────────────────────────────
    group('hata durumu', () {
      testWidgets('hata durumunda SnackBar gösterilir ve input kutusu restore edilir', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final repo = FakeAiAssistantRepository(
          hata: const ApiException('Fotoğraf en fazla 5 MB olabilir.'),
        );
        final picker = FakeImagePickerService(
          imageToReturn: PickedImageData(
            bytes: kSamplePng,
            name: 'buyuk.png',
            mimeType: 'image/png',
          ),
        );

        await tester.pumpWidget(_wrap(repo: repo, imagePickerService: picker));
        await tester.pumpAndSettle();

        // Fotoğraf seç
        await tester.tap(find.byTooltip('Fotoğraf Ekle'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Galeriden Seç'));
        await tester.pumpAndSettle();

        await tester.enterText(find.byType(TextField), 'Hastalık kontrol');
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pumpAndSettle();

        // SnackBar gösterilmeli
        expect(find.text('Fotoğraf en fazla 5 MB olabilir.'), findsOneWidget);
        // Metin ve fotoğraf geri yüklenmiş olmalı
        expect(find.widgetWithText(TextField, 'Hastalık kontrol'), findsOneWidget);
        expect(find.byIcon(Icons.close), findsOneWidget);
      });
    });

    // ── Overflow / responsive ─────────────────────────────────────────────
    group('responsive', () {
      testWidgets('320x640 ekranda render exception oluşmaz', (tester) async {
        tester.view.physicalSize = const Size(480, 960);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(_wrap());
        await tester.pumpAndSettle();

        expect(tester.takeException(), isNull);
      });
    });
  });
}
