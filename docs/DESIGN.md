# DESIGN PRINCIPLES & GUIDELINES

## 1. Core Design Philosophy
Since Tarla Asistanı will be used by farmers of all ages and different levels of technology literacy, **simplicity, legibility, and accessibility** are prioritized.

> "The application must be easy to use in the field, under the sun, and even with gloves."

## 2. Color Palette (Proposed)
- **Primary Color:** Crop green representing agriculture and nature (e.g., `#2E7D32`)
- **Secondary Color:** Earth/harvest colors (e.g., `#F57F17` or `#8D6E63`)
- **Alert Colors:**
  - Error/Critical Risk (Frost, Hail): Red (`#D32F2F`)
  - Warning (Medium risk): Orange (`#F57C00`)
  - Info: Blue (`#1976D2`)
- **Background:** Light gray or white (high contrast to prevent glare under the sun)

## 3. Typography
- **Readability:** Text sizes should be minimum 16sp.
- **Font Family:** Modern, easy-to-read sans-serif (e.g., `Roboto`, `Inter`, or `Open Sans`).
- Hierarchy should be made clear by using Bold weight in main headings.

## 4. UI/UX Principles
1. **Large Hitboxes:** Buttons must be at least 48x48 dp in size.
2. **Less Information, More Meaning:** Not too much data should be shown on the screen at the same time. The answer to the question "What should I do today?" should be visible at first glance (maximum 3 main tasks).
3. **Use of Icons and Visuals:** Intuitive icons (drop for irrigation, leaf for fertilization, etc.) must always be used to support texts.
4. **Voice Usage:** A prominent "Microphone / Speak" button must be included in every field where text can be entered.
5. **Feedback:** When the user completes a task (e.g., irrigation done), a satisfying visual feedback (green check, animation) must be shown on the screen.

## 5. Web Panel (Agronomist/Admin) Design
- Unlike farmers, **data density and speed** are prioritized in the agronomist panel.
- Structures suitable for desktop use such as tables, filtering, quick search, and viewing case details on a single screen (split-screen or drawer) will be preferred.
