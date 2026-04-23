# Chapter 6.2 — Drug Endpoints

> Drug name suggestion, full analysis, and Financial Planner drug search.

← [API Contract Index](../ch06-api-contract/ch06-api-contract.md)

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
```

> **Note:** `medicareCostEstimate` is `null` when CMS data is unavailable. `interactions`, `dosageAlerts`, and `duplicateTherapies` may be empty.

> **Note:** `nearbyPharmacies` is **not** included in the drug analysis response. Pharmacy data is fetched on-demand via the separate `GET /api/pharmacy/lookup` endpoint.

> **Validation:** If no valid drugs are identified (e.g. typos), the response contains `{ "drugs": [], "message": "No valid drugs could be identified..." }`.

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

← [API Contract Index](../ch06-api-contract/ch06-api-contract.md) | [← Auth & Profile](ch06-01-auth-profile-endpoints.md) | [Next: Pharmacy & Plan Endpoints →](ch06-03-pharmacy-plan-endpoints.md)
