# AGRICULTURAL RULES & LOGIC

This document defines the basic logical boundaries and business rules that the system (and AI) will use when making agricultural recommendations and generating tasks.

## 1. Task Prioritization Logic
The priority of tasks (CRITICAL, HIGH, MEDIUM, LOW) is determined according to the following rules:

1. **Weather-Sourced Risks (Critical):** Frost, hail warning, extreme heat wave. (Displayed on the main screen with a red warning.)
2. **Agronomist (Expert) Tasks (High):** Direct prescription or action written to the farmer by the agronomist.
3. **Crop Calendar-Sourced Work (Medium):** Planting time has arrived, fertilization period has started, etc.
4. **System Reminders (Low):** "Check the field", "1 week has passed since the last irrigation", etc.

## 2. Weather Rules
- **Frost Risk:** If the temperature is expected to drop to 0°C and below, a notification is sent at least 24 hours before the event.
- **Strong Wind:** If the wind speed reaches 30 km/h within 24 hours, the farmer
  is advised to consider postponing spraying and to check field conditions.
- **Heavy Rain:** If precipitation probability reaches 70% and hourly
  precipitation reaches 5 mm within 12 hours, the farmer is advised to review
  irrigation and drainage.

Weather rules create cautious, traceable alerts; they never make the final
agricultural decision for the farmer. Provider failure never presents cached
weather as current: the latest successful snapshot is visibly marked stale.

## 3. Artificial Intelligence (AI) Boundaries
Agricultural advice is of critical importance. Wrong advice can lead to complete crop loss.
- **No Diagnosis Rule:** The AI CANNOT make a DEFINITIVE disease diagnosis based on a photo. It only makes a preliminary identification such as "This could be a fungal disease" and directly transfers the case to the **Agronomist**.
- **No Drug Recommendation Rule:** The system or AI definitely does not directly recommend a chemical drug's commercial brand or dosage. This operation can only be performed by an authorized **Agronomist**.

## 4. User Notification Limits
- The farmer should not receive more than one warning for the same risk within 12 hours (spam prevention).
- Notifications should not be sent before 07:00 in the morning and after 21:00 in the evening in the user's local time (except for Critical Frost warning).
