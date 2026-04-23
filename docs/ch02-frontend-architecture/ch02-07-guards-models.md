# Chapter 2.7 — Interceptors, Guards & Models

> HTTP interceptor, route guards, and all TypeScript model/interface definitions.

← [Chapter 2 — Frontend Architecture (Index)](../ch02-frontend-architecture/ch02-frontend-architecture.md)

---

## Interceptors & Guards

### `authInterceptor` (`interceptors/auth.interceptor.ts`)
- **Type:** `HttpInterceptorFn` (functional).
- **Behavior:** Attaches `Authorization: Bearer <token>` header to all outgoing HTTP requests.

### `httpErrorInterceptor` (`interceptors/http-error.interceptor.ts`)
- **Type:** `HttpInterceptorFn` (functional).
- **Behavior:** Global API error handler. Catches all `HttpErrorResponse` errors and opens a Material Dialog popup (`ErrorDialogComponent`) with a user-friendly message and collapsible technical details (method, URL, status code).
- **Status-Code Mapping:** `0` → offline/network error, `401` → session expired, `403` → forbidden, `404` → not found, `408/504` → timeout, `429` → rate limit, `5xx` → server error.
- **Silent URLs:** Auth endpoints (`/api/auth/`) are excluded — login/signup errors continue to be handled inline by their own components.
- **Dedup:** Only one popup shown at a time (guard via `ErrorNotificationService.isOpen`).
- **Propagation:** Error is still re-thrown (`throwError`) so component-level `subscribe({ error })` handlers continue to work.

### `authGuard` (`guards/auth.guard.ts`)
- **Type:** `CanActivateFn`.
- **Behavior:** Checks `AuthService.isAuthenticated()`. If false, redirects to `/signin`.

### `profileCompleteGuard` (`guards/profile-complete.guard.ts`)
- **Type:** `CanActivateFn`.
- **Behavior:** Protects `/medicare-analysis`. On deep-link/hard refresh, it loads profile first (if not already loaded) before deciding. If profile is incomplete, redirects to `/profile`; otherwise preserves the current analysis route.

### `dashboardRedirectGuard` (`guards/dashboard-redirect.guard.ts`)
- **Type:** `CanActivateFn`.
- **Behavior:** Auto-redirect for default dashboard child route (`''`). Always routes to `/profile` after login. Always returns false (pure redirect guard).

---

## Models

### Drug Models (`models/drug.model.ts`)

```typescript
interface DrugAnalysisResponse {
  drugs: Drug[];
  interactions: DrugInteraction[];
  dosageAlerts: DosageAlert[];
  duplicateTherapies: DuplicateTherapy[];
  nearbyPharmacies?: PharmacyWithPricing[]; // populated on-demand via separate API call
  message?: string;
}

interface Formulation {
  dosageForm: string;
  strength: string;
  packaging: string;
  ndcCode: string;
}

interface Drug {
  drugInput: string;
  normalizedDrugName: string;
  brandNames: string[];
  genericName: string;
  synonyms: string[];
  therapeuticCategory: string;
  drugClass: string;
  mechanismOfAction: string;
  dosageForms: string[];
  formulations: Formulation[];  // validated (dosageForm+strength+packaging+ndcCode) tuples
  strengths: string[];          // flat array (populated from formulations for backward compat)
  packaging: string[];          // flat array (populated from formulations for backward compat)
  rxNormId: string;
  ndcCodes: string[];           // flat array (populated from formulations for backward compat)
  estimatedRetailCostUSD: string;
  estimatedMedicarePartDCostUSD: string;
  medicareNegotiatedPriceUSD: string;
  confidenceScore?: number;
  alternatives: DrugAlternative[];
  genericSwitchSuggestion?: GenericSwitchSuggestion;
  contraindications: string[];
}

interface DrugInteraction { drugA, drugB, severity: 'High'|'Moderate'|'Low', description, clinicalConsequence, recommendation }
interface DosageAlert { drugName, inputDosage, recommendedRange, severity, message }
interface DuplicateTherapy { drugs: string[], therapeuticClass, message }
interface DrugAlternative { name, type, costDifference, clinicalNote }
interface GenericSwitchSuggestion { from, to, estimatedSavings }
```

### Pharmacy Models (`models/drug.model.ts`)

