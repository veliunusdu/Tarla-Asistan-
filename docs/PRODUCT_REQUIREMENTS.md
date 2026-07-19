# PRODUCT REQUIREMENTS

## 1. Document Information

- **Project Name:** Tarla Asistanı (Farm Assistant)
- **Document Type:** Product Requirements Document (PRD)
- **Version:** 0.1
- **Status:** Draft
- **Phase:** MVP Planning
- **Target Platform:** Android mobile application and web-based agronomist panel

---

## 2. Document Purpose

This document defines the product features, user needs, user flows, acceptance criteria, and basic business rules to be included in the Tarla Asistanı MVP.

The main purpose of the document is to ensure that the product, design, and software teams work on the same scope.

Features not explicitly mentioned in this document are not included in the MVP scope unless approved by the team.

---

## 3. Product Definition

Tarla Asistanı is a digital farm management application where the farmer registers their field in the system, views daily farm tasks, records their activities by voice or writing, reports problems with photos, and contacts an agronomist when necessary.

The core promise of the product:

> **Shows the three most important tasks for your farm every morning.**

---

## 4. Product Goals

### 4.1 Primary Goal

To enable the farmer to manage daily field work in a more organized, understandable, and trackable way.

### 4.2 Sub-goals

- Provide daily prioritized tasks to the farmer
- Translate weather risks into agricultural actions
- Enable easy recording of farm activities
- Structure communication between the farmer and the agronomist
- Facilitate reporting problems with photos and voice
- Collect farm history in one place
- Enable agronomists to track more farmers
- Escalate uncertain agricultural situations to expert control

---

# 5. User Roles

## 5.1 Farmer

The farmer uses the mobile application.

The farmer can:

- Create a profile.
- Add fields/farms.
- Enter crop information.
- View daily tasks.
- Complete tasks.
- Create activity logs.
- Send voice recordings.
- Report problems with photos.
- Send questions to the agronomist.
- View expert responses.
- Review farm history.

---

## 5.2 Agronomist / Agricultural Engineer

The agronomist uses the web panel.

The agronomist can:

- View farmers assigned to them.
- Review open cases.
- View cases sorted by priority.
- View the farmer's field history.
- Review photos, voice recordings, weather, and crop information.
- Send responses to the farmer.
- Create tasks for the farmer.
- Request additional information.
- Escalate/assign the case to another expert.
- Close the case.

---

## 5.3 Administrator (Admin)

The administrator role will be limited in the MVP.

The administrator can:

- View users.
- Assign agronomists to farmers.
- Manage supported crops.
- Manage agricultural task templates.
- Monitor system notifications.
- View error and usage logs.

---

# 6. User Stories

## 6.1 Registration and Login

### US-001

As a farmer, I want to register with my phone number so that I can use the application without experiencing a complex membership process.

### US-002

As a user, I want to log in with a verification code (OTP) so that I do not have to remember a password.

### US-003

As a farmer, I want to enter my basic profile information so that the application can show me appropriate content.

### US-004

As a user, I want to change my notification preferences so that I only receive the notifications I need.

---

## 6.2 Farm Management

### US-005

As a farmer, I want to add my farm/field from the map so that I can receive weather and task information specific to my farm.

### US-006

As a farmer, I want to name my field so that I can easily distinguish between multiple fields.

### US-007

As a farmer, I want to record the crop I grow and the planting date so that I can receive tasks suitable for the crop stage.

### US-008

As a farmer, I want to specify my irrigation method so that suggestions regarding irrigation can be more meaningful.

### US-009

As a farmer, I want to update my farm information later so that I can correct wrong or changed information.

### US-010

As a farmer, I want to add multiple fields so that I can track all my fields in the same application.

---

## 6.3 Daily Tasks

### US-011

As a farmer, I want to see the most important tasks I need to do that day when I open the application so that I do not forget important work.

### US-012

As a farmer, I want to see why a task was created so that I can understand the recommendation.

### US-013

