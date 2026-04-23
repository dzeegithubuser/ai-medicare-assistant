# Medicare Analysis — Cost Projection Architecture & Calculation Logic

## Entry Point

The user triggers cost projection from the Plans step (via the chat assistant or "Run Analysis" button). This calls:

```
POST /api/plan-recommendation/evaluate-costs  (PlanRecommendationController.EvaluateCosts)
  └─ CostProjectionService.EvaluateCostsAsync()
```

---

## Step 1 — Build the Request from Profile + Plan Inputs

`BuildMedicareRequestWithProfileAsync()` assembles the `IndividualMedicareRequest`.

### From MongoDB Profile (user-specific)

| Field | Source |
|---|---|
| `BirthDate` (MM-yyyy) | `profile.DateOfBirth` parsed + reformatted |
| `LifeExpectancy` | `profile.LifeExpectancy` |
| `HealthGrade` | `profile.HealthCondition` (1–5 scale) |
| `StateName` | `profile.State` (code → full name via dictionary) |
| `Zipcode` | `profile.ZipCode` |
| `TaxFilingStatus` | `profile.TaxFilingStatus` |
| `Tobacco` | `profile.TobaccoStatus` |
| `MagiTier` | `profile.MagiTier` (income tier for IRMAA surcharge calc) |
| `CoverageYear` | `profile.CoverageYear` |
| `ConciergeIncluded / ConciergePremium` | `profile.Concierge` / `profile.ConciergeAmount` |
| `CalculateForAdjustedMonth` | If coverageYear == current year: `12 - currentMonth + 1` (partial year). Otherwise: 12 |

### From the Selected Plan (passed by the UI in the DTO)

| Field | Meaning |
|---|---|
| `PlanBundleCode` | Identifies plan type: `ABGD` (Original + Plan G + Part D), `ABFD` (Plan F), `ABND` (Plan N), `ABCD` (MA + Part D) |
| `MedicareAdvantagePremium` | Monthly MA premium from the selected plan |
| `MaWithPrescriptionBenefit` | Whether MA includes Part D |
| `PartDOOP` | Actual Part D out-of-pocket for the first year (from drug analysis) |
| `PartDOOPFullYear` | Full-year Part D OOP |
| `PartABenefitServiceCost` | Part A hospital benefit cost |
| `PartBBenefitServiceCost` | Part B service cost |
| `PartDPremium` | Monthly Part D premium |
| `SupplementPlanType` | Medigap Plan G / F / N |
| `Dental`, `DentalHealthGrade` | Dental add-on config |
| `BoughtPlanA`, `ReserveDaysUsed` | Hospital reserve day tracking |

---

## Step 2 — Financial Planner Calculation (External API)

`IndividualMedicareService.CalculateAsync()` posts the request to the external Financial Planner actuarial engine:

```
POST http://<FinancialPlanner:BaseUrl>/individualMedicareR5
```

### Year-by-Year Breakdown (`IndividualMedicareDetail[]`)

One entry per calendar year from coverage year through life expectancy.

| Field | What it represents |
|---|---|
| `Year` | Calendar year |
| `MonthsUsedForExpenseCalc` | Months counted (partial for first/last year) |
| `PartAPremium` | Part A monthly premium × months |
| `PartBPremium` | Part B standard premium × months |
| `PartBPremiumSurcharge` | IRMAA surcharge on Part B (income-based) |
| `MedicareAdvantagePremium` | MA premium that year |
| `PartDPremium` | Part D premium that year |
| `PartDPremiumSurcharge` | IRMAA surcharge on Part D |
| `ConciergePremium` | Concierge medicine premium if enrolled |
| `PartAOOP` | Part A out-of-pocket (hospital stays, reserve days) |
| `PartBOOP` | Part B out-of-pocket (20% coinsurance, deductible) |
| `PartDOOP` | Part D drug out-of-pocket (catastrophic cap honoured) |
| `DentalPremium` / `DentalOOP` | Dental if elected |
| `PlanGPremium` / `PlanFPremium` / `PlanNPremium` | Medigap supplement premium that year |
| `TotalABGD` | Sum for Original Medicare + Plan G + Part D bundle |
| `TotalABFD` | Sum for Original Medicare + Plan F + Part D bundle |
| `TotalABND` | Sum for Original Medicare + Plan N + Part D bundle |
| `TotalABCD` | Sum for Medicare Advantage + Part D bundle |
| `TotalABMedicareAdvantage` | MA total only |

