# DATABASE ARCHITECTURE

## 1. Overview
PostgreSQL will be used as the relational database management system in the Tarla Asistanı project. This document defines the basic database tables and relationships required for the MVP phase.

## 2. Core Tables (MVP)

### 2.1 Users (users)
Represents all actors in the system (Farmer, Agronomist, Admin).
- `id` (UUID, PK)
- `phone_number` (String, Unique)
- `first_name` (String)
- `last_name` (String)
- `role` (Enum: FARMER, AGRONOMIST, ADMIN)
- `created_at` (Timestamp)

### 2.2 Farms (farms)
Holds information about the fields/farms belonging to farmers.
- `id` (UUID, PK)
- `user_id` (UUID, FK -> users.id)
- `name` (String)
- `latitude` (Float)
- `longitude` (Float)
- `size_in_hectares` (Float)
- `created_at` (Timestamp)

### 2.3 Crop Periods (crops)
Represents the crop planted in the field.
- `id` (UUID, PK)
- `farm_id` (UUID, FK -> farms.id)
- `crop_type` (String)
- `planted_at` (Date)
- `harvested_at` (Date, Nullable)
- `status` (Enum: ACTIVE, ARCHIVED)

### 2.4 Tasks (tasks)
Daily tasks assigned to the farmer or generated automatically.
- `id` (UUID, PK)
- `farm_id` (UUID, FK -> farms.id)
- `title` (String)
- `description` (Text)
- `priority` (Enum: LOW, MEDIUM, HIGH, CRITICAL)
- `status` (Enum: NEW, COMPLETED, IGNORED)
- `due_date` (Date)
- `created_by` (UUID, FK -> users.id)

### 2.5 Activity Logs (activities)
Holds operations (irrigation, fertilization, etc.) performed by the farmer in the field.
- `id` (UUID, PK)
- `farm_id` (UUID, FK -> farms.id)
- `activity_type` (Enum: IRRIGATION, FERTILIZATION, SPRAYING, PRUNING, OTHER)
- `description` (Text)
- `activity_date` (Timestamp)
- `media_url` (String, Nullable)

### 2.6 Cases (cases)
Problems reported by the farmer with photos/voice.
- `id` (UUID, PK)
- `farm_id` (UUID, FK -> farms.id)
- `agronomist_id` (UUID, FK -> users.id, Nullable)
- `category` (Enum: DISEASE, PEST, SOIL, OTHER)
- `status` (Enum: OPEN, IN_PROGRESS, WAITING_INFO, RESOLVED, CLOSED)
- `created_at` (Timestamp)

### 2.7 Messages (messages)
Agronomist-farmer correspondence within the case.
- `id` (UUID, PK)
- `case_id` (UUID, FK -> cases.id)
- `sender_id` (UUID, FK -> users.id)
- `content` (Text)
- `media_url` (String, Nullable)
- `created_at` (Timestamp)

## 3. Relationship Schema (ERD - Summary)
- 1 User (FARMER) -> N Farms
- 1 Farm -> N Crops (Only 1 can be active at a time)
- 1 Farm -> N Tasks
- 1 Farm -> N Activities
- 1 Farm -> N Cases
- 1 Case -> N Messages
- 1 User (AGRONOMIST) -> N Cases (Assigned cases)
