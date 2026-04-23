# Chapter 6.4 — Chat Endpoints

> Chat session persistence, intent classification, and AI-driven extraction endpoints.

← [API Contract Index](../ch06-api-contract/ch06-api-contract.md)

---

## Chat Session Persistence (Phase 1)

### `GET /api/chat/session`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Returns the active persisted chat session for the authenticated user. Phase 1 includes chat messages and minimal UI state (`editMode`).

**Response:**
```json
{
  "messages": [
    {
      "role": "assistant",
      "content": "Hello! I'm your AI Medicare Assistant.",
      "timestamp": "2026-04-06T10:15:32.000Z"
    }
  ],
  "uiState": {
    "editMode": false
  }
}
```

### `PATCH /api/chat/session/messages`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Persists the latest chat transcript for the current session (rolling window, max 200 messages on backend).

**Request:**
```json
{
  "messages": [
    {
      "role": "user",
      "content": "Change my ZIP to 33140",
      "timestamp": "2026-04-06T10:16:02.000Z"
    }
  ]
}
```

### `PATCH /api/chat/session/ui-state`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Persists minimal chat-related UI state for restore after refresh/login.

**Request:**
```json
{
  "editMode": true
}
```

---

## Chat Intent Classification

### `POST api/chat/intent`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Classifies a user's free-text chat message into one of 17 intents using AI (Anthropic Claude). Used by the frontend `ChatComponent` to route user messages to the appropriate action (navigation, plan section switching, actions, save analysis, or drug input flow). Optionally extracts parameters (names, profile fields, prescription names, analysis names) from the message. System prompt loaded from `Prompts/system/chat-intent-system.txt`.

**Request:**
```json
{
  "message": "I want to change my first name to Krishna",
  "isProfileComplete": true
}
```

**Response:**
```json
{
  "intent": "NAVIGATE_PROFILE",
  "params": {
    "firstName": "Krishna",
    "lastName": null,
    "prescriptionName": null,
    "analysisName": null,
    "gender": null,
    "dateOfBirth": null,
    "tobaccoStatus": null,
    "healthCondition": null,
    "taxFilingStatus": null,
    "coverageYear": null,
    "zipCode": null,
    "addressLine1": null,
    "lifeExpectancy": null
  },
  "confirmationMessage": "Opening your profile so you can update your first name."
}
```

**Intent Values:**

| Intent | Description |
|--------|-------------|
| `NAVIGATE_PROFILE` | View or edit profile (may extract firstName/lastName) |
| `NAVIGATE_ANALYSIS_DRUGS` | Go to drug analysis step |
| `NAVIGATE_PHARMACIES` | Find or view pharmacies |
| `NAVIGATE_PLANS` | View Medicare plan recommendations |
| `NAVIGATE_COST_PROJECTIONS` | View cost projections / financial forecasts |
| `SWITCH_TO_PDP` | Switch to Part D (PDP) + Medigap plan section |
| `SWITCH_TO_MA` | Switch to Medicare Advantage (MA) plan section |
| `ACTION_RESET_ANALYSIS` | Reset/clear the current analysis |
| `ACTION_SAVE_PRESCRIPTION` | Save current prescription (may extract prescriptionName) |
| `ACTION_SIGN_OUT` | Sign out / log out |
| `ACTION_LOAD_PRESCRIPTIONS` | Load previously saved prescriptions / navigate to saved data |
| `ACTION_SAVE_ANALYSIS` | Save current analysis as a named recommendation (may extract analysisName) |
| `ACTION_RUN_ANALYSIS` | Run/calculate cost analysis for the current plan |
| `NAVIGATE_SAVED_ANALYSES` | Navigate to the saved data page |
| `ACTION_HELP` | User asks "what can I do?" or requests help |
| `DRUG_INPUT` | Message contains drug names or a prescription list |
| `UNKNOWN` | None of the above apply |

> **Notes:**
> - `params` is optional and only populated for intents that support parameter extraction (`NAVIGATE_PROFILE` for names and profile fields, `ACTION_SAVE_PRESCRIPTION` for prescription name, `ACTION_SAVE_ANALYSIS` for analysis name).
> - `confirmationMessage` is a short, friendly message (max ~15 words) suitable for display in the chat panel.
> - On classification failure, the backend returns `{ "intent": "UNKNOWN", "confirmationMessage": "I'm not sure what you'd like to do..." }`.

---

## Chat Profile Extraction

### `POST api/chat/extract-profile`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Extracts profile fields from a user's natural language message. Used when the user is on the `/profile` page with an incomplete profile. The AI understands which fields are missing and asks for remaining ones.

