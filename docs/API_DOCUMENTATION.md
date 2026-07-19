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
  "token": "eyJhbGciOiJIUzI1NiIsIn...",
  "user": {
    "id": "uuid",
    "role": "FARMER"
  }
}
```

## 3. Farm Management (Farms)

### 3.1 List Farms
`GET /farms`
- **Auth:** `Bearer Token`
- **Response:** List of farms belonging to the user.

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
