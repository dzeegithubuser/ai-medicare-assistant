# Chapter 6 — API Contract

> Endpoints, request/response schemas, and JSON examples.

---

## Authentication

### `POST api/auth/signup`

**Auth:** Public.

**Request:**
```json
{ "email": "user@example.com", "phone": "5551234567", "password": "Secret123!", "confirmPassword": "Secret123!" }
```

**Response:**
```json
{ "success": true, "message": "Account created.", "token": "<jwt>" }
```

---

### `POST api/auth/signin`

**Auth:** Public.

**Request:**
```json
{ "email": "user@example.com", "password": "Secret123!" }
```

**Response:**
```json
{ "success": true, "message": "Sign-in successful.", "token": "<jwt>" }
```

---

### `POST api/auth/forgot-password`

**Auth:** Public.

**Purpose:** Generates a 30-minute reset token and sends a password-reset email. Returns `success: true` regardless of whether the email exists (prevents enumeration).

**Request:**
```json
{ "email": "user@example.com" }
```

**Response:**
```json
{ "success": true, "message": "If that email exists you will receive a reset link shortly." }
```

---

### `POST api/auth/reset-password`

**Auth:** Public.

**Purpose:** Validates the JWT reset token (must have `purpose: password-reset` claim, expires in 30 min) and updates the password hash.

**Request:**
```json
{ "token": "<reset-jwt>", "newPassword": "NewSecret123!", "confirmPassword": "NewSecret123!" }
```

**Response (success):**
```json
{ "success": true, "message": "Password reset successfully." }
```

**Response (failure):**
```json
{ "success": false, "message": "Invalid or expired reset token." }
```

---

### `POST api/auth/change-password`

**Auth:** `[Authorize]` — Bearer JWT required.

**Purpose:** Changes the password for the currently authenticated user. Old password is verified with BCrypt before writing the new hash.

**Request:**
```json
{ "oldPassword": "CurrentSecret123!", "newPassword": "NewSecret456!", "confirmPassword": "NewSecret456!" }
```

**Response (success):**
```json
{ "success": true, "message": "Password changed successfully." }
```

**Response (failure — wrong old password):**
```json
{ "success": false, "message": "Current password is incorrect." }
```

> **Notes:**
> - `newPassword` minimum length is 8 characters.
> - `confirmPassword` must match `newPassword` (validated server-side via `[Compare]`).
> - `userId` is extracted from the `NameIdentifier` claim in the Bearer JWT — no userId in the request body.

---

## Drug Name Suggestion (Step 1)

### `POST api/drug/suggest-names`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Identifies and suggests correct pharmaceutical drug names from free-text user input. This is the first step of the drug search flow — the user confirms correct names before full analysis.

**Request:**
```json
{
  "input": "Eliquis 50mg 3daily, Ibuprofen 800mg, Naproxen 500mg"
}
```

**Response:**
```json
{
  "suggestions": [
    {
      "inputName": "Eliquis",
      "candidates": [
        { "name": "Eliquis", "type": "Brand", "confidence": 0.98 },
        { "name": "apixaban", "type": "Generic", "confidence": 0.95 }
      ]
    },
    {
      "inputName": "Ibuprofen",
      "candidates": [
        { "name": "Ibuprofen", "type": "Generic", "confidence": 0.99 }
      ]
    },
    {
      "inputName": "Naproxen",
      "candidates": [
        { "name": "Naproxen", "type": "Generic", "confidence": 0.99 },
        { "name": "Aleve", "type": "Brand", "confidence": 0.85 }
      ]
    }
  ]
}
```

> **Notes:**
> - `inputName` is the raw drug name extracted from the user's input (dosage/frequency stripped).
> - `candidates` is ordered by confidence (highest first), up to 3 per input.
> - `type` is either `"Brand"` or `"Generic"`.
> - If the input is not recognizable as a drug, `candidates` will be an empty array for that entry.

---

## Drug Analysis (Step 2)

### `POST api/drug/analyze`

**Auth:** `[Authorize]` — JWT required. Zipcode is read from the user's saved address profile.

**Request:**
```json
{
  "prescription": "Eliquis 5mg daily, Famciclovir 250mg"
}
```

