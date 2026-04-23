# Chapter 8.3 — Pharmacy & Plan Features

> Pharmacy lookup, multi-pharmacy selection, plan recommendations, comparison, gap coverage, and Financial Planner plan integration.

← [Feature Catalog Index](../ch08-feature-catalog/ch08-feature-catalog.md)

---

## ✅ Nearby Pharmacy Search & AI-Powered Per-Pharmacy Pricing (On-Demand)
- **What:** Finds nearby pharmacies and generates per-pharmacy, per-drug pricing via AI. Triggered on-demand when user clicks "Find Nearby Pharmacies" button below drug cards.
- **APIs:** IChatClient for pharmacy-specific AI pricing.
- **Backend:** `Pharmacy/` directory contains `FinancialPlannerPharmacyService`. Pharmacy data is fetched via the `GET /api/pharmacy/lookup` endpoint (Financial Planner getPharmacies API).
- **Graceful degradation chain:** AI pricing → fallback prices → `null` ("—").
- **Frontend:** Pharmacy selection is handled within the `PharmaciesStepComponent` in the Medicare analysis wizard.
- **Design Principles:** All changes additive. No database migrations. Optional fields. Graceful degradation. On-demand loading reduces initial analysis time.

---

## ✅ Pharmacy Lookup
- **What:** Finds pharmacies via the Financial Planner API.
- **Endpoint:** `GET /api/pharmacy/lookup` — returns pharmacy results.
- **Purpose:** Primary pharmacy source for the pharmacy selection step.

---

## ✅ Financial Planner Pharmacy Lookup (Primary)
- **What:** Fetches paginated pharmacies near the user via the Financial Planner `getPharmacies` API using lat/lng from the user profile. Primary pharmacy source for step 2 of the wizard — replaces the NPI-only lookup in the UI while keeping the NPI code intact.
- **Endpoint:** `GET /api/pharmacy/lookup?page=1&size=20&radius=25&name=CVS` — returns `PharmacyLookupResponse` with paginated pharmacy list, total count, total pages, and search radius.
- **Backend:**
  - **Interface:** `IPharmacyLookupService` in `Domain/Interfaces/IPharmacyLookupService.cs` — defines `GetPharmaciesAsync(PharmacyLookupRequest)`. Request/response models co-located: `PharmacyLookupRequest` (Lat, Lng, Radius, Name, Page, Size), `PharmacyLookupResponse` (pharmacies, page, size, totalPharmacies, totalPages, searchRadiusInMiles), `PharmacyLookupEntry` (pharmacyNumber, pharmacyName, latitude, longitude, address, distance, zipcode).
  - **Service:** `FinancialPlannerPharmacyService` in `Infrastructure/Pharmacy/` — HTTP GET to `{baseUrl}/getPharmacies` with JSON body, Basic auth from config. 15s timeout.
  - **Controller:** `PharmacyController.Lookup()` reads lat/lng from user profile via `ProfileService`, returns 400 if missing.
  - **DI:** `AddHttpClient<IPharmacyLookupService, FinancialPlannerPharmacyService>` with 15s timeout.
- **Frontend:**
  - **Models:** `PharmacyLookupEntry`, `PharmacyLookupResponse` interfaces in `drug.model.ts`.
  - **Service:** `DrugService.lookupPharmacies({ page?, size?, radius?, name? })` — `GET /api/pharmacy/lookup` with query params.
  - **State:** `MedicareStateService` extended with `pharmacyLookup` (signal), `isPharmacyLookupLoading`, `hasPharmacyLookup` (computed), `selectedLookupPharmacies` (signal, max 5), `hasSelectedLookupPharmacies` (computed). `toggleLookupPharmacy()` and `isLookupPharmacySelected()` methods. Both signals persisted/restored in state cycle.
  - **UI:** `PharmacyStepComponent` completely rewritten — filter bar (pharmacy name search, radius dropdown 10/25/50/100 mi, page size dropdown 10/20/50, search/clear buttons), pharmacy cards (name, number, distance badge, address, zipcode, checkbox toggle max 5), two Google Maps action buttons per card ("Spot on Map" + "Directions"), pagination controls (prev/next + page number window), selected pharmacies summary panel with remove buttons, loading spinner and empty state.
