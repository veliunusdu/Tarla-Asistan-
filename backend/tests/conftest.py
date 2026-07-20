import os
from contextlib import asynccontextmanager

os.environ["DATABASE_URL"] = "sqlite+pysqlite:///:memory:"
os.environ["JWT_SECRET"] = "test-jwt-secret-that-is-long-enough-123"
os.environ["OTP_HASH_SECRET"] = "test-otp-secret-that-is-long-enough-123"
os.environ["OTP_EXPOSE_IN_RESPONSE"] = "true"
os.environ["AGRONOMIST_PHONE_NUMBERS"] = "+905551112233"

import pytest
from fastapi.testclient import TestClient
from sqlalchemy import create_engine
from sqlalchemy.orm import Session, sessionmaker
from sqlalchemy.pool import StaticPool

from app.database import Base, get_db
from app.dependencies import get_otp_store
from app.main import app
from app.otp import OtpRateLimited


@asynccontextmanager
async def test_lifespan(application):
    yield


app.router.lifespan_context = test_lifespan


class MemoryOtpStore:
    def __init__(self):
        self.codes: dict[str, str] = {}
        self.attempts: dict[str, int] = {}
        self.cooldowns: set[str] = set()

    def issue(self, phone_number: str, digest: str, ttl: int, cooldown: int) -> None:
        if phone_number in self.cooldowns:
            raise OtpRateLimited
        self.cooldowns.add(phone_number)
        self.codes[phone_number] = digest
        self.attempts.pop(phone_number, None)

    def get_digest(self, phone_number: str) -> str | None:
        return self.codes.get(phone_number)

    def increment_attempts(self, phone_number: str, ttl: int) -> int:
        self.attempts[phone_number] = self.attempts.get(phone_number, 0) + 1
        return self.attempts[phone_number]

    def consume(self, phone_number: str) -> None:
        self.codes.pop(phone_number, None)
        self.attempts.pop(phone_number, None)


@pytest.fixture
def db_session():
    engine = create_engine(
        "sqlite+pysqlite://",
        connect_args={"check_same_thread": False},
        poolclass=StaticPool,
    )
    Base.metadata.create_all(engine)
    session_factory = sessionmaker(bind=engine, expire_on_commit=False)
    with session_factory() as session:
        yield session


@pytest.fixture
def client(db_session: Session):
    otp_store = MemoryOtpStore()

    def override_db():
        yield db_session

    app.dependency_overrides[get_db] = override_db
    app.dependency_overrides[get_otp_store] = lambda: otp_store
    with TestClient(app) as test_client:
        yield test_client
    app.dependency_overrides.clear()
