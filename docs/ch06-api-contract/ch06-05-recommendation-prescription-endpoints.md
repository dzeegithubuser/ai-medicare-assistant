# Chapter 6.5 — Recommendation & Prescription Endpoints

> Recommendation CRUD for saved analyses and legacy prescription persistence.

← [API Contract Index](../ch06-api-contract/ch06-api-contract.md)

---

## Recommendation Endpoints

> CRUD operations for the user's active Medicare recommendation document.

### `GET /api/recommendation`

**Auth:** Bearer JWT required

**Response (200):**
```json
{
  "id": "6612a...",
  "userId": "guid",
  "name": "Medicare Plan — Apr 2026",
  "profile": {
    "firstName": "John", "lastName": "Doe",
    "dateOfBirth": "1960-05-15", "gender": "M",
    "zipCode": "33140", "county": "Miami-Dade", "state": "FL",
    "healthCondition": 2, "lifeExpectancy": 95,
    "tobaccoStatus": 0, "taxFilingStatus": "SINGLE",
    "magiTier": "1", "coverageYear": 2026,
    "concierge": 0, "conciergeAmount": null
  },
  "drugList": [],
  "planSelections": [],
  "pharmacies": [],
  "mailOrderPharmacy": null,
  "lastCostSnapshot": null
}
```

**Response (404):** No active recommendation.

### `GET /api/recommendation/{id}`

**Auth:** Bearer JWT required

**Purpose:** Returns a full recommendation by ID. Used by the saved analysis selection flow to load drug list and pharmacies into the wizard.

**Response (200):** Same schema as `GET /api/recommendation` (full recommendation document).

**Response (404):** Recommendation not found or does not belong to user.

### `GET /api/recommendation/all`

**Auth:** Bearer JWT required

**Purpose:** Returns all saved recommendations/analyses for the authenticated user, sorted by creation date descending. Used by the Saved Data page.

**Response (200):**
```json
[
  {
    "id": "6612a...",
    "name": "Medicare Plan — Apr 2026",
    "status": "completed",
    "drugCount": 3,
    "planCount": 2,
    "hasCostSnapshot": true,
    "lifetimeTotal": 485000,
    "createdAt": "2026-04-08T10:30:00Z",
    "updatedAt": "2026-04-08T11:00:00Z"
  }
]
```

### `POST /api/recommendation`

**Request:**
```json
{
  "name": "Medicare Plan — Apr 2026",
  "profile": { ... },
  "drugs": [{ "drugName": "Eliquis", "dosage": "5mg", "quantity": 60 }],
  "plans": [{
    "planType": "PDP", "planName": "SilverScript", "carrier": "CVS Health",
    "monthlyPremium": 28.50, "planId": "S1234",
    "deductible": 590, "starRating": 4.5,
    "totalPrescriptionCost": 1200, "totalPlanCost": 1542,
    "prescriptionDrugCovered": true,
    "unavailableDrugs": [],
    "planExpenses": [{ "name": "Annual Premium", "amount": 342 }]
  }],
  "costSnapshot": {
    "lifetimeTotal": 485000, "currentYearTotal": 14500,
    "averageAnnual": 16000, "projectionYears": 30,
    "lifetimePremiums": 300000, "lifetimeOOP": 150000,
    "lifetimeIrmaa": 35000, "costTrajectory": "Rising",
    "supplementPlanType": "Plan G", "supplementPlanPremium": 150,
    "yearlyDetails": [{ "year": 2026, "partAPremium": 0, "partBPremium": 185, "..." : "..." }],
    "evaluation": {
      "planName": "SilverScript", "planBundleCode": "S1234",
      "lifetimeSummary": { "totalPremiums": 300000, "totalOutOfPocket": 150000, "totalCombined": 485000, "projectionYears": 30, "averageAnnualCost": 16000 },
      "costTrajectory": "Rising", "trajectoryExplanation": "...",
      "yearlyHighlights": [], "categories": [], "savingsTips": [],
      "overallAssessment": "..."
    }
  },
  "force": false
}
```

