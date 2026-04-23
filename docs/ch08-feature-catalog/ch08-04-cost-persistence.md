# Chapter 8.4 — Cost Projections & Persistence Features

> Lifetime cost projections, analysis snapshots, recommendation CRUD, and saved data views.

← [Feature Catalog Index](../ch08-feature-catalog/ch08-feature-catalog.md)

---

## ✅ Lifetime Medicare Cost Projections (Financial Planner API)
- **What:** Integrates with the external Financial Planner `individualMedicareR5` API to compute lifetime Medicare cost projections for a user's selected plan — year-by-year breakdowns of Part A/B premiums, surcharges, OOP costs, Part D, concierge, and dental. Also calls `expensesPresentValue` API to compute present value of lifetime expenses at a 6% discount rate.
- **Flow:** User selects a plan → clicks "Calculate Lifetime Cost" button in the Selected Plans summary panel → `SavePrescriptionDialogComponent` opens pre-populated with a default name (`{FirstName} {Plan Type} – MM/DD/YYYY`, e.g. `John Medicare Advantage – 04/18/2026`) → user optionally edits name and clicks Save → `POST /api/plan-recommendation/evaluate-costs` → `CostProjectionService` loads user profile, resolves state code to full name, formats DOB as `MM-yyyy`, computes remaining months for adjusted month, maps all profile + plan fields → calls Financial Planner API → calls Present Value API (yearwise expenses, discount=6) → passes results to AI for evaluation → returns combined `CostProjectionResult` with financial data + present value + AI-generated insights → plans saved to `userAnalysisSelections` → recommendation saved inline to `recommendations` collection (with full profile + drugs + pharmacies + plans + cost snapshot) → user is navigated to `/medicare-analysis/cost-projections` page showing interactive Chart.js dashboards.
- **Backend:**
  - **Domain Models:** `IndividualMedicareRequest` (full API payload with 30+ fields including `supplementPlanType`), `IndividualMedicareResponse` (lifetime aggregates including `conciergeIncluded`, plan-specific lifetime fields: `lifeTimeABGD/ABFD/ABND/ABCDExpenses/Premium/Oop` + year-by-year list), `IndividualMedicareDetail` (per-year premiums/OOP/surcharges + `planGPremium`, `planFPremium`, `planNPremium`, `totalABGD/ABFD/ABND/ABCD`) — all in `Models/IndividualMedicare.cs`. `PresentValueRequest`, `PresentValueResponse`, `YearExpense`, `PvEntry` — in `Models/PresentValue.cs`. `CostProjectionResult` (with `PresentValue` field), `LifetimeTotals` (with `TotalIrmaa`, `SupplementPlanType`, `SupplementPlanPremium`, `ConciergeIncluded`, `LifeTimeConciergePremium`, 12 plan-specific lifetime fields), `CostEvaluation`, `LifetimeSummary`, `YearlyHighlight`, `CostCategory`, `SavingsTip` — all in `Models/CostProjection.cs`.
  - **Interface:** `IIndividualMedicareService` with `CalculateAsync(request, cancellationToken)` method. `IPresentValueService` with `CalculateAsync(request, cancellationToken)` for present value computation. `ICostEvaluationAiService` with `EvaluateAsync()` method for AI cost evaluation (accepts `supplementPlanType` and `supplementPlanPremium` parameters).
  - **Service:** `IndividualMedicareService` in `Infrastructure/FinancialPlanner/` — HTTP POST with Basic auth token from config, JSON serialization, structured logging. `PresentValueService` in `Infrastructure/FinancialPlanner/` — HTTP POST to `/expensesPresentValue` with same auth pattern. `CostEvaluationAiService` in `Infrastructure/AI/` — builds prompts via `PromptBuilder.BuildCostEvaluation()`, renders year-by-year breakdown text, calls `IChatClient`, parses AI JSON response into `CostEvaluation`.
  - **Application Service:** `CostProjectionService` orchestrates the full pipeline: profile resolution via `ProfileService`, state code→name resolution via static dictionary, DOB formatting, remaining months calculation, builds `IndividualMedicareRequest`, calls Financial Planner API, then AI evaluation, then Present Value API (non-fatal — wrapped in try/catch). Populates `LifetimeTotals` with `TotalIrmaa` (combined Part B + Part D surcharges), `SupplementPlanType`, `SupplementPlanPremium`, `ConciergeIncluded`, `LifeTimeConciergePremium`, and 12 plan-specific lifetime fields (ABGD/ABFD/ABND/ABCD × Expenses/Premium/Oop). Sets `CostProjectionResult.PresentValue` from PV API response. Method: `EvaluateCostsAsync()` (combined `CostProjectionResult`).
  - **Snapshot:** Cost snapshot uses plan-specific lifetime fields based on `SupplementPlanType` (G→ABGD, F→ABFD, N→ABND, C→ABCD, MA→ABMedicareAdvantage) and stores real FP present value instead of AI-derived `TotalCombined`.
  - **Controller:** `PlanRecommendationController.EvaluateCosts()` — thin delegation to `CostProjectionService` via `MapToInput()` helper. `CalculateCostsRequestDto` for plan-specific inputs (planBundleCode, premiums, OOP, benefit costs, supplementDataProvided, partDDataProvided, reserveDaysUsed, dental, dentalHealthGrade, boughtPlanA, medicareAdvantageDataProvided, partDPremium, calculateForAdjustedMonth, supplementPlanType).
  - **DI:** `AddHttpClient<IIndividualMedicareService, IndividualMedicareService>` with 30s timeout. `AddHttpClient<IPresentValueService, PresentValueService>` with 30s timeout. `AddScoped<ICostEvaluationAiService, CostEvaluationAiService>()`. `AddScoped<CostProjectionService>()`.
  - **AI Prompts:** 4 prompt files for cost evaluation: `cost-evaluation-system.txt` (Medicare financial advisor role, 8 rules), `cost-evaluation.txt` (task description), `cost-evaluation-schema.txt` (JSON schema with output rules), `cost-evaluation.txt` (template with 15 placeholders including plan details, lifetime totals, Total IRMAA, supplement plan type/premium, and yearly breakdown).
