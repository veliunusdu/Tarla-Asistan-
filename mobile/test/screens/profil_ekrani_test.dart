import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/profile/data/profile_repository.dart';
import 'package:mobile/features/profile/domain/user_profile.dart';
import 'package:mobile/screens/profil_ekrani.dart';

class _ProfileRepository implements ProfileRepository {
  UserProfileUpdate? update;
  @override
  Future<UserProfile> getCurrentProfile() async => const UserProfile(
    id: 'user-1',
    phoneNumber: '+905551112233',
    role: 'FARMER',
    termsAccepted: true,
    notificationsEnabled: true,
    fullName: 'Ayşe Demir',
    province: 'Ankara',
    district: 'Çankaya',
  );

  @override
  Future<void> requestDeletion(String confirmation) async {}

  @override
  Future<UserProfile> updateProfile(UserProfileUpdate update) async {
    this.update = update;
    return UserProfile(
      id: 'user-1',
      phoneNumber: '+905551112233',
      role: 'FARMER',
      termsAccepted: true,
      notificationsEnabled: update.notificationsEnabled,
      fullName: 'Ayşe Demir',
      province: 'Ankara',
      district: 'Çankaya',
    );
  }
}

void main() {
  testWidgets('confirms before signing out', (tester) async {
    var calls = 0;
    await tester.pumpWidget(
      MaterialApp(home: ProfilEkrani(onLogout: () async => calls++)),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Çıkış yap'));
    await tester.pumpAndSettle();

    expect(find.text('Çıkış yapmak istiyor musunuz?'), findsOneWidget);
    expect(calls, 0);
  });

  testWidgets('loads account information from the backend repository', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(home: ProfilEkrani(repository: _ProfileRepository())),
    );
    await tester.pumpAndSettle();

    expect(find.text('Ayşe Demir'), findsOneWidget);
    expect(find.text('+905551112233'), findsOneWidget);
    expect(find.text('Ankara, Çankaya'), findsOneWidget);
  });

  testWidgets('saves notification preference to the backend', (tester) async {
    final repository = _ProfileRepository();
    await tester.pumpWidget(
      MaterialApp(home: ProfilEkrani(repository: repository)),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.byType(Switch));
    await tester.pump();
    await tester.pump();

    expect(repository.update?.notificationsEnabled, isFalse);
  });
}