As a farmer, I want to see the urgency level of a task so that I can understand which work to do first.

### US-014

As a farmer, I want to mark a task as completed so that my farm history can be updated.

### US-015

As a farmer, I want to specify why I did not implement a task so that the system and the expert can see this situation.

### US-016

As a farmer, I want to add photos or notes to the task so that I can record the result of the operation I performed.

### US-017

As an agronomist, I want to create tasks for the farmer so that I can follow up on the implementation of the recommendation.

---

## 6.4 Weather and Risk Alerts

### US-018

As a farmer, I want to see the weather forecast specific to my farm so that I can plan my farm work.

### US-019

As a farmer, I want to receive notifications when there is a risk of frost so that I can take timely precautions.

### US-020

As a farmer, I want to be warned if strong wind is expected so that I can postpone inappropriate farm operations.

### US-021

As a farmer, I want to see upcoming precipitation information as a task so that I can review my irrigation decision.

### US-022

As a farmer, I want to see which hours the weather warning covers so that I can plan my work correctly.

---

## 6.5 Activity Log

### US-023

As a farmer, I want to record the irrigation I did so that I can see when and how much I irrigated.

### US-024

As a farmer, I want to record fertilization and spraying operations so that my farm history is organized.

### US-025

As a farmer, I want to record activities by voice so that I do not have to fill out long forms.

### US-026

As a farmer, I want to confirm how the system understood the voice recording so that I can prevent incorrect logs from being created.

### US-027

As a farmer, I want to add photos to the activity log so that I can document the result of the operation.

### US-028

As a farmer, I want to be able to correct an activity log I created incorrectly.

---

## 6.6 Problem Reporting

### US-029

As a farmer, I want to report a problem I see in the field with a photo so that the expert can evaluate the situation.

### US-030

As a farmer, I want to send a voice description of the problem so that I do not have to write long text.

### US-031

As a farmer, I want to specify how much of the field the problem is seen in so that the expert can understand the spread of the problem.

### US-032

As a farmer, I want to track the status of the problem I sent so that I can understand when the response will arrive.

### US-033

As a farmer, I want to see the engineer's request for additional information so that I can send the required photo or explanation.

---

## 6.7 Agronomist Panel

### US-034

As an agronomist, I want to see open cases sorted by priority so that I can respond to urgent cases first.

### US-035

As an agronomist, I want to see the farmer's recent activities so that I can make a more accurate evaluation.

### US-036

As an agronomist, I want to see photo, voice, weather, and crop information in the case details on the same screen.

### US-037

As an agronomist, I want to send written or voice responses to the farmer.

### US-038

As an agronomist, I want to request additional information from the farmer.

### US-039

As an agronomist, I want to create a new task based on the case.

### US-040

As an agronomist, I want to close the resolved case.

### US-041

As an agronomist, I want to transfer a case outside my area of expertise to another expert.

---

## 6.8 Farm Diary / Log

### US-042

As a farmer, I want to see all operations performed in my field in chronological order.

### US-043

As a farmer, I want to see completed tasks and expert responses in the same history feed.

### US-044

As an agronomist, I want to filter the farm history so that I can see only relevant activities.

---

## 6.9 Notifications

### US-045

As a farmer, I want to receive a notification when a new task is received.

### US-046

As a farmer, I want to receive a notification when the engineer responds.

### US-047

As a farmer, I want to receive reminders for uncompleted important tasks.

### US-048

As a user, I want to distinguish notifications by priority level.

---

## 6.10 Offline Use

### US-049

As a farmer, I want to see downloaded tasks when there is no internet.

### US-050

As a farmer, I want to create photos and activity logs when there is no internet.

### US-051

As a farmer, I want the records to be sent automatically when internet connectivity is restored.

---

# 7. Features and Acceptance Criteria

## 7.1 Login with Phone Number

### Description

The user logs in to the system with a phone number and SMS verification code.

### Acceptance Criteria