- **Frontend:**
  - **Plan Card Button:** "Calculate Lifetime Cost" button added to each `PlanCardComponent` via `@Input() isCostLoading` and `@Output() calculateCost`. Shows loading spinner during API call.
  - **State:** `MedicareStateService` extended with `costProjection`, `hasCostProjection` signals and `setCostProjection()` method.
  - **Request Model:** `CalculateCostsRequest` includes all Financial Planner fields: `planBundleCode`, `medicareAdvantagePremium`, `maWithPrescriptionBenefit`, `partDOOP`, `partDOOPFullYear`, `partABenefitServiceCost`, `partBBenefitServiceCost`, `planRecommendName`, `recommendationListId`, `supplementDataProvided`, `partDDataProvided`, `reserveDaysUsed`, `dental`, `dentalHealthGrade`, `boughtPlanA`, `medicareAdvantageDataProvided`, `partDPremium`, `calculateForAdjustedMonth`, `supplementPlanType`.
  - **Response Model:** `EvaluateCostsResponse` includes `presentValue` (from FP Present Value API). `LifetimeTotals` includes `totalIrmaa`, `supplementPlanType`, `supplementPlanPremium`, `lifeTimeConciergePremium`, `conciergeIncluded`, and 12 plan-specific lifetime fields (`lifeTimeABGD/ABFD/ABND/ABCDExpenses/Premium/Oop`). `IndividualMedicareDetail` includes `planGPremium`, `planFPremium`, `planNPremium`, `totalABGD/ABFD/ABND/ABCD`. `ExpenseTableRow` interface for expense table data.
  - **Service:** `PlanRecommendationService.evaluateCosts()` calls `POST /api/plan-recommendation/evaluate-costs`.
  - **Cost Projections Page:** `CostProjectionsComponent` at route `/medicare-analysis/cost-projections`. Standalone component with Chart.js 4.x integration. Navigation guard: `ngOnInit` checks `hasCostProjection()` — if false, resets state and redirects to `/medicare-analysis/plans`. Five charts: (1) line chart — total annual cost trajectory over time, (2) stacked bar chart — premium vs OOP vs surcharges per year, (3) doughnut chart — lifetime cost category breakdown, (4) bar chart — Part B + Part D surcharges by year, (5) **medicare projection chart** — stacked bar with 3 layers: base Premium (rgb(132,201,54)), IRMAA Surcharge (rgb(106,162,42)), Out-of-Pocket (rgb(204,0,0)), with summary strip showing Present Value, bundle Total Expenses, and Total IRMAA Surcharge. Also renders: **Medicare Expense Table** (7-column table showing coverage year + lifetime totals by Medicare bundle using plan-specific lifetime fields), lifetime summary cards (total premiums, total OOP, combined total, projection years, average annual), cost trajectory banner with explanation, yearly highlights table (flagged years), cost category analysis with progress bars, savings tips with priority badges, and overall AI assessment.
  - **Recommendation Detail Parity:** `RecommendationDetailComponent` Cost tab mirrors the cost-projections page — same Medicare Expense Table, Medicare Projection Chart + summary strip, plus all 4 original charts, sourcing data from `rec.lastCostSnapshot` instead of live state.
  - **Chart.js Setup:** Manually registers Chart.js controllers (`LineController`, `BarController`, `DoughnutController`), elements, scales, and plugins. Charts built in `afterNextRender()` lifecycle. `OnDestroy` cleans up chart instances.
  - **Navigation:** After evaluation completes, `PlanRecommendationComponent` navigates to `/medicare-analysis/cost-projections`. Back button returns to `/medicare-analysis/plans`.
  - **Inline Recommendation Save:** `runLifetimeCostEvaluation(recommendationName?)` accepts the name directly. After `saveCurrentPlans` succeeds, if a name was provided it calls `AnalysisSnapshotService.save(name)` immediately (before navigation). On 409 Conflict it retries with `force=true`. The chat confirmation path (`fromChatConfirmation=true`) passes no name to `runLifetimeCostEvaluation` and continues to rely on `pendingCostRunRecommendationName` + `tryAutoSaveRecommendation` in `CostProjectionsComponent.ngOnInit`.
  - **Default Dialog Name:** `SavePrescriptionDialogComponent` accepts `defaultName?: string` in its `SavePrescriptionDialogData`. `PlanRecommendationComponent.buildDefaultRecommendationName()` reads `profileService.profile()?.profile?.firstName`, formats today's date as `MM/DD/YYYY`, and builds `{FirstName} Medicare Advantage – MM/DD/YYYY` (or `Part D + Medigap` for the partd section). Falls back to the plan type alone if profile is not yet loaded.
  - **DB Persistence Pipeline:** `YearlyDetailDoc/Dto` stores all per-year fields including `planG/F/NPremium` and `totalABGD/ABFD/ABND/ABCD`. `CostSnapshotDoc` stores `PresentValue` (real FP value), plan-specific `LifetimeTotal`, `SupplementPlanType`, `SupplementPlanPremium`. `RecommendationController` mappers (`MapToYearlyDetailDto/Doc`) include all extended fields. `AnalysisSnapshotService` maps all new yearly fields during save.
