# Chapter 2.4 — Components: Medicare Analysis

> Analysis wizard shell, drug search & formulation, pharmacy lookup, plan recommendations, and cost projections.

← [Chapter 2 — Frontend Architecture (Index)](../ch02-frontend-architecture/ch02-frontend-architecture.md)

---

### `AnalysisShellComponent` (`medicare-analysis/analysis-shell.component.ts`, `.html`, `.scss`)
- **Role:** Parent wizard shell for the Medicare analysis flow (four primary steps). Routed to at `/medicare-analysis`.
- **Guarded by:** `profileCompleteGuard`.
- **Layout:** Vertical flex — step indicator (top), `<router-outlet>` (scrollable middle), Back/Continue navigation bar (bottom).
- **Step Indicator:** Horizontal numbered badges (1·Profile → 2·Drugs → 3·Pharmacies → 4·Plans) connected by lines. Current step highlighted in cyan, completed steps show a check icon, future steps are grey. Forward navigation to a later step is blocked until prior prerequisites are met (e.g. cannot jump to Pharmacies before drugs are confirmed — `canNavigateToStep()`). `/medicare-analysis/cost-projections` is a fifth child route (cost dashboard) and does not add a stepper step.
- **Navigation Bar:** Back button (left, hidden on step 1) and Continue button (right, hidden on step 4 — Plans). Continue on step 1 (Profile) is always enabled when the guard allows analysis (profile complete). Continue on step 2 requires `hasDrugDetails()` and `hasConfirmedDrugs()`; step 3 requires selected lookup or legacy pharmacies. `goNext()` sets `pharmacySelectionConfirmed` when advancing from Pharmacies to Plans. Emits system messages on navigation and new analysis.
- **Step Tracking:** Reads/writes `MedicareStateService.currentStep` (`1 | 2 | 3 | 4`). Persisted snapshots include `analysisStepSchemaVersion: 2`; older session data without that field migrates legacy steps 1–3 to new steps 2–4. Child step components set `currentStep` on init (`profile` → 1, `drugs` → 2, `pharmacies` → 3, `plans` → 4).
- **State:** Injects `MedicareStateService`, `Router`. No local state beyond step definitions.

### `DrugsStepComponent` (`medicare-analysis/drug-step/drug-step.component.ts`, `.html`)
- **Role:** Drugs step (shell step 2) for the Financial Planner analysis wizard — supports both direct page-based drug search and detailed formulation selection workflow.
- **State:** Injects `MedicareStateService`, `DrugService`, `PrescriptionService`, `MatSnackBar`, `MatDialog`. Local signals: `formulationSelections`, `drugSelections`, `drugQuantities`. Shared: `confirmedDrugNames` (delegated to `MedicareStateService.confirmedDrugNames`).
- **OnInit:** Sets `currentStep` to `2`. Auto-fetches drug details if needed. Restores all selections from `sessionStorage`.
- **Direct Drug Search (drugs page):** Includes an input area in the Drugs page that reuses the same name-suggestion and confirm flow as chat (`ChatDrugFlowService`). Users can type drugs on the left panel, verify candidates, and run bulk search without using chat.
- **Sub-components:**
  - **`InteractionAlertsComponent`** — Severity-coded drug interaction cards (High/Moderate/Low). Input: `interactions`.
  - **`DuplicateTherapyAlertsComponent`** — Amber duplicate therapy warning cards. Input: `duplicateTherapies`.
  - **`DrugSelectionPanelComponent`** — 4-step guided selection (type→form→strength→qty/month) + confirm/edit buttons per drug. Exports `DrugSelectionState` interface. Inputs: `result`, `selection`, `selectedFormulation`, `quantity`, `confirmed`. Outputs: `typeSelected`, `dosageFormSelected`, `strengthSelected`, `quantityChanged`, `quantityPresetSelected`, `drugConfirmed`, `drugEditRequested`.
  - **`SelectedDrugsSummaryComponent`** — Confirmed drugs summary with edit/remove + save prescription button. Input: `confirmedDrugs`. Outputs: `editDrug`, `removeDrug`, `savePrescription`.
