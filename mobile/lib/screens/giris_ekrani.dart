import 'package:firebase_auth/firebase_auth.dart';
import 'package:flutter/material.dart';

import '../services/firebase_auth_service.dart';

enum _AuthMode { signIn, register }

class GirisEkrani extends StatefulWidget {
  const GirisEkrani({super.key, required this.authService});

  final FirebaseAuthService authService;

  @override
  State<GirisEkrani> createState() => _GirisEkraniState();
}

class _GirisEkraniState extends State<GirisEkrani> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();
  _AuthMode _mode = _AuthMode.signIn;
  bool _loading = false;
  String? _error;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    _confirmPasswordController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final email = _emailController.text.trim();
      final password = _passwordController.text;
      if (_mode == _AuthMode.register) {
        await widget.authService.register(email: email, password: password);
      } else {
        await widget.authService.signIn(email: email, password: password);
      }
    } on FirebaseAuthException catch (error) {
      if (mounted) setState(() => _error = _firebaseErrorMessage(error.code));
    } catch (_) {
      if (mounted) setState(() => _error = 'Giriş işlemi tamamlanamadı.');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  String _firebaseErrorMessage(String code) {
    switch (code) {
      case 'invalid-credential':
      case 'user-not-found':
      case 'wrong-password':
        return 'E-posta veya şifre hatalı.';
      case 'email-already-in-use':
        return 'Bu e-posta adresi zaten kayıtlı.';
      case 'weak-password':
        return 'Şifre en az 6 karakter olmalı.';
      case 'invalid-email':
        return 'Geçerli bir e-posta adresi girin.';
      case 'too-many-requests':
        return 'Çok fazla deneme yapıldı. Lütfen daha sonra tekrar deneyin.';
      default:
        return 'Kimlik doğrulama işlemi tamamlanamadı.';
    }
  }

  void _setMode(_AuthMode mode) {
    setState(() {
      _mode = mode;
      _error = null;
      _confirmPasswordController.clear();
    });
  }

  @override
  Widget build(BuildContext context) {
    final isRegister = _mode == _AuthMode.register;
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 480),
              child: Form(
                key: _formKey,
                child: AutofillGroup(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Semantics(
                        header: true,
                        child: Text(
                          'Tarla Asistanı',
                          style: Theme.of(context).textTheme.headlineLarge,
                          textAlign: TextAlign.center,
                        ),
                      ),
                      const SizedBox(height: 8),
                      Text(
                        isRegister
                            ? 'Hesabını oluştur ve tarlanı yönetmeye başla.'
                            : 'Hesabınla güvenli şekilde giriş yap.',
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: 32),
                      SegmentedButton<_AuthMode>(
                        segments: const [
                          ButtonSegment(
                            value: _AuthMode.signIn,
                            label: Text('Giriş yap'),
                            icon: Icon(Icons.login),
                          ),
                          ButtonSegment(
                            value: _AuthMode.register,
                            label: Text('Kayıt ol'),
                            icon: Icon(Icons.person_add_outlined),
                          ),
                        ],
                        selected: {_mode},
                        onSelectionChanged: _loading
                            ? null
                            : (selection) => _setMode(selection.first),
                      ),
                      const SizedBox(height: 24),
                      TextFormField(
                        controller: _emailController,
                        enabled: !_loading,
                        keyboardType: TextInputType.emailAddress,
                        autofillHints: const [AutofillHints.email],
                        decoration: const InputDecoration(
                          labelText: 'E-posta adresi',
                          hintText: 'ornek@email.com',
                          prefixIcon: Icon(Icons.email_outlined),
                        ),
                        validator: (value) =>
                            RegExp(
                              r'^[^@\s]+@[^@\s]+\.[^@\s]+$',
                            ).hasMatch((value ?? '').trim())
                            ? null
                            : 'Geçerli bir e-posta adresi girin.',
                      ),
                      const SizedBox(height: 16),
                      TextFormField(
                        controller: _passwordController,
                        enabled: !_loading,
                        obscureText: true,
                        autofillHints: [
                          isRegister
                              ? AutofillHints.newPassword
                              : AutofillHints.password,
                        ],
                        decoration: const InputDecoration(
                          labelText: 'Şifre',
                          prefixIcon: Icon(Icons.lock_outline),
                        ),
                        validator: (value) => (value ?? '').length >= 6
                            ? null
                            : 'Şifre en az 6 karakter olmalı.',
                      ),
                      if (isRegister) ...[
                        const SizedBox(height: 16),
                        TextFormField(
                          controller: _confirmPasswordController,
                          enabled: !_loading,
                          obscureText: true,
                          autofillHints: const [AutofillHints.newPassword],
                          decoration: const InputDecoration(
                            labelText: 'Şifre tekrarı',
                            prefixIcon: Icon(Icons.lock_outline),
                          ),
                          validator: (value) =>
                              value == _passwordController.text
                              ? null
                              : 'Şifreler aynı değil.',
                        ),
                      ],
                      if (_error != null) ...[
                        const SizedBox(height: 16),
                        Semantics(
                          liveRegion: true,
                          child: Text(
                            _error!,
                            style: TextStyle(
                              color: Theme.of(context).colorScheme.error,
                            ),
                          ),
                        ),
                      ],
                      const SizedBox(height: 24),
                      FilledButton.icon(
                        onPressed: _loading ? null : _submit,
                        icon: _loading
                            ? const SizedBox.square(
                                dimension: 20,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                ),
                              )
                            : Icon(isRegister ? Icons.person_add : Icons.login),
                        label: Text(isRegister ? 'Hesap oluştur' : 'Giriş yap'),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