**Response:**
```json
{
  "drugs": [
    {
      "drugInput": "Eliquis 5mg daily",
      "normalizedDrugName": "apixaban",
      "brandNames": ["Eliquis"],
      "genericName": "apixaban",
      "synonyms": ["Eliquis"],
      "therapeuticCategory": "anticoagulant",
      "drugClass": "Factor Xa inhibitor",
      "mechanismOfAction": "Selectively inhibits Factor Xa...",
      "dosageForms": ["tablet", "suspension"],
      "formulations": [
        {"dosageForm": "tablet", "strength": "2.5 mg", "packaging": "Bottle of 60 tablets", "ndcCode": "00003-0894-21"},
        {"dosageForm": "tablet", "strength": "5 mg", "packaging": "Bottle of 60 tablets", "ndcCode": "00003-0898-21"}
      ],
      "strengths": ["2.5 mg", "5 mg"],
      "packaging": ["Bottle of 60 tablets"],
      "rxNormId": "1364430",
      "ndcCodes": ["00003-0894-21", "00003-0898-21"],
      "estimatedRetailCostUSD": "$500 - $600",
      "estimatedMedicarePartDCostUSD": "$40 - $100",
      "medicareNegotiatedPriceUSD": "$0.50 - $5",
      "medicareVerificationLink": "https://www.medicare.gov/plan-compare/#/",
      "confidenceScore": 0.95,
      "alternatives": [
        {
          "name": "Warfarin",
          "type": "Generic",
          "costDifference": "-$450/year",
          "clinicalNote": "Requires INR monitoring; more drug/food interactions"
        }
      ],
      "genericSwitchSuggestion": {
        "from": "Eliquis",
        "to": "Apixaban",
        "estimatedSavings": "$200 - $350/year"
      },
      "contraindications": ["Active pathological bleeding", "Severe hepatic impairment"],
      "medicareCostEstimate": {
        "source": "CMS Medicare Part D Spending Data",
        "dataYear": "2022",
        "drugName": "ELIQUIS (apixaban)",
        "totalClaims": 52345678,
        "totalBeneficiaries": 3456789,
        "averageCostPerClaim": 285.50,
        "averageMedicarePaymentPerClaim": 210.75,
        "averageBeneficiaryCostShare": 74.25,
        "totalSpending": 14950000000,
        "averageSpendingPerBeneficiary": 4325.80
      }
    }
  ],
  "interactions": [
    {
      "drugA": "Eliquis",
      "drugB": "Ibuprofen",
      "severity": "High",
      "description": "Increased risk of bleeding when combining anticoagulants with NSAIDs",
      "clinicalConsequence": "Elevated bleeding risk including GI hemorrhage",
      "recommendation": "Avoid combination or use with extreme caution; monitor for signs of bleeding"
    }
  ],
  "dosageAlerts": [
    {
      "drugName": "Eliquis",
      "inputDosage": "50mg",
      "recommendedRange": "2.5 mg - 5 mg twice daily",
      "severity": "High",
      "message": "Dosage exceeds maximum recommended range"
    }
  ],
  "duplicateTherapies": [
    {
      "drugs": ["Ibuprofen", "Naproxen"],
      "therapeuticClass": "NSAID",
      "message": "Both drugs are NSAIDs — using together increases risk of GI bleeding and renal injury"
    }
  ],
}

> **Note:** `nearbyPharmacies` is **not** included in the drug analysis response. Pharmacy data is fetched on-demand via the separate `GET /api/pharmacy/search` endpoint.
```

> **Note:** `medicareCostEstimate` is `null` when CMS data is unavailable. `interactions`, `dosageAlerts`, and `duplicateTherapies` may be empty.

> **Validation:** If no valid drugs are identified (e.g. typos), the response contains `{ "drugs": [], "message": "No valid drugs could be identified..." }`.

---

## Pharmacy Endpoints

| Method | Route | Query Params | Auth | Description |
|--------|-------|------|------|-------------|
| GET | `/api/pharmacy/nearby` | `zip` (optional) | Bearer JWT | Lightweight NPI-only nearby pharmacy lookup (no pricing). Returns `PharmacyResult[]` for multi-select (up to 5). Falls back to user's saved zip. |
| GET | `/api/pharmacy/lookup` | `page`, `size`, `radius`, `name` (all optional) | Bearer JWT | Paginated pharmacy lookup via Financial Planner API. Uses lat/lng from user profile. Returns `PharmacyLookupResponse` with distance, address, zipcode. Defaults: page=1, size=20, radius=25. |
| GET | `/api/pharmacy/search` | `zip` (optional), `drugs` (comma-separated RxCUIs) | Bearer JWT | On-demand nearby pharmacies with AI pricing. Falls back to user's saved zip. |

