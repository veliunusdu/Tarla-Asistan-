import hashlib
import hmac
import secrets
from dataclasses import dataclass
from typing import Protocol

from redis import Redis

from app.config import Settings


class OtpError(Exception):
    pass


class OtpRateLimited(OtpError):
    pass


class OtpInvalid(OtpError):
    pass


class OtpExpired(OtpError):
    pass


class OtpStore(Protocol):
    def issue(self, phone_number: str, digest: str, ttl: int, cooldown: int) -> None: ...
    def get_digest(self, phone_number: str) -> str | None: ...
    def increment_attempts(self, phone_number: str, ttl: int) -> int: ...
    def consume(self, phone_number: str) -> None: ...


@dataclass
class RedisOtpStore:
    client: Redis

    def issue(self, phone_number: str, digest: str, ttl: int, cooldown: int) -> None:
        cooldown_key = f"otp:cooldown:{phone_number}"
        if not self.client.set(cooldown_key, "1", ex=cooldown, nx=True):
            raise OtpRateLimited
        pipe = self.client.pipeline()
        pipe.set(f"otp:code:{phone_number}", digest, ex=ttl)
        pipe.delete(f"otp:attempts:{phone_number}")
        pipe.execute()

    def get_digest(self, phone_number: str) -> str | None:
        value = self.client.get(f"otp:code:{phone_number}")
        if value is None:
            return None
        return value.decode() if isinstance(value, bytes) else str(value)

    def increment_attempts(self, phone_number: str, ttl: int) -> int:
        key = f"otp:attempts:{phone_number}"
        attempts = self.client.incr(key)
        if attempts == 1:
            self.client.expire(key, ttl)
        return attempts

    def consume(self, phone_number: str) -> None:
        self.client.delete(
            f"otp:code:{phone_number}",
            f"otp:attempts:{phone_number}",
        )


class OtpService:
    def __init__(self, store: OtpStore, settings: Settings):
        self.store = store
        self.settings = settings

    def _digest(self, phone_number: str, code: str) -> str:
        return hmac.new(
            self.settings.otp_hash_secret.encode(),
            f"{phone_number}:{code}".encode(),
            hashlib.sha256,
        ).hexdigest()

    def issue(self, phone_number: str) -> str:
        code = f"{secrets.randbelow(1_000_000):06d}"
        self.store.issue(
            phone_number,
            self._digest(phone_number, code),
            self.settings.otp_ttl_seconds,
            self.settings.otp_request_cooldown_seconds,
        )
        return code

    def verify(self, phone_number: str, code: str) -> None:
        expected = self.store.get_digest(phone_number)
        if expected is None:
            raise OtpExpired
        attempts = self.store.increment_attempts(
            phone_number, self.settings.otp_ttl_seconds
        )
        if attempts >= self.settings.otp_max_attempts:
            self.store.consume(phone_number)
            raise OtpInvalid
        if not hmac.compare_digest(expected, self._digest(phone_number, code)):
            raise OtpInvalid
        self.store.consume(phone_number)
