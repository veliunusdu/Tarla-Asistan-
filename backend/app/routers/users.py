from fastapi import APIRouter, BackgroundTasks, Depends, status
from sqlalchemy.orm import Session

from app.account_deletion import (
    process_account_deletion_safely,
    request_account_deletion,
)
from app.database import get_db
from app.dependencies import (
    get_account_deletion_request_user,
    get_current_user,
    require_roles,
)
from app.models import Profile, User, UserRole
from app.schemas import (
    AccountDeletionRequest,
    AccountDeletionResponse,
    ProfileUpdate,
    UserResponse,
)
from app.models import utcnow

router = APIRouter(prefix="/users", tags=["Kullanıcılar"])


@router.post(
    "/me/deletion-request",
    response_model=AccountDeletionResponse,
    status_code=status.HTTP_202_ACCEPTED,
    summary="Hesap silme isteği oluştur",
)
def request_deletion(
    payload: AccountDeletionRequest,
    background_tasks: BackgroundTasks,
    user: User = Depends(get_account_deletion_request_user),
    db: Session = Depends(get_db),
) -> AccountDeletionResponse:
    job = request_account_deletion(db, user, utcnow())
    background_tasks.add_task(process_account_deletion_safely, job.id)
    return AccountDeletionResponse(request_id=job.id, status=job.status.value)


@router.put("/me", response_model=UserResponse, summary="Zorunlu profili tamamla")
def update_profile(
    payload: ProfileUpdate,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> User:
    profile = user.profile or Profile(user_id=user.id)
    profile.full_name = payload.full_name
    profile.province = payload.province
    profile.district = payload.district
    profile.terms_accepted = payload.terms_accepted
    profile.notifications_enabled = payload.notifications_enabled
    user.profile = profile
    db.add(profile)
    db.commit()
    db.refresh(user)
    return user


@router.get("/farmer-area", response_model=UserResponse, include_in_schema=False)
def farmer_area(
    user: User = Depends(require_roles(UserRole.FARMER)),
) -> User:
    return user


@router.get("/agronomist-area", response_model=UserResponse, include_in_schema=False)
def agronomist_area(
    user: User = Depends(require_roles(UserRole.AGRONOMIST)),
) -> User:
    return user
