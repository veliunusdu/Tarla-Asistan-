import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../services/api_client.dart';
import '../services/auth_service.dart';

class GirisEkrani extends StatefulWidget {
  const GirisEkrani({
    super.key,
    required this.authService,
    required this.onLoggedIn,
  });

  final AuthService authService;
  final Future<void> Function() onLoggedIn;

  @override
  State<GirisEkrani> createState() => _GirisEkraniState();
}

class _GirisEkraniState extends State<GirisEkrani> {
  final _formKey = GlobalKey<FormState>();
  final _phoneController = TextEditingController(text: '+90');
  final _otpController = TextEditingController();
  bool _otpSent = false;
  bool _loading = false;
  String? _error;

  @override
  void dispose() {
    _phoneController.dispose();
    _otpController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final phone = _normalizedPhone;
      if (!_otpSent) {
        final result = await widget.authService.requestOtp(phone);
        if (!mounted) return;
        setState(() {
          _otpSent = true;
          if (result.debugOtp != null) _otpController.text = result.debugOtp!;
        });
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              'Kod gönderildi. ${result.expiresIn} saniye geçerli.',
            ),
          ),
        );
      } else {
        await widget.authService.verifyOtp(phone, _otpController.text.trim());
        await widget.onLoggedIn();
      }
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  String get _normalizedPhone {
    final raw = _phoneController.text.trim();
    final digits = raw.replaceAll(RegExp(r'\D'), '');
    if (raw.startsWith('+')) return '+$digits';
    if (digits.length == 10) return '+90$digits';
    if (digits.startsWith('0')) return '+9$digits';
    return '+$digits';
  }

  @override
  Widget build(BuildContext context) {
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
                        _otpSent
                            ? 'Telefonunuza gelen 6 haneli kodu girin.'
                            : 'Telefon numaranızla güvenli giriş yapın.',
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: 32),
                      TextFormField(
                        controller: _phoneController,
                        enabled: !_otpSent && !_loading,
                        keyboardType: TextInputType.phone,
                        autofillHints: const [AutofillHints.telephoneNumber],
                        decoration: const InputDecoration(
                          labelText: 'Telefon numarası',
                          hintText: '+905551234567',
                          prefixIcon: Icon(Icons.phone_outlined),
                        ),
                        validator: (_) =>
                            RegExp(r'^\+90\d{10}$').hasMatch(_normalizedPhone)
                            ? null
                            : 'Numarayı +905551234567 biçiminde girin.',
                      ),
                      if (_otpSent) ...[
                        const SizedBox(height: 16),
                        TextFormField(
                          controller: _otpController,
                          enabled: !_loading,
                          keyboardType: TextInputType.number,
                          autofillHints: const [AutofillHints.oneTimeCode],
                          inputFormatters: [
                            FilteringTextInputFormatter.digitsOnly,
                            LengthLimitingTextInputFormatter(6),
                          ],
                          decoration: const InputDecoration(
                            labelText: 'Doğrulama kodu',
                            prefixIcon: Icon(Icons.password_outlined),
                          ),
                          validator: (value) => (value ?? '').length == 6
                              ? null
                              : '6 haneli doğrulama kodunu girin.',
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
                            : Icon(_otpSent ? Icons.login : Icons.sms_outlined),
                        label: Text(_otpSent ? 'Giriş yap' : 'Kod gönder'),
                      ),
                      if (_otpSent)
                        TextButton(
                          onPressed: _loading
                              ? null
                              : () => setState(() {
                                  _otpSent = false;
                                  _otpController.clear();
                                  _error = null;
                                }),
                          child: const Text('Telefon numarasını değiştir'),
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
