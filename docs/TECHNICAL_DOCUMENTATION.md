# TECHNICAL DOCUMENTATION

## 1. Document Information

- **Project Name:** Tarla Asistanı (Farm Assistant)
- **Document Type:** Technical Documentation
- **Version:** 0.1
- **Status:** Draft
- **Phase:** MVP Planning
- **Target Platforms:** Android mobile application and web-based agronomist panel

---

## 2. Document Purpose

The purpose of this document is to explain the technical architecture, technologies to be used, system services, security approach, offline working model, and deployment structure of the Tarla Asistanı MVP.

This document serves as a common technical reference for the following teams:

- Mobile application developers
- Web frontend developers
- Backend developers
- DevOps developers
- AI developers
- Test team
- Product managers
- Agronomists / Agricultural engineers

Technology choices specified in the document can be changed according to project needs. Important technical changes should be added to the decision records (ADR).

---

# 3. System Overview

Tarla Asistanı consists of three basic user interfaces and a central backend system:

1. Mobile application used by farmers
2. Web panel used by agronomists
3. Administration panel used by system administrators

These interfaces connect to the system via the central backend API.

The backend is responsible for the following operations:

- Authentication
- User and permission management
- Farm management
- Crop and production period management
- Daily task management
- Processing weather data
- Activity logs
- Photo and voice files
- Case and message management
- Notification delivery
- Synchronization of offline records
- Agricultural rule engine
- Communication with AI services
- System logs and error tracking

---

# 4. Technical Architecture

## 4.1 High-Level Architecture

```text
┌─────────────────────────┐
│ Farmer Mobile App       │
│ Android / Flutter       │
└────────────┬────────────┘
             │
             │ HTTPS / REST API
             │
┌────────────▼────────────┐
│                         │
│      Backend API        │
│                         │
│ Authentication          │
│ Farm Management         │
│ Task Management         │
│ Activity Management     │
│ Case Management         │
│ Notification Management │
│ Synchronization         │
│                         │
└───┬────────┬────────┬───┘
    │        │        │
    │        │        │
    ▼        ▼        ▼
┌────────┐ ┌────────┐ ┌──────────────┐
│PostgreSQL│ │File    │ │Redis / Queue │
│Database│ │Storage │ │Background Job│
└────────┘ └────────┘ └──────────────┘
    │
    ├───────────────┐
    │               │
    ▼               ▼
┌──────────────┐  ┌───────────────────┐
│Weather Serv. │  │ AI Service        │
└──────────────┘  └───────────────────┘

┌─────────────────────────┐
│ Agronomist Panel        │
│ Web Application         │
└────────────┬────────────┘
             │
             └──────────── Backend API

┌─────────────────────────┐
│ Administration Panel    │
│ Web Application         │
└────────────┬────────────┘
             │
             └──────────── Backend API
```

---

## 4.2 Recommended Architectural Approach

A **modular monolith architecture** will be used in the MVP phase.

In this approach, the system runs as a single backend application, but the codebase is divided into modules according to business domains.

Recommended backend modules:

- Authentication
- Users
- Farms
- Crops
- Tasks
- Activities
- Cases
- Messages
- Weather
- Notifications
- Media
- Synchronization
- Agricultural Rules
- Artificial Intelligence
- Audit Logs
- Administration

### Reasons for Preferring Modular Monolith

- Developed faster compared to microservices.
- Easier to manage for small teams.
- Deployment and debugging processes are simpler.
- Database operations are easier to manage.
- Does not create unnecessary infrastructure complexity in the MVP phase.
- Modules needed in the future can be converted into separate services.

Direct microservice architecture should not be used in the MVP phase.

---

## 4.3 Layered Backend Structure

The backend application should be divided into the following layers:

```text
Controller / API Layer
          ↓
Application / Service Layer
          ↓
Domain / Business Rules Layer
          ↓
Repository / Data Access Layer
          ↓
Database and External Services
```

### API Layer

- Accepts HTTP requests.
- Validates incoming data.
- Checks user authorization.
- Calls application services.
- Returns standard API response.

### Application Services Layer

- Manages user transactions.
- Coordinates multiple business rules.
- Manages database transactions.
- Starts notification or background jobs.

### Domain Layer

- Contains core business rules.
- Task prioritization
- Case status transitions
- Agricultural safety rules
- Activity validation
- User permissions

### Data Access Layer