- **Config:** `FinancialPlanner:BaseUrl` and `FinancialPlanner:AuthToken` in `appsettings.json`.
- **Field Mapping:** Profile fields (DOB, state, zip, tax filing, MAGI tier, health, tobacco, concierge, life expectancy, coverage year) automatically mapped from saved profile. Plan-specific fields (bundle code, MA premium, Part D OOP, benefit costs) provided by the frontend via request DTO.

---

## ✅ Cost Projection & Snapshot

- **What:** Cost projection snapshots capture plan-specific lifetime cost data for comparison.
- **Snapshot:** Uses plan-specific lifetime fields based on `SupplementPlanType` (G→ABGD, F→ABFD, N→ABND, C→ABCD, MA→ABMedicareAdvantage) and stores real FP present value from `CostProjectionResult.PresentValue`.

---

## ✅ Save Analysis (Chat + UI Button)

- **What:** Users can save their complete Medicare analysis (profile, drugs, pharmacies, plans, cost projections) as a named recommendation for future reference.
- **Three Entry Points:**
  1. **Plan page Calculate button (primary):** "Calculate Lifetime Cost" button in `SelectedPlansSummaryComponent` → name dialog pre-populated with `{FirstName} {Plan Type} – MM/DD/YYYY` → user saves → plans saved + recommendation saved inline before navigation to cost-projections. No separate save step required.
  2. **Chat:** Say "save analysis as My Medicare Plan" → AI extracts `ACTION_SAVE_ANALYSIS` intent with `analysisName` parameter → saves directly if prerequisites met.
  3. **UI Button:** "Save Analysis" button in `CostProjectionsComponent` header → opens `SavePrescriptionDialogComponent` (with custom title/subtitle/icon) → user enters name → saves.
