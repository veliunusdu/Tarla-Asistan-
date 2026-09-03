import 'package:firebase_auth/firebase_auth.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'firebase_options.dart';
import 'models/notification_target.dart';
import 'features/activities/data/backend_faaliyet_repository.dart';
import 'features/ai_assistant/data/backend_ai_assistant_repository.dart';
import 'features/cases/data/backend_case_repository.dart';
import 'features/weather/data/backend_weather_repository.dart';
import 'features/fields/data/backend_farm_repository.dart';
import 'features/fields/data/backend_tarla_repository.dart';
import 'features/profile/data/backend_profile_repository.dart';
import 'screens/giris_ekrani.dart';
import 'screens/notification_target_screen.dart';
import 'screens/onboarding_ekrani.dart';
import 'screens/ana_ekran.dart';
import 'services/api_client.dart';
import 'services/auth_service.dart';
import 'services/database_helper.dart';
import 'services/firebase_auth_service.dart';
import 'services/firestore_user_profile_service.dart';
import 'services/notification_service.dart';
import 'services/post_login_initializer.dart';
import 'services/sync_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  _configureDevelopmentErrorLogging();
  try {
    await Firebase.initializeApp(
      options: DefaultFirebaseOptions.currentPlatform,
    );
  } catch (e) {
    debugPrint('Firebase initialization failed: $e');
  }
  final prefs = await SharedPreferences.getInstance();
  var firebaseReady = false;
  if (!kIsWeb) {
    try {
      firebaseReady = await initializeFirebaseMessaging();
    } catch (_) {
      firebaseReady = false;
    }
  }
  runApp(
    TarimAsistaniApp(
      isFirstRun: prefs.getBool('isFirstRun') ?? true,
      firebaseReady: firebaseReady,
    ),
  );
}

void _configureDevelopmentErrorLogging() {
  if (!kDebugMode) return;

  FlutterError.onError = (details) {
    FlutterError.presentError(details);
    debugPrint('Flutter framework error: ${details.exceptionAsString()}');
  };
  PlatformDispatcher.instance.onError = (error, stackTrace) {
    debugPrint('Unhandled application error: $error\n$stackTrace');
    return false;
  };
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
  late final AuthService _backendAuthService;
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
    _authService = FirebaseAuthService();
    _backendAuthService = AuthService();
    _apiClient = ApiClient(
      idTokenProvider: _backendAuthService.currentAccessToken,
      forceRefreshTokenProvider: _refreshBackendSession,
    );
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
      _postLoginFuture = _initializeAuthenticatedUser(user);
    }
    return _postLoginFuture!;
  }

  Future<void> _initializeAuthenticatedUser(User user) async {
    final idToken = await user.getIdToken();
    if (idToken == null || idToken.isEmpty) {
      throw StateError(
        'Firebase oturumu doğrulanamadı. Lütfen tekrar giriş yapın.',
      );
    }
    await _backendAuthService.authenticateWithFirebase(idToken);
    await _postLoginInitializer.initialize(
      uid: user.uid,
      phoneNumber: user.phoneNumber,
      email: user.email,
    );
  }

  Future<String?> _refreshBackendSession() async {
    final user = FirebaseAuth.instance.currentUser;
    if (user == null) return null;
    final idToken = await user.getIdToken(true);
    if (idToken == null || idToken.isEmpty) return null;
    return _backendAuthService.authenticateWithFirebase(idToken);
  }

  Future<void> _finishOnboarding() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool('isFirstRun', false);
    if (mounted) setState(() => _isFirstRun = false);
  }

  Future<void> _logout() async {
    try {
      await _notificationService.deactivateCurrentDevice();
    } catch (e) {
      debugPrint('Logout: device deactivation failed: $e');
    }

    await _notificationService.resetAfterLogout();

    try {
      await DatabaseHelper.instance.clearUserData();
    } catch (e) {
      debugPrint('Logout: clearUserData failed: $e');
    }

    try {
      await _backendAuthService.logout();
    } catch (e) {
      debugPrint('Logout: backend logout failed: $e');
    }

    try {
      await _authService.signOut();
    } catch (e) {
      debugPrint('Logout: auth signOut failed: $e');
    }

    if (mounted) {
      setState(() {
        _postLoginUid = null;
        _postLoginFuture = null;
      });
    }
  }

  @override
  void dispose() {
    _notificationService.dispose();
    _syncService.dispose();
    _apiClient.close();
    _backendAuthService.close();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      debugShowCheckedModeBanner: false,
      locale: const Locale('tr'),
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
      supportedLocales: const [Locale('tr'), Locale('en')],
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
                        debugPrint('Authentication initialization error: ${initialization.error}');
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
                                  if (kDebugMode && initialization.error != null) ...[
                                    const SizedBox(height: 8),
                                    Text(
                                      '${initialization.error}',
                                      style: TextStyle(
                                        fontSize: 12,
                                        color: Theme.of(context).colorScheme.error,
                                      ),
                                      textAlign: TextAlign.center,
                                    ),
                                  ],
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
                      final tarlaRepo = BackendTarlaRepository(
                        remote: BackendFarmRepository(apiClient: _apiClient),
                      );
                      return AnaEkran(
                        onLogout: _logout,
                        tarlaRepository: tarlaRepo,
                        faaliyetRepository: BackendFaaliyetRepository(
                          apiClient: _apiClient,
                          tarlaRepository: tarlaRepo,
                        ),
                        weatherRepository: BackendWeatherRepository(
                          apiClient: _apiClient,
                        ),
                        aiRepository: BackendAiAssistantRepository(
                          apiClient: _apiClient,
                        ),
                        profileRepository: BackendProfileRepository(_apiClient),
                        apiClient: _apiClient,
                        caseRepository: BackendCaseRepository(
                          apiClient: _apiClient,
                        ),
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
