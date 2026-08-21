enum KullaniciRolu { ciftci, ziraatMuhendisi }

class Kullanici {
  final String id;
  final String name;
  final String phone; // OTP için gerekli
  final KullaniciRolu role;

  Kullanici({
    required this.id,
    required this.name,
    required this.phone,
    required this.role,
  });

  factory Kullanici.fromJson(Map<String, dynamic> json) {
    return Kullanici(
      id: json['id'],
      name: json['name'],
      phone: json['phone'],
      role: KullaniciRolu.values.firstWhere(
        (e) => e.toString() == json['role'],
        orElse: () => KullaniciRolu.ciftci,
      ),
    );
  }

  Map<String, dynamic> toJson() {
    return {'id': id, 'name': name, 'phone': phone, 'role': role.toString()};
  }
}
