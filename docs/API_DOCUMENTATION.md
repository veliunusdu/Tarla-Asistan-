# API DOCUMENTATION

## 1. General Information
- **Base URL:** `https://api.tarla-asistani.com/api/v1` (for MVP)
- **Content-Type:** `application/json`
- **Authentication:** Bearer Token (JWT)

## 2. Authentication (Auth)

### 2.1 Request OTP (Farmer)
`POST /auth/request-otp`
```json
// Request
{
  "phone_number": "+905551234567"
}

// Response (200 OK)
{
  "message": "OTP code has been sent."
}
```

### 2.2 Verify OTP
`POST /auth/verify-otp`
```json
// Request
{
  "phone_number": "+905551234567",
  "otp_code": "123456"
}

// Response (200 OK)
{
  "access_token": "eyJhbGciOiJIUzI1NiIsIn...",
  "refresh_token": "opaque-random-token",
  "token_type": "bearer",
  "expires_in": 3600,
  "user": {
    "id": "uuid",
    "role": "FARMER"
  }
}
```

### 2.3 Refresh Session
`POST /auth/refresh`
```json
{
  "refresh_token": "opaque-random-token"
}
```
- Rotates the refresh token. A used, expired, or revoked token returns `401`.

### 2.4 Logout
`POST /auth/logout`
```json
{
  "refresh_token": "opaque-random-token"
}
```
- Revokes the refresh session and returns `204 No Content`.

### 2.5 Current User
`GET /auth/me`
- **Auth:** `Bearer Token`

### 2.6 Complete Profile
`PUT /users/me`
```json
{
  "full_name": "Veli Ünüşdü",
  "province": "Konya",
  "district": "Selçuklu",
  "terms_accepted": true,
  "notifications_enabled": true
}
```

## 3. Farm Management (Farms)

### 3.1 List Farms
`GET /farms`
- **Auth:** `Bearer Token`
- **Response:** List of farms belonging to the user.

Ownership is checked server-side. A farm that does not belong to the active user
is returned as `404` so its existence is not disclosed.

### 3.2 Add Farm
`POST /farms`
```json
// Request
{
  "name": "North Wheat Field",
  "latitude": 39.92077,
  "longitude": 32.85411,
  "crop_type": "WHEAT"
}
```

## 4. Tasks (Tasks)

### 4.1 Get Daily Tasks
`GET /farms/{farm_id}/tasks?date=YYYY-MM-DD`
- **Response:** Priority 3 task list on the relevant date (or today).

### 4.2 Complete Task
`POST /tasks/{task_id}/complete`
- **Response:** Task is marked as completed and recorded as an activity.

## 5. Cases (Cases)

### 5.1 Report Case (Problem)
`POST /cases`
```json
// Request
{
  "farm_id": "uuid",
  "category": "DISEASE",
  "description": "There is yellowing on the leaves.",
  "media_urls": ["https://s3.aws.com/.../photo.jpg"]
}
```

## 6. Error Responses
```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid parameters provided.",
    "details": ["phone_number format is incorrect"]
  }
}
```