- The user must be able to enter a valid phone number.
- Invalid phone numbers must not be accepted.
- The verification code must be sent to the user via SMS.
- The verification code must have a validity period.
- An error message must be shown when an incorrect code is entered.
- A temporary block must be applied after a certain number of failed attempts.
- Previously registered users should not have to create a profile again.
- The user must be able to log out.

---

## 7.2 Profile Creation

### Description

This is the section where the user's basic information is saved.

### Required Fields

- Name
- Phone number
- City (Province)
- District
- User role
- Terms of use approval

### Optional Fields

- Profile picture
- Preferred language
- Voice use preference
- Notification preferences

### Acceptance Criteria

- The profile must not be completed without filling in the required fields.
- The user must be able to edit their profile later.
- The phone number must not be directly editable from the profile screen.
- Notification preferences must be toggleable individually.
- The user must be able to create an account deletion request.

---

## 7.3 Farm/Field Creation

### Description

The farmer registers their field in the system.

### Required Information

- Farm name
- Location
- Crop
- Planting or sowing date

### Optional Information

- Farm boundaries
- Farm size
- Crop variety
- Irrigation method
- Soil type
- Note

### Acceptance Criteria

- The user must be able to select a location on the map.
- When location permission is not granted, the user must be able to make a manual selection from the map.
- The farm name cannot be left blank.
- A warning must be shown when the same user creates more than one farm with the same name.
- The farm must be linked to a user.
- The user must only be able to edit their own farms.
- Confirmation must be requested before deleting a farm.
- When a farm is deleted, linked records must not be permanently deleted immediately (soft delete/archiving preferred).
- Farm status must be maintainable as active or archived.

---

## 7.4 Crop Information

### Description

The section where the crop grown in the field is defined.

### Acceptance Criteria

- The user must be able to choose from the supported crops list.
- When an unsupported crop is selected, the user must be informed.
- The planting or sowing date cannot be in the future.
- Crop variety should not be mandatory.
- If there is an active crop period in the same field, the user must be warned when adding a new crop.
- The harvested crop period must be closable.
- Changing crop information must not delete past tasks.

---

## 7.5 Daily Tasks

### Description

The farmer is shown the most important farm tasks for that day.

### Task Fields

- Task title
- Task description
- Farm
- Task source
- Priority
- Confidence level
- Creation time
- Last completion time
- Task status

### Task Statuses

- New
- Viewed
- Planned
- Completed
- Not Applied
- Overdue
- Cancelled

### Acceptance Criteria

- A maximum of three prioritized tasks must be shown on the main screen.
- Critical weather warnings can be displayed independently of the task limit.
- Each task must be linked to a farm.
- The reason for creation must be specified in each task.
- Automated tasks and expert tasks must be visually distinguished.
- Expert tasks can have higher priority than automated tasks.
- The user must be able to mark the task as completed.
- The user must be able to select a reason for not applying the task.
- The completed task must be recorded in the farm log.
- Completed tasks must not be deleted from history.
- Overdue tasks must be shown separately.
- For low-confidence tasks, expert evaluation should be suggested.

---

## 7.6 Weather and Risk Alerts

### Description

Weather forecast and agricultural risk information are shown based on the farm location.

### MVP Risk Types

- Precipitation
- Frost
- Extreme temperature
- Strong wind
- High humidity
- Hail risk

### Acceptance Criteria

- Weather information must be retrieved based on the farm location.
- The last update time of the weather data must be shown.
- The user must be informed when the weather service cannot be reached.
- Outdated data must not be shown as current.
- Notifications must be sent for critical risks.
- Notifications must not be sent repeatedly at short intervals for the same risk.
- The alert must include a start and end time.
- The alert should be expressed in risk language rather than as a definitive result.
- Weather data alone must not create a definitive spraying or irrigation decision.

---

## 7.7 Activity Log

### Description

The farmer records the operations performed in the field.

### Activity Types

- Irrigation
- Fertilization
- Spraying
- Pruning
- Field Check
- Harvest
- Other

