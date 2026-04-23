# Chapter 6.3 — Pharmacy & Plan Endpoints

> Pharmacy lookup, plan recommendations, cost projections, and Financial Planner plan APIs.

← [API Contract Index](../ch06-api-contract/ch06-api-contract.md)

---

## Pharmacy Endpoints

| Method | Route | Query Params | Auth | Description |
|--------|-------|------|------|-------------|
| GET | `/api/pharmacy/lookup` | `page`, `size`, `radius`, `name` (all optional) | Bearer JWT | Paginated pharmacy lookup via Financial Planner API. Uses lat/lng from user profile. Returns `PharmacyLookupResponse` with distance, address, zipcode. Defaults: page=1, size=20, radius=25. |

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

← [API Contract Index](../ch06-api-contract/ch06-api-contract.md) | [← Drug Endpoints](ch06-02-drug-endpoints.md) | [Next: Chat Endpoints →](ch06-04-chat-endpoints.md)