```typescript
interface PharmacyResult { npi, name, legalName, address, addressLine2, city, state, zipCode, phone, fax, pharmacyType, enumerationDate }
interface DrugPrice { drugName, ndc, rxCui, retailPrice: number|null, medicarePrice: number|null, genericPrice: number|null, planCopay?: number|null, formularyTier?: number|null, requiresPriorAuth?: boolean|null, isPreferredPharmacy?: boolean|null }
interface PharmacyWithPricing { pharmacy: PharmacyResult, drugs: DrugPrice[], totalRetailCost: number|null, totalMedicareCost: number|null, totalGenericCost: number|null, totalPlanCopay?: number|null, isPreferredNetwork?: boolean|null }
interface PlanPharmacySearchRequest { planId, zipCode?, drugs: {rxCui, drugName}[], planCoverages: PlanCoverageInput[] }
interface PlanCoverageInput { rxCui, drugName, formularyTier, monthlyCopay, isCovered, requiresPriorAuth }
```

### Auth Models (`models/auth.model.ts`)

```typescript
interface SignUpRequest { email, phone, password, confirmPassword }
interface SignInRequest { email, password }
interface ForgotPasswordRequest { email }
interface ResetPasswordRequest { token, newPassword, confirmPassword }
interface AuthResponse { success, message, token?, expiresAt?, user?: AuthUser }
interface AuthUser { id, email, phone }
```

### Profile Models (`models/profile.model.ts`)

```typescript
interface UserProfileResponse { profile: ProfileDto | null, isProfileComplete: boolean }
interface ProfileDto { firstName, lastName, coverageYear, healthCondition, taxFilingStatus, magiTier, gender, tobaccoStatus, dateOfBirth, concierge, conciergeAmount, alternateEmail, alternateMobile, lifeExpectancy, addressLine1, addressLine2, street, city, state, zipCode, county, countyCode, latitude, longitude }
```

### Reference Data Models (`models/reference-data.model.ts`)

```typescript
interface LabelValue { value: string; label: string }
interface MagiTierOption { value, label, description }
interface HouseholdSizeOption { value: number, label }
interface ReferenceData { genders, maritalStatuses, taxFilingStatuses, incomeFilingStatuses, magiTiersByFiling: Record<string, MagiTierOption[]>, tobaccoStatuses, disabilityStatuses, chronicConditions, usStates, householdSizes }
```

### Plan Recommendation Models (`models/plan-recommendation.model.ts`)

```typescript
interface PlanRecommendationRequest { drugs: DrugSummaryInput[], selectedPharmacies?: SelectedPharmacyInput[] }
interface SelectedPharmacyInput { npi, name, pharmacyType }
interface DrugSummaryInput { rxCui, drugName, genericName, ndc?, estimatedRetailPrice? }
interface PlanRecommendationResult { lisEligible, lisTier, recommendedPlanType, eligibilitySummary, lisCallToAction?, rankedPlans[] }
interface RankedPlan { planId, planName, planType, planCategory: 'MA_ONLY'|'PDP_ONLY'|'PDP_MEDIGAP'|'MA_PDP', insuranceName, monthlyPremium, annualDeductible, annualMoop, estimatedAnnualDrugCost, estimatedAnnualTotalCost, drugCoverages[], aiExplanation, starRating, hasPreferredPharmacyNetwork, planFinderUrl, networkType, includesDental, includesVision, includesHearing, includesFitness, includesOtc, otcAllowancePerQuarter, gapCoverage, mailOrderSavings, providerNetworkSize, emergencyCoverage, pros: string[], cons: string[], costBreakdowns?: PlanCostBreakdown[] }
interface PlanCostBreakdown { pharmacyName, pharmacyNpi, isPreferredPharmacy, annualPremium, annualDeductible, annualDrugCopay, annualTotal, drugCopays: DrugCopayDetail[] }
interface DrugCopayDetail { drugName, rxCui, formularyTier, monthlyCopay, annualCopay, isCovered, preferredDiscount }
interface PlanDrugCoverage { drugName, rxCui, isCovered, formularyTier, monthlyCopay, requiresPriorAuth, hasQuantityLimit, quantityLimitDetail? }
interface LisCheckResult { lisEligible, lisTier }
interface GapAdviceRequest { planId, planName, planType, missingCoverages: string[] }
interface GapCoverageResult { gapPlans: GapPlan[], comparisonTip: string }
interface GapPlan { category, planName, planType, carrier, monthlyPremiumRange, annualDeductible, coverageHighlights: string[], whyNeeded, enrollmentTip, priority: 'Essential'|'Recommended'|'Optional' }
```

### Cost Projection Models (`models/cost-projection.model.ts`)