### Required Fields

- Farm
- Activity type
- Date
- Description

### Optional Fields

- Duration
- Amount
- Unit
- Photo
- Voice recording
- Person performing the operation
- Cost

### Acceptance Criteria

- The activity must be linked to a farm.
- Future dates must not be recorded as completed activities.
- Voice recordings must be automatically convertible into draft activities.
- Information extracted from the voice recording must be verified by the user.
- Automated activities must not be finalized without user approval.
- The user must be able to edit the activity log.
- The change history of the edited log must be kept.
- Deleted activities must be archived so they can be recovered.
- The activity must appear in the farm log.

---

## 7.8 Reporting Problems with Photos

### Description

The farmer sends a problem in the field to the expert with photos, voice, or text.

### Required Fields

- Farm
- Problem category
- At least one description or photo

### Problem Categories

- Suspected Disease
- Suspected Pest
- Leaf Color Change
- Growth Problem
- Irrigation Problem
- Soil Problem
- Other

### Acceptance Criteria

- The user must be able to upload multiple photos.
- Photo taking guidance must be shown.
- The user must be able to add voice or written explanations.
- The user must be able to select the spread level of the problem.
- The sent case must create a unique case record.
- The case status must be visible to the user.
- A notification must be sent to the relevant engineer after the case is submitted.
- Information must not be lost when the upload fails.
- If there is no internet, the case must be saved as a draft.
- The system must not give a definitive diagnosis.
- If there is an AI result, it must be stated that this is a preliminary assessment.

---

## 7.9 Agronomist Panel

### Description

The web panel where experts manage farmer cases.

### Case Statuses

- New
- Under Review
- Awaiting Info
- Answered
- Being Followed
- Closed
- Transferred to Another Expert

### Acceptance Criteria

- The engineer must only see the farmers they are authorized to track.
- Cases must be sortable by priority, date, and status.
- The engineer must be able to see the farm and crop information in the case details.
- Recent weather information must be displayable.
- Recent activities must be displayable.
- Photo and voice files must be openable.
- The engineer must be able to send written responses.
- The engineer must be able to send voice responses.
- The engineer must be able to request additional info.
- The engineer must be able to create tasks.
- A resolution note must be entered before the case is closed.
- Closed cases must be openable again.
- The history of case transactions must be kept.

---

## 7.10 Farm Diary / Log

### Description

The screen where all activities, tasks, cases, and expert responses belonging to the farm are shown in chronological order.

### Acceptance Criteria

- Records must be sortable from newest to oldest.
- The user must be able to filter by record type.
- Activity logs must be shown.
- Completed tasks must be shown.
- Opened cases must be shown.
- Expert responses must be shown.
- Photos must be linked to the relevant record.
- The user must only see their own farm log.
- Agronomists must only see the logs of farms they are authorized to view.

---

## 7.11 Notifications

### Notification Types

- Critical weather warning
- New daily task
- Expert response
- Additional information request
- Task reminder
- System announcement

### Acceptance Criteria

- The notification must be sent to the relevant user.
- When the notification is clicked, redirection must be made to the relevant screen.
- Critical and normal notifications must be visually distinguished.
- The user must be able to turn off normal notification types.
- A separate explanation must be shown to the user for critical warnings.
- Unnecessary duplicate notifications must not be sent for the same event.
- Notification sending history must be kept.

---

## 7.12 Offline Use

### Acceptance Criteria

- Previously loaded tasks must be viewable when offline.
- Activity logs must be createable when offline.
- Photos must be takeable when offline.
- Records must be stored securely on the device.
- Automatic synchronization must be performed when an internet connection is established.
- If synchronization fails, the user must be informed.
- The same record must not be created twice.
- Records created offline must appear as "Awaiting send".

---

# 8. User Flows

## 8.1 First Registration Flow

