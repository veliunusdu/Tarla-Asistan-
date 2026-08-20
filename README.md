# Tarla Asistanı 🌾

> **"Shows the three most important tasks for your farm every morning."**

Tarla Asistanı (Farm Assistant) is an AI-powered digital farm management system designed to help farmers plan their daily agricultural activities with minimum effort. It enables easy data collection from the field and bridges communication between farmers and agronomists (agricultural engineers) through a centralized web panel.

---

## 🚀 Key Features (MVP)

### 📱 Farmer Mobile Application (Field-Oriented)
*   **Actionable Daily Tasks:** Displays the 3 most critical tasks for the day, keeping the interface simple and free of information clutter.
*   **Offline-First Support:** Allows data entry in rural areas without internet. Changes are synchronized automatically once a connection is established, utilizing a `client_operation_id` to prevent duplicate records.
*   **Voice Activity Logs:** Allows logging of activities (irrigation, fertilization, spraying, etc.) using voice notes, which are converted to structured logs automatically.
*   **Rapid Problem Reporting:** Farmers can take photos of crop diseases or growth anomalies and record voice messages to report issues directly to agronomists.

### 💻 Agronomist Web Panel (Decision-Oriented)
*   **Case Tracking System:** Centralizes and prioritizes incoming photo/voice problem reports from farmers based on urgency.
*   **Farm History Feed:** Provides a comprehensive chronological view of all activities (irrigation, fertilization, spraying) performed on a farm during the season before making recommendations.
*   **Task Assignment:** Agronomists can create specific tasks for farmers, instantly sending push notifications to their devices.

---

## 📁 Repository Structure

```text
Tarla-Asistani/
│
├── docs/                      # Detailed English documentation
│   ├── PROJECT_OVERVIEW.md     # Vision, target users, values & roadmap
│   ├── PRODUCT_REQUIREMENTS.md # User stories, functional & non-functional requirements
│   ├── TECHNICAL_DOCUMENTATION.md # Architecture, layers, offline-first design
│   ├── DATABASE.md             # Schema definitions and PostgreSQL entities
│   ├── API_DOCUMENTATION.md    # RESTful API endpoints and response payloads
│   ├── DESIGN.md               # UI/UX guides, typography, and contrast rules
│   ├── AGRICULTURAL_RULES.md   # Risk parameters, thresholds & AI boundaries
│   ├── DECISIONS.md            # Architecture Decision Records (ADRs)
│   ├── SECURITY.md             # Encryption, RBAC & API security details
│   ├── TESTING_STRATEGY.md     # Unit, integration, E2E & UAT strategies
│   ├── DEPLOYMENT.md           # Continuous delivery & environment settings
│   ├── BACKLOG.md              # Product backlog & sprint breakdowns
│   └── CHANGELOG.md            # Keep a Changelog tracking file
│
├── backend/                    # FastAPI (Python) backend codebase
├── mobile/                     # Flutter (Dart) mobile application
├── web/                        # React / Next.js (TypeScript) agronomist panel
└── README.md                   # This overview file
```

---

## 🛠️ Technology Stack

| Layer | Technologies |
| :--- | :--- |
| **Backend** | Python, FastAPI, SQLAlchemy, Alembic, PostgreSQL + PostGIS, Redis |
| **Mobile App** | Flutter, Dart, SQLite/Hive |
| **Web Panel** | React, Next.js, TypeScript, Tailwind CSS |
| **AI / Services** | Speech-to-Text (Whisper/GCP), OpenAI API (Analysis), Firebase Cloud Messaging (FCM) |
| **DevOps** | Docker, Docker Compose, GitHub Actions |

---

## 💻 Local Setup (For Developers)

### 1. Clone the repository
```bash
git clone https://github.com/veliunusdu/Tarla-Asistan-.git
cd Tarla-Asistan-
```

### 2. Configure Environment Variables
Copy the `.env.example` files to `.env` in both the `backend/` and `web/` folders and configure your keys (SMS APIs, database credentials, OpenWeather keys, etc.).

### 3. Spin up Infrastructure
Run Docker Compose in the root directory to set up PostgreSQL, PostGIS, and Redis:
```bash
docker-compose up -d
```

