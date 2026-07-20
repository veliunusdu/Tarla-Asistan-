# DATABASE ARCHITECTURE

## 1. Overview
PostgreSQL will be used as the relational database management system in the Tarla Asistanı project. This document defines the basic database tables and relationships required for the MVP phase.

## 2. Core Tables (MVP)

### 2.1 Users (users)
Represents all actors in the system (Farmer, Agronomist, Admin).
- `id` (UUID, PK)
- `phone_number` (String, Unique)
- `role` (Enum: FARMER, AGRONOMIST)
- `created_at` (Timestamp)
- `updated_at` (Timestamp)

### 2.2 Profiles (profiles)
Stores the required and optional user profile fields separately from identity.
- `user_id` (UUID, PK/FK -> users.id)
- `full_name` (String)
- `province` (String)
- `district` (String)
- `terms_accepted` (Boolean)
- `notifications_enabled` (Boolean)
- `created_at`, `updated_at` (Timestamp)

### 2.3 Refresh Sessions (refresh_tokens)
Stores only SHA-256 hashes of opaque refresh tokens.
- `id` (UUID, PK)
- `user_id` (UUID, FK -> users.id)
- `family_id` (UUID)
- `token_hash` (String, Unique)
- `expires_at`, `revoked_at`, `created_at` (Timestamp)

### 2.4 Farms (farms)
Holds information about the fields/farms belonging to farmers.
- `id` (UUID, PK)
- `owner_id` (UUID, FK -> users.id)
- `name` (String)
- `latitude` (Float)
- `longitude` (Float)
- `size_in_hectares` (Float)
- `irrigation_method` (Enum: DRIP, SPRINKLER, FLOOD, RAINFED, OTHER)
- `soil_type` (String, Nullable)
- `note` (String, Nullable)
- `archived_at` (Timestamp, Nullable)
- `created_at`, `updated_at` (Timestamp)

`(owner_id, archived_at)` ve `(owner_id, name)` bileşik indeksleri listeleme ve
aynı ad uyarısı sorgularını destekler.

### 2.5 Crop Periods (crop_periods)
Represents the crop planted in the field.
- `id` (UUID, PK)
- `farm_id` (UUID, FK -> farms.id)
- `crop_type` (Enum: WHEAT, BARLEY, CORN, SUNFLOWER, TOMATO)
- `variety` (String, Nullable)
- `planted_at` (Date)
- `harvested_at` (Date, Nullable)
- `status` (Enum: ACTIVE, ARCHIVED)
- `created_at`, `updated_at` (Timestamp)

PostgreSQL kısmi benzersiz indeksi bir tarlada en fazla bir `ACTIVE` dönem
olmasını veritabanı seviyesinde de güvenceye alır.

### 2.6 Weather Snapshots (weather_snapshots)
Stores normalized successful provider responses for safe fallback.
- `id` (UUID, PK)
- `farm_id` (UUID, FK -> farms.id)
- `provider` (String)
- `payload` (JSON)
- `fetched_at` (Timestamp)

### 2.7 Tasks (tasks)
Daily tasks assigned to the farmer or generated automatically.
- `id` (UUID, PK)
- `farm_id` (UUID, FK -> farms.id)
- `crop_period_id` (UUID, FK -> crop_periods.id, Nullable)
- `created_by_id` (UUID, FK -> users.id, Nullable)
- `title` (String)
- `description` (Text)
- `reason` (Text)
- `priority` (Enum: LOW, MEDIUM, HIGH, CRITICAL)
- `status` (Enum: NEW, VIEWED, PLANNED, COMPLETED, NOT_APPLIED, OVERDUE,
  CANCELLED)
- `source` (Enum: SYSTEM, CROP_CALENDAR, WEATHER, EXPERT)
- `confidence` (Enum: LOW, MEDIUM, HIGH)
- `due_date` (Date)
- `dedupe_key` (String)
- `not_applied_reason`, `completion_note`, `photo_url` (Nullable)
- `viewed_at`, `completed_at`, `created_at`, `updated_at` (Timestamp)

`(farm_id, due_date, dedupe_key)` benzersiz indeksi aynı günlük görevin tekrar
üretilmesini veritabanı seviyesinde engeller.

### 2.8 Activity Logs (activities)
Holds operations (irrigation, fertilization, etc.) performed by the farmer in the field.
- `id` (UUID, PK)
- `farm_id` (UUID, FK -> farms.id)
- `crop_period_id` (UUID, FK -> crop_periods.id, Nullable)
- `task_id` (UUID, FK -> tasks.id, Nullable, Unique)
- `created_by_id` (UUID, FK -> users.id, Nullable)
- `activity_type` (Enum: IRRIGATION, FERTILIZATION, SPRAYING, PRUNING,
  FIELD_CHECK, HARVEST, OTHER)
- `status` (Enum: DRAFT, CONFIRMED)
- `source` (Enum: MANUAL, VOICE, TASK)
- `description` (Text)
- `occurred_at` (Timestamp)
- `duration_minutes`, `amount`, `unit`, `performed_by`, `cost` (Nullable)
- `photo_url`, `voice_url`, `voice_transcript` (Nullable)
- `confirmed_at`, `archived_at`, `created_at`, `updated_at` (Timestamp)

Görev tamamlama kaydında `task_id` benzersizdir; tekrarlanan tamamlama
istekleri ikinci bir faaliyet oluşturmaz.

### 2.9 Activity Revisions (activity_revisions)
Faaliyet düzenlemelerinde değişen alanların önceki değerlerini korur.
- `id` (UUID, PK)
- `activity_id` (UUID, FK -> activities.id)
- `changed_by_id` (UUID, FK -> users.id, Nullable)
- `previous_values` (JSON)
- `changed_at` (Timestamp)

### 2.10 Cases (cases)
Problems reported by the farmer with photos/voice.
- `id` (UUID, PK)
- `farm_id` (UUID, FK -> farms.id)
- `agronomist_id` (UUID, FK -> users.id, Nullable)
- `category` (Enum: DISEASE, PEST, SOIL, OTHER)
- `status` (Enum: OPEN, IN_PROGRESS, WAITING_INFO, RESOLVED, CLOSED)
- `created_at` (Timestamp)

### 2.11 Messages (messages)
Agronomist-farmer correspondence within the case.
- `id` (UUID, PK)
- `case_id` (UUID, FK -> cases.id)
- `sender_id` (UUID, FK -> users.id)
- `content` (Text)
- `media_url` (String, Nullable)
- `created_at` (Timestamp)

## 3. Relationship Schema (ERD - Summary)
- 1 User -> 0..1 Profile
- 1 User -> N Refresh Sessions
- 1 User (FARMER) -> N Farms
- 1 Farm -> N Crop Periods (Only 1 can be active at a time)
- 1 Farm -> N Weather Snapshots
- 1 Farm -> N Tasks
- 1 Farm -> N Activities
- 1 Task -> 0..1 Completion Activity
- 1 Activity -> N Revisions
- 1 Farm -> N Cases
- 1 Case -> N Messages
- 1 User (AGRONOMIST) -> N Cases (Assigned cases)