### `GET api/pharmacy/lookup`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Returns paginated pharmacies near the user via the Financial Planner `getPharmacies` API. Uses latitude/longitude from the user's saved profile. Primary pharmacy source for step 2 of the analysis wizard — user searches, filters, and selects up to 5 pharmacies.

**Query Parameters:** `page` (default 1), `size` (default 20), `radius` (miles, default 25), `name` (optional pharmacy name filter).

**Response:**
```json
{
  "pharmacies": [
    {
      "pharmacyNumber": "0347198",
      "pharmacyName": "CVS PHARMACY",
      "latitude": 39.6019,
      "longitude": -104.8766,
      "address": "499 E HAMPDEN AVE, STE 150, ENGLEWOOD",
      "distance": "2.3",
      "zipcode": "80113"
    }
  ],
  "page": 1,
  "size": 20,
  "totalPharmacies": 142,
  "totalPages": 8,
  "searchRadiusInMiles": 25
}
```

> **Notes:**
> - `distance` is in miles from the user's profile coordinates.
> - `pharmacyNumber` is the Financial Planner pharmacy identifier (not NPI).
> - Returns 400 if user profile has no latitude/longitude.

---

### `GET api/pharmacy/nearby`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Returns lightweight pharmacy location data (NPI Registry only, no pricing). Legacy endpoint — kept for backward compatibility. See `GET api/pharmacy/lookup` for the primary pharmacy lookup.

**Query Parameters:** `zip` (optional) — falls back to user's saved address zip.

**Response:**
```json
[
  {
    "npi": "1234567890",
    "name": "CVS PHARMACY",
    "legalName": "CVS PHARMACY INC",
    "address": "123 MAIN ST",
    "addressLine2": "",
    "city": "BEVERLY HILLS",
    "state": "CA",
    "zipCode": "90210",
    "phone": "3105551234",
    "fax": "3105551235",
    "pharmacyType": "Community/Retail Pharmacy",
    "enumerationDate": "2005-01-15"
  }
]
```

### `GET api/pharmacy/search`

**Response:**
```json
[
  {
    "pharmacy": { "npi": "1234567890", "name": "CVS PHARMACY", "address": "123 MAIN ST", "city": "BEVERLY HILLS", "state": "CA", "zipCode": "90210", "phone": "3105551234" },
    "drugs": [
      { "drugName": "apixaban", "ndc": "00003-0894-21", "rxCui": "1364430", "retailPrice": 285.50, "medicarePrice": null, "genericPrice": null }
    ],
    "totalRetailCost": 285.50,
    "totalMedicareCost": null,
    "totalGenericCost": null
  }
]
```

---

