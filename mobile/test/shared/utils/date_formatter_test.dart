import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/shared/utils/date_formatter.dart';

void main() {
  group('date_formatter', () {
    test('formatTarih formats correctly across months', () {
      expect(formatTarih(DateTime(2026, 1, 5)), '5 Oca 2026');
      expect(formatTarih(DateTime(2026, 5, 18)), '18 May 2026');
      expect(formatTarih(DateTime(2026, 9, 3)), '3 Eyl 2026');
      expect(formatTarih(DateTime(2026, 12, 31)), '31 Ara 2026');
    });

    test('trAylar contains 12 months', () {
      expect(trAylar, hasLength(12));
      expect(trAylar[0], 'Oca');
      expect(trAylar[11], 'Ara');
    });
  });
}
