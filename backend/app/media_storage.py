from pathlib import Path
from typing import Protocol

from app.config import Settings


class MediaStorageError(RuntimeError):
    pass


class MediaStorageMissing(MediaStorageError):
    pass


class MediaStorage(Protocol):
    def save(self, key: str, content: bytes, content_type: str) -> None: ...
    def load(self, key: str) -> bytes: ...
    def delete(self, key: str) -> None: ...


class LocalMediaStorage:
    def __init__(self, storage_path: str):
        self.root = Path(storage_path).resolve()

    def save(self, key: str, content: bytes, content_type: str) -> None:
        target = (self.root / key).resolve()
        if target.parent != self.root:
            raise MediaStorageError("Geçersiz medya depolama anahtarı.")
        self.root.mkdir(parents=True, exist_ok=True)
        target.write_bytes(content)

    def load(self, key: str) -> bytes:
        target = (self.root / key).resolve()
        if target.parent != self.root:
            raise MediaStorageMissing("Medya dosyası bulunamadı.")
        try:
            return target.read_bytes()
        except FileNotFoundError as exc:
            raise MediaStorageMissing("Medya dosyası bulunamadı.") from exc

    def delete(self, key: str) -> None:
        target = (self.root / key).resolve()
        if target.parent == self.root:
            target.unlink(missing_ok=True)


class R2MediaStorage:
    def __init__(self, *, client, bucket: str):
        self.client = client
        self.bucket = bucket

    def save(self, key: str, content: bytes, content_type: str) -> None:
        try:
            self.client.put_object(
                Bucket=self.bucket,
                Key=key,
                Body=content,
                ContentType=content_type,
            )
        except Exception as exc:
            raise MediaStorageError("R2 medya dosyası kaydedilemedi.") from exc

    def load(self, key: str) -> bytes:
        try:
            response = self.client.get_object(Bucket=self.bucket, Key=key)
            return response["Body"].read()
        except Exception as exc:
            error_code = getattr(exc, "response", {}).get("Error", {}).get("Code")
            if error_code in {"NoSuchKey", "404", "NoSuchBucket"}:
                raise MediaStorageMissing("Medya dosyası bulunamadı.") from exc
            raise MediaStorageError("R2 medya dosyası okunamadı.") from exc

    def delete(self, key: str) -> None:
        try:
            self.client.delete_object(Bucket=self.bucket, Key=key)
        except Exception as exc:
            raise MediaStorageError("R2 medya dosyası silinemedi.") from exc


def create_media_storage(settings: Settings) -> MediaStorage:
    if settings.media_storage_provider == "local":
        return LocalMediaStorage(settings.media_storage_path)
    if settings.media_storage_provider != "r2":
        raise ValueError("MEDIA_STORAGE_PROVIDER yalnızca 'local' veya 'r2' olabilir.")
    required = {
        "R2_ACCOUNT_ID": settings.r2_account_id,
        "R2_BUCKET": settings.r2_bucket,
        "R2_ACCESS_KEY_ID": settings.r2_access_key_id,
        "R2_SECRET_ACCESS_KEY": settings.r2_secret_access_key,
    }
    missing = [name for name, value in required.items() if not value]
    if missing:
        raise ValueError(f"R2 yapılandırması eksik: {', '.join(missing)}")
    try:
        import boto3
    except ImportError as exc:
        raise ValueError("R2 için boto3 paketi kurulmalıdır.") from exc
    client = boto3.client(
        "s3",
        endpoint_url=f"https://{settings.r2_account_id}.r2.cloudflarestorage.com",
        aws_access_key_id=settings.r2_access_key_id,
        aws_secret_access_key=settings.r2_secret_access_key,
        region_name="auto",
    )
    return R2MediaStorage(client=client, bucket=settings.r2_bucket)