## Reference Data Endpoint

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/reference-data` | Public | All master data for profile form dropdowns |

**Response:**
```json
{
  "genders": [{ "value": "Male", "label": "Male" }],
  "maritalStatuses": [{ "value": "Single", "label": "Single" }],
  "taxFilingStatuses": [{ "value": "Single", "label": "Single" }],
  "incomeFilingStatuses": [{ "value": "Single", "label": "Single" }],
  "magiTiersByFiling": {
    "Single": [
      { "value": "Tier 1", "label": "Tier 1 — ≤ 138% FPL (≤ $21,597)", "description": "Medicaid eligible" },
      { "value": "Tier 2", "label": "Tier 2 — 139–200% FPL ($21,598–$31,200)", "description": "Enhanced subsidies" },
      { "value": "Tier 3", "label": "Tier 3 — 201–250% FPL ($31,201–$39,000)", "description": "Cost-sharing reductions" },
      { "value": "Tier 4", "label": "Tier 4 — 251–400% FPL ($39,001–$62,400)", "description": "Premium tax credits" },
      { "value": "Tier 5", "label": "Tier 5 — > 400% FPL (> $62,400)", "description": "No subsidies" }
    ],
    "Married Filing Jointly": [
      { "value": "Tier 1", "label": "Tier 1 — ≤ 138% FPL (≤ $29,274)", "description": "Medicaid eligible" }
    ]
  },
  "tobaccoStatuses": [{ "value": "Non-user", "label": "Non-user" }],
  "disabilityStatuses": [{ "value": "None", "label": "None" }],
  "chronicConditions": [
    { "value": "Hypertension", "label": "Hypertension (High Blood Pressure)" },
    { "value": "Type 2 Diabetes", "label": "Type 2 Diabetes" }
  ],
  "usStates": [{ "value": "AL", "label": "Alabama" }],
  "householdSizes": [{ "value": 1, "label": "1 (Individual)" }, { "value": 2, "label": "2 people" }]
}
```

---

## Profile Endpoints

| Method | Route | Body | Auth | Description |
|--------|-------|------|------|-------------|
| GET | `/api/profile` | — | Bearer JWT | `UserProfileResponse` with profile + `isProfileComplete` |
| POST | `/api/profile` | `ProfileDto` | Bearer JWT | Save/update consolidated profile |

**ProfileDto:**
```json
{
  "firstName": "Bill",
  "lastName": "Mrs",
  "coverageYear": 2026,
  "healthCondition": 1,
  "taxFilingStatus": "MARRIED_FILING_JOINTLY",
  "magiTier": "Tier 1",
  "gender": "F",
  "tobaccoStatus": 0,
  "dateOfBirth": "1960-05-15",
  "concierge": 0,
  "conciergeAmount": null,
  "alternateEmail": null,
  "alternateMobile": null,
  "lifeExpectancy": 95,
  "addressLine1": "123 Main St",
  "city": "New York",
  "state": "NY",
  "zipCode": "10001",
  "county": "New York",
  "countyCode": "36061",
  "latitude": 40.7128,
  "longitude": -74.006
}
```

**Response:**
```json
{
  "profile": {
    "firstName": "Bill",
    "lastName": "Mrs",
    "coverageYear": 2026,
    "healthCondition": 1,
    "taxFilingStatus": "MARRIED_FILING_JOINTLY",
    "magiTier": "Tier 1",
    "gender": "F",
    "tobaccoStatus": 0,
    "dateOfBirth": "1960-05-15",
    "concierge": 0,
    "conciergeAmount": null,
    "alternateEmail": null,
    "alternateMobile": null,
    "lifeExpectancy": 95,
    "addressLine1": "123 Main St",
    "city": "New York",
    "state": "NY",
    "zipCode": "10001",
    "county": "New York",
    "countyCode": "36061",
    "latitude": 40.7128,
    "longitude": -74.006
  },
  "isProfileComplete": true
}
```

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

## Plan Recommendation Endpoints

### `POST api/plan-recommendation`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Generates 5 ranked Medicare plan recommendations based on the user's profile and drug list.

**Request:**
```json
{
  "drugs": [
    {
      "rxCui": "1364430",
      "drugName": "apixaban",
      "genericName": "apixaban",
      "ndc": "00003-0894-21",
      "estimatedRetailPrice": 500
    }
  ],
  "selectedPharmacies": [
    { "npi": "1234567890", "name": "CVS PHARMACY", "pharmacyType": "Community/Retail Pharmacy" },
    { "npi": "9876543210", "name": "WALGREENS", "pharmacyType": "Community/Retail Pharmacy" }
  ]
}
```

**Response:**
```json
{
  "lisEligible": false,
  "lisTier": "None",
  "recommendedPlanType": "MedicareAdvantage",
  "eligibilitySummary": "Based on your income...",
  "lisCallToAction": null,
  "rankedPlans": [
    {
      "planId": "H1234-001-0",
      "planName": "Aetna Medicare Advantage",
      "planType": "MA-PD",
      "planCategory": "MA_ONLY",
      "insuranceName": "Aetna",
      "monthlyPremium": 0,
      "annualDeductible": 505,
      "annualMoop": 8300,
      "estimatedAnnualDrugCost": 1200,
      "estimatedAnnualTotalCost": 1200,
      "drugCoverages": [
        {
          "drugName": "apixaban",
          "rxCui": "1364430",
          "isCovered": true,
          "formularyTier": 3,
          "monthlyCopay": 47,
          "requiresPriorAuth": false,
          "hasQuantityLimit": false,
          "quantityLimitDetail": null
        }
      ],
      "aiExplanation": "This plan offers...",
      "starRating": "4.5",
      "hasPreferredPharmacyNetwork": true,
      "planFinderUrl": "https://www.medicare.gov/plan-compare/#/",
      "networkType": "HMO",
      "includesDental": true,
      "includesVision": true,
      "includesHearing": true,
      "includesFitness": true,
      "includesOtc": true,
      "otcAllowancePerQuarter": 50.00,
      "gapCoverage": "Some",
      "mailOrderSavings": true,
      "providerNetworkSize": "Large",
      "emergencyCoverage": true,
      "pros": ["$0 premium", "Includes dental & vision", "Large provider network"],
      "cons": ["HMO requires referrals", "No out-of-network coverage"],
      "costBreakdowns": [
        {
          "pharmacyName": "CVS PHARMACY",
          "pharmacyNpi": "1234567890",
          "isPreferredPharmacy": true,
          "annualPremium": 0,
          "annualDeductible": 505,
          "annualDrugCopay": 451.20,
          "annualTotal": 956.20,
          "drugCopays": [
            {
              "drugName": "apixaban",
              "rxCui": "1364430",
              "formularyTier": 3,
              "monthlyCopay": 37.60,
              "annualCopay": 451.20,
              "isCovered": true,
              "preferredDiscount": true
            }
          ]
        },
        {
          "pharmacyName": "WALGREENS",
          "pharmacyNpi": "9876543210",
          "isPreferredPharmacy": false,
          "annualPremium": 0,
          "annualDeductible": 505,
          "annualDrugCopay": 564,
          "annualTotal": 1069,
          "drugCopays": [
            {
              "drugName": "apixaban",
              "rxCui": "1364430",
              "formularyTier": 3,
              "monthlyCopay": 47,
              "annualCopay": 564,
              "isCovered": true,
              "preferredDiscount": false
            }
          ]
        }
      ]
    }
  ]
}
```

> **Note:** `costBreakdowns` is only populated when `selectedPharmacies` is provided. Sorted cheapest-first. Preferred pharmacies get ~20% copay discount. Plan-level `estimatedAnnualTotalCost` uses the cheapest pharmacy's total.

### `GET api/plan-recommendation/lis-check`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Quick check of Low-Income Subsidy eligibility based on user's saved income profile.

**Response:**
```json
{
  "lisEligible": true,
  "lisTier": "Full"
}
```

### `POST api/plan-recommendation/evaluate-costs`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Calculates lifetime Medicare cost projections via the Financial Planner API, then runs AI evaluation to produce chart-ready insights with cost trajectory, category breakdowns, yearly highlights, savings tips, and an overall assessment. Delegates to `CostProjectionService.EvaluateCostsAsync()`.

**Request:**
```json
{
  "planBundleCode": "MA-PD",
  "medicareAdvantagePremium": 0,
  "maWithPrescriptionBenefit": true,
  "partDOOP": 1200,
  "partDOOPFullYear": 1200,
  "partABenefitServiceCost": 0,
  "partBBenefitServiceCost": 0,
  "planRecommendName": "Aetna Medicare Advantage",
  "recommendationListId": "6654a1b2c3d4e5f678901234",
  "supplementDataProvided": true,
  "partDDataProvided": true,
  "reserveDaysUsed": 0,
  "dental": false,
  "dentalHealthGrade": "",
  "boughtPlanA": false,
  "medicareAdvantageDataProvided": true,
  "partDPremium": 0,
  "calculateForAdjustedMonth": true,
  "supplementPlanType": "G"
}
```

**Response:**
```json
{
  "yearlyDetails": [
    {
      "year": 2026,
      "monthsUsedForExpenseCalc": 9,
      "partAPremium": 0,
      "partBPremium": 1746.00,
      "partBPremiumSurcharge": 0,
      "medicareAdvantagePremium": 0,
      "partDPremium": 0,
      "partDPremiumSurcharge": 0,
      "conciergePremium": 0,
      "partAOOP": 0,
      "partBOOP": 500.00,
      "partDOOP": 1200.00,
      "totalABMedicareAdvantage": 3446.00,
      "reserveDaysLeft": 60,
      "dentalPremium": 0,
      "dentalOOP": 0
    }
  ],
  "lifetimeTotals": {
    "lifeTimeABMedicareAdvantageExpenses": 125000.00,
    "lifeTimeABMedicareAdvantagePremium": 85000.00,
    "lifeTimeABMedicareAdvantageOop": 40000.00,
    "lifeTimeDSurcharge": 0,
    "lifeTimeBSurcharge": 0,
    "totalIrmaa": 0,
    "supplementPlanType": "G",
    "supplementPlanPremium": 150.00
  },
  "evaluation": {
    "planName": "Aetna Medicare Advantage",
    "planBundleCode": "MA-PD",
    "lifetimeSummary": {
      "totalPremiums": 85000.00,
      "totalOutOfPocket": 40000.00,
      "totalCombined": 125000.00,
      "projectionYears": 29,
      "averageAnnualCost": 4310.34
    },
    "costTrajectory": "Rising",
    "trajectoryExplanation": "Costs increase steadily due to inflation and aging...",
    "yearlyHighlights": [
      { "year": 2026, "totalCost": 3446.00, "flag": "Lowest", "explanation": "First year with partial months" },
      { "year": 2054, "totalCost": 8200.00, "flag": "Highest", "explanation": "Peak costs near end of projection" }
    ],
    "categories": [
      { "name": "Part B Premium", "lifetimeTotal": 65000.00, "percentOfTotal": 52.0, "trend": "Rising", "insight": "Largest cost driver..." },
      { "name": "Part D OOP", "lifetimeTotal": 30000.00, "percentOfTotal": 24.0, "trend": "Stable", "insight": "Drug costs remain steady..." }
    ],
    "savingsTips": [
      { "title": "Consider Generic Alternatives", "description": "Switching to generic drugs could reduce Part D OOP...", "estimatedSavings": "$5,000-$10,000 lifetime", "priority": "High" }
    ],
    "overallAssessment": "This MA-PD plan provides good value with $0 premium and moderate OOP costs..."
  }
}
```

> **Note:** The `yearlyDetails` array contains the raw Financial Planner API result. The `evaluation` object is AI-generated and includes chart-ready data for visualization. `costTrajectory` is one of: `Rising`, `Stable`, `Declining`, `Mixed`. `yearlyHighlights[].flag` is one of: `Highest`, `Lowest`, `Spike`, `Normal`. `categories[].trend` is one of: `Rising`, `Stable`, `Declining`. `savingsTips[].priority` is one of: `High`, `Medium`, `Low`.

---

## Plan-Aware Pharmacy Search

### `POST api/pharmacy/plan-search`

**Auth:** `[Authorize]` — JWT required.

**Purpose:** Searches nearby pharmacies and overlays plan-specific copay pricing from the selected plan's formulary data.

**Request:**
```json
{
  "planId": "H1234-001-0",
  "drugs": [
    { "rxCui": "1364430", "drugName": "apixaban" }
  ],
  "planCoverages": [
    {
      "rxCui": "1364430",
      "drugName": "apixaban",
      "formularyTier": 3,
      "monthlyCopay": 47,
      "isCovered": true,
      "requiresPriorAuth": false
    }
  ]
}
```

**Response:** Same structure as `GET /api/pharmacy/search` but with plan-aware fields: `planCopay`, `formularyTier`, `requiresPriorAuth`, `isPreferredPharmacy` per drug, and `totalPlanCopay`, `isPreferredNetwork` per pharmacy.

---

## Migration Endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/migration/applied` | Public | Lists all applied EF Core migrations |
| GET | `/api/migration/pending` | Public | Lists pending (unapplied) migrations |
| POST | `/api/migration/apply` | Public | Applies all pending migrations to the database |

