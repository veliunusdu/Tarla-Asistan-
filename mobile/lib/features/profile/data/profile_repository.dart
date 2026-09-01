import '../domain/user_profile.dart';

abstract interface class ProfileRepository {
  Future<UserProfile> getCurrentProfile();
  Future<UserProfile> updateProfile(UserProfileUpdate update);
  Future<void> requestDeletion(String confirmation);
}