```text
User opens the app
        ↓
Enters phone number
        ↓
SMS verification code is sent
        ↓
User enters the code
        ↓
Code is verified
        ↓
Profile information is retrieved
        ↓
Terms of use are approved
        ↓
First farm addition screen opens
        ↓
Farm and crop information are saved
        ↓
Main screen is displayed
```

### Alternative Flows

- If the SMS does not arrive, the user can request a code again.
- If the code is incorrect, the user is informed.
- The user can postpone adding a farm for later.
- If location permission is not granted, manual map selection can be made.

---

## 8.2 Farm Addition Flow

```text
Farmer taps the "Add Farm" option
        ↓
Enters the farm name
        ↓
Selects location from map
        ↓
Selects crop
        ↓
Enters planting or sowing date
        ↓
Adds optional information
        ↓
Checks information
        ↓
Saves the farm
        ↓
Farm appears on the main screen
```

---

## 8.3 Daily Task Flow

```text
System checks farm and crop data
        ↓
Weather and crop schedule are evaluated
        ↓
Tasks are created
        ↓
Priority sorting is performed
        ↓
A maximum of three tasks are shown on the main screen
        ↓
Farmer opens the task
        ↓
Reviews the task reason
        ↓
Taps the "I will apply" option
        ↓
Completes the task
        ↓
Adds photos or notes
        ↓
Taps the "Completed" option
        ↓
Task is recorded in the farm log
```

---

## 8.4 Task Non-Application Flow

```text
Farmer opens the task
        ↓
Taps the "Not Applied" option
        ↓
Selects the reason for non-application
        ↓
Adds optional explanation
        ↓
Record is sent to the system
        ↓
Task status is updated
        ↓
Expert is informed if necessary
```

### Reasons for Non-Application

- Weather was not suitable
- No equipment
- No labor
- Did not think the task was necessary
- Want to discuss with the expert
- Other

---

## 8.5 Voice Activity Log Flow

```text
Farmer taps the "Record Activity" option
        ↓
Opens the microphone option
        ↓
Explains the operation verbally
        ↓
Voice is converted to text
        ↓
System separates farm, operation, and time
        ↓
Draft record is shown
        ↓
Farmer checks information
        ↓
Corrects if necessary
        ↓
Approves the record
        ↓
Activity is added to the farm log
```

### Example Voice Input

> Today morning I irrigated the vineyard for two hours.

### System Output

- Farm: Vineyard Farm
- Activity: Irrigation
- Date: Today
- Time: Morning
- Duration: 2 hours

---

## 8.6 Problem Reporting with Photos Flow

```text
Farmer taps the "Report Problem" option
        ↓
Selects the farm
        ↓
Selects the problem category
        ↓
Sees photo-taking tips
        ↓
Uploads photos
        ↓
Adds voice or written explanations
        ↓
Selects the spread of the problem
        ↓
Checks information
        ↓
Submits the case
        ↓
Case appears on the agronomist panel
        ↓
Farmer views the case status
```

---

## 8.7 Expert Case Review Flow

```text
Agronomist logs in to the panel
        ↓
Sees new cases
        ↓
Sorts cases by priority
        ↓
Opens a case
        ↓
Reviews farm, crop, and past activities
        ↓
Checks photos and descriptions
        ↓
Requests additional information if necessary
        ↓
Creates response or task
        ↓
Sends response to the farmer
        ↓
Case is put into follow-up status
        ↓
Case is closed when resolved
```

---

## 8.8 Requesting Additional Info Flow

```text
Agronomist opens case details
        ↓
Taps the "Request Additional Info" option
        ↓
Selects or writes the requested info
        ↓
Notification is sent to the farmer
        ↓
Farmer adds required info or photo
        ↓
Case is put back into the review queue
```

---

## 8.9 Offline Recording Flow

```text
Internet connection is lost
        ↓
Farmer creates activity or photo record
        ↓
Record is saved on the device
        ↓
Shown as "Awaiting send"
        ↓
Internet connection is restored
        ↓
System sends the record automatically
        ↓
If successful, status is updated
        ↓
If unsuccessful, user is informed
```