---

## County Lookup Endpoints

### `POST api/county-lookup/getCountycodeList`

**Auth:** Public (no JWT required).

**Purpose:** Returns county code entries for a given ZIP code. Used by the profile form for county/city cascading dropdowns.

**Request:**
```json
{
  "zipCode": "80112"
}
```

**Response:**
```json
[
  {
    "countyCode": "08005",
    "countyName": "Arapahoe",
    "stateCode": "CO",
    "city": "Centennial"
  }
]
```

### `GET api/county-lookup/constants/magi-tiers`

**Auth:** Public (no JWT required).

**Purpose:** Returns MAGI tier options for a given tax filing status and coverage year. Used by the profile form to populate the MAGI tier dropdown dynamically based on the user's selected tax filing status.

**Query Parameters:** `filingStatus` (required), `coverageYear` (required).

**Response:**
```json
[
  { "value": "Tier 1", "label": "Tier 1 — ≤ 138% FPL (≤ $21,597)", "description": "Medicaid eligible" },
  { "value": "Tier 2", "label": "Tier 2 — 139–200% FPL ($21,598–$31,200)", "description": "Enhanced subsidies" },
  { "value": "Tier 3", "label": "Tier 3 — 201–250% FPL ($31,201–$39,000)", "description": "Cost-sharing reductions" },
  { "value": "Tier 4", "label": "Tier 4 — 251–400% FPL ($39,001–$62,400)", "description": "Premium tax credits" },
  { "value": "Tier 5", "label": "Tier 5 — > 400% FPL (> $62,400)", "description": "No subsidies" }
]
```

