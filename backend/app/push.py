import uuid
from typing import Protocol

import httpx

from app.config import Settings


class PushProviderError(RuntimeError):
    pass


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


def create_push_provider(settings: Settings) -> PushProvider:
    provider_name = settings.push_provider.casefold()
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
    raise ValueError("PUSH_PROVIDER yalnızca 'noop' veya 'http' olabilir.")