**Response (201):** Created recommendation document.
**Response (409):** Recommendation already exists for this user (use `force: true` to overwrite).

### `PUT /api/recommendation/profile`
Updates profile snapshot. **Request:** `ProfileSnapshot` object.

### `PUT /api/recommendation/drugs`
Updates drug list. **Request:** `{ "drugs": [{ "drugName": "Eliquis", "dosage": "5mg", "quantity": 60, "refillFrequency": "Monthly" }] }`

### `PUT /api/recommendation/pharmacy`
Updates pharmacy selection. **Request:** `{ "pharmacies": [{ ... }], "mailOrder": { ... } }`

### `PUT /api/recommendation/plans`
Updates plan selections. **Request:** `{ "plans": [{ "planType": "PDP", "planName": "SilverScript", "carrier": "CVS Health", "monthlyPremium": 28.50, "planId": "S1234" }] }`

### `DELETE /api/recommendation?confirmed=true`
Permanently deletes recommendation. Query parameter `confirmed=true` required.

---

## Prescription Endpoints

### `POST api/prescription`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Saves a named prescription with all confirmed drug selections to MongoDB.

**Request:**
```json
{
  "name": "My Daily Prescription",
  "drugs": [
    {
      "drugInput": "Eliquis 5mg",
      "normalizedDrugName": "apixaban",
      "genericName": "apixaban",
      "selectedName": "Eliquis",
      "nameType": "brand",
      "dosageForm": "tablet",
      "strength": "5 mg",
      "packaging": "Bottle of 60 tablets",
      "rxNormId": "1364430",
      "ndcCode": "00003-0898-21",
      "therapeuticCategory": "anticoagulant",
      "drugClass": "Factor Xa inhibitor"
    }
  ]
}
```

**Response:**
```json
{
  "id": "6654a1b2c3d4e5f678901234",
  "name": "My Daily Prescription",
  "createdDate": "2026-03-20T10:30:00Z",
  "drugs": [
    {
      "drugInput": "Eliquis 5mg",
      "normalizedDrugName": "apixaban",
      "genericName": "apixaban",
      "selectedName": "Eliquis",
      "nameType": "brand",
      "dosageForm": "tablet",
      "strength": "5 mg",
      "packaging": "Bottle of 60 tablets",
      "rxNormId": "1364430",
      "ndcCode": "00003-0898-21",
      "therapeuticCategory": "anticoagulant",
      "drugClass": "Factor Xa inhibitor"
    }
  ]
}
```

### `GET api/prescription`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Returns all prescriptions for the authenticated user, ordered by most recent.

**Response:**
```json
[
  {
    "id": "6654a1b2c3d4e5f678901234",
    "name": "My Daily Prescription",
    "createdDate": "2026-03-20T10:30:00Z",
    "drugs": [...]
  }
]
```

---

### `GET api/prescription/{id}`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Returns a single prescription by ID. Used by the saved prescription selection flow to load full drug details into the wizard.

**Response (200):**
```json
{
  "id": "6654a1b2c3d4e5f678901234",
  "name": "My Daily Prescription",
  "createdDate": "2026-03-20T10:30:00Z",
  "drugs": [
    {
      "drugInput": "Eliquis",
      "normalizedDrugName": "apixaban",
      "genericName": "apixaban",
      "selectedName": "Eliquis",
      "nameType": "brand",
      "dosageForm": "Tablet",
      "strength": "5 MG",
      "packaging": "30 per pack",
      "rxNormId": "1364430",
      "ndcCode": "00003-0894-21"
    }
  ]
}
```

**Response (404):** Prescription not found or does not belong to user.

---

← [API Contract Index](../ch06-api-contract/ch06-api-contract.md) | [← Chat Endpoints](ch06-04-chat-endpoints.md) | [Next: LTC Endpoints →](ch06-06-ltc-endpoints.md)
