from collections.abc import Callable
from typing import Any, Protocol

from firebase_admin import auth, exceptions as firebase_exceptions, firestore
from google.api_core import exceptions as google_exceptions
from google.cloud.firestore_v1.base_query import FieldFilter

from app.config import Settings, get_settings
from app.firebase_auth import get_firebase_auth_app

FIRESTORE_WRITE_BATCH_SIZE = 450
FIREBASE_PROJECT_ID = "demo2-c4265"
FIREBASE_AUTH_UNAVAILABLE = "FIREBASE_AUTH_UNAVAILABLE"
FIREBASE_CONFIGURATION_INVALID = "FIREBASE_CONFIGURATION_INVALID"
FIRESTORE_UNAVAILABLE = "FIRESTORE_UNAVAILABLE"


class AccountDeletionProviderError(RuntimeError):
    """An external account-deletion operation failed with a safe, fixed code."""

    def __init__(self, code: str):
        self.code = code
        super().__init__(code)


class FirebaseAccountGateway(Protocol):
    def revoke_tokens(self, uid: str) -> None: ...

    def anonymize_firestore(self, uid: str, anonymous_subject: str) -> None: ...

    def delete_auth_user(self, uid: str) -> None: ...


class FirebaseAdminAccountGateway:
    def __init__(
        self,
        *,
        app: object | None = None,
        auth_module: Any = auth,
        firestore_factory: Callable[..., Any] = firestore.client,
        settings: Settings | None = None,
    ):
        app_initialization_failed = False
        resolved_app = app
        if resolved_app is None:
            try:
                resolved_app = get_firebase_auth_app()
            except Exception:
                app_initialization_failed = True
        if app_initialization_failed:
            raise AccountDeletionProviderError(FIREBASE_AUTH_UNAVAILABLE)

        valid_project = False
        try:
            valid_project = resolved_app.project_id == FIREBASE_PROJECT_ID
        except Exception:
            pass
        if not valid_project:
            raise AccountDeletionProviderError(FIREBASE_CONFIGURATION_INVALID)

        self._app = resolved_app
        self._auth = auth_module
        self._firestore_factory = firestore_factory
        self._settings = settings or get_settings()

    def revoke_tokens(self, uid: str) -> None:
        provider_failed = False
        try:
            self._auth.revoke_refresh_tokens(uid, app=self._app)
        except self._auth.UserNotFoundError:
            return
        except (firebase_exceptions.FirebaseError, ValueError):
            provider_failed = True
        if provider_failed:
            raise AccountDeletionProviderError(FIREBASE_AUTH_UNAVAILABLE)

    def anonymize_firestore(self, uid: str, anonymous_subject: str) -> None:
        provider_failed = False
        try:
            client = self._firestore_factory(
                app=self._app,
                database_id=self._settings.firestore_database_id,
            )
            farms = (
                client.collection("farms")
                .where(filter=FieldFilter("ownerId", "==", uid))
                .stream()
            )
            batch = client.batch()
            pending_writes = 0
            for farm in farms:
                batch.update(
                    farm.reference,
                    {
                        "ownerId": anonymous_subject,
                        "anonymousOwnerId": anonymous_subject,
                    },
                )
                pending_writes += 1
                if pending_writes == FIRESTORE_WRITE_BATCH_SIZE:
                    batch.commit()
                    batch = client.batch()
                    pending_writes = 0
            if pending_writes:
                batch.commit()
            client.collection("users").document(uid).delete()
        except (
            google_exceptions.GoogleAPIError,
            firebase_exceptions.FirebaseError,
            ValueError,
        ):
            provider_failed = True
        if provider_failed:
            raise AccountDeletionProviderError(FIRESTORE_UNAVAILABLE)

    def delete_auth_user(self, uid: str) -> None:
        provider_failed = False
        try:
            self._auth.delete_user(uid, app=self._app)
        except self._auth.UserNotFoundError:
            return
        except (firebase_exceptions.FirebaseError, ValueError):
            provider_failed = True
        if provider_failed:
            raise AccountDeletionProviderError(FIREBASE_AUTH_UNAVAILABLE)