---

## Financial Planner Drug Endpoints

### `POST api/FinancialPlannerDrug/search-bulk`

**Auth:** `[Authorize]` (JWT required).

**Purpose:** Searches multiple drugs, matches each by `displayName`, fetches formulation details, and if more than one drug, calls AI to evaluate pairwise interactions and detect duplicate therapies.

**Request:**
```json
{
  "drugNames": ["Eliquis", "Lisinopril", "Metformin"]
}
```

**Response:**
```json
{
  "results": [
    {
      "drugName": "Eliquis",
      "search": { "webServiceStatus": "SUCCESS", "drugList": [...] },
      "matchedDrug": { "rxcui": "1364441", "displayName": "Eliquis", "prescription": true },
      "detail": { "drugDetailAdvanceList": [...] }
    }
  ],
  "interactions": [
    {
      "drugA": "Eliquis",
      "drugB": "Lisinopril",
      "severity": "Moderate",
      "description": "Concurrent use may increase bleeding risk",
      "clinicalConsequence": "Potential for enhanced anticoagulant effect",
      "recommendation": "Monitor for signs of bleeding"
    }
  ],
  "duplicateTherapies": []
}
```

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

## Chat Orchestrator

> Conversational endpoint for managing Medicare recommendations through natural language. Uses a finite state machine with 19 domain intents.