- **Prerequisite Check:** `AnalysisSnapshotService.canSave()` verifies 5 prerequisites: profile complete, drugs confirmed, pharmacies selected, plan selected, cost projection available. Shows descriptive error message if any prerequisite is unmet. (Note: this check is used by the chat and cost-projections page paths; the plan-page inline save bypasses this check as all prerequisites are guaranteed at that point by UI guards.)
- **Overwrite Handling:** If a recommendation already exists (409 Conflict), the user is asked "Would you like to overwrite?" → `force: true` re-sends the request.
- **Reset After Save:** On successful save, calls `state.resetAll()` and navigates to `/medicare-analysis/profile` to start a fresh analysis.
- **Backend:** `POST /api/recommendation` with expanded request body including `CostSnapshotDto` (yearly details + full AI evaluation) and `SelectedPlanDto` (7 expanded fields: deductible, starRating, totalPrescriptionCost, totalPlanCost, prescriptionDrugCovered, unavailableDrugs, planExpenses).
- **Frontend:**
  - **`AnalysisSnapshotService`** — Assembles full snapshot from current state signals. `canSave()` checks prerequisites. `save(name, force?)` builds request and calls `RecommendationService.create()`.
  - **`ChatRouterService`** — `ACTION_SAVE_ANALYSIS` handler with dialog, prerequisite check, overwrite confirmation via `pendingSaveAnalysisOverwrite` signal.
  - **`CostProjectionsComponent`** — Save Analysis button, `saveAnalysis()` method with dialog + auto-overwrite on 409.

---

## ✅ Expanded Analysis Persistence for PDF Generation

- **What:** The recommendation document stores enough data to recreate a full PDF report without re-running any API calls or AI analysis.
- **Expanded `SelectedPlanDoc/Dto`:** +7 fields — `deductible`, `starRating`, `totalPrescriptionCost`, `totalPlanCost`, `prescriptionDrugCovered`, `unavailableDrugs[]`, `planExpenses[]`.
- **Expanded `CostSnapshotDoc/Dto`:** +4 fields — `supplementPlanType`, `supplementPlanPremium`, `yearlyDetails[]` (16 financial fields per year), `evaluation` (full AI analysis).
- **New Embedded Documents (Backend):** `YearlyDetailDoc`, `CostEvaluationDoc`, `LifetimeSummaryDoc`, `YearlyHighlightDoc`, `CostCategoryDoc`, `SavingsTipDoc`, `PlanExpenseDoc`.
- **New DTOs:** `RecommendationSummaryResponse`, `YearlyDetailDto`, `CostEvaluationDto`, `LifetimeSummaryDto`, `YearlyHighlightDto`, `CostCategoryDto`, `SavingsTipDto`, `PlanExpenseDto`.
- **New Frontend Interfaces:** `YearlyDetailDto`, `CostEvaluationDto`, `LifetimeSummarySnapDto`, `YearlyHighlightDto`, `CostCategorySnapDto`, `SavingsTipSnapDto`, `PlanExpenseDto`.
- **Controller Mapping:** 8+ new mapping helpers in `RecommendationController` for bidirectional document↔DTO conversion.

---

## ✅ Recommendation Snapshot Persistence (Full Analysis CRUD)

