import uuid
from pathlib import Path
from typing import Any, Protocol

import httpx

from app.config import Settings


class PushProviderError(RuntimeError):
    def __init__(self, message: str, *, invalid_device_token: bool = False):
        super().__init__(message)
        self.invalid_device_token = invalid_device_token


class PushProvider(Protocol):
    def send(
        self,
        *,
        device_token: str,
        title: str,
        body: str,
        data: dict[str, str],
    ) -> str: ...


class NoopPushProvider:
    """Local/pilot-safe provider that records delivery without external traffic."""

    def send(
        self,
        *,
        device_token: str,
        title: str,
        body: str,
        data: dict[str, str],
    ) -> str:
        return f"local-{uuid.uuid4()}"


class HttpPushProvider:
    """Provider-neutral HTTPS adapter for an FCM/APNs gateway."""

    def __init__(self, url: str, token: str, timeout_seconds: float):
        self.url = url
        self.token = token
        self.timeout_seconds = timeout_seconds

    def send(
        self,
        *,
        device_token: str,
        title: str,
        body: str,
        data: dict[str, str],
    ) -> str:
        try:
            response = httpx.post(
                self.url,
                headers={"Authorization": f"Bearer {self.token}"},
                json={
                    "token": device_token,
                    "notification": {"title": title, "body": body},
                    "data": data,
                },
                timeout=self.timeout_seconds,
            )
            response.raise_for_status()
            payload = response.json()
        except (httpx.HTTPError, ValueError) as exc:
            raise PushProviderError("Push sağlayıcısına ulaşılamadı.") from exc
        message_id = payload.get("message_id") or payload.get("name")
        if not message_id:
            raise PushProviderError("Push sağlayıcısı mesaj kimliği döndürmedi.")
        return str(message_id)


class FirebasePushProvider:
    """Firebase Cloud Messaging adapter for Android and iOS device tokens."""

    def __init__(
        self,
        *,
        app: object,
        messaging_module: Any,
        invalid_token_error_types: tuple[type[Exception], ...] = (),
    ):
        self.app = app
        self.messaging = messaging_module
        self.invalid_token_error_types = invalid_token_error_types

    def send(
        self,
        *,
        device_token: str,
        title: str,
        body: str,
        data: dict[str, str],
    ) -> str:
        message = self.messaging.Message(
            token=device_token,
            notification=self.messaging.Notification(title=title, body=body),
            data=data,
        )
        try:
            return str(self.messaging.send(message, app=self.app))
        except Exception as exc:
            if self.invalid_token_error_types and isinstance(
                exc, self.invalid_token_error_types
            ):
                raise PushProviderError(
                    "FCM cihaz tokenı artık geçerli değil.", invalid_device_token=True
                ) from exc
            raise PushProviderError("FCM bildirimi gönderilemedi.") from exc


def create_firebase_push_provider(settings: Settings) -> FirebasePushProvider:
    if not settings.firebase_service_account_path:
        raise ValueError(
            "Firebase push sağlayıcısı için FIREBASE_SERVICE_ACCOUNT_PATH zorunludur."
        )
    credential_path = Path(settings.firebase_service_account_path)
    if not credential_path.is_file():
        raise ValueError("Firebase servis hesabı dosyası bulunamadı.")
    try:
        import firebase_admin
        from firebase_admin import credentials, messaging
    except ImportError as exc:
        raise ValueError("firebase-admin paketi kurulu değil.") from exc
    options = {"projectId": settings.firebase_project_id} if settings.firebase_project_id else None
    try:
        app = firebase_admin.initialize_app(
            credentials.Certificate(str(credential_path)), options=options, name="tarla-fcm"
        )
    except ValueError:
        app = firebase_admin.get_app("tarla-fcm")
    return FirebasePushProvider(
        app=app,
        messaging_module=messaging,
        invalid_token_error_types=(
            messaging.UnregisteredError,
            messaging.SenderIdMismatchError,
        ),
    )


def create_push_provider(settings: Settings) -> PushProvider:
    provider_name = settings.push_provider.casefold()
    if provider_name == "firebase":
        return create_firebase_push_provider(settings)
    if provider_name == "http":
        if not settings.push_gateway_url or not settings.push_gateway_token:
            raise ValueError(
                "HTTP push sağlayıcısı için PUSH_GATEWAY_URL ve "
                "PUSH_GATEWAY_TOKEN zorunludur."
            )
        if not settings.push_gateway_url.startswith("https://"):
            raise ValueError("Push gateway adresi HTTPS kullanmalıdır.")
        return HttpPushProvider(
            settings.push_gateway_url,
            settings.push_gateway_token,
            settings.push_timeout_seconds,
        )
    if provider_name == "noop":
        return NoopPushProvider()
    raise ValueError("PUSH_PROVIDER yalnızca 'noop', 'http' veya 'firebase' olabilir.")
