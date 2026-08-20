import uuid
from typing import Literal, Protocol

import httpx
from pydantic import BaseModel, ConfigDict, Field, field_validator

from app.config import Settings


class ChatHistoryItem(BaseModel):
    role: Literal["user", "assistant"]
    content: str = Field(min_length=1, max_length=12000)

    @field_validator("content")
    @classmethod
    def require_content(cls, value: str) -> str:
        value = value.strip()
        if not value:
            raise ValueError("history içindeki content boş olamaz.")
        return value


class AIChatRequest(BaseModel):
    model_config = ConfigDict(arbitrary_types_allowed=True)

    message: str = Field(min_length=1, max_length=12000)
    field_id: str | None = Field(default=None, max_length=200)
    conversation_id: str | None = Field(default=None, max_length=200)
    history: list[ChatHistoryItem] | None = None
    photo_bytes: bytes | None = Field(default=None, exclude=True)
    photo_content_type: str | None = Field(default=None, exclude=True)

    @field_validator("message")
    @classmethod
    def require_message(cls, value: str) -> str:
        value = value.strip()
        if not value:
            raise ValueError("message boş olamaz.")
        return value

    @field_validator("field_id", "conversation_id")
    @classmethod
    def strip_identifiers(cls, value: str | None) -> str | None:
        if value is None:
            return None
        value = value.strip()
        return value or None


class AIChatResponse(BaseModel):
    reply: str
    conversation_id: str


class AIChatProvider(Protocol):
    def generate(self, request: AIChatRequest) -> AIChatResponse: ...


class AIChatProviderError(RuntimeError):
    pass


class AIChatPhotoUnsupported(AIChatProviderError):
    pass


class LocalAIChatProvider:
    """Deterministic provider used until a real AI service is configured."""

    def generate(self, request: AIChatRequest) -> AIChatResponse:
        conversation_id = request.conversation_id or str(uuid.uuid4())
        if request.photo_bytes is not None:
            reply = "Fotoğrafınızı aldım. AI sağlayıcısı bağlandığında ayrıntılı analiz dönecek."
        else:
            reply = "Mesajınızı aldım. AI sağlayıcısı bağlandığında ayrıntılı yanıt dönecek."
        return AIChatResponse(reply=reply, conversation_id=conversation_id)


class DeepSeekAIChatProvider:
    def __init__(self, *, api_key: str, model: str, timeout_seconds: float, base_url: str = "https://api.deepseek.com"):
        self.api_key = api_key
        self.model = model
        self.timeout_seconds = timeout_seconds
        self.base_url = base_url.rstrip("/")

    def generate(self, request: AIChatRequest) -> AIChatResponse:
        if request.photo_bytes is not None:
            raise AIChatPhotoUnsupported(
                "DeepSeek sağlayıcısı fotoğraf analizini desteklemiyor."
            )
        messages = [
            {
                "role": "system",
                "content": "Tarla Asistanı için Türkçe, güvenli ve uygulanabilir tarım önerileri ver.",
            },
            *(item.model_dump() for item in request.history or []),
            {"role": "user", "content": request.message},
        ]
        try:
            response = httpx.post(
                f"{self.base_url}/chat/completions",
                headers={
                    "Authorization": f"Bearer {self.api_key}",
                    "Content-Type": "application/json",
                },
                json={
                    "model": self.model,
                    "messages": messages,
                    "temperature": 0.2,
                },
                timeout=self.timeout_seconds,
            )
            response.raise_for_status()
            payload = response.json()
            reply = payload["choices"][0]["message"]["content"]
        except (httpx.HTTPError, KeyError, IndexError, TypeError, ValueError) as exc:
            raise AIChatProviderError("AI sağlayıcısına şu anda ulaşılamıyor.") from exc
        if not isinstance(reply, str) or not reply.strip():
            raise AIChatProviderError("AI sağlayıcısı geçerli bir yanıt döndürmedi.")
        return AIChatResponse(
            reply=reply.strip(),
            conversation_id=request.conversation_id or str(uuid.uuid4()),
        )


def create_ai_chat_provider(settings: Settings) -> AIChatProvider:
    if settings.ai_chat_provider == "local":
        return LocalAIChatProvider()
    if settings.ai_chat_provider == "deepseek":
        if not settings.deepseek_api_key:
            raise ValueError("DeepSeek için DEEPSEEK_API_KEY zorunludur.")
        return DeepSeekAIChatProvider(
            api_key=settings.deepseek_api_key,
            model=settings.deepseek_model,
            timeout_seconds=settings.deepseek_timeout_seconds,
            base_url=settings.deepseek_base_url,
        )
    raise ValueError("AI_CHAT_PROVIDER yalnızca 'local' veya 'deepseek' olabilir.")