**Request:**
```json
{
  "message": "I'm John Smith, male, born 01/15/1955, non-smoker, ZIP 80113",
  "missingFields": ["firstName", "lastName", "gender", "dateOfBirth", "tobaccoStatus", "zipCode"]
}
```

**Response:**
```json
{
  "extractedFields": {
    "firstName": "John",
    "lastName": "Smith",
    "gender": "Male",
    "dateOfBirth": "01/15/1955",
    "tobaccoStatus": "Non-Tobacco",
    "zipCode": "80113"
  },
  "reply": "Got it! I've filled in your name, gender, date of birth, tobacco status, and ZIP code. You still need to provide your health condition, tax filing status, and coverage year."
}
```

---

## Chat Drug Selection Extraction

### `POST api/chat/extract-drug-selection`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Extracts drug formulation selection commands from chat messages. Used when the user is on `/medicare-analysis/drugs` with loaded drug details. Supports selecting type, dosage form, strength, quantity, confirming all, removing, or editing drugs.

**Request:**
```json
{
  "message": "select Lisinopril generic tablet 10mg 30 per month",
  "availableDrugs": [
    {
      "name": "Lisinopril",
      "types": ["Generic", "Brand"],
      "dosageForms": { "Generic": ["Tablet", "Capsule"], "Brand": ["Tablet"] },
      "strengths": { "Generic|Tablet": ["5 MG", "10 MG", "20 MG", "40 MG"] }
    }
  ]
}
```

**Response:**
```json
{
  "drugName": "Lisinopril",
  "type": "Generic",
  "dosageForm": "Tablet",
  "strength": "10 MG",
  "quantity": 30,
  "action": "select",
  "reply": "Selected Lisinopril: Generic Tablet 10 MG, 30/month. Ready to confirm!"
}
```

**Action Values:** `select` (apply selection), `options` (show available formulations), `confirm_all` (confirm all complete selections), `remove` (remove drug from selections), `edit` (un-confirm for re-selection).

---

## Chat Pharmacy Selection Extraction

### `POST api/chat/extract-pharmacy-selection`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Extracts pharmacy selection/removal/search commands from chat messages. Used when the user is on `/medicare-analysis/pharmacies` with loaded pharmacy lookup results.

**Request:**
```json
{
  "message": "select the CVS pharmacy",
  "availablePharmacies": [
    { "name": "CVS PHARMACY", "address": "123 Main St", "distance": "1.2", "zipcode": "80113" },
    { "name": "WALGREENS", "address": "456 Oak Ave", "distance": "2.5", "zipcode": "80113" }
  ],
  "selectedPharmacies": []
}
```

**Response:**
```json
{
  "pharmacyName": "CVS PHARMACY",
  "action": "select",
  "searchTerm": null,
  "reply": "Selected **CVS PHARMACY** (123 Main St, 1.2 mi away). You can select up to 4 more pharmacies."
}
```

**Action Values:** `select` (add to selection), `remove` (deselect), `list` (show selected pharmacies), `search` (filter by name).

---

## Chat Plan Selection Extraction

### `POST api/chat/extract-plan-selection`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Extracts plan selection/removal/section-switching commands from chat messages. Used when the user is on `/medicare-analysis/plans` with loaded plan data.

**Request:**
```json
{
  "message": "select the SilverScript plan",
  "availablePlans": [
    { "planName": "SilverScript Choice", "planType": "PDP", "carrier": "CVS Health", "monthlyPremium": 28.50 },
    { "planName": "Humana Gold Plus", "planType": "MA-PD", "carrier": "Humana", "monthlyPremium": 0 }
  ],
  "selectedPlans": []
}
```

**Response:**
```json
{
  "planName": "SilverScript Choice",
  "planType": "PDP",
  "action": "select",
  "section": null,
  "reply": "Selected **SilverScript Choice** (PDP) plan."
}
```

**Action Values:** `select` (choose a plan), `remove` (deselect a plan), `switch_section` (change between PDP and MA views).

---

## ~~Chat Orchestrator~~ (Removed)

> The chatbot orchestrator (`ChatOrchestratorController`, `ChatOrchestratorService`, `ConvStateService`, `DeltaCalculationService`) has been fully removed from the codebase. Chat coordination is now handled by `ChatRouterService` with `ChatIntentService`, page-specific extraction services, and `ChatNavigationFlowService`.

---

← [API Contract Index](../ch06-api-contract/ch06-api-contract.md) | [← Pharmacy & Plans](ch06-03-pharmacy-plan-endpoints.md) | [Next: Recommendation & Prescription →](ch06-05-recommendation-prescription-endpoints.md)