### Lifetime Aggregates (pre-summed by the FP engine across all years)

- `LifeTimeABGDExpenses/Premium/OOP`
- `LifeTimeABFDExpenses/Premium/OOP`
- `LifeTimeABNDExpenses/Premium/OOP`
- `LifeTimeABCDExpenses/Premium/OOP`
- `LifeTimeABMedicareAdvantageExpenses/Premium/OOP`
- `LifeTimeBSurcharge + LifeTimeDSurcharge` → **Total IRMAA**
- `LifeTimeConciergePremium`

---

## Step 3 — AI Evaluation (Claude / Anthropic)

`CostEvaluationAiService.EvaluateAsync()` builds a prompt from the FP results and calls the AI model.

### Prompt Assembly (from `Prompts/` directory)

| File | Purpose |
|---|---|
| `system/cost-evaluation-system.txt` | Medicare financial expert system persona |
| `tasks/cost-evaluation.txt` | Instructs AI to analyse lifetime totals, identify trends, flag high-cost years, produce savings tips |
| `schemas/cost-evaluation-schema.txt` | Strict JSON output shape |
| `templates/cost-evaluation.txt` | Filled with actual numbers: plan name, lifetime totals, year-by-year line per year |

### Template Variables Injected

| Placeholder | Value |
|---|---|
| `{{PLAN_NAME}}` | Selected plan name |
| `{{PLAN_BUNDLE_CODE}}` | ABGD / ABFD / ABND / ABCD |
| `{{COVERAGE_YEAR}}` | User's coverage start year |
| `{{LIFE_EXPECTANCY}}` | From profile |
| `{{TAX_FILING_STATUS}}` | From profile |
| `{{STATE_NAME}}` | From profile |
| `{{LIFETIME_AB_MA_EXPENSES}}` | FP lifetime total |
| `{{LIFETIME_AB_MA_PREMIUM}}` | FP lifetime premium |
| `{{LIFETIME_AB_MA_OOP}}` | FP lifetime OOP |
| `{{LIFETIME_D_SURCHARGE}}` | FP Part D IRMAA |
| `{{LIFETIME_B_SURCHARGE}}` | FP Part B IRMAA |
| `{{TOTAL_IRMAA}}` | Sum of B + D surcharges |
| `{{SUPPLEMENT_PLAN_TYPE}}` | Medigap plan type |
| `{{SUPPLEMENT_PLAN_PREMIUM}}` | Medigap premium |
| `{{YEARLY_BREAKDOWN}}` | One line per year with all premium + OOP values |

### AI Output (`CostEvaluation`)

| Field | What it contains |
|---|---|
| `lifetimeSummary` | `totalPremiums`, `totalOutOfPocket`, `totalCombined`, `projectionYears`, `averageAnnualCost` |
| `costTrajectory` | `"Rising"` / `"Stable"` / `"Declining"` / `"Mixed"` |
| `trajectoryExplanation` | 1–2 sentence narrative |
| `yearlyHighlights` | Flags highest, lowest, and spike years with explanations |
| `categories` | Per-cost-category with `lifetimeTotal`, `percentOfTotal`, `trend`, `insight` |
| `savingsTips` | 3–5 prioritised savings recommendations with estimated dollar savings |
| `overallAssessment` | 2–3 sentence plan cost-effectiveness verdict |

---

## Step 4 — Present Value Calculation (External API)

`PresentValueService.CalculateAsync()` calls the FP engine's second endpoint:

```
POST <FinancialPlanner:BaseUrl>/expensesPresentValue
```

### Input Built from FP Yearly Data

