import 'package:firebase_auth/firebase_auth.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'firebase_options.dart';
import 'models/notification_target.dart';
import 'screens/giris_ekrani.dart';
import 'screens/notification_target_screen.dart';
import 'screens/onboarding_ekrani.dart';
import 'screens/ozet_ekrani.dart';
import 'services/api_client.dart';
import 'services/firebase_auth_service.dart';
import 'services/firestore_farm_repository.dart';
import 'services/firestore_user_profile_service.dart';
import 'services/notification_service.dart';
import 'services/post_login_initializer.dart';
import 'services/sync_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await Firebase.initializeApp(options: DefaultFirebaseOptions.currentPlatform);
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
      firebaseReady: firebaseReady,
    ),
  );
}

class TarimAsistaniApp extends StatefulWidget {
  const TarimAsistaniApp({
    super.key,
    required this.isFirstRun,
    required this.firebaseReady,
    this.authStateChanges,
  });

  final bool isFirstRun;
  final bool firebaseReady;
  final Stream<User?>? authStateChanges;

  @override
  State<TarimAsistaniApp> createState() => _TarimAsistaniAppState();
}

class _TarimAsistaniAppState extends State<TarimAsistaniApp> {
  final _navigatorKey = GlobalKey<NavigatorState>();
  final _messengerKey = GlobalKey<ScaffoldMessengerState>();
  late final ApiClient _apiClient;
  late final FirebaseAuthService _authService;
  late final SyncService _syncService;
  late final NotificationService _notificationService;
  late final PostLoginInitializer _postLoginInitializer;
  late bool _isFirstRun;
  String? _postLoginUid;
  Future<void>? _postLoginFuture;

  @override
  void initState() {
    super.initState();
    _isFirstRun = widget.isFirstRun;
    _apiClient = ApiClient();
    _authService = FirebaseAuthService();
    _syncService = SyncService(_apiClient);
    _notificationService = NotificationService(
      _apiClient,
      _navigatorKey,
      _messengerKey,
      widget.firebaseReady,
    );
    _postLoginInitializer = PostLoginInitializer(
      profileProvisioner: FirestoreUserProfileService(),
      initializeSync: _syncService.initialize,
      initializeNotifications: _notificationService.initializeAfterLogin,
    );
  }

  Stream<User?> get _authStates =>
      widget.authStateChanges ?? FirebaseAuth.instance.authStateChanges();

  Future<void> _initializationFor(User user) {
    if (_postLoginUid != user.uid || _postLoginFuture == null) {
      _postLoginUid = user.uid;
      _postLoginFuture = _postLoginInitializer.initialize(
        uid: user.uid,
        phoneNumber: user.phoneNumber,
      );
    }
    return _postLoginFuture!;
  }

  Future<void> _onLogout() async {
    await _notificationService.deactivateCurrentDevice();
    await _authService.signOut();
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
          : StreamBuilder<User?>(
              stream: _authStates,
              builder: (context, snapshot) {
                if (snapshot.connectionState == ConnectionState.waiting) {
                  return const Scaffold(
                    body: Center(child: CircularProgressIndicator()),
                  );
                }
                if (snapshot.hasData) {
                  final user = snapshot.data!;
                  return FutureBuilder<void>(
                    future: _initializationFor(user),
                    builder: (context, initialization) {
                      if (initialization.connectionState !=
                          ConnectionState.done) {
                        return const Scaffold(
                          body: Center(child: CircularProgressIndicator()),
                        );
                      }
                      if (initialization.hasError) {
                        return Scaffold(
                          body: Center(
                            child: Padding(
                              padding: const EdgeInsets.all(24),
                              child: Column(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  const Text(
                                    'Hesabınız hazırlanamadı. Bağlantınızı kontrol edip tekrar deneyin.',
                                    textAlign: TextAlign.center,
                                  ),
                                  const SizedBox(height: 16),
                                  FilledButton(
                                    onPressed: () => setState(() {
                                      _postLoginUid = null;
                                      _postLoginFuture = null;
                                    }),
                                    child: const Text('Tekrar dene'),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        );
                      }
                      return OzetEkrani(
                        syncService: _syncService,
                        apiClient: _apiClient,
                        onLogout: _onLogout,
                        repository: FirestoreFarmRepository(uid: user.uid),
                      );
                    },
                  );
                }
                _postLoginUid = null;
                _postLoginFuture = null;
                return GirisEkrani(authService: _authService);
              },
            ),
    );
  }
}