- **Config:** `FinancialPlanner:BaseUrl` and `FinancialPlanner:AuthToken` in `appsettings.json` (shared with other Financial Planner services).

---

## ✅ Multi-Pharmacy Selection (Up to 5)
- **What:** Users can select up to 5 pharmacies to compare plan costs side-by-side. Replaces the previous single-pharmacy selection model.
- **Flow:** Pharmacy cards in step 2 show toggle checkboxes. Each click toggles selection (check/uncheck). Counter shows "X/5 selected". 6th selection attempt is silently rejected.
- **Backend:** `PlanRecommendationRequest.SelectedPharmacies` accepts `List<SelectedPharmacy>` (capped at 5 via `.Take(5)` in controller). `PlanScoringAiService.BuildPharmacyContext()` renders a numbered list for AI prompt context.
- **Frontend:** Two selection models coexist:
  - **Legacy (NPI):** `MedicareStateService.selectedPharmacies` signal. `togglePharmacy(pharmacy)`. `isPharmacySelected(npi)`. `hasSelectedPharmacies` computed.
  - **Financial Planner:** `MedicareStateService.selectedLookupPharmacies` signal. `toggleLookupPharmacy(pharmacy)` (max 5). `isLookupPharmacySelected(pharmacyNumber)`. `hasSelectedLookupPharmacies` computed. Both persisted in state cycle.
- **UI:** Emerald-themed pharmacy cards with custom checkboxes. Selected pharmacies summary panel with remove buttons. "X/5 selected" badge.

---

## ✅ Medicare Plan Recommendation (AI-First + LIS Eligibility, On-Demand)
- **What:** Recommends ranked Medicare plans (MA-PD, PDP+Medigap, D-SNP) personalized to user's drugs, income, health, and location. Triggered on-demand when user clicks "Load Medicare Plan Recommendations" button below drug cards.
- **4-Step Flow:**
  1. **User Profile** — Complete address + income + health.
  2. **Drug Selection** — Analyze prescriptions, confirm drug selections (no costing).
  3. **Pharmacy Selection** — Click "Find Nearby Pharmacies" to get lightweight NPI list. Toggle-select up to 5 pharmacies.
  4. **Plan Recommendation** — Click "Load Medicare Plan Recommendations" (only shown after ≥1 pharmacy selected). Plans include per-pharmacy cost breakdowns.
- **Backend:** Plan recommendation orchestrates: county lookup → LIS tier → AI scoring (with pharmacy context) → CMS enrichment → pharmacy cost breakdowns. `PlanScoringAiService` generates 5 ranked plans with `{{PHARMACY_CONTEXT}}` placeholder for selected pharmacies. `CountyLookupService` fetches county data via Financial Planner API. LIS: 2025 FPL thresholds.
- **AI Extended Fields:** AI generates 12 additional fields per plan: `networkType` (HMO/PPO/PFFS/HMO-POS), benefit flags (`includesDental`, `includesVision`, `includesHearing`, `includesFitness`, `includesOtc`), `otcAllowancePerQuarter`, `gapCoverage` (None/Some/Full), `mailOrderSavings`, `providerNetworkSize` (Large/Medium/Small), `emergencyCoverage`, and `pros`/`cons` bullet lists. Additionally, each plan includes a `planCategory` field (`MA_ONLY`, `PDP_ONLY`, `PDP_MEDIGAP`, `MA_PDP`) indicating the coverage bundling strategy.
- **Frontend:** `PlanRecommendationComponent` orchestrates plan loading, compare state, LIS banner, and Part D gap fill via `ensurePartDGapLoadForMA()`. Decomposed into child components: `RecommendationCardComponent` (individual plan card), `MedigapCardComponent` (Medigap supplemental plan card), `MedigapGapSectionComponent`, `PartdGapSectionComponent` (Part D gap plan cards with checkboxes), `PlanDetailDialogComponent` (full plan detail dialog), and `SelectedPlansSummaryComponent`. All tooltip data centralized in `data/tooltips.ts`.
- **Early Summary Panel:** `hasAnyPlanSelected` computed signal in `PlanRecommendationComponent` shows `SelectedPlansSummaryComponent` as soon as _any_ plan is selected in the active section (MA or Part D), even before the selection is complete. The summary is rendered with `[canCalculate]="hasCompleteSelection()"` passed as input — when `false`, the Calculate button is disabled and an amber hint guides the user (e.g., "Select a Part D gap plan below to calculate your total cost."). `hasCompletePlanSelection` in `MedicareStateService` remains the gate for enabling the actual cost evaluation.
- **Design:** AI-first approach — no fragile CMS Plan Finder REST API dependency.