| Parameter | Value |
|---|---|
| `FromYear` / `ToYear` | First and last years from the year list |
| `Expenses[]` | Per year: `TotalABGD + TotalABFD + TotalABND + TotalABCD + TotalABMedicareAdvantage` |
| `PvAsOnYear1` | Coverage year (reference point for discounting) |
| `Discount` | **6** (hardcoded 6% discount rate) |
| `RateOfReturn1` | 0 |

The PV engine discounts all future yearly expenses back to today's dollars. `pvList[0].PresentValue` = **net present value of the user's lifetime Medicare expenses**.

---

## Final Response Assembly (`CostProjectionResult`)

```
CostProjectionResult
├─ YearlyDetails      ← raw year-by-year rows from FP (drives charts)
├─ LifetimeTotals     ← aggregated lifetime figures from FP (drives summary cards)
├─ Evaluation         ← AI narrative, categories, yearly highlights, savings tips
└─ PresentValue       ← PV-discounted lifetime total ($)
```

---

## UI Rendering (`CostProjectionsComponent`)

Reads `MedicareStateService.costProjection()` signal and renders five Chart.js charts:

| Chart | Data source |
|---|---|
| Line chart | Yearly total cost over time |
| Bar chart | Premium vs OOP breakdown per year |
| Doughnut chart | Cost category share from `evaluation.categories` |
| Stacked chart | Plan bundle comparison (ABGD vs ABFD vs ABND vs ABCD) |
| Medicare projection chart | Cost trajectory line |

Also renders:
- **Savings tips** panel (from AI evaluation)
- **Overall assessment** panel (AI narrative)
- **Yearly highlights** (AI-flagged high/low/spike years)

### Hard Refresh / Missing Data Behaviour

If `costProjection()` is `null` on `ngOnInit`, the component resets the entire analysis and redirects back to `/profile` with an assistant message explaining the reset.

### Auto-Save Behaviour

On reaching the cost projections page, if `pendingCostRunRecommendationName` is set, the component auto-calls `AnalysisSnapshotService.save()` to persist the full recommendation (profile + drugs + pharmacies + plans + cost result) as a named snapshot in MongoDB.

---

## Data Flow Diagram

```
User clicks "Run Analysis"
        │
        ▼
POST /api/plan-recommendation/evaluate-costs
        │
        ├─ 1. GET Profile from MongoDB
        │       └─ Extract: DOB, state, zip, life expectancy,
        │                   health grade, MAGI tier, tobacco,
        │                   concierge, coverage year
        │
        ├─ 2. POST → FinancialPlanner /individualMedicareR5
        │       └─ Returns: IndividualMedicareDetail[] (per year)
        │                   + lifetime aggregates
        │
        ├─ 3. POST → AI (Claude)
        │       └─ Returns: CostEvaluation (narrative + tips)
        │
        └─ 4. POST → FinancialPlanner /expensesPresentValue
                └─ Returns: Present value of lifetime expenses

        ▼
CostProjectionResult (combined) → MedicareStateService.costProjection signal
```

---

---

# Long-Term Care (LTC) — Cost Projection Architecture & Calculation Logic

## Overview

LTC cost projection estimates long-term care expenses across four care types over the user's remaining life. Unlike Medicare, LTC has **two steps** (Profile + Care Type) and **one combined API call** that returns both the actuarial projection and AI evaluation simultaneously.

---

## Step 1 — Profile (Shared with Medicare)

**UI Component:** `UserProfileComponent` (route: `/ltc/profile`)

Identical to Medicare's profile step. On "Continue":
- `LtcShellComponent.goNext()` → `profileService.requestSaveFromChat()` (increments `chatSaveRequestId` signal)
- `UserProfileComponent` effect watches `chatSaveRequestId` → calls `save()`
- `save()` → `POST /api/profile` → **MongoDB `users` collection**
- On success: sets `ltcProfileIntroComplete = true` → shell advances to Step 2

**Profile fields used in LTC projection:**

| Field | Usage |
|---|---|
| `dateOfBirth` | Calculates current age |
| `lifeExpectancy` | End year of projection |
| `gender` | Actuarial risk factor |
| `state` | Resolves to full state name for location input |
| `zipCode` | Geographic pricing factor |
| `tobaccoStatus` | Risk factor |

---