### 4. Running the Project
Refer to individual READMEs inside `/backend`, `/mobile`, and `/web` for starting development servers.

### Sprint 1 quick start

```bash
cp .env.example .env
docker compose up --build
```

- Agronomist panel: `http://localhost:3000`
- API and Swagger: `http://localhost:8000/docs`
- Health check: `http://localhost:8000/health`

The local development agronomist phone defaults to `+905551112233`. Development
OTP display is enabled by Compose only; keep `OTP_EXPOSE_IN_RESPONSE=false` in
shared environments. If a port is already occupied, set `WEB_PORT` or `API_PORT`
in the root `.env` file before starting Compose.

---

## 📖 Documentation Index
For in-depth explanations of modules, schemas, and UX guidelines, explore the files in the [`docs/`](./docs) directory:
- 💡 [Project Vision & Roadmap](./docs/PROJECT_OVERVIEW.md)
- 📝 [Product Requirements (PRD)](./docs/PRODUCT_REQUIREMENTS.md)
- ⚙️ [Technical Architecture](./docs/TECHNICAL_DOCUMENTATION.md)
- 🗄️ [Database Entity Diagrams](./docs/DATABASE.md)
- 🔌 [REST API Endpoints](./docs/API_DOCUMENTATION.md)
- 🌐 [Shareable API Documentation (HTML)](./docs/api-docs.html)
- 🤖 [OpenAPI 3 Specification (JSON)](./docs/openapi.json)
- 🛡️ [Security Guidelines](./docs/SECURITY.md)

---

## Firebase staging checklist

The Firebase migration uses project `demo2-c4265`, the named **Enterprise**
Firestore database `tarla-asistani`, and the Android package
`com.tarlaasistani.pilot`. Firestore starts empty; do not migrate the existing
PostgreSQL records as part of this rollout.

Before staging, a project administrator must enable **Firebase Authentication →
Phone**, and register the SHA-1 and SHA-256 fingerprints for both the debug and
release Android signing keys. Do not commit a keystore, fingerprints that reveal
private key material, `google-services.json`, or any service-account file.

Set only these non-secret Firebase values for a backend process started from
`backend/` (the JSON file itself must be supplied separately and kept out of
Git):

```env
FIREBASE_AUTH_ENABLED=true
FIREBASE_PROJECT_ID=demo2-c4265
FIREBASE_SERVICE_ACCOUNT_PATH=secrets/firebase-service-account.json
```

For Docker Compose, mount that same file read-only at
`/run/secrets/firebase-service-account.json` and set
`FIREBASE_SERVICE_ACCOUNT_PATH` to that container path. The Compose files pass
`FIREBASE_AUTH_ENABLED` through explicitly; staging must set it to `true`.
`compose.staging.yaml` is an override and must always be merged after
`compose.yaml`, never used on its own. After an authorized operator supplies a
secure, untracked staging environment file, recreate the backend container so
the changed environment is applied:

```powershell
docker compose --env-file <secure-staging-env> -f compose.yaml -f compose.staging.yaml up -d --build --force-recreate backend
```

Run the local Firestore rules suite before an administrator deploys the checked
in rules and index configuration:

```powershell
cd firebase-emulator-tests
npm run test:emulator
```

After the test is green and the administrator has confirmed the target project
and named database, the remote rules-and-indexes deployment gate is:

```powershell
npx -y firebase-tools@latest deploy --only firestore:rules,firestore:indexes --project demo2-c4265
```

Then verify `GET /health/ready` after the recreated backend is healthy, and sign in on a
physical Android device. Create one farm and activity, create another activity
while offline, reconnect, and confirm the offline activity appears once. Use
the resulting Firebase ID token as `Authorization: Bearer <Firebase ID token>`
for a protected AI request. `/health/ready` checks PostgreSQL and Redis; it is
not a Firebase credential probe.

See [the Firebase API integration note](./docs/OPENAPI.md) for the bearer-token
contract and staging gates.

---

## 📜 License
This project is licensed under the MIT License - see the LICENSE file for details.