### `POST /api/chat/orchestrate`

**Auth:** Bearer JWT required

**Request:**
```json
{
  "message": "change my ZIP code to 33140"
}
```

**Response (200):**
```json
{
  "message": "Change **ZIP code** from **90210** to **33140**?\n\n_Note: A full cost recalculation will be performed after confirmation._",
  "requiresConfirmation": true,
  "delta": {
    "previousLifetimeTotal": 485000,
    "updatedLifetimeTotal": 462000,
    "previousCurrentYearTotal": 14500,
    "updatedCurrentYearTotal": 13800,
    "previousPresentValue": 310000,
    "updatedPresentValue": 295000,
    "fieldChanged": "ZIP code",
    "previousValue": "90210",
    "newValue": "33140",
    "narrativeSummary": "Moving to **33140** reduces your lifetime costs by **$23,000**...",
    "ltcPresentValueDelta": -15000
  },
  "displayData": null,
  "nextIntent": null
}
```

**Response fields:**
| Field | Type | Description |
|-------|------|-------------|
| `message` | string | Markdown-formatted assistant response |
| `requiresConfirmation` | bool | Whether the UI should show yes/no buttons |
| `delta` | DeltaResult? | Cost impact preview (lifetime, current year, present value) |
| `displayData` | DisplayData? | Structured UI data — `type` controls rendering (`help_menu`, `projections`, `plan_detail`, `summary`) |
| `nextIntent` | string? | Hint for follow-up intent routing |

**Confirmation flow:** When `requiresConfirmation=true`, the frontend sends `"yes"` or `"no"` as the next message. The backend either commits the pending change or cancels it.

**Delete flow:** Delete requires two-step confirmation: (1) "Are you sure?" → yes → (2) "Type DELETE MY RECOMMENDATION exactly" → phrase match → deleted.