## Step 2 — Care Type

**UI Component:** `LtcCareTypeStepComponent` (route: `/ltc/care-type`)

**State management:** `LtcStateService` (Angular signals)

### Hydration on Load (return visit / hard refresh)

On component construction, `GET /api/ltc/current` is called to re-hydrate from MongoDB:

```
GET /api/ltc/current
  └─ LtcSelectionsController.GetCurrent()
       └─ LtcSelectionsRepository.GetCurrentAsync(userId)
            └─ MongoDB ltcCurrentSelections (one doc per user)
```

Response fields are set into both signals AND the form (`{ emitEvent: false }` to prevent triggering auto-save):

```typescript
this.state.healthProfile.set(current.healthProfile);
this.state.adultDayYears.set(current.numberOfAdultDayHealthCareYears);
this.state.homeCareYears.set(current.numberOfHomeCareYears);
this.state.nursingCareYears.set(current.numberOfNursingCareYears);
form.patchValue({ ... }, { emitEvent: false });
```

### Auto-Save on Value Change

`form.valueChanges` → `debounceTime(1000)` → `PUT /api/ltc/current` (inputs only, no projection result):

```typescript
// Saves care-type inputs after 1s of inactivity — mirrors Medicare drug/pharmacy step saves
form.valueChanges.pipe(
  debounceTime(1000),
  switchMap(() => ltcService.saveCurrent({ healthProfile, adultDayYears, homeCareYears, nursingCareYears }))
)
```

**MongoDB document stored (`ltcCurrentSelections`):**

```
{
  userId,
  healthProfile,              // 1=Best … 5=Minimum
  numberOfAdultDayHealthCareYears,
  numberOfHomeCareYears,
  numberOfNursingCareYears,
  createdAt, updatedAt
}
```

> Note: projection result is **NOT** stored in current selections (same as Medicare — only inputs are persisted here).

### Chat-Driven Updates

- If user is on the care-type page: `pendingChatCareType` signal is set → `effect()` in component patches the form → form change triggers auto-save
- If user is on another page: state signals are updated directly, then navigated to care-type page

---

## Step 3 — Run Projection

Triggered by "Run Projection" button (opens name dialog first) or via chat `handleRunProjection`.

### Build `LtcProjectionRequest`

Assembled from profile signal + care-type state signals:

| Field | Source |
|---|---|
| `age` | Calculated from `profile.dateOfBirth` |
| `pvAsOfYear` | `new Date().getFullYear()` |
| `lifeExpectancy` | `profile.lifeExpectancy` |
| `healthProfile` | `ltcState.healthProfile()` (1–5) |
| `location` | `profile.state` code → full label via `ReferenceDataService.usStates()` |
| `zipcode` | `profile.zipCode` |
| `tobacco` | `profile.tobaccoStatus` |
| `gender` | `profile.gender` |
| `numberOfAdultDayHealthCareLTCYears` | `ltcState.adultDayYears()` |
| `numberOfHomeCareLTCYears` | `ltcState.homeCareYears()` |
| `numberOfNursingCareLTCYears` | `ltcState.nursingCareYears()` |
| `numberOfAssistedCareLTCYears` | Hardcoded `0` (not exposed in UI) |
| `alzheimersFlag / heartStorkeFlag` | Hardcoded `0` |
| `transactionTypeFlag` | Hardcoded `'false'` |
| `currentLifeStyleExpenses` | Hardcoded `1` |

### POST /api/long-term-care

`LongTermCareController.GetProjection()` runs two operations in sequence:

```
POST /api/long-term-care
  ├─ 1. LongTermCareService.GetProjectionAsync()
  │       └─ POST FinancialPlanner /longTermCareR4
  │            └─ Returns: LongTermCareResponse (actuarial projection)
  │
  └─ 2. LtcEvaluationAiService.EvaluateAsync(projection, age, state, …)
          └─ POST AI (Claude)
               └─ Returns: LtcCostEvaluation (AI narrative + tips)

  ▼
LtcProjectionResult { Projection, Evaluation }
```

---

## Financial Planner API (`/longTermCareR4`)