---

## ✅ Plan-Aware Pharmacy Search
- **Status:** Feature structure exists but `PlanPharmacyService` has been removed. Plan-specific pharmacy pricing is not currently active.

---

## ✅ Per-Pharmacy Cost Breakdown in Plan Recommendations
- **What:** Each recommended plan includes a cost breakdown for every selected pharmacy, showing annual premium, deductible, drug copay, and total — with per-drug copay details and preferred pharmacy discounts.
- **Backend:** Plan recommendation computes pharmacy cost breakdowns — iterates each selected pharmacy per plan. Preferred pharmacies get copay discounts. `RankedPlan.CostBreakdowns` is a `List<PlanCostBreakdown>` sorted cheapest-first. Plan totals re-calculated from best pharmacy.
- **Frontend:** `PlanCostBreakdownComponent` renders `plan.costBreakdowns` via a self-contained toggle button. Each pharmacy shows an indigo card with cost grid + per-drug copay table with tier chips and preferred discount icons. Component manages its own expanded/collapsed state.
- **Models:** `PlanCostBreakdown` (pharmacyName, pharmacyNpi, isPreferredPharmacy, annualPremium, annualDeductible, annualDrugCopay, annualTotal, drugCopays). `DrugCopayDetail` (drugName, rxCui, formularyTier, monthlyCopay, annualCopay, isCovered, preferredDiscount).

---

## ✅ Plan Card Toggle UI (Clean Design)
- **What:** Plan recommendation cards show a compact view by default (header + cost grid + action buttons). Two toggle buttons reveal detail sections on-demand.
- **Toggle Buttons:**
  - **"Plan Features"** (`PlanDrugCoverageComponent`) — expands drug coverage table (tier badges, copay/mo, PA/QL flags) and AI explanation text.
  - **"Cost Breakup (N)"** (`PlanCostBreakdownComponent`) — expands per-pharmacy cost breakdown cards (badge shows pharmacy count). Only visible when pharmacies were selected.
- **State:** Each child component manages its own `expanded` boolean — no parent Set tracking needed. Buttons toggle independently per plan card.
- **UX:** Keeps the initial plan list scannable. Users can quickly compare plan costs across the list, then drill into specific plans for drug coverage or pharmacy cost details.

---

## ✅ Plan Comparison (Side-by-Side)
- **What:** Users can compare up to 3 Medicare plans side-by-side in a detailed comparison table.
- **Flow:** Check compare checkbox on plan cards (max 3) → sticky indigo bar appears at ≥2 → click "Compare Plans" → `PlanComparePanelComponent` renders comparison table.
- **Comparison Table:** 15 rows: insurance, network, star rating, monthly premium, annual deductible, est. drug cost, est. total, max OOP, benefits (dental/vision/hearing/fitness/OTC/mail-order), gap coverage, per-drug coverage (tier + copay), preferred pharmacy.
- **Winner Indicators:** Green check_circle icon on the lowest-cost plan per cost row.
- **Frontend:** `PlanComparePanelComponent` — `@Input plans`, `@Output closed/cleared`. `getCompareWinner()` compares numeric fields across plans. Parent `PlanRecommendationComponent` manages compare state array and panel visibility.

