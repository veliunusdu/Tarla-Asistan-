# TESTING STRATEGY

Testing strategy to be applied to ensure the application works without errors in the field and farm.

## 1. Unit Tests
- **Scope:** Domain business rules, agricultural calculations, task prioritization algorithms, date formatting.
- **Tools:** Jest/PyTest/GoTest for backend, Flutter Unit Tests for mobile.
- **Goal:** Especially testing "Weather Rules" and "Task creation logic" by isolating external dependencies.

## 2. Integration Tests
- **Scope:** Database queries, API endpoints, External APIs (Weather service, SMS service).
- **Strategy:** Mock services will be used for external APIs. It will be tested whether API endpoints return correct responses and correctly check authorization.

## 3. UI and End-to-End (E2E) Tests
- **Scope:** Scenarios of the farmer logging into the application, adding a farm, completing tasks when offline, and synchronizing data when internet is available.
- **Tools:** Flutter Integration Test, Cypress or Playwright for Web Panel.

## 4. User Acceptance Testing (UAT)
- When the MVP is completed, pilot tests will be conducted with real users (30-50 farmers and 2-3 agronomists).
- Under real internet connection conditions in the field, mobile app behaviors such as offline functionality, synchronization success, and battery consumption metrics will be analyzed.

## 5. Test Environments
1. **Local:** The testing environment on the developers' own machines.
2. **Staging (Test):** The server environment where QA tests and customer tests are performed before going live, containing data similar to real but fake data.
3. **Production (Live):** The final environment used by real farmers. No code that has not been tested in the Staging environment is transferred to the Production environment.
