import 'dart:async';
import 'package:firebase_auth/firebase_auth.dart';
import 'api_client.dart';

bool isTemporaryConnectionFailure(Object error) =>
    error is TimeoutException ||
    (error is ApiException &&
        error.retryable &&
        error.statusCode != 401 &&
        error.statusCode != 403) ||
    (error is FirebaseAuthException && error.code == 'network-request-failed');

String userErrorMessage(Object? error) {
  if (error is ApiException) {
    switch (error.statusCode) {
      case 401:
        return 'Oturumunuz sona erdi. Tekrar giriş yapın.';
      case 403:
        return 'Bu işlem için yetkiniz bulunmuyor.';
      case 404:
        return 'Kayıt bulunamadı. Listeyi yenileyin.';
      case 429:
        return 'Çok fazla istek gönderildi. Biraz sonra tekrar deneyin.';
      case 503:
        return 'Hizmet geçici olarak kullanılamıyor. Daha sonra tekrar deneyin.';
    }
    if ((error.statusCode ?? 0) >= 500) {
      return 'Sunucuda bir sorun oluştu. Daha sonra tekrar deneyin.';
    }
    return error.message;
  }
  if (error is TimeoutException)
    return 'Bağlantı zaman aşımına uğradı. Tekrar deneyin.';
  if (error is FirebaseAuthException) {
    if (error.code == 'network-request-failed')
      return 'İnternet bağlantısı kurulamadı. Bağlantınızı kontrol edin.';
    return 'Oturum doğrulanamadı. Tekrar giriş yapın.';
  }
  return 'İşlem tamamlanamadı. Lütfen tekrar deneyin.';
}