```typescript
interface CalculateCostsRequest { planBundleCode, medicareAdvantagePremium, maWithPrescriptionBenefit, partDOOP, partDOOPFullYear, partABenefitServiceCost, partBBenefitServiceCost, planRecommendName, recommendationListId, supplementDataProvided, partDDataProvided, reserveDaysUsed, dental, dentalHealthGrade, boughtPlanA, medicareAdvantageDataProvided, partDPremium, calculateForAdjustedMonth, supplementPlanType }
interface EvaluateCostsResponse { yearlyDetails: IndividualMedicareDetail[], lifetimeTotals: LifetimeTotals, evaluation: CostEvaluation }
interface IndividualMedicareDetail { year, monthsUsedForExpenseCalc, partAPremium, partBPremium, partBPremiumSurcharge, medicareAdvantagePremium, partDPremium, partDPremiumSurcharge, conciergePremium, partAOOP, partBOOP, partDOOP, totalABMedicareAdvantage, reserveDaysLeft, dentalPremium, dentalOOP }
interface LifetimeTotals { lifeTimeABMedicareAdvantageExpenses, lifeTimeABMedicareAdvantagePremium, lifeTimeABMedicareAdvantageOop, lifeTimeDSurcharge, lifeTimeBSurcharge, totalIrmaa, supplementPlanType, supplementPlanPremium }
interface CostEvaluation { planName, planBundleCode, lifetimeSummary: LifetimeSummary, costTrajectory: 'Rising'|'Stable'|'Declining'|'Mixed', trajectoryExplanation, yearlyHighlights: YearlyHighlight[], categories: CostCategory[], savingsTips: SavingsTip[], overallAssessment }
interface LifetimeSummary { totalPremiums, totalOutOfPocket, totalCombined, projectionYears, averageAnnualCost }
interface YearlyHighlight { year, totalCost, flag: 'Highest'|'Lowest'|'Spike'|'Normal', explanation }
interface CostCategory { name, lifetimeTotal, percentOfTotal, trend: 'Rising'|'Stable'|'Declining', insight }
interface SavingsTip { title, description, estimatedSavings, priority: 'High'|'Medium'|'Low' }
```

### Chat Message (in `drug-state.service.ts`)

```typescript
interface ChatMessage { role: 'user' | 'assistant' | 'system', content: string, timestamp: Date }
```

### Orchestrator Models (`models/orchestrator.model.ts`)

```typescript
interface OrchestratorRequest  { message: string }
interface OrchestratorResponse {
  reply: string;
  state: string;            // FSM state (e.g. 'idle', 'awaiting_drug_name', 'awaiting_confirmation')
  awaitingConfirmation: boolean;
  delta?: DeltaResult;
  displayData?: DisplayData;
}
interface DeltaResult {
  lifetimeBefore: number; lifetimeAfter: number;
  thisYearBefore: number; thisYearAfter: number;
  pvBefore: number;       pvAfter: number;
}
interface DisplayData { type: string; payload: any }
```

### Recommendation Models (`models/recommendation.model.ts`)

```typescript
interface RecommendationSummaryResponse {
  id: string;
  name: string;
  status: string;              // 'completed' | 'in-progress'
  drugCount: number;
  planCount: number;
  hasCostSnapshot: boolean;
  lifetimeTotal: number | null;
  createdAt: string;
  updatedAt: string;
}

interface CreateRecommendationRequest {
  name: string;
  profile: ProfileSnapshotDto;
  drugs: SelectedDrugDto[];
  pharmacies: SelectedPharmacySnapDto[];
  mailOrderPharmacy: MailOrderPharmacyDto | null;
  plans: SelectedPlanDto[];
  costSnapshot: CostSnapshotDto | null;
  force?: boolean;
}

interface SelectedPlanDto {
  planType: string;
  planName: string;
  carrier: string;
  monthlyPremium: number;
  planId: string;
  deductible: number;
  starRating: number;
  totalPrescriptionCost: number;
  totalPlanCost: number;
  prescriptionDrugCovered: boolean;
  unavailableDrugs: string[];
  planExpenses: PlanExpenseDto[];
}

interface PlanExpenseDto { name: string; amount: number }

interface CostSnapshotDto {
  lifetimeTotal: number;
  currentYearTotal: number;
  averageAnnual: number;
  projectionYears: number;
  lifetimePremiums: number;
  lifetimeOOP: number;
  lifetimeIrmaa: number;
  costTrajectory: string;
  supplementPlanType: string;
  supplementPlanPremium: number;
  yearlyDetails: YearlyDetailDto[];
  evaluation: CostEvaluationDto | null;
}

interface YearlyDetailDto {
  year: number;
  partAPremium: number; partBPremium: number;
  partBSurcharge: number; maPremium: number;
  partDPremium: number; partDSurcharge: number;
  conciergePremium: number;
  partAOOP: number; partBOOP: number; partDOOP: number;
  totalABMA: number;
  dentalPremium: number; dentalOOP: number;
  reserveDaysLeft: number; monthsUsed: number;
}

interface CostEvaluationDto {
  planName: string; planBundleCode: string;
  lifetimeSummary: LifetimeSummarySnapDto;
  costTrajectory: string; trajectoryExplanation: string;
  yearlyHighlights: YearlyHighlightDto[];
  categories: CostCategorySnapDto[];
  savingsTips: SavingsTipSnapDto[];
  overallAssessment: string;
}

interface LifetimeSummarySnapDto { totalPremiums: number; totalOutOfPocket: number; totalCombined: number; projectionYears: number; averageAnnualCost: number }
interface YearlyHighlightDto { year: number; totalCost: number; flag: string; explanation: string }
interface CostCategorySnapDto { name: string; lifetimeTotal: number; percentOfTotal: number; trend: string; insight: string }
interface SavingsTipSnapDto { title: string; description: string; estimatedSavings: string; priority: string }
```