---

## ✅ AI Gap Coverage Plans (with Sub-Component & Selection)
- **What:** PDP plans lack Part A/B/dental/vision/hearing coverage. An AI-powered panel displays actual complementary plans (with carrier, premium range, deductible, coverage highlights) to fill each gap. Users can select gap plans via checkboxes — selecting any gap plan auto-selects the parent PDP plan for comparison.
- **Flow:** Amber banner on PDP plan cards → click "Find Gap Coverage Plans" → AI-generated gap coverage panel shows structured plan cards organized by coverage category. Each gap plan card has a checkbox for selection.
- **Backend:** `PromptBuilder` assembles system/task/schema/template prompts with `{{PLAN_NAME}}`, `{{PLAN_TYPE}}`, `{{MISSING_COVERAGES}}` placeholders. AI returns structured JSON (`GapCoverageResult` with `GapPlanDto[]`) — parsed and returned as-is. Schema enforces: category, planName, planType, carrier, monthlyPremiumRange, annualDeductible, coverageHighlights, whyNeeded, enrollmentTip, priority (Essential/Recommended/Optional).
- **Frontend:** Extracted to `PlanGapCoverageComponent` sub-component (standalone, OnPush). Calls `PlanRecommendationService.getGapAdvice()` directly. Response cached per plan — subsequent clicks toggle visibility without re-fetching. Each gap plan rendered as a card with category icon, priority badge, cost row, coverage highlight chips, enrollment tip, and **mat-checkbox**. Selected gap plans tracked in a local `Set` and emitted via `gapPlanSelected` output. `ChangeDetectorRef.markForCheck()` used to ensure OnPush detection works with async subscription callbacks.
- **Parent Integration:** `PlanCardComponent` handles `gapPlanSelected` via `onGapPlanSelected()` — auto-selects the parent plan for comparison (if not already selected and compare limit not reached) and emits `compareToggled`.
- **Prompts:** Gap coverage prompts integrated into the cost-evaluation prompt chain.
- **Models:** `GapCoverageResult` (gapPlans, comparisonTip). `GapPlan` (category, planName, planType, carrier, monthlyPremiumRange, annualDeductible, coverageHighlights, whyNeeded, enrollmentTip, priority).

---

## ✅ Financial Planner Plan Recommendations (Part D, Medicare Advantage, Medigap)

- **What:** Three standalone FP-integrated plan recommendation endpoints that allow the frontend to fetch real Financial Planner plan data (Part D, MA, Medigap quotes) and display them in the Medicare analysis step 4 plan cards.
- **Part D Plan Recommendation:**
  - **Backend:** `PartDPlanController` — `[Authorize]` `POST /api/PartDPlan/recommend`. `IPartDPlanRecommendationService` / `PartDPlanRecommendationService` in Infrastructure.
  - **Domain:** `PartDPlanRecommendationRequest`, `CountyCodeModel`, `PrescriptionInput`, `PharmacyInput` in `Models/PartDPlanRecommendation.cs`.
  - **Frontend:** `part-d-plan.service.ts` calls `POST /api/PartDPlan/recommend`. Model types: `PartDPlanRecommendationRequest`, `CountyCodeModel`, `PrescriptionInput`, `PartDPharmacyInput` in `models/part-d-plan.model.ts`.
