class ApiConfig {
  ApiConfig({String? baseUrl})
    : baseUrl = _normalizeBaseUrl(
        baseUrl ??
            const String.fromEnvironment('API_BASE_URL', defaultValue: ''),
      );

  final String baseUrl;

  static String _normalizeBaseUrl(String value) {
    final normalized = value.trim().replaceFirst(RegExp(r'/+$'), '');

    if (normalized.isEmpty) {
      throw StateError(
        'API_BASE_URL tanımlı değil. Uygulamayı '
        '--dart-define=API_BASE_URL=https://... ile başlatın.',
      );
    }

    final uri = Uri.tryParse(normalized);
    if (uri == null ||
        !uri.hasScheme ||
        !uri.hasAuthority ||
        (uri.scheme != 'http' && uri.scheme != 'https')) {
      throw StateError(
        'API_BASE_URL geçerli bir http veya https adresi olmalıdır.',
      );
    }

    return normalized;
  }
}
