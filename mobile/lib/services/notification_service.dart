import 'dart:async';
import 'dart:io';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../config/app_config.dart';
import '../models/notification_target.dart';
import 'api_client.dart';

enum PushState { unavailable, notRequested, authorized, denied, failed }

@pragma('vm:entry-point')
Future<void> firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  if (!AppConfig.firebaseConfigured) return;
  await Firebase.initializeApp(options: AppConfig.firebaseOptions);
}

Future<bool> initializeFirebaseMessaging() async {
  if (!AppConfig.firebaseConfigured) return false;
  await Firebase.initializeApp(options: AppConfig.firebaseOptions);
  FirebaseMessaging.onBackgroundMessage(firebaseMessagingBackgroundHandler);
  return true;
}

class NotificationService {
  NotificationService(
    this._apiClient,
    this._navigatorKey,
    this._messengerKey,
    this._firebaseReady,
  );

  final ApiClient _apiClient;
  final GlobalKey<NavigatorState> _navigatorKey;
  final GlobalKey<ScaffoldMessengerState> _messengerKey;
  final bool _firebaseReady;
  final state = ValueNotifier<PushState>(PushState.notRequested);

  StreamSubscription<RemoteMessage>? _openedSubscription;
  StreamSubscription<RemoteMessage>? _foregroundSubscription;
  StreamSubscription<String>? _tokenSubscription;
  bool _initialized = false;

  Future<void> initializeAfterLogin() async {
    if (_initialized) {
      await registerCurrentToken();
      return;
    }
    if (!_firebaseReady) {
      state.value = PushState.unavailable;
      return;
    }
    _initialized = true;
    final messaging = FirebaseMessaging.instance;
    final prefs = await SharedPreferences.getInstance();
    final permissionWasRequested =
        prefs.getBool('notification_permission_requested') ?? false;
    NotificationSettings settings;
    try {
      settings = await messaging.getNotificationSettings();
      if (settings.authorizationStatus == AuthorizationStatus.notDetermined) {
        settings = await messaging.requestPermission(
          alert: true,
          badge: true,
          sound: true,
          provisional: false,
        );
        await prefs.setBool('notification_permission_requested', true);
      }
      if (settings.authorizationStatus == AuthorizationStatus.denied) {
        state.value = PushState.denied;
        if (permissionWasRequested) _showPermissionMessage();
        return;
      }
      state.value = PushState.authorized;
      await registerCurrentToken();
      _tokenSubscription = messaging.onTokenRefresh.listen(_registerToken);
      _openedSubscription = FirebaseMessaging.onMessageOpenedApp.listen(
        _openMessage,
      );
      _foregroundSubscription = FirebaseMessaging.onMessage.listen(
        _showForegroundMessage,
      );
      final initialMessage = await messaging.getInitialMessage();
      if (initialMessage != null) {
        WidgetsBinding.instance.addPostFrameCallback(
          (_) => _openMessage(initialMessage),
        );
      }
    } on FirebaseException {
      state.value = PushState.failed;
    }
  }

  Future<void> registerCurrentToken() async {
    if (!_firebaseReady || state.value == PushState.denied) return;
    try {
      final token = await FirebaseMessaging.instance.getToken();
      if (token != null && token.length >= 16) await _registerToken(token);
    } on FirebaseException {
      state.value = PushState.failed;
    }
  }

  Future<void> _registerToken(String token) async {
    try {
      final device = await _apiClient.postJson('/notifications/devices', {
        'token': token,
        'platform': Platform.isIOS ? 'IOS' : 'ANDROID',
      });
      final deviceId = device['id']?.toString();
      if (deviceId != null) {
        final prefs = await SharedPreferences.getInstance();
        await prefs.setString('notification_device_id', deviceId);
      }
    } on ApiException {
      // Token refresh and the next authenticated launch retry registration.
    }
  }

  Future<void> deactivateCurrentDevice() async {
    final prefs = await SharedPreferences.getInstance();
    final deviceId = prefs.getString('notification_device_id');
    if (deviceId == null) return;
    try {
      await _apiClient.sendQueued(
        method: 'DELETE',
        endpoint: '/notifications/devices/$deviceId',
        body: const <String, dynamic>{},
      );
      await prefs.remove('notification_device_id');
    } on ApiException {
      // Logout still proceeds; the server token can be replaced on the next login.
    }
  }

  void _openMessage(RemoteMessage message) {
    final target = NotificationTarget.fromData(message.data);
    if (target == null || target.type == NotificationTargetType.unknown) {
      _messengerKey.currentState?.showSnackBar(
        const SnackBar(content: Text('Bildirim bağlantısı geçersiz.')),
      );
      return;
    }
    _navigatorKey.currentState?.pushNamed(
      '/notification-target',
      arguments: target,
    );
  }

  void _showForegroundMessage(RemoteMessage message) {
    final title = message.notification?.title ?? 'Yeni bildirim';
    final target = NotificationTarget.fromData(message.data);
    _messengerKey.currentState
      ?..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text(title),
          action: target == null
              ? null
              : SnackBarAction(
                  label: 'Aç',
                  onPressed: () => _openMessage(message),
                ),
        ),
      );
  }

  void _showPermissionMessage() {
    _messengerKey.currentState?.showSnackBar(
      const SnackBar(
        content: Text(
          'Bildirim izni kapalı. Sistem ayarlarından istediğiniz zaman açabilirsiniz.',
        ),
      ),
    );
  }

  Future<void> dispose() async {
    await _openedSubscription?.cancel();
    await _foregroundSubscription?.cancel();
    await _tokenSubscription?.cancel();
    state.dispose();
  }
}
