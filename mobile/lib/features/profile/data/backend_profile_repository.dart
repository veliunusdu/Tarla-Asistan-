import '../../../services/api_client.dart';
import '../domain/user_profile.dart';

class BackendProfileRepository {
  const BackendProfileRepository(this._client);

  final ApiClient _client;

  Future<UserProfile> getCurrentProfile() async =>
      UserProfile.fromJson(await _client.getJson('/auth/me'));

  Future<UserProfile> updateProfile(UserProfileUpdate update) async =>
      UserProfile.fromJson(await _client.putJson('/users/me', update.toJson()));

  Future<void> requestDeletion(String confirmation) => _client.postJson(
    '/users/me/deletion-request',
    {'confirmation': confirmation},
  );
}