- **Medicare Advantage Plan Recommendation:**
  - **Backend:** `MedicareAdvantagePlanController` — `[Authorize]` `POST /api/MedicareAdvantagePlan/recommend`. `IMedicareAdvantagePlanService` / `MedicareAdvantagePlanService` in Infrastructure. Request extends Part D fields with `medicareAdvantage: true`.
  - **Frontend:** `medicare-advantage-plan.service.ts`. Model: `MedicareAdvantagePlanRequest` in `models/medicare-advantage-plan.model.ts` (extends PartD request shape with `medicareAdvantage: true`).
- **Medigap Plan Quotes:**
  - **Backend:** `MedigapPlanController` — `[Authorize]` `POST /api/MedigapPlan/quotes`. `IMedigapPlanQuotesService` / `MedigapPlanQuotesService` in Infrastructure.
  - **Domain:** `MedigapPlanQuotesRequest`, `MedigapPlanQuotesResponse`, `MedigapPlanQuote` + nested carrier/rate structs in `Models/MedigapPlanQuotes.cs`.
  - **Frontend:** `medigap-plan.service.ts`. Models: `MedigapPlanQuotesRequest`, `MedigapPlanQuotesResponse`, `MedigapPlan`, `MedigapRate`, `MedigapCompanyBase` in `models/medigap-plan.model.ts`.
- **Plan Recommendation Component (`PlanRecommendationComponent`):**
  - Section chooser when no `activeSection` — two cards: "Part D + Medigap" / "Medicare Advantage" separated by a vertical "OR" divider (horizontal on mobile).
  - After selection: Shows full-width plan list for the active section.
  - Sub-components: `RecommendationCard`, `MedigapCard`, `MedigapGapSection`, `PartdGapSection`, `PlanDetailDialog`, `SelectedPlansSummary`.
  - Reconciles saved plan stubs from `ChatAnalysisSelectionHydrationService` with live API rows (match by id/name, clear unmatched).
  - Plan selections posted as system messages; chat-driven picks use `pendingPlanSelection` to avoid duplicate bubbles.
  - **Plan Card Enrichment:** `PlanCardEnrichmentService` (pure computation, no HTTP) computes derived display fields from raw API responses. `PlanRecommendationComponent` creates `computed()` enrichment maps (`partDEnrichmentMap`, `maEnrichmentMap`, `medigapEnrichmentMap`) keyed by plan ID/key, passed to card components as `[enriched]` input.
  - **Part D card enrichment:** Formatted plan ID (`contractId-planId-segmentId`), insurance carrier (from `contractIdCarrierMap[contractId]`), Part D surcharge (response-level `partDPremiumSurcharge`), prescription OOP, drugs covered X/Y, pharmacies in network X/Y.
  - **Medicare Advantage card enrichment:** Same as Part D plus combined surcharges (Part B + Part D), healthcare OOP (`partAandBBenefitServiceCost`), `hasPrescriptionDrug` flag for conditional Rx OOP display.
  - **Medigap card enrichment:** Insurance carrier (from `contractIdCarrierMap[naic]`), premium cents→dollars conversion (`rate.month/100`, `rate.annual/100`), Part B surcharge, healthcare OOP (`partBServiceOOP`), remaining months count. Gap section component (`MedigapGapSectionComponent`) also injects `PlanCardEnrichmentService` directly.
  - **Part D gap section enrichment:** `PartDGapSectionComponent` injects `PlanCardEnrichmentService` and passes enriched data to recommendation cards displayed in the MA gap section.
- **State:** `MedicareStateService` signals: `partDPlans`, `medigapQuotes`, `maPlans`, `selectedPartDPlan`, `selectedMedigapPlan`, `selectedMAPlan`, `selectedMAGapPartDPlan`, `activeSection`.

---

← [Feature Catalog Index](../ch08-feature-catalog/ch08-feature-catalog.md) | [← Drug Analysis](ch08-02-drug-analysis.md) | [Next: Cost Projections & Persistence →](ch08-04-cost-persistence.md)
