import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

void main() {
  test('Android manifest declares foreground location permissions', () {
    final manifest =
        File('android/app/src/main/AndroidManifest.xml').readAsStringSync();

    expect(manifest, contains('android.permission.ACCESS_FINE_LOCATION'));
    expect(manifest, contains('android.permission.ACCESS_COARSE_LOCATION'));
    expect(manifest, isNot(contains('android.permission.ACCESS_BACKGROUND_LOCATION')));
  });

  test('pubspec declares location and OpenStreetMap packages', () {
    final pubspec = File('pubspec.yaml').readAsStringSync();

    expect(pubspec, contains('geolocator: ^14.0.2'));
    expect(pubspec, contains('flutter_map: ^8.2.2'));
  });

  test('iOS plist explains foreground location usage', () {
    final plist = File('ios/Runner/Info.plist').readAsStringSync();

    expect(plist, contains('<key>NSLocationWhenInUseUsageDescription</key>'));
    expect(
      plist,
      contains(
        'Tarla konumunuzu kaydetmek ve yerel hava durumunu göstermek için '
        'konumunuza erişiyoruz.',
      ),
    );
  });
}