---

# 9. Business Rules

## 9.1 General Rules

1. Each user can only access data they are authorized to view.
2. The farmer can only see their own farms.
3. The agronomist can only see the farmers assigned to them or their institution.
4. User roles cannot be changed except by the administrator role.
5. All important operations must be recorded with date and user information.
6. Deletion operations should be done as archiving rather than permanent deletion where possible.
7. Version history of agricultural content changes must be kept.

---

## 9.2 Daily Task Rules

1. A maximum of three prioritized tasks are shown on the main screen at the same time.
2. Critical weather warnings are not included in this limit.
3. Tasks created by the expert can be prioritized over automated tasks.
4. The same task content should not be created repeatedly for the same farm.
5. Tasks must be associated with the farm, crop, and date.
6. Each automated task must have a reason for creation.
7. Low-confidence tasks must not be expressed definitively.
8. When the user completes the task, the operation must be added to the farm log.
9. Overdue tasks must not be automatically considered completed.
10. The history of cancelled tasks must be preserved.

---

## 9.3 Agricultural Safety Rules

1. The system must not show an unverified condition as a definitive diagnosis.
2. The AI response and the expert response must be clearly separated.
3. Low-confidence results must be directed to the agronomist.
4. Drug names and dosage recommendations must not be sent without expert approval.
5. The application must not replace the official product label and expert evaluation.
6. Recommendations based on weather data must be presented in risk language.
7. Photo analysis alone must not be accepted as sufficient for a definitive decision.
8. Data source and update time must be shown in critical agricultural advice.
9. The system must report to the user when information is missing.
10. Field checks or additional photos must be requested from the user when necessary.

---

## 9.4 Weather Data Rules

1. Weather information must be retrieved based on the farm location.
2. The last update time of the weather data must be shown.
3. Weather data older than the specified period must not be accepted as current.
4. If the weather service is not working, tasks must not be shown as if created with definitive data.
5. The same risk notification must not be sent repeatedly within the specified period.
6. The critical risk level must be based on thresholds defined by the expert.
7. Weather risks can be interpreted differently depending on the crop type.
8. The user must be informed that weather forecasts may change.

---

## 9.5 Activity Log Rules

1. Each activity must be linked to a farm.
2. The transaction type and date of the activity are mandatory.
3. Activities created from voice recordings must not be finalized without user approval.
4. The user can edit past activities.
5. The previous values of the edited records must be kept in the system.
6. Operations planned in the future and completed activities must be separate record types.
7. Photos and voice recordings must be associated with the relevant activity.
8. Deleted activities must be recoverable for a specified period.

---

## 9.6 Case Rules

1. Each case must be linked to a farmer and a farm.
2. At least one description or photo is required to create a case.
3. New cases are automatically created in the "New" status.
4. Case priority can be changed by the system or the expert.
5. A notification must be sent to the farmer when the engineer responds.
6. When additional information is requested, the case must be put in the "Awaiting Info" status.
7. When the farmer sends additional info, the case must be put back in the review queue.
8. Closed cases must not be deleted.
9. Closed cases must be openable again when necessary.
10. All messages and status changes within the case must be recorded.

---

## 9.7 Notification Rules

1. Critical notifications must be separated from normal notifications.
2. The user must be able to turn off normal notification types.
3. There must be a duplicate notification limit for the same event.
4. The notification must direct the user to the relevant content.
5. Normal notifications can be postponed during night hours.
6. Critical weather notifications must not be delayed.
7. Notifications that cannot be sent must be recorded in the system.
8. Unnecessary technical terms should not be used in the notification content.

---

## 9.8 Offline Use Rules

1. Offline records must be stored securely on the device.
2. Records created offline must be clearly shown to the user.
3. When the internet connection is restored, records must be sent automatically.
4. The same record must not be sent more than once.
5. Synchronization errors must be reported to the user.
6. The user must be able to manually resend the failed record.
7. An error log must be created in case of critical data loss.