- **What:** The system persists a complete recommendation snapshot (profile + drugs + pharmacies + plans + cost projections) in MongoDB, enabling the orchestrator to manage the active recommendation lifecycle via natural language and allowing users to view, compare, and reload past analyses.
- **MongoDB Collection:** `recommendations` — one document per active recommendation per user (no multi-version history; overwrite with `force: true`).
- **Document (`RecommendationDocument`):** Stores all fields needed to reconstruct a full analysis display without re-running any APIs: `profileSnapshot`, `drugList`, `pharmacies`, `mailOrderPharmacy`, `planSelections`, `costSnapshot` (with `yearlyDetails[]`, `evaluation` EmbeddedDoc); all with embedded document types.
- **Backend CRUD:**
  - `GET /api/recommendation` — get active recommendation
  - `GET /api/recommendation/{id}` — get by ID (full detail)
  - `GET /api/recommendation/all` — get all summaries for current user
  - `POST /api/recommendation` — create (with `?force=true` for overwrite)
  - `PUT /api/recommendation/profile` — update profile snapshot
  - `PUT /api/recommendation/drugs` — update drug list
  - `PUT /api/recommendation/pharmacy` — update pharmacy selection
  - `PUT /api/recommendation/plans` — update plan selections
  - `PUT /api/recommendation/cost-snapshot` — update cost snapshot
  - `DELETE /api/recommendation` — delete active recommendation
- **Application Layer:** `RecommendationService` (CRUD methods), `IRecommendationRepository` (MongoDB).
- **Frontend:** `RecommendationService` (HTTP CRUD), `RecommendationStateService` (signal-based: `activeRecommendation`, `hasRecommendation`, `refreshAfterUpdate`, `clear`). Loaded on `DashboardComponent.ngOnInit` via `loadActiveRecommendation$()`.

---

## ✅ User Analysis Selections Persistence

- **What:** Users' Medicare analysis wizard selections (drugs, pharmacies, plans, active section) are persisted across logins in a dedicated MongoDB document, separate from the full recommendation snapshot.
- **MongoDB Collection:** `userAnalysisSelections` — one document per user.
- **Document (`UserAnalysisSelectionsDocument`):** Stores: `drugs[]` (with formulation + detail), `pharmacies: UserAnalysisPharmacyDoc[]`, `plans: UserAnalysisPlanDoc[]`, `activeSection` (partd/ma).
- **Backend:** `IUserAnalysisSelectionsRepository`, updates via `PUT` calls on underlying recommendation sub-fields.
- **LTC Selections Persistence:** A separate `ltcCurrentSelections` MongoDB collection stores the most recent LTC care-type selections per user (`LtcCurrentSelectionsDocument`). `LtcSelectionsController` — `[Authorize]` `PUT /api/ltc/current` (save), `GET /api/ltc/current` (load current).

---

## ✅ Saved Data Page (Filter / Sort / Pagination / Compare)

- **What:** A full-featured page listing saved recommendation analyses with client-side search, filter, sort, pagination, and a compare basket. Accessible via header button, dropdown menu, and chat navigation.
- **Route:** `/saved` → `RecommendationComponent`; `/saved/compare` → `RecommendationCompareComponent`.
- **Filter / Sort / Pagination:**
  - **Search:** Text input filters cards by analysis name (case-insensitive, real-time).
  - **Type pills:** All / Medicare / Long Term Care — filters by `recommendation.type`.
  - **Sort:** 6 options — Newest First, Oldest First, Name A–Z, Name Z–A, Highest Cost, Lowest Cost.
  - **Pagination:** Configurable page size (10/25/50); Prev/Next and page number buttons.
- **Compare Basket:**
  - Each card has an **Add to Compare** / **Remove** toggle button.
  - Sticky ribbon at the bottom of the screen appears when ≥1 item is in the basket. At 2 items, a **Compare** button navigates to `/saved/compare`.
  - Compare is type-aware — Medicare and Long Term Care analyses are compared in their respective context.
  - `RecommendationCompareComponent` orchestrates comparison: reads `ids` from query params, `forkJoin` loads both records, determines mode (`medicare` / `longterm` / `cross`), renders hero header with **Illustration A/B aliases** (recommendation name shown as primary title, alias below in orange/green), and dispatches to:
    - `CompareMedicareComponent` — 4-tab Medicare comparison shell. `CompareMedicareMetricsComponent` renders KPI cards above tabs. Tabs: Overview (`TabOverviewComponent` — 6 KPI deltas + 5 key-difference sections), Profile (`TabProfileComponent` — shared, 4 grouped card sections), Rx, Pharmacy & Plans (`TabRxPharmacyPlansComponent` — side-by-side Rx drug cards + storefront cards + detailed plan cards), Cost Analysis (`TabCostAnalysisComponent` — Chart.js line + bar charts with orange/green series + year-by-year delta table).
    - `CompareLtcComponent` — 4-tab LTC comparison (Overview, Profile, Care Config, Cost Analysis). `CompareLtcMetricsComponent` renders KPI cards above tabs.
    - `CompareCrossComponent` — 3-tab cross-type comparison (Overview with disclaimer, Profile, Cost Summary). `CompareCrossMetricsComponent` renders KPI cards above tabs.
  - **Illustration Aliasing:** Left recommendation is labeled **Illustration A** (orange), right is **Illustration B** (green). Constants `LABEL_A` / `LABEL_B` in `compare-helpers.ts` control the alias text; change once to update everywhere. Chart colors use `CHART_COLOR_A` (orange `#c2410c`) and `CHART_COLOR_B` (green `#15803d`). Orange/green palette chosen for WCAG AA contrast (6:1+) for elderly readability.
  - Shared helpers in `compare-helpers.ts`: label/color constants, delta formatting, trajectory icons/colors, star arrays, profile row builder with grouped labels (personal/location/health/financial) and inline formatters (health condition, gender, tobacco, concierge, tax filing).
