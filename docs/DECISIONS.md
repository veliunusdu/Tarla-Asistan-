# ARCHITECTURE & DECISION LOG (ADR)

This document lists important technical, architectural, and product decisions (Architecture Decision Records) taken throughout the project.

## ADR-001: Backend Architecture Selection
- **Date:** MVP Start
- **Decision:** A **Modular Monolith** architecture will be used instead of microservices.
- **Rationale:** Due to the project being in the MVP stage, small team size, and the need to launch quickly, the extra DevOps and communication costs of microservices have been avoided.

## ADR-002: Offline-First Approach
- **Date:** MVP Start
- **Decision:** The mobile application will adopt an offline-first approach with a local database.
- **Rationale:** Poor or no internet connection in the fields should not prevent core goals (viewing tasks, recording activities, taking photos). Data will be stored on the device and synchronized (Sync Queue) to the backend when internet is available.

## ADR-003: Farmer Authentication Method
- **Date:** MVP Start
- **Decision:** Only **Phone Number and SMS (OTP)** will be used for farmers instead of Email/Password.
- **Rationale:** The target audience has a high probability of forgetting passwords. The lowest friction entry method is the phone number.

## ADR-004: Scope of Artificial Intelligence Use
- **Date:** MVP Start
- **Decision:** AI will not make autonomous decisions; it will act as a support (copilot) and case prioritizer for experts.
- **Rationale:** Avoiding legal and financial risks that may arise from incorrect agricultural recommendations.

## ADR-005: Cross-Platform Mobile Framework
- **Date:** MVP Start
- **Decision:** The mobile application will be developed with **Flutter**.
- **Rationale:** To be able to generate both Android and iOS outputs with a single codebase. Although the priority in the first stage is Android, iOS support can be provided easily. Rapid development of UI components.
