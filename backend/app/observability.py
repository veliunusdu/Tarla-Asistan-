import json
import logging
import sys
from collections import defaultdict
from datetime import datetime, timezone
from threading import Lock


class JsonFormatter(logging.Formatter):
    def format(self, record: logging.LogRecord) -> str:
        payload = {
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "level": record.levelname,
            "logger": record.name,
            "message": record.getMessage(),
        }
        for key in (
            "request_id",
            "method",
            "path",
            "status_code",
            "duration_ms",
            "notification_dedupe_key",
        ):
            value = getattr(record, key, None)
            if value is not None:
                payload[key] = value
        if record.exc_info:
            payload["exception"] = self.formatException(record.exc_info)
        return json.dumps(payload, ensure_ascii=False)


def configure_logging(level: str) -> None:
    handler = logging.StreamHandler(sys.stdout)
    handler.setFormatter(JsonFormatter())
    root = logging.getLogger()
    root.handlers.clear()
    root.addHandler(handler)
    root.setLevel(level.upper())


class RequestMetrics:
    def __init__(self):
        self._lock = Lock()
        self.requests_total: dict[tuple[str, str, int], int] = defaultdict(int)
        self.duration_seconds_total: dict[tuple[str, str], float] = defaultdict(float)

    def record(self, method: str, path: str, status_code: int, duration: float) -> None:
        route = _normalized_path(path)
        with self._lock:
            self.requests_total[(method, route, status_code)] += 1
            self.duration_seconds_total[(method, route)] += duration

    def render(self) -> str:
        lines = [
            "# HELP tarla_http_requests_total Total HTTP requests.",
            "# TYPE tarla_http_requests_total counter",
        ]
        with self._lock:
            for (method, path, status_code), count in sorted(
                self.requests_total.items()
            ):
                lines.append(
                    "tarla_http_requests_total"
                    f'{{method="{method}",path="{path}",status="{status_code}"}} {count}'
                )
            lines.extend(
                [
                    "# HELP tarla_http_request_duration_seconds_total Total request duration.",
                    "# TYPE tarla_http_request_duration_seconds_total counter",
                ]
            )
            for (method, path), duration in sorted(self.duration_seconds_total.items()):
                lines.append(
                    "tarla_http_request_duration_seconds_total"
                    f'{{method="{method}",path="{path}"}} {duration:.6f}'
                )
        return "\n".join(lines) + "\n"


def _normalized_path(path: str) -> str:
    parts = path.strip("/").split("/")
    normalized = ["{id}" if _looks_like_uuid(part) else part for part in parts]
    return "/" + "/".join(normalized)


def _looks_like_uuid(value: str) -> bool:
    if len(value) != 36:
        return False
    try:
        import uuid

        uuid.UUID(value)
    except ValueError:
        return False
    return True