- **Parent responsibilities:** Expansion panel accordion (`mat-accordion multi`) with auto-collapse on confirm and auto-open next. `confirmDrug()`, `editDrug()`, `removeDrug()` manage panel state + shared confirmed set. `savePrescription()` opens `SavePrescriptionDialogComponent` and calls `PrescriptionService.save()`. `persistSelections()` / `restoreSelections()` handle four `sessionStorage` keys.
- **Computed Signals:** `results`, `interactions`, `duplicateTherapies`, `allSelected`, `selectedCount`, `confirmedCount`, `confirmedDrugsList`, `hasAnyConfirmed`.
- **Chat-Driven Drug Selection:** `effect()` watches `state.pendingDrugSelection` signal. `applyChatDrugSelection()` handles actions: `confirm_all` → `confirmAllReadyDrugs()`, `remove` → `removeDrug(name)`, `edit` → `editDrug(name)`, `select` → `applySelectionToDrug()` with fuzzy matching for form/strength (auto-confirms when all 4 fields complete). `findMatchingDrugName()` does case-insensitive partial match against loaded drug names.

### `SavePrescriptionDialogComponent` (`medicare-analysis/drug-step/save-prescription-dialog/save-prescription-dialog.component.ts`, `.html`)
- **Role:** Confirmation popup dialog for saving a prescription. Used by `DrugsStepComponent` via the Save Prescription button.
- **State:** Signal `prescriptionName` for the input field. Accepts optional `SavePrescriptionDialogData` via `MAT_DIALOG_DATA` to customise `title`, `subtitle`, and `icon` (defaults: "Save Prescription", "Enter a name for this prescription to save it", "save").
- **Features:**
  - Header with configurable icon and descriptive text.
  - `mat-form-field` with outline appearance for prescription name input (maxlength 200).
  - **Submit** button: closes dialog returning the name string. Disabled when name is empty.
  - **Cancel** button: closes dialog returning null.
  - Supports Enter key to submit.
- **Dialog Config:** 440–480px width, autoFocus, `prescription-dialog` panel class.
- **Data Flow:** Returns `string | null` — parent receives the name, builds `SavePrescriptionRequest` from confirmed drugs, calls `PrescriptionService.save()`, shows `MatSnackBar` on success/error.