External actuarial engine calculates year-by-year care costs from the current age to life expectancy.

### Response Fields (`LongTermCareResponse`)

**Per care type (Adult Day / Home / Assisted / Nursing):**

| Field | Meaning |
|---|---|
| `adultDayHealthCare` | Raw annual care cost |
| `expectedAdultDayHealthCare` | Inflation-adjusted expected cost (present value basis) |
| `presentValueExpectedAdultDayHealthCare` | PV-discounted expected cost |
| `startingYearOfAdultDayHealthCare` | Calendar year care begins |
| `numberOfAdultDayHealthCareLTCYears` | Duration of care |
| `futureAdultDayHealthCareExpenseList[]` | Year-by-year expense entries `{ year, expense }` |

Same pattern repeated for `HomeCare`, `AssistedCare`, `NursingCare`.

**Summary fields:**

| Field | Meaning |
|---|---|
| `age`, `gender`, `state`, `zipcode` | Echo of input |
| `lifeExpenctancy` | Echo of life expectancy |
| `healthProfile` | Echo of quality-of-care profile |
| `tobaccoUsage`, `alzheimersFlag`, `heartStorkeFlag` | Echo of risk flags |

---

## AI Evaluation (`LtcEvaluationAiService`)

### Yearly Breakdown Assembly

`BuildYearlyBreakdown()` merges all four `futureXxxExpenseList[]` arrays into a unified year-keyed dictionary:

```
Year 2026: AdultDay=$X, HomeCare=$Y, AssistedCare=$Z, NursingCare=$W, Total=$T
Year 2027: ...
```

All years across all care types are collected and sorted chronologically.

### Prompt Template Variables

| Placeholder | Value |
|---|---|
| `{{AGE}}` | Calculated age |
| `{{GENDER}}` | Profile gender |
| `{{STATE}}` | Resolved state name from projection |
| `{{HEALTH_PROFILE}}` | Label: Excellent / Good / Average / Below Average / Poor |
| `{{LIFE_EXPECTANCY}}` | From profile |
| `{{ADULT_DAY_YEARS}}` / `{{ADULT_DAY_START_YEAR}}` | Duration + start year |
| `{{HOME_CARE_YEARS}}` / `{{HOME_CARE_START_YEAR}}` | Duration + start year |
| `{{ASSISTED_CARE_YEARS}}` / `{{ASSISTED_CARE_START_YEAR}}` | Duration + start year |
| `{{NURSING_CARE_YEARS}}` / `{{NURSING_CARE_START_YEAR}}` | Duration + start year |
| `{{ADULT_DAY_TOTAL}}` / `{{ADULT_DAY_PV}}` | Lifetime + PV per care type |
| `{{HOME_CARE_TOTAL}}` / `{{HOME_CARE_PV}}` | Lifetime + PV |
| `{{ASSISTED_CARE_TOTAL}}` / `{{ASSISTED_CARE_PV}}` | Lifetime + PV |
| `{{NURSING_CARE_TOTAL}}` / `{{NURSING_CARE_PV}}` | Lifetime + PV |
| `{{YEARLY_BREAKDOWN}}` | Full merged year-by-year table |

### AI Output (`LtcCostEvaluation`)

| Field | Content |
|---|---|
| `lifetimeSummary` | `totalCost`, `totalPresentValue`, `projectionYears`, `averageAnnualCost` |
| `costTrajectory` | `Rising` / `Stable` / `Declining` / `Mixed` |
| `trajectoryExplanation` | 1–2 sentence narrative |
| `yearlyHighlights` | Flags `Highest`, `Lowest`, `Spike`, `Normal` years with explanations |
| `categories` | Per care type: `lifetimeTotal`, `presentValue`, `percentOfTotal`, `trend`, `insight` |
| `savingsTips` | Prioritised savings recommendations with `estimatedSavings` |
| `overallAssessment` | 2–3 sentence verdict |

---

## Saving After Projection

### 1. Save inputs to current selections (upsert)

`PUT /api/ltc/current` — overwrites the MongoDB `ltcCurrentSelections` document with the confirmed inputs:

