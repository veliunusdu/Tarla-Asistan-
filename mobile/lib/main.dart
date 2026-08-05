import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'models/notification_target.dart';
import 'screens/giris_ekrani.dart';
import 'screens/notification_target_screen.dart';
import 'screens/onboarding_ekrani.dart';
import 'screens/ozet_ekrani.dart';
import 'services/api_client.dart';
import 'services/auth_service.dart';
import 'services/notification_service.dart';
import 'services/sync_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final prefs = await SharedPreferences.getInstance();
  var firebaseReady = false;
  try {
    firebaseReady = await initializeFirebaseMessaging();
  } on FirebaseException {
    firebaseReady = false;
  }
  runApp(
    TarimAsistaniApp(
      isFirstRun: prefs.getBool('isFirstRun') ?? true,
      isAuthenticated: (prefs.getString('access_token') ?? '').isNotEmpty,
      firebaseReady: firebaseReady,
    ),
  );
}

class TarimAsistaniApp extends StatefulWidget {
  const TarimAsistaniApp({
    super.key,
    required this.isFirstRun,
    required this.isAuthenticated,
    required this.firebaseReady,
  });

  final bool isFirstRun;
  final bool isAuthenticated;
  final bool firebaseReady;

  @override
  State<TarimAsistaniApp> createState() => _TarimAsistaniAppState();
}

class _TarimAsistaniAppState extends State<TarimAsistaniApp> {
  final _navigatorKey = GlobalKey<NavigatorState>();
  final _messengerKey = GlobalKey<ScaffoldMessengerState>();
  late final ApiClient _apiClient;
  late final AuthService _authService;
  late final SyncService _syncService;
  late final NotificationService _notificationService;
  late bool _isFirstRun;
  late bool _isAuthenticated;

  @override
  void initState() {
    super.initState();
    _isFirstRun = widget.isFirstRun;
    _isAuthenticated = widget.isAuthenticated;
    _apiClient = ApiClient();
    _authService = AuthService();
    _syncService = SyncService(_apiClient);
    _notificationService = NotificationService(
      _apiClient,
      _navigatorKey,
      _messengerKey,
      widget.firebaseReady,
    );
    if (_isAuthenticated) {
      WidgetsBinding.instance.addPostFrameCallback((_) => _startServices());
    }
  }

  Future<void> _startServices() async {
    await _syncService.initialize();
    await _notificationService.initializeAfterLogin();
  }

  Future<void> _onLoggedIn() async {
    setState(() => _isAuthenticated = true);
    await _startServices();
  }

  Future<void> _onLogout() async {
    await _notificationService.deactivateCurrentDevice();
    await _authService.logout();
    if (mounted) setState(() => _isAuthenticated = false);
  }

  Future<void> _finishOnboarding() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool('isFirstRun', false);
    if (mounted) setState(() => _isFirstRun = false);
  }

  @override
  void dispose() {
    _notificationService.dispose();
    _syncService.dispose();
    _apiClient.close();
    _authService.close();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      debugShowCheckedModeBanner: false,
      navigatorKey: _navigatorKey,
      scaffoldMessengerKey: _messengerKey,
      title: 'Tarla Asistanı',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF1B5E20),
          brightness: Brightness.light,
        ),
        useMaterial3: true,
        visualDensity: VisualDensity.standard,
        inputDecorationTheme: const InputDecorationTheme(
          border: OutlineInputBorder(),
          filled: true,
        ),
        filledButtonTheme: FilledButtonThemeData(
          style: FilledButton.styleFrom(
            minimumSize: const Size(48, 52),
            textStyle: const TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
      ),
      onGenerateRoute: (settings) {
        if (settings.name == '/notification-target' &&
            settings.arguments is NotificationTarget) {
          return MaterialPageRoute<void>(
            builder: (_) => NotificationTargetScreen(
              target: settings.arguments! as NotificationTarget,
              apiClient: _apiClient,
            ),
          );
        }
        return null;
      },
      home: _isFirstRun
          ? OnboardingEkrani(onFinished: _finishOnboarding)
          : !_isAuthenticated
          ? GirisEkrani(authService: _authService, onLoggedIn: _onLoggedIn)
          : OzetEkrani(
              syncService: _syncService,
              apiClient: _apiClient,
              onLogout: _onLogout,
            ),
    );
  }
}