---

# 10. Error and Empty States

## 10.1 General Error Message

> The operation could not be completed at this time. Please check your connection and try again.

## 10.2 When Weather Data Cannot Be Retrieved

> Current weather information for your farm is currently unavailable. Weather suggestions are temporarily not shown as last updated data may be outdated.

## 10.3 When Voice Recording Cannot Be Understood

> We could not fully understand the voice recording. You can try recording again or enter the information manually.

## 10.4 When Photos Cannot Be Uploaded

> Photos could not be sent yet. It will be retried when the internet connection is restored.

## 10.5 When There Is No Daily Task

> There is no new prioritized task for today. You can check your farm log or the weather forecast.

## 10.6 When Case Is Not Answered

> Your question has been forwarded to the agronomist. You will receive a notification when the answer arrives.

## 10.7 When Location Permission Is Not Granted

> You can continue without location permission. Select your farm manually on the map.

---

# 11. Non-Functional Requirements

## 11.1 Usability

- Main transactions must be completed in at most three or four steps.
- Large and clear buttons must be used.
- Technical expressions must be simplified as much as possible.
- Important operations must be supported by voice usage.
- Information density on the main screen must be kept low.

## 11.2 Performance

- The main screen should open within a few seconds under normal connection.
- Pagination must be used in list screens.
- Photos must be appropriately shrunk before uploading.
- User actions must not be blocked under low connection.

## 11.3 Security

- User authentication must be mandatory.
- Communication must be over a secure connection.
- File accesses must be authorized.
- User data must be protected with role-based access.
- Critical administrator operations must be recorded.

## 11.4 Accessibility

- Text sizes must be legible.
- Buttons must be large enough to be easily tapped.
- Color alone must not be used as a status indicator.
- Voice explanations and text must be presented together.

## 11.5 Compatibility

- MVP must primarily support common and current Android versions.
- The agronomist panel must work on current desktop browsers.
- Responsive design compatible with different screen sizes must be used.

---

# 12. MVP Prioritization

## Must Have

- Login with phone number
- Profile creation
- Farm creation
- Crop information
- Daily tasks
- Weather and risk alerts
- Written activity log
- Problem reporting with photos
- Agronomist panel
- Expert response
- Farm log/diary
- Notifications

## Should Have

- Voice activity log
- Offline recording
- Task confidence level
- Expert sending voice response
- Multiple farm support
- Additional information request system

## Could Have

- Simple cost field
- Ready-made expert response templates
- Advanced filtering
- Agricultural content management panel
- WhatsApp redirection

## Won't Have

- Satellite analysis
- Drone management
- Sensor integration
- Tractor integration
- Automatic pesticide and dosage recommendation
- Marketplace
- Credit and insurance
- Advanced accounting
- Cooperative panel
- Yield estimation

---

# 13. MVP Success Criteria

The MVP can be considered successful under the following conditions:

- If at least half of the pilot users use the application weekly
- If a significant portion of daily tasks are opened by users
- If farmers can create activity logs
- If the problem reporting flow with photos works smoothly
- If expert responses can be sent through the same system
- If farmers can understand the reason for recommendations
- If experts find case management more organized than phone and messaging
- If no critical data loss is experienced
- If the incorrect or unnecessary warning rate is at an acceptable level
- If a significant portion of users want to continue using the application at the end of the pilot

---

# 14. Open Questions

- Which crop will the first pilot be conducted with?
- Which city or district will the first pilot be conducted in?
- Which weather service will be used?
- Which technology will be used for voice recording?
- How many farmers will one agronomist be responsible for?
- What will be the target expert response time?
- Will farmers prefer the application or WhatsApp integration more?
- How long will offline records be stored on the device?
- How long will photo and voice records be preserved?
- Who will prepare the agricultural task rules?
- Who will approve changes to agricultural content?
- Will the first version be developed only for Android?
- Through which channel will user support be provided?