### LTC Models (`models/ltc.model.ts`)

```typescript
interface LtcProjectionRequest { /* profile-derived fields: age, gender, state, zip, countyCode, healthProfile, lifeExpectancy, tobaccoUsage, adultDayYears, homeCareYears, nursingCareYears */ }
interface LtcProjectionResponse {
  age: number; healthProfile: number; gender?: string; state?: string; zipcode: number; countyCode: number;
  lifeExpenctancy: number; tobaccoUsage: boolean; currentLifeStyleExpenses: number;
  numberOfAdultDayHealthCareLTCYears: number; numberOfHomeCareLTCYears: number;
  numberOfAssistedCareLTCYears: number; numberOfNursingCareLTCYears: number;
  adultDayHealthCare: number; presentValueAdultDayHealthCare: number;
  homeCare: number; presentValueHomeCare: number;
  assistedCare: number; presentValueAssistedCare: number;
  nursingCare: number; presentValueNursingCare: number;
  futureAdultDayHealthCareExpenseList: LtcExpenseEntry[];
  futureHomeCareExpenseList: LtcExpenseEntry[];
  futureAssistedCareExpensesList: LtcExpenseEntry[];
  futureNursingCareExpensesList: LtcExpenseEntry[];
  expectedHomeCare: number; expectedNursingCare: number;
  presentValueExpectedHomeCare: number; presentValueExpectedNursingCare: number;
}
interface LtcExpenseEntry { year: number; expense: number }
```

### Plan Models

**`models/part-d-plan.model.ts`:**
```typescript
interface PartDPlanRecommendationRequest { userId, sortRecommendations, countycodeModel: CountyCodeModel, prescriptions: PrescriptionInput[], pharmacies: PartDPharmacyInput[], taxFilingStatus, magiTier, healthGrade, birthDate, coverageYear, planPage, planPageSize, recommendationPage, recommendationPageSize, starRatingFilter, prescriptionCoverageFilter, contractIdFilter, mailOrderPharmacy, ... }
interface CountyCodeModel { zipcode, state, stateCode, city, latitude, longitude, countyCode, countyName }
interface PrescriptionInput { /* drug selection fields */ }
interface PartDPharmacyInput { /* pharmacy fields */ }
```

**`models/medicare-advantage-plan.model.ts`:**
```typescript
// Extends PartDPlanRecommendationRequest with medicareAdvantage: true
interface MedicareAdvantagePlanRequest extends PartDPlanRecommendationRequest { medicareAdvantage: true }
type MedicareAdvantagePlanResponse = PartDPlanRecommendationResponse
```

**`models/medigap-plan.model.ts`:**
```typescript
interface MedigapPlanQuotesRequest { zip5, gender, tobacco, birthDate, plan, county, taxFilingStatus, magiTier, healthProfile, coverageYear, versionId }
interface MedigapPlanQuotesResponse { contractIdCarrierMap: Record<string,string>, deductible: number, planList: MedigapPlan[] }
interface MedigapPlan { key, age, plan, rate: MedigapRate|null, rate_type, company_base: MedigapCompanyBase|null, discounts, fees, ... }
```

---

← [Services](ch02-06-services.md) | [Chapter 2 — Frontend Architecture (Index)](../ch02-frontend-architecture/ch02-frontend-architecture.md) | [Next → Configuration, Styling & UI Flow](ch02-08-config-styling-flow.md)
