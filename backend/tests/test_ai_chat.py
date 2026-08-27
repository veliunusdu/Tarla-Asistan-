import json

import httpx
import pytest
from fastapi.testclient import TestClient

from app.ai_chat import (
    AIChatProviderError,
    AIChatRequest,
    DeepSeekAIChatProvider,
    GeminiAIChatProvider,
)
from tests.test_auth import login


def auth_headers(client: TestClient) -> dict[str, str]:
    auth = login(client)
    return {"Authorization": f"Bearer {auth['access_token']}"}


def test_json_chat_returns_reply_and_conversation_id(client: TestClient):
    response = client.post(
        "/api/v1/ai/chat",
        headers=auth_headers(client),
        json={"message": "Yapraklar sararıyor."},
    )
    assert response.status_code == 200
    body = response.json()
    assert body["reply"]
    assert body["conversation_id"]


def test_multipart_chat_accepts_png(client: TestClient):
    response = client.post(
        "/api/v1/ai/chat",
        headers=auth_headers(client),
        data={"message": "Fotoğrafı incele."},
        files={"photo": ("leaf.png", b"\x89PNG\r\n", "image/png")},
    )
    assert response.status_code == 200


def test_chat_accepts_multipart_history_json(client: TestClient):
    response = client.post(
        "/api/v1/ai/chat",
        headers=auth_headers(client),
        data={
            "message": "Devam et.",
            "history": json.dumps([{"role": "user", "content": "Önceki soru"}]),
        },
    )
    assert response.status_code == 200


def test_chat_rejects_invalid_history_role(client: TestClient):
    response = client.post(
        "/api/v1/ai/chat",
        headers=auth_headers(client),
        json={
            "message": "Soru",
            "history": [{"role": "system", "content": "x"}],
        },
    )
    assert response.status_code == 422


def test_chat_rejects_non_image_photo(client: TestClient):
    response = client.post(
        "/api/v1/ai/chat",
        headers=auth_headers(client),
        data={"message": "İncele."},
        files={"photo": ("notes.txt", b"text", "text/plain")},
    )
    assert response.status_code == 415


def test_chat_rejects_photo_over_5_mib(client: TestClient):
    response = client.post(
        "/api/v1/ai/chat",
        headers=auth_headers(client),
        data={"message": "İncele."},
        files={
            "photo": (
                "large.png",
                b"x" * (5 * 1024 * 1024 + 1),
                "image/png",
            )
        },
    )
    assert response.status_code == 413


def test_chat_requires_authentication(client: TestClient):
    response = client.post("/api/v1/ai/chat", json={"message": "Soru"})
    assert response.status_code == 401


def test_deepseek_provider_forwards_history_and_returns_reply(monkeypatch):
    captured: dict[str, object] = {}

    def fake_post(url, *, headers, json, timeout):
        captured.update(url=url, headers=headers, json=json, timeout=timeout)
        return httpx.Response(
            200,
            json={"choices": [{"message": {"content": "Sulamayı sabah yapın."}}]},
            request=httpx.Request("POST", url),
        )

    monkeypatch.setattr("app.ai_chat.httpx.post", fake_post)
    provider = DeepSeekAIChatProvider(
        api_key="test-key",
        model="deepseek-v4-flash",
        timeout_seconds=12,
    )

    response = provider.generate(
        AIChatRequest.model_validate(
            {
                "message": "Ne yapmalıyım?",
                "conversation_id": "conversation-1",
                "history": [{"role": "user", "content": "Yapraklar kuruyor."}],
            }
        )
    )

    assert response.reply == "Sulamayı sabah yapın."
    assert response.conversation_id == "conversation-1"
    assert captured["url"] == "https://api.deepseek.com/chat/completions"
    assert captured["headers"] == {
        "Authorization": "Bearer test-key",
        "Content-Type": "application/json",
    }
    assert captured["json"] == {
        "model": "deepseek-v4-flash",
        "messages": [
            {
                "role": "system",
                "content": "Tarla Asistanı için Türkçe, güvenli ve uygulanabilir tarım önerileri ver.",
            },
            {"role": "user", "content": "Yapraklar kuruyor."},
            {"role": "user", "content": "Ne yapmalıyım?"},
        ],
        "temperature": 0.2,
    }
    assert captured["timeout"] == 12


def test_deepseek_provider_rejects_photo_without_network(monkeypatch):
    monkeypatch.setattr(
        "app.ai_chat.httpx.post",
        lambda *args, **kwargs: pytest.fail("DeepSeek fotoğraf için çağrılmamalı"),
    )
    provider = DeepSeekAIChatProvider(
        api_key="test-key",
        model="deepseek-v4-flash",
        timeout_seconds=12,
    )

    with pytest.raises(AIChatProviderError, match="fotoğraf analizini desteklemiyor"):
        provider.generate(
            AIChatRequest(message="Fotoğrafı incele.", photo_bytes=b"image")
        )


def test_gemini_provider_multimodal_sends_inline_data(monkeypatch):
    captured = {}

    def mock_post(url, **kwargs):
        captured["url"] = url
        captured["headers"] = kwargs.get("headers", {})
        captured["json"] = kwargs.get("json")
        return httpx.Response(
            200,
            json={
                "candidates": [
                    {
                        "content": {
                            "parts": [
                                {
                                    "text": "Fotoğraftaki yaprakta külleme hastalığı başlangıcı görülüyor."
                                }
                            ]
                        }
                    }
                ]
            },
            request=httpx.Request("POST", url),
        )

    monkeypatch.setattr("app.ai_chat.httpx.post", mock_post)
    provider = GeminiAIChatProvider(
        api_key="gemini-test-key",
        model="gemini-2.0-flash",
        timeout_seconds=15,
    )

    response = provider.generate(
        AIChatRequest(
            message="Bu yapraktaki beyaz toz nedir?",
            photo_bytes=b"dummy-image-bytes",
            photo_content_type="image/jpeg",
        )
    )

    assert "külleme hastalığı" in response.reply
    assert captured["headers"].get("x-goog-api-key") == "gemini-test-key"
    assert "gemini-2.0-flash:generateContent" in captured["url"]
    assert "?key=" not in captured["url"]
    user_parts = captured["json"]["contents"][-1]["parts"]
    assert any("inline_data" in part for part in user_parts)
    assert any(part.get("text") == "Bu yapraktaki beyaz toz nedir?" for part in user_parts)

