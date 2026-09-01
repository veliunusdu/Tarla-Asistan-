# Firestore Security Audit

Target: Firebase project `demo2-c4265`, Standard Firestore database `(default)` in `nam5`.

Collections used by the mobile app:

- `users/{uid}`: owner-only private profile containing an email or phone number.
- `farms/{farmId}`: owner-scoped farm records.
- `farms/{farmId}/activities/{activityId}`: owner-scoped, append-only activity records.

Rule review:

- Unauthenticated reads and writes are denied.
- Users can only create or read their own profile and cannot change role or identity fields.
- Email profiles must match `request.auth.token.email`; phone profiles must match the phone claim.
- Farm and activity records enforce owner IDs, schema constraints, and immutable fields.
- Unknown paths are denied.

Residual review item: test the rules with two real Firebase users before broad release to verify create, read, and owner-isolation behavior in the deployed Standard database.