### `PharmacyStepComponent` (`medicare-analysis/pharmacy-step/pharmacy-step.component.ts`, `.html`, `.scss`)
- **Role:** Step 3 of the analysis wizard — Financial Planner pharmacy lookup with filters, pagination, and multi-selection.
- **State:** Injects `MedicareStateService`, `DrugService`. Local signals: `nameFilter`, `radiusFilter` (default '25'), `pageSize` (default 20), `currentPage` (default 1). Readonly arrays: `radiusOptions` ['10', '25', '50', '100'], `pageSizeOptions` [10, 20, 50].
- **OnInit:** Sets `currentStep` to `3`. Auto-loads pharmacies via `DrugService.lookupPharmacies()` if not already loaded.
- **Imports:** `FormsModule`, `MatButtonModule`, `MatFormFieldModule`, `MatInputModule`, `MatSelectModule`, `MatIconModule`, `MatTooltipModule`.
- **Features:**
  - Header with pharmacy icon and "Select Your Pharmacies" title.
  - **Filter bar:** Pharmacy name text input (supports Enter key to search), radius dropdown (10/25/50/100 miles), per-page dropdown (10/20/50), Search button, Clear button. Filter field subscript wrappers hidden via SCSS for compact layout.
  - Loading spinner while pharmacies are being fetched.
  - **Results summary:** Total pharmacies count, search radius, page X/Y indicator, selected count badge (X/5).
  - **Pharmacy cards:** Name, pharmacyNumber (#), distance badge (formatted to 1 decimal + " mi"), address, zipcode, custom checkbox toggle (max 5). Two Google Maps action icon buttons per card: "Spot on Map" (`getSpotOnMapUrl` — `https://www.google.com/maps?q=NAME,ADDRESS,ZIPCODE`) and "Directions" (`getDirectionsUrl` — `https://www.google.com/maps/dir/?api=1&destination=NAME,ADDRESS,ZIPCODE`).
  - **Pagination:** Prev/Next buttons, page number buttons (window of ±2 around current page), disabled states at boundaries.
  - **Selected Pharmacies Review:** Emerald summary panel listing selected pharmacies (name, address, zipcode, distance) with remove (×) buttons.
  - Empty state when no pharmacies are found — suggests adjusting filters or increasing radius.
- **Methods:** `loadPharmacies()`, `applyFilters()` (resets to page 1), `clearFilters()` (resets name, radius to 25, page to 1), `goToPage(n)`, `onPageSizeChange(n)`, `getSpotOnMapUrl(pharmacy)`, `getDirectionsUrl(pharmacy)`, `formatDistance(distance)`, `getPageNumbers()`.
- **Chat-Driven Pharmacy Selection:** `effect()` watches `state.pendingPharmacySelection` signal. `applyChatPharmacySelection()` handles actions: `search` → sets `nameFilter` + `applyFilters()`, `select` → `findPharmacyByName()` + `toggleLookupPharmacy()`, `remove` → finds in selected list + `toggleLookupPharmacy()`. `findPharmacyByName()` does case-insensitive partial match (exact match first, then partial, preferring closest).

### `PlansStepComponent` (`medicare-analysis/plans-step/plans-step.component.ts`, `.html`, `.scss`)
- **Role:** Step 4 of the analysis wizard — Financial Planner Medicare plan recommendations (Part D + Medigap vs Medicare Advantage).
- **State:** Injects `MedicareStateService`. Embeds only **`PlanRecommendationComponent`** via `<app-plan-recommendation>`.
- **OnInit:** Sets `currentStep` to `4`.
- **Features:**
  - Header with health icon and "Medicare Plan Recommendations" title (Part D + Medigap vs MA subtitle).
  - **Refresh Recommendations** button when Part D or MA plan lists have finished loading (not loading).
  - **`PlanRecommendationComponent`** — section chooser (PDP+Medigap vs MA) when no `activeSection`, full-width section when active, selected-plans summary, lifetime cost action, reconciliation of saved-analysis stubs to live API rows, assistant messages for select/remove/clear/match (see component below).

### `PlanRecommendationComponent` (`plan-recommendation/plan-recommendation.component.ts`, `.html`)
- **Role:** plan UI for analysis step 4 — Part D list, Medigap gap section, MA list, MA gap Part D, selected-plans summary, lifetime cost evaluation.
- **Behavior (non-exhaustive):** Loads Part D / MA when `activeSection` is set; loads Medigap quotes after Part D selection. **`ChatAnalysisSelectionHydrationService`** may pre-fill selections from the active recommendation; this component **reconciles** hydrated stubs with rows returned by the plan APIs (match by id/name) and **clears** selections that do not appear in the current lists, with **`PLAN_MESSAGES.*`** assistant explanations. UI-driven plan picks post **`addAssistantMessage`** (not only system pills). Chat-driven picks use `pendingPlanSelection` / `applyChatPlanSelection` with `fromChat` to avoid duplicate bubbles when the extractor already replied.
- **Plan Card Enrichment:** Injects `PlanCardEnrichmentService` and creates three `computed()` enrichment maps (`partDEnrichmentMap`, `maEnrichmentMap`, `medigapEnrichmentMap`). Each map is keyed by `contractId-planId` (Part D/MA) or `key` (Medigap) and contains derived display fields computed from raw API responses. Helper methods `getPartDEnriched()`, `getMAEnriched()`, `getMedigapEnriched()` pass enriched data to card `[enriched]` inputs. Enriched fields include: formatted plan IDs, insurance carrier lookups, Part D surcharge, prescription OOP, pharmacy network ratios, drug coverage ratios, Medigap cents→dollars conversion, Part B surcharge, healthcare OOP, remaining months, MA combined surcharges.
- **Section Chooser:** When no `activeSection` — two cards: "Part D + Medigap" / "Medicare Advantage" separated by a vertical "OR" divider (3-column grid `sm:grid-cols-[1fr_auto_1fr]`) that switches to a horizontal line divider on mobile.

### `PlanRecommendationComponent` (`plan-recommendation/plan-recommendation.component.ts`, `.html`, `.scss`)
- **Role:** Medicare plan recommendations panel. Loaded on-demand.
- **State:** Injects `MedicareStateService`, `PlanRecommendationService`, `DrugService`.
- **Local State:** `expandedFeatures` Set and `expandedCostBreakup` Set track which plan cards have their detail sections expanded.
- **Features:**
  - `loadPlans()` builds `DrugSummaryInput[]` from current drugs, collects `selectedPharmacies` from state (up to 5), and calls `PlanRecommendationService.recommend()` with both.
  - 5 ranked plan cards showing:
    - **Header:** Plan name, type badge (MA-PD/PDP/D-SNP with tooltip), network type badge (HMO/PPO/PFFS/HMO-POS with tooltip), provider network size badge (Large/Medium/Small), insurance name, star rating (full+half stars).
    - **Coverage badges row:** Static Part A/B/D/Dental/Vision/Hearing badges (green = included, gray strikethrough = not included) driven by `PLAN_COVERAGE_INFO` in `tooltips.ts`, based on plan type.
    - **Extra benefits row:** AI-driven badges — OTC allowance ($/qtr), Mail-Order Savings, Gap Coverage (None/Some/Full), Emergency Nationwide, Fitness program.
    - **Pros & Cons:** Side-by-side green/red panels with AI-generated bullet-point highlights and trade-offs.
    - **Cost grid:** Monthly premium, annual deductible, est. drug cost/yr, est. total/yr, max out-of-pocket.
  - **Gap Coverage Plans:** Delegated to `PlanGapCoverageComponent` sub-component. Amber banner with "Find Gap Coverage Plans" button for PDP plans. Calls `POST /api/plan-recommendation/gap-advice` which returns structured plan data (`GapPlan[]`). Gap plan cards include checkboxes for selection — selecting any gap plan auto-selects the parent PDP plan for comparison.
  - LIS eligibility banner (amber) when `lisEligible` is true.
  - **Compare Plans:** Checkbox on each card (max 3). Sticky indigo bar appears at ≥2 selections. Opens comparison table panel.
  - All labels, descriptions, colors, and tooltips are centralized in `data/tooltips.ts` for easy maintenance.

- **Component Decomposition:** `PlanRecommendationComponent` is decomposed into 5 components:

  | Component | Selector | Role | @Input | @Output |
  |-----------|----------|------|--------|---------|
  | `PlanRecommendationComponent` | `app-plan-recommendation` | Plan loading, compare state, LIS banner, recommended badge | — | — |
  | `PlanCardComponent` | `app-plan-card` | Individual plan card — header, badges, benefits, pros/cons, cost grid, calculate cost button | `plan`, `index`, `isCompareSelected`, `compareDisabled`, `isSelected`, `isCostLoading` | `compareToggled`, `calculateCost` |
  | `PlanGapCoverageComponent` | `app-plan-gap-coverage` | Gap coverage plans sub-component — AI gap plan cards with checkboxes, selection tracking | `plan` | `gapPlanSelected` |
  | `PlanComparePanelComponent` | `app-plan-compare-panel` | Side-by-side comparison table (max 3 plans, green winner indicators per row) | `plans` | `closed`, `cleared` |
  | `PlanCostBreakdownComponent` | `app-plan-cost-breakdown` | Collapsible per-pharmacy cost breakdown cards with copay details | `breakdowns` | — |
  | `PlanDrugCoverageComponent` | `app-plan-drug-coverage` | Collapsible drug coverage table (tier badges, PA/QL flags) + AI explanation | `drugCoverages`, `aiExplanation` | — |

  - Each child component manages its own expanded/collapsed state.
  - `PlanCostBreakdownComponent`, `PlanDrugCoverageComponent`, and `PlanGapCoverageComponent` are children of `PlanCardComponent`.
  - Tooltip/description helpers imported directly from `data/tooltips.ts` — no parent-to-child passing.
  - `PlanGapCoverageComponent` calls `PlanRecommendationService.getGapAdvice()` directly and manages its own gap plan state, selection tracking (checkbox Set), and change detection (`ChangeDetectorRef.markForCheck()` for OnPush). Emits `gapPlanSelected` output with selected gap plans.
  - `PlanCardComponent` handles `gapPlanSelected` via `onGapPlanSelected()` — auto-selects the parent plan for comparison when any gap plan checkbox is checked.
  - Parent helpers: `parsePrice()`, `formatPlanType()`, `getPlanTypeBadgeClass()`, `getPlanTypeDescription()`.
  - **Calculate Lifetime Cost:** Each plan card has a "Calculate Lifetime Cost" button at the bottom. `@Input() isCostLoading` shows a spinner during calculation. `@Output() calculateCost` emits the plan to the parent. Parent `PlanRecommendationComponent` handles the call to `PlanRecommendationService.evaluateCosts()`, stores result in `MedicareStateService.costProjection`, and navigates to `/medicare-analysis/cost-projections`.

### `CostProjectionsComponent` (`cost-projections/cost-projections.component.ts`, `.html`, `.scss`)
- **Role:** Full-page cost projections dashboard with Chart.js visualizations and AI-generated insights. Routed at `/medicare-analysis/cost-projections`.
- **State:** Injects `MedicareStateService` (reads `costProjection` signal), `Router`.
- **Chart.js Integration:** Chart.js 4.x with manual controller registration (`LineController`, `BarController`, `DoughnutController`, `ArcElement`, `LineElement`, `BarElement`, `PointElement`, `CategoryScale`, `LinearScale`, `Tooltip`, `Legend`, `Filler`). Charts built in `afterNextRender()` lifecycle hook.
- **5 Charts:**
  1. **Line Chart:** Total annual cost trajectory over projection period (filled area).
  2. **Stacked Bar Chart:** Premium vs Out-of-Pocket vs Surcharges breakdown per year.
  3. **Doughnut Chart:** Lifetime cost category breakdown (from AI `categories` data).
  4. **Bar Chart:** Part B + Part D surcharges by year.
  5. **Medicare Projection Chart:** Stacked bar chart with 3 layers — base Premium (rgb(132,201,54)), IRMAA Surcharge (rgb(106,162,42)), Out-of-Pocket (rgb(204,0,0)). Premium bars use base amounts (premium − surcharge). Tooltip shows per-bar total. Full-width card with summary strip below: Present Value as of coverage year, bundle-specific Total Expenses, Total IRMAA Surcharge.
- **Medicare Expense Table:** 7-column table showing current coverage year and lifetime totals by Medicare bundle (ABD+G, AB+MA, etc.). Uses plan-specific lifetime fields (`lifeTimeABGDExpenses/Premium/Oop`, etc.) from the FP API response instead of summing yearly details. Bundle label dynamically generated from `planBundleCode` + `supplementPlanType` + concierge indicator.
- **Computed Getters:** `bundleLabel` (dynamic bundle label), `expenseTableRow` (current year + lifetime expense/premium/OOP), `presentValueAmount` (from FP Present Value API), `planSpecificLifetimeExpense`, `totalIrmaaSurcharge`, `coverageYear`.
- **Dashboard Sections:**
  - Lifetime summary cards (total premiums, total OOP, combined total, projection years, average annual cost).
  - Medicare expense table (7-column: bundle label, coverage year totals, lifetime totals).
  - Medicare projection chart with summary strip (PV, total expenses, IRMAA).
  - Cost trajectory banner with Rising/Stable/Declining/Mixed indicator and AI explanation.
  - Yearly highlights table (flagged years: Highest, Lowest, Spike, Normal).
  - Cost category analysis with progress bars and trend indicators.
  - Savings tips cards with priority badges (High/Medium/Low) and estimated savings.
  - Overall AI assessment text.
- **Navigation:** Back button navigates to `/medicare-analysis/plans`.
- **Save Analysis Button:** Header contains a "Save Analysis" `mat-flat-button` with `assessment` icon. `saveAnalysis()` opens `SavePrescriptionDialogComponent` (with custom title "Save Analysis", subtitle "Enter a name for this analysis", icon "assessment"), calls `AnalysisSnapshotService.save()`, handles 409 conflict with automatic overwrite. On success, calls `state.resetAll()` and navigates to `/medicare-analysis/profile`.
- **Cleanup:** `OnDestroy` destroys all Chart instances to prevent memory leaks.
- **Imports:** `CommonModule`, `CurrencyPipe`, `MatIconModule`, `MatButtonModule`, `MatCardModule`, `MatTooltipModule`, `MatProgressSpinnerModule`.

---

← [Chat Components](ch02-03-components-chat.md) | [Chapter 2 — Frontend Architecture (Index)](../ch02-frontend-architecture/ch02-frontend-architecture.md) | [Next → Saved Data & LTC Components](ch02-05-components-saved-ltc.md)