```json
{
  "healthProfile": 2,
  "numberOfAdultDayHealthCareYears": 3,
  "numberOfHomeCareYears": 5,
  "numberOfNursingCareYears": 2
}
```

> Projection result JSON is **not** stored here — only the inputs (same as Medicare).

### 2. Save named recommendation snapshot

`POST /api/recommendation` (type: `'longterm'`) — stores the full named snapshot in MongoDB `recommendations` collection:

```
{
  name: <user-chosen name>,
  type: 'longterm',
  profile: { ... full profile snapshot ... },
  ltcSnapshot: {
    healthProfile, adultDayYears, homeCareYears, nursingCareYears,
    totalCost       = expectedAdultDay + expectedHome + expectedAssisted + expectedNursing,
    totalPresentValue = PV(adultDay) + PV(home) + PV(assisted) + PV(nursing),
    costTrajectory, trajectoryExplanation, overallAssessment,
    yearlyHighlights[], categories[], savingsTips[]
  }
}
```

---

## Projection Results Page

**UI Component:** `LtcProjectionStepComponent` (route: `/ltc/projection`)

### Hard Refresh Behaviour

If `ltcResult()` signal is `null` (hard refresh, new session), the user is **redirected to `/ltc/care-type`** to re-run the projection. This mirrors Medicare's behaviour where the cost projection page redirects to profile when no result is in memory.

### Charts Rendered (Chart.js)

| Chart | Data Source |
|---|---|
| Line chart | Merged yearly totals from all four `futureXxxExpenseList[]` |
| Stacked bar | Per-year breakdown by care type |
| Doughnut | `evaluation.categories` share |

### Result Signals

```typescript
readonly result     = computed(() => this.state.ltcResult());
readonly projection = computed(() => this.result()?.projection ?? null);
readonly evaluation = computed(() => this.result()?.evaluation ?? null);
```

---

## Save at Every Step — LTC vs Medicare Comparison

| Step | Medicare | LTC |
|---|---|---|
| Profile | `POST /api/profile` → MongoDB (on Continue) | Same — `POST /api/profile` → MongoDB (on Continue) |
| Drugs | `PUT /api/prescription/current` → MongoDB on step leave | N/A |
| Pharmacy | `PUT /api/prescription/pharmacy` → MongoDB on step leave | N/A |
| Plans | `PUT /api/prescription/plans` → MongoDB on step leave | N/A |
| Care Type inputs | N/A | `PUT /api/ltc/current` → MongoDB auto-save (1s debounce on form change) + on projection run |
| Projection result | NOT stored in current — re-run from plan step | NOT stored in current — re-run from care-type step |
| Named snapshot | `POST /api/recommendation` (`type: 'medicare'`) | `POST /api/recommendation` (`type: 'longterm'`) |

---

## LTC Data Flow Diagram

```
User clicks "Run Projection"
        │
        ├─ (name dialog) → user enters analysis name
        │
        ▼
POST /api/long-term-care
        │
        ├─ 1. POST → FinancialPlanner /longTermCareR4
        │       └─ Returns: LongTermCareResponse
        │              ├─ futureAdultDayHealthCareExpenseList[]
        │              ├─ futureHomeCareExpenseList[]
        │              ├─ futureAssistedCareExpensesList[]
        │              ├─ futureNursingCareExpensesList[]
        │              └─ expectedXxx + presentValueExpectedXxx per care type
        │
        └─ 2. POST → AI (Claude)
                └─ Returns: LtcCostEvaluation
                       ├─ lifetimeSummary
                       ├─ costTrajectory / trajectoryExplanation
                       ├─ yearlyHighlights[]
                       ├─ categories[]
                       ├─ savingsTips[]
                       └─ overallAssessment

        ▼
LtcProjectionResult { projection, evaluation }
        │
        ├─ ltcState.ltcResult.set(result)          ← in-memory signal
        │
        ├─ PUT /api/ltc/current                    ← upsert inputs only (no result JSON)
        │
        └─ POST /api/recommendation                ← named snapshot (type: 'longterm')
```
        │
        ▼
CostProjectionsComponent → 5 Chart.js charts + AI panels
```
