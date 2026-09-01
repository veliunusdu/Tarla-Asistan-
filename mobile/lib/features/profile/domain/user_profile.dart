class UserProfile {
  const UserProfile({
    required this.id,
    required this.phoneNumber,
    required this.role,
    required this.termsAccepted,
    required this.notificationsEnabled,
    this.fullName,
    this.province,
    this.district,
  });

  final String id;
  final String phoneNumber;
  final String role;
  final bool termsAccepted;
  final bool notificationsEnabled;
  final String? fullName;
  final String? province;
  final String? district;

  factory UserProfile.fromJson(Map<String, dynamic> json) => UserProfile(
    id: json['id'].toString(),
    phoneNumber: json['phone_number']?.toString() ?? '',
    role: json['role']?.toString() ?? 'FARMER',
    termsAccepted: json['terms_accepted'] as bool? ?? false,
    notificationsEnabled: json['notifications_enabled'] as bool? ?? true,
    fullName: json['full_name'] as String?,
    province: json['province'] as String?,
    district: json['district'] as String?,
  );
}

class UserProfileUpdate {
  const UserProfileUpdate({
    required this.fullName,
    required this.province,
    required this.district,
    required this.termsAccepted,
    required this.notificationsEnabled,
  });

  final String fullName;
  final String province;
  final String district;
  final bool termsAccepted;
  final bool notificationsEnabled;

  Map<String, dynamic> toJson() => {
    'full_name': fullName,
    'province': province,
    'district': district,
    'terms_accepted': termsAccepted,
    'notifications_enabled': notificationsEnabled,
  };
}