**Error (400):**
```json
{ "error": "Message is required." }
```

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
  "pharmacy": null,
  "mailOrderPharmacy": null,
  "lastCostSnapshot": null
}
```

**Response (404):** No active recommendation.

### `GET /api/recommendation/{id}`

**Auth:** Bearer JWT required

**Purpose:** Returns a full recommendation by ID. Used by the saved analysis selection flow to load drug list and pharmacy into the wizard.

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
Updates pharmacy selection. **Request:** `{ "pharmacy": { ... }, "mailOrder": { ... } }`

### `PUT /api/recommendation/plans`
Updates plan selections. **Request:** `{ "plans": [{ "planType": "PDP", "planName": "SilverScript", "carrier": "CVS Health", "monthlyPremium": 28.50, "planId": "S1234" }] }`

### `DELETE /api/recommendation?confirmed=true`
Permanently deletes recommendation. Query parameter `confirmed=true` required.

---

## Long Term Care Endpoint

### `POST /api/long-term-care`

**Auth:** Bearer JWT required.

**Purpose:** Calculates Long Term Care cost projections using the Financial Planner LTC API. Backend reads user profile (DOB, gender, state, ZIP, countyCode, lifeExpectancy, tobaccoStatus) from DB and merges with care-type selections from the request body.

**Request:**
```json
{
  "healthProfile": 2,
  "numberOfAdultDayHealthCareLTCYears": 0,
  "numberOfHomeCareLTCYears": 3,
  "numberOfNursingCareLTCYears": 2
}
```

**Response:**
```json
{
  "age": 65,
  "healthProfile": 2,
  "gender": "M",
  "state": "FL",
  "zipcode": 33140,
  "countyCode": 11,
  "lifeExpenctancy": 95,
  "tobaccoUsage": false,
  "currentLifeStyleExpenses": 0,
  "numberOfHomeCare​LTCYears": 3,
  "numberOfNursingCare​LTCYears": 2,
  "homeCare": 95000,
  "presentValueHomeCare": 72000,
  "nursingCare": 180000,
  "presentValueNursingCare": 130000,
  "futureHomeCareExpenseList": [
    { "year": 2030, "expense": 28000 },
    { "year": 2031, "expense": 29400 },
    { "year": 2032, "expense": 30870 }
  ],
  "futureNursingCareExpensesList": [
    { "year": 2033, "expense": 89000 },
    { "year": 2034, "expense": 93450 }
  ],
  "expectedHomeCare": 95000,
  "presentValueExpectedHomeCare": 72000,
  "expectedNursingCare": 180000,
  "presentValueExpectedNursingCare": 130000
}
```

---

## LTC Selections Endpoints

### `PUT /api/ltc/current`

**Auth:** Bearer JWT required.

**Purpose:** Saves the user's current LTC care-type selections to MongoDB (`ltcCurrentSelections` collection). One document per user — upserted on each call.

**Request:**
```json
{
  "healthProfile": 2,
  "adultDayYears": 0,
  "homeCareYears": 3,
  "nursingCareYears": 2,
  "ltcResultJson": "{ ... serialized LtcProjectionResponse ... }"
}
```

**Response (200):** `{ "success": true }`

### `GET /api/ltc/current`

**Auth:** Bearer JWT required.

**Purpose:** Returns the most recently saved LTC selections for the current user.

**Response (200):** Same shape as PUT request body.  
**Response (404):** No LTC selections saved yet.

---

## Financial Planner Plan Recommendation Endpoints

### `POST /api/PartDPlan/recommend`

**Auth:** Bearer JWT required.

**Purpose:** Returns Part D (PDP) plan recommendations from the Financial Planner API for the user's confirmed drug selections.

**Request:** `PartDPlanRecommendationRequest` — includes `userId`, `countycodeModel` (ZIP, state, lat/lng, countyCode), `prescriptions` (rx inputs), `pharmacies`, `taxFilingStatus`, `magiTier`, `healthGrade`, `birthDate`, `coverageYear`, pagination, and filter fields.

**Response:** Financial Planner `PartDPlanRecommendationResponse` — plan list with premiums, OOP, drug costs, and pharmacy network data.

---

### `POST /api/MedicareAdvantagePlan/recommend`

**Auth:** Bearer JWT required.

**Purpose:** Returns Medicare Advantage (MA-PD) plan recommendations from the Financial Planner API. Same request shape as Part D with `medicareAdvantage: true`.

**Request:** `MedicareAdvantagePlanRequest` — extends `PartDPlanRecommendationRequest` with `medicareAdvantage: true`.

**Response:** `MedicareAdvantagePlanResponse` — same type as `PartDPlanRecommendationResponse`.

---

### `POST /api/MedigapPlan/quotes`

**Auth:** Bearer JWT required.

**Purpose:** Returns Medigap (Medicare Supplement) plan quotes from the Financial Planner API.

**Request:**
```json
{
  "zip5": "33140",
  "gender": "M",
  "tobacco": 0,
  "birthDate": "05-1960",
  "plan": "G",
  "county": "Miami-Dade",
  "taxFilingStatus": "SINGLE",
  "magiTier": 1,
  "healthProfile": 2,
  "coverageYear": "2026",
  "versionId": "AIVANTE"
}
```

**Response:** `MedigapPlanQuotesResponse` — `{ contractIdCarrierMap, deductible, planList: MedigapPlan[] }` where each `MedigapPlan` includes carrier, rate range, rate type, discounts, fees, select network info.

---

← [Chapter 5 — Data & Authentication](ch05-data-and-auth.md) | [Table of Contents](APPLICATION_BLUEPRINT.md) | [Chapter 7 → Project Structure](ch07-project-structure.md)
