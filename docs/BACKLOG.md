# PRODUCT BACKLOG

This document contains the general pool of features and tasks to be developed in the MVP stage and beyond. As the development process progresses, this document will be updated or moved to a tool like Trello/Jira.

## Sprint 1: Basic Infrastructure and Auth
- [ ] Creation of backend project skeleton (Node.js/Python).
- [ ] Setting up database schemas (PostgreSQL).
- [ ] Creation of Flutter project skeleton.
- [ ] Writing the farmer SMS/OTP login service.
- [ ] Creating profile creation screens (Name, Surname).
- [ ] Integrating basic UI/UX theme settings into the application.

## Sprint 2: Farm and Crop Management
- [ ] Farmer selecting location on map and adding farm.
- [ ] Creating farm size and soil/irrigation type selection forms.
- [ ] Creating crop catalog (e.g., Wheat, Corn, Tomato).
- [ ] Entering crop planting date on farm.
- [ ] Writing Farms and Crops CRUD APIs on the backend.

## Sprint 3: Tasks and Activity Log
- [ ] Designing the Daily Tasks list on the main screen.
- [ ] Writing the logic for completing tasks and "Not Applied" options.
- [ ] Creating activity log (Irrigation, Fertilization) entry forms.
- [ ] Installing local database (Offline storage) in the mobile application.
- [ ] Feature for syncing activities recorded offline when internet is available.

## Sprint 4: Voice Command and Problem Reporting (Cases)
- [ ] Recording voice from device microphone and Speech-to-Text integration.
- [ ] Flow for taking a photo, tagging the problem, and creating a "Case".
- [ ] Logic for uploading cases to S3/Cloud storage.

## Sprint 5: Agronomist Panel and Notifications
- [ ] Setting up the Agronomist Web panel skeleton (React/NextJS).
- [ ] Listing incoming cases and detail view.
- [ ] Expert assigning response (message) and task to case.
- [ ] Setting up Push Notifications (FCM) infrastructure (for weather alerts and expert messages).

## Future Plans (Post-MVP)
- WhatsApp integration.
- Integration of tractor and hardware sensor data.
- Satellite image analysis.
