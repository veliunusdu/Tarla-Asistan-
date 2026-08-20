import asyncio
from contextlib import asynccontextmanager
import logging
from time import perf_counter
import uuid

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse, PlainTextResponse
from fastapi.middleware.cors import CORSMiddleware
from redis import Redis
from sqlalchemy import text

from app.config import get_settings
from app.account_deletion import (
    ACCOUNT_DELETION_UNEXPECTED,
    run_startup_account_deletion_retries,
)
from app.ai_chat import create_ai_chat_provider
from app.media_storage import create_media_storage
from app.database import SessionLocal
from app.otp import RedisOtpStore
from app.observability import RequestMetrics, configure_logging
from app.push import create_push_provider
from app.routers import (
    activities,
    ai,
    auth,
    cases,
    farms,
    media,
    notifications,
    pilot,
    production_periods,
    tasks,
    users,
    weather,
)
from app.weather import create_weather_provider

settings = get_settings()
configure_logging(settings.log_level)
logger = logging.getLogger(__name__)
request_metrics = RequestMetrics()


@asynccontextmanager
async def lifespan(app: FastAPI):
    redis_client = Redis.from_url(settings.redis_url, decode_responses=True)
    app.state.redis = redis_client
    app.state.otp_store = RedisOtpStore(redis_client)
    app.state.weather_provider = create_weather_provider(settings)
    app.state.push_provider = create_push_provider(settings)
    app.state.ai_chat_provider = create_ai_chat_provider(settings)
    app.state.media_storage = create_media_storage(settings)
    try:
        await asyncio.to_thread(run_startup_account_deletion_retries)
    except Exception:
        logger.error(
            "account_deletion_startup_failed error_code=%s",
            ACCOUNT_DELETION_UNEXPECTED,
        )
    yield
    redis_client.close()


app = FastAPI(
    title=settings.app_name,
    version="0.1.0",
    description="Tarla Asistanı modüler monolit API",
    lifespan=lifespan,
)
app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.cors_origins,
    allow_credentials=True,
    allow_methods=["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"],
    allow_headers=["Authorization", "Content-Type", "Idempotency-Key"],
)


@app.middleware("http")
async def security_headers(request: Request, call_next):
    request_id = request.headers.get("X-Request-ID") or str(uuid.uuid4())
    request.state.request_id = request_id[:100]
    started = perf_counter()
    response = await call_next(request)
    duration = perf_counter() - started
    if settings.metrics_enabled:
        request_metrics.record(
            request.method, request.url.path, response.status_code, duration
        )
    logger.info(
        "http_request",
        extra={
            "request_id": request.state.request_id,
            "method": request.method,
            "path": request.url.path,
            "status_code": response.status_code,
            "duration_ms": round(duration * 1000, 2),
        },
    )
    response.headers["X-Request-ID"] = request.state.request_id
    response.headers["X-Content-Type-Options"] = "nosniff"
    response.headers["X-Frame-Options"] = "DENY"
    response.headers["Referrer-Policy"] = "strict-origin-when-cross-origin"
    return response


@app.exception_handler(Exception)
async def unhandled_exception(request: Request, exc: Exception):
    request_id = getattr(request.state, "request_id", str(uuid.uuid4()))
    logger.exception(
        "unhandled_exception",
        exc_info=exc,
        extra={
            "request_id": request_id,
            "method": request.method,
            "path": request.url.path,
        },
    )
    return JSONResponse(
        status_code=500,
        content={"detail": "Beklenmeyen bir hata oluştu.", "request_id": request_id},
        headers={"X-Request-ID": request_id},
    )


@app.get("/health", tags=["Sistem"])
def health() -> dict[str, str]:
    with SessionLocal() as db:
        db.execute(text("SELECT 1"))
    app.state.redis.ping()
    return {"status": "ok", "database": "ok", "redis": "ok"}


@app.get("/health/live", tags=["Sistem"])
def liveness() -> dict[str, str]:
    return {"status": "ok"}


@app.get("/health/ready", tags=["Sistem"])
def readiness() -> dict[str, str]:
    return health()


@app.get("/metrics", response_class=PlainTextResponse, include_in_schema=False)
def metrics() -> PlainTextResponse:
    if not settings.metrics_enabled:
        return PlainTextResponse("metrics disabled\n", status_code=404)
    return PlainTextResponse(
        request_metrics.render(), media_type="text/plain; version=0.0.4"
    )


app.include_router(auth.router, prefix=settings.api_v1_prefix)
app.include_router(users.router, prefix=settings.api_v1_prefix)
app.include_router(farms.router, prefix=settings.api_v1_prefix)
app.include_router(production_periods.router, prefix=settings.api_v1_prefix)
app.include_router(weather.router, prefix=settings.api_v1_prefix)
app.include_router(tasks.router, prefix=settings.api_v1_prefix)
app.include_router(activities.router, prefix=settings.api_v1_prefix)
app.include_router(media.router, prefix=settings.api_v1_prefix)
app.include_router(cases.router, prefix=settings.api_v1_prefix)
app.include_router(notifications.router, prefix=settings.api_v1_prefix)
app.include_router(pilot.router, prefix=settings.api_v1_prefix)
app.include_router(ai.router, prefix=settings.api_v1_prefix)