- Manages database queries.
- Manages caching mechanisms.
- Provides access to external services.

---

# 5. Technologies and Tools

## 5.1 Frontend and Mobile

- **Mobile Application:** Flutter / Dart
- **Web Panel (Agronomist and Admin):** React / TypeScript / Next.js
- **State Management (Mobile):** Bloc / Riverpod
- **State Management (Web):** Redux / Zustand

## 5.2 Backend

- **Programming Language:** .NET 8 (ASP.NET Core) / TypeScript
- **Database (Relational):** PostgreSQL
- **Cache and Queue:** Redis
- **File Storage:** AWS S3 / MinIO (or similar object storage solutions)

## 5.3 AI and External Services

- **Weather Forecast:** OpenWeatherMap / Tomorrow.io etc. (To be finalized)
- **Speech-to-Text:** Google Cloud Speech-to-Text or OpenAI Whisper
- **Image Analysis and NLP:** OpenAI API or custom trained models (Agriculture specific)
- **SMS Service:** Twilio / AWS SNS / Local SMS providers
- **Push Notifications:** Firebase Cloud Messaging (FCM)

## 5.4 DevOps and Deployment

- **Containerization:** Docker
- **Orchestration:** Docker Compose / Kubernetes (In later stages)
- **CI/CD:** GitHub Actions / GitLab CI
- **Cloud Provider:** AWS / Google Cloud / Azure

---

# 6. Security and Authentication

## 6.1 Authentication System

- Login with OTP (SMS verification code) for farmers.
- Login based on Email + Password or OAuth (Google, etc.) for agronomists and admins.
- Use of JSON Web Tokens (JWT) for authorization.
- Ensuring session continuity with refresh token mechanism.

## 6.2 Authorization (RBAC)

System access will be restricted based on user roles:
- `FARMER`: Can only access their own farms and data.
- `AGRONOMIST`: Can access data of farmers assigned to them and relevant cases.
- `ADMIN`: Can access all system data, reports, and settings.

## 6.3 Data Security

- Communication must be fully over HTTPS (TLS 1.2+).
- Sensitive information (user contact information, etc.) will be stored encrypted in the database.
- API input validation (e.g. Joi or Zod) will be performed against SQL Injection and XSS attacks.
- API abuse and brute-force attacks must be prevented with rate-limiting.

---

# 7. Offline Working (Offline-First) Model

Considering that the application will be used in rural areas and low internet connectivity environments, an "Offline-First" approach will be adopted.

## 7.1 In-Device Storage

- A local database (e.g. SQLite / Hive) will be used within the mobile application.
- Farm information, past tasks, current tasks, and weather forecasts will be stored on the device based on the last synchronization.

## 7.2 Offline Data Entry and Synchronization

- Activities, photos, and notes added when there is no internet will be saved to the local database.
- Unique, temporary identifiers (UUID) will be assigned to these data.
- When an internet connection is established, a background synchronization queue (Sync Queue) will transmit the data to the backend in order.
- Compression can be applied when synchronizing photos and media files to protect mobile data quotas.

## 7.3 Conflict Resolution

- Simple conflicts (e.g. completing the same task from multiple devices) will be resolved based on the server timestamp (LWW - Last Write Wins).

---

# 8. API Design

The API will be designed in accordance with RESTful principles.

Example Endpoints:
- `POST /api/v1/auth/request-otp`
- `POST /api/v1/auth/verify-otp`
- `GET /api/v1/farms`
- `POST /api/v1/farms`
- `GET /api/v1/farms/{farmId}/tasks`
- `POST /api/v1/farms/{farmId}/activities`
- `POST /api/v1/cases`
- `GET /api/v1/agronomist/cases`

The error format must be standard:
```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid farm ID.",
    "details": [...]
  }
}
```

---

# 9. Test Strategy

- **Unit Tests:** Domain business rules (task prioritization, risk assessment) will be covered.
- **Integration Tests:** API endpoints, database operations, and external service calls (by mocking) will be tested.
- **E2E (End-to-End) Tests:** Basic user flows in the mobile application (login, adding a farm, offline sync) will be subjected to automated tests.

---

# 10. Monitoring and Logging

- Centralized Logging: Server errors and access logs will be transferred to a centralized service (ELK Stack or Datadog/Sentry).
- Application Errors: Crashlytics or Sentry will be used for errors in the mobile application and web panel.
- Performance Monitoring: API response times and database query performances will be monitored regularly.
