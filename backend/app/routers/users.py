from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session

from app.database import get_db
from app.dependencies import get_current_user, require_roles
from app.models import Profile, User, UserRole
from app.schemas import ProfileUpdate, UserResponse

router = APIRouter(prefix="/users", tags=["Kullanıcılar"])


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