- **Card Layout (4-row grid):** Analysis name (displayed in **uppercase**), creation date, type badge, drug count, plan count, lifetime total (when available), status pill. Compare basket slots also show the name in uppercase.
- **Empty State:** "No saved analyses" when list is empty; "No results" when active filters match nothing.
- **Backend:** `GET /api/recommendation/all` returns `RecommendationSummaryResponse[]` (id, name, status, drugCount, planCount, hasCostSnapshot, lifetimeTotal, dates). `IRecommendationRepository.GetAllByUserIdAsync()` sorted by CreatedAt desc.
- **Frontend Navigation (3 entry points):**
  1. **Header icon button:** `folder_open` icon always visible in the toolbar.
  2. **Dropdown menu item:** "Saved Data" in user menu (when profile complete).
  3. **Chat:** `NAVIGATE_SAVED_ANALYSES` intent routes to `/saved`. `ACTION_LOAD_PRESCRIPTIONS` also navigates to `/saved`.
  3. **Chat:** `NAVIGATE_SAVED_ANALYSES` intent routes to `/saved`. `ACTION_LOAD_PRESCRIPTIONS` also navigates to `/saved` (page now shows analyses only).

---

## ✅ Saved Recommendation Detail View

- **What:** A dedicated full-detail view for any saved recommendation, accessible from the Saved Data page. Professional redesign matching the compare page design language.
- **Route:** `/saved/:id` → `RecommendationDetailComponent`.
- **Design:**
  - **Hero Header:** Dark gradient bar with type badge (Medicare/LTC), back button, save date.
  - **Medicare KPI Strip:** 6 cards above tabs (Lifetime, Premiums, OOP, IRMAA, Present Value, Current Year).
  - **Medicare Tabs (5):**
    1. **Profile** — 3 grouped section cards (Personal, Location, Health & Financial) with colored icons and human-readable labels.
    2. **Prescriptions** — Drug count pill + clean HTML table.
    3. **Pharmacy** — Storefront-style cards with type badge, phone/distance/NPI icons, mail-order card.
    4. **Plans** — Card-per-plan with colored type headers, 6-metric grid, visual star ratings, unavailable drug chips.
    5. **Cost & Charts** — Trajectory banner, Chart.js charts (line, stacked bar, doughnut, projection), Medicare Expense Table, summary strip.
  - **LTC Tabs (3):** Profile, Care Config, Cost Analysis (trajectory, categories, tips, assessment).
- **Helper Methods:** `fmtGender()`, `fmtHealth()`, `fmtTaxFiling()`, `starArray()`.
- **Backend:** `GET /api/recommendation/{id}` returns full `RecommendationResponse` including embedded `CostSnapshotDto` with `yearlyDetails[]` and `evaluation`.
- **Frontend:** `RecommendationDetailComponent` — standalone, OnPush, Chart.js manually registered. `RecommendationService.getById(id)` called on init from route param.

---

← [Feature Catalog Index](../ch08-feature-catalog/ch08-feature-catalog.md) | [← Pharmacy & Plans](ch08-03-pharmacy-plans.md) | [Next: Chat Features →](ch08-05-chat-features.md)
