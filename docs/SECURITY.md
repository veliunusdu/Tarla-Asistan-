# SECURITY GUIDELINES

Security measures to be taken to protect user data and agricultural data in the Tarla Asistanı system.

## 1. Data Encryption and Communication
- **Transit Encryption:** All API calls between the client and the server will be made over **HTTPS (TLS 1.2 or higher)** by default.
- **At-Rest Encryption:** PII (Personally Identifiable Information) data of users (phone number, address, etc.) will be stored encrypted in the database (PostgreSQL). Encryption-at-rest will be activated in cloud storage (S3 etc.).

## 2. Authentication and Token Management
- **JWT (JSON Web Token)** will be used for API security.
- Access Tokens will be short-lived (e.g., 1 hour), and Refresh Tokens will be longer-lived (e.g., 30 days) and revocable from the database.
- Refresh tokens are opaque random values, stored only as SHA-256 hashes, rotated
  on every use, and revoked on logout.
- SMS verification codes (OTP) for farmer logins will be valid for a maximum of 3 minutes and will be subject to attempt limits (Rate Limiting).
- OTP values are HMAC-hashed in Redis, single-use, and never exposed or logged
  outside explicitly enabled local development.

## 3. Authorization (Role-Based Access Control)
- **IDOR (Insecure Direct Object Reference) Protection:** The `farm_id` or `case_id` parameters requested by users will be validated within the scope of the requesting user's authorization (e.g., a farmer cannot retrieve someone else's farm by ID).
- Agronomists can only access farms/farmers that fall under their authorization or are assigned to them.

## 4. API Security (Rate Limiting & Security Headers)
- IP-based rate limiting will be applied at the API Gateway or Backend level against DDoS and Brute-Force attacks.
- Security headers (Helmet.js, CORS restrictions, CSP) will be added for the web panel.

## 5. Media Uploads
- Photos and voice recordings uploaded by farmers will be strictly checked for size, file extension, and Mime-Type (e.g., only `image/jpeg`, `image/png`, `audio/mp4`) before being accepted to the server.
- Upload of executable files will be strictly blocked against malware threats.
