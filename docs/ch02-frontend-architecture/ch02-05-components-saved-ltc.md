# Chapter 2.5 — Components: Saved Data, Compare & LTC

> Saved analyses list, detail view, side-by-side comparison (Medicare, LTC, cross-type), and Long Term Care wizard.

← [Chapter 2 — Frontend Architecture (Index)](../ch02-frontend-architecture/ch02-frontend-architecture.md)

---

### `RecommendationComponent` (`recommendation/recommendation.component.ts`, `.html`)
- **Role:** Saved analyses list page with full client-side filter, sort, pagination, and compare basket. Routed at `/saved`.
- **State:** Injects `RecommendationService`, `Router`. Local signals: `recommendations` (`RecommendationSummaryResponse[]`), `loadingRecommendations`, `searchTerm`, `selectedType`, `sortBy`, `currentPage`, `pageSize`, `compareBasket` (up to 2 items).
- **OnInit:** Loads `RecommendationService.getAll()`.
- **Filter/Sort/Pagination:**
  - **Search:** Text input filters by analysis name (case-insensitive).
  - **Type pills:** All / Medicare / Long Term Care — filters `recommendation.type`.
  - **Sort:** 6 options — Newest First, Oldest First, Name A–Z, Name Z–A, Highest Cost, Lowest Cost.
  - **Pagination:** Configurable page size (6/12/24); Prev/Next page buttons with page indicator.
- **Compare Basket:**
  - Each card shows an **Add to Compare** / **Remove** toggle button.
  - Ribbon appears above the cards when ≥1 item is in the basket. At 2 items, **Compare Now** button navigates to `/saved/compare`.
  - Compare is type-aware — Medicare analyses and Long Term Care analyses are compared separately.
- **Card Layout (two-column bottom section):** Row 1: status icon + analysis name (uppercase) + type/status badges. Row 2: saved date. Bottom section uses a flex row: left column holds stats (drug count, plan count, lifetime total for Medicare; health profile, care type chips for LTC) and selected plan chips; right column holds stacked "View details" and "Compare" action buttons. Cards use `items-stretch` grid with `flex-col` for equal height across rows.
- **Themed Background:** Page background uses `bg-[var(--app-bg)]` CSS variable to respect the active theme (Navy & Gold, Lavender Calm, or Teal Medical).
- **Empty State:** Shows "No saved analyses" guidance when the full list is empty; "No results" when filters return nothing.
- **Back button:** Navigates to `/medicare-analysis`.
- **Pattern:** Standalone, OnPush.

### `RecommendationCompareComponent` (`recommendation/compare/recommendation-compare.component.ts`, `.html`)
- **Role:** Orchestrator for side-by-side comparison of two saved analyses. Routed at `/saved/compare?ids=id1,id2`.
- **State:** Reads `ids` from query params, `forkJoin` loads both full `RecommendationResponse` records. Determines comparison `mode` — `'medicare'` (both Medicare), `'longterm'` (both LTC), or `'cross'` (mixed types). Signals: `left`, `right`, `loading`, `error`.
- **Illustration Aliasing:** Left recommendation is aliased as **Illustration A** (orange color) and right as **Illustration B** (green color) — centralized via `LABEL_A` / `LABEL_B` constants from `compare-helpers.ts`. The header card shows the recommendation's actual name as the primary title with the illustration alias displayed below in the corresponding color.
- **Hero Header:** 3-column grid — Left rec card (name, Illustration A alias in orange, date, type badge), VS badge (mode label), Right rec card (name, Illustration B alias in green, date, type badge).
- **Mode Dispatch:** `@switch(mode())` delegates to `CompareMedicareComponent`, `CompareLtcComponent`, or `CompareCrossComponent`.
- **Pattern:** Standalone, OnPush.

### `CompareMedicareComponent` (`recommendation/compare/medicare/compare-medicare.component.ts`, `.html`, `.scss`)
- **Role:** Medicare-vs-Medicare comparison — pure tab shell, no logic.
- **Inputs:** `left`, `right` (`RecommendationResponse`).
- **SCSS:** `compare-medicare.component.scss` — active tab styling with primary color (`--color-cyan-600`) background, white text/icon, rounded top corners.
- **Children:**
  - `CompareMedicareMetricsComponent` — unified metrics grid (above tabs).
  - **4 Tabs** (lazy via `matTabContent`): Overview (`TabOverviewComponent`), Profile (`TabProfileComponent`), Rx, Pharmacy & Plans (`TabRxPharmacyPlansComponent`), Cost Analysis (`TabCostAnalysisComponent`).
- **Pattern:** Standalone, OnPush.

### Compare Metrics Components (Extracted)
Three per-type metrics components render KPI cards above the comparison tabs. All share the same unified single-grid template pattern via an `allMetrics()` computed signal that merges cost and profile metrics into one `grid-cols-2 md:grid-cols-3` grid:

- **`CompareMedicareMetricsComponent`** (`medicare/compare-medicare-metrics.component.ts`) — Cost metrics: Lifetime Cost, Present Value, IRMAA Surcharge, Avg Annual Cost (from `lastCostSnapshot`). Profile metrics: Coverage Years, ZIP Code.
- **`CompareLtcMetricsComponent`** (`ltc/compare-ltc-metrics.component.ts`) — Cost metrics: Lifetime Cost, Present Value, Avg Annual Cost (from `ltcSnapshot`). Profile metrics: Projection Years, ZIP Code.
- **`CompareCrossMetricsComponent`** (`cross/compare-cross-metrics.component.ts`) — Cross-type aware — dispatches to LTC or Medicare snapshot per side. Cost metrics: Lifetime Cost, Present Value, Avg Annual Cost. Profile metrics: ZIP Code.

All metrics components use `LABEL_A` / `LABEL_B` with orange/green color coding for elderly-friendly readability.

### Medicare Tab Sub-Components
Five standalone tab components extracted from the compare shell:

- **`TabOverviewComponent`** (`medicare/tab-overview/`) — 6 KPI delta cards, winner banner, profile differences, prescriptions summary, pharmacy comparison, plans comparison, projection summary + trajectory cards.
- **`TabCostAnalysisComponent`** (`medicare/tab-cost-analysis/`) — Chart.js line + bar charts using `CHART_COLOR_A` (orange) / `CHART_COLOR_B` (green), year-by-year delta table, category comparison, assessment cards.
- **`TabRxPharmacyPlansComponent`** (`medicare/tab-rx-pharmacy-plans/`) — Side-by-side prescription drug cards, pharmacy comparison cards, plan cards with star ratings.
- **`TabProfileComponent`** (`compare/tab-profile/`) — 4 grouped sections (Personal, Location, Health, Financial) with match column. Shared across all comparison modes.

### `CompareLtcComponent` (`recommendation/compare/ltc/compare-ltc.component.ts`, `.html`, `.scss`)
- **Role:** LTC-vs-LTC comparison — 4-tab deep dive.
- **Inputs:** `left`, `right` (`RecommendationResponse`).
- **SCSS:** `compare-ltc.component.scss` — active tab styling with primary color (`--color-cyan-600`) background, white text/icon, rounded top corners.
- **Children:** `CompareLtcMetricsComponent` (above tabs), `TabProfileComponent` (shared).
- **Computed Signals:** `costDelta`, `pvDelta`, `avgAnnualDelta`, `winner`, `winnerName`, `winnerSavings`, `profileRows`, `profileDiffs`, `careConfigRows` (Health Profile, Adult Day Years, Home Care Years, Nursing Care Years), `careConfigDiffs`.
- **4 Tabs:**
  1. **Overview** — 4 KPI delta cards (Total Cost, Present Value, Avg Annual, Projection Years), green gradient winner banner, profile differences table with count badge, care config summary grid cards with match indicators, trajectory comparison side-by-side.
  2. **Profile** — 4 grouped sections (Personal, Location, Health, Financial) with colored icon headers, match column (check/warning icons).
  3. **Care Config** — Config table with match column, side-by-side cost total cards (orange left / green right) showing Total Cost + Present Value.
  4. **Cost Analysis** — Category comparison with progress bars and trend badges, savings recommendations with priority pills, side-by-side overall assessment cards with colored left borders.
- **Color Coding:** All labels and section headers use orange for Illustration A / green for Illustration B.
- **Pattern:** Standalone, OnPush.

### `CompareCrossComponent` (`recommendation/compare/cross/compare-cross.component.ts`, `.html`, `.scss`)
- **Role:** Medicare-vs-LTC cross-type comparison — 3-tab layout.
- **Inputs:** `left`, `right` (`RecommendationResponse`).
- **SCSS:** `compare-cross.component.scss` — active tab styling with primary color (`--color-cyan-600`) background, white text/icon, rounded top corners.
- **Children:** `CompareCrossMetricsComponent` (above tabs), `TabProfileComponent` (shared).
- **Computed Signals:** `leftType`, `rightType` (inferred `RecommendationCategory`), `leftLifetime`, `rightLifetime` (via `lifetimeCost()` helper), `profileRows`, `profileDiffs`, `costDelta`, `deltaIcon`, `deltaLabel`.
- **3 Tabs:**
  1. **Overview** — Amber cross-type warning banner, 3-card KPI strip (left cost with type badge, right cost with type badge, cost difference delta), green gradient winner banner (uses `cheaperLabel` ternary for Illustration A/B), profile differences table.
  2. **Profile** — 4 grouped sections (Personal, Location, Health, Financial) with colored icon headers, match column.
  3. **Cost Summary** — Side-by-side evaluation cards with type badges, trajectory indicators from `evaluation` sub-object, assessment cards with colored left borders, blue info note explaining cross-type comparison caveats.
- **Color Coding:** All labels and section headers use orange for Illustration A / green for Illustration B.
- **Pattern:** Standalone, OnPush.

### `compare-helpers.ts` — Shared Comparison Utilities
Centralized constants, color palette, and helper functions used across all comparison components:

**Constants:**
| Constant | Value | Purpose |
|---|---|---|
| `LABEL_A` | `'Illustration A'` | Left-side alias (replaces raw recommendation name) |
| `LABEL_B` | `'Illustration B'` | Right-side alias |
| `CHART_COLOR_A` | `'#c2410c'` (orange-700) | Chart border color for left series |
| `CHART_COLOR_A_BG` | `'rgba(234, 88, 12, 0.08)'` | Chart fill background for left series |
| `CHART_COLOR_A_FILL` | `'rgba(234, 88, 12, 0.7)'` | Bar fill color for left series |
| `CHART_COLOR_B` | `'#15803d'` (green-700) | Chart border color for right series |
| `CHART_COLOR_B_BG` | `'rgba(22, 163, 74, 0.08)'` | Chart fill background for right series |
| `CHART_COLOR_B_FILL` | `'rgba(22, 163, 74, 0.7)'` | Bar fill color for right series |

**Functions:** `deltaClass`, `deltaIcon`, `deltaLabel`, `getTrajectoryIcon`, `getTrajectoryColor`, `getPriorityColor`, `starArray`, `typeBadgeClass`, `typeLabel`, `buildProfileRows` (returns `ProfileRow[]` grouped by `personal | location | health | financial`, with inline label formatters).

**Design — Elderly Accessibility:** Orange/green palette chosen for high-contrast WCAG AA compliance (6:1+ ratio). All text labels use `text-orange-700` / `text-green-700` shades. Chart colors use darker orange (`#c2410c`) and green (`#15803d`) for distinguishability by users with color vision deficiency.

### `RecommendationDetailComponent` (`recommendation/recommendation-detail.component.ts`, `.html`, `.scss`)
- **Role:** Full detail view for a single saved recommendation. Routed at `/saved/:id`.
- **State:** Injects `RecommendationService`, `ActivatedRoute`, `Router`. Loads recommendation via `id` route param.
- **Chart.js:** Manually registers all chart controllers (LineController, BarController, DoughnutController) — same setup as `CostProjectionsComponent`.
- **Design:** Matches the compare page design language:
  - **Header:** Flat flex row matching compare page — back button, analysis name (`text-xl font-bold text-gray-900`), type badge pill (cyan for Medicare, violet for LTC), save date (`text-sm text-gray-500`). Themed page background via `bg-[var(--app-bg)]`.
  - **Medicare KPI Strip:** 6 cards above tabs (Lifetime, Premiums, OOP, IRMAA, Present Value, Current Year).
  - **Medicare Tabs (3):** Active-tab primary color styling via `rec-detail-medicare.component.scss` (same `--color-cyan-600` pattern as compare).**
    1. **Profile** — 3 grouped section cards (Personal, Location, Health & Financial) with colored icons and human-readable labels.
    2. **Prescriptions** — Drug count pill + clean HTML table (drug name, dosage, quantity, refill).
    3. **Pharmacy** — Storefront-style cards with type badge, phone/distance/NPI icons, mail-order card.
    4. **Plans** — Card-per-plan with colored type headers, 6-metric grid (Monthly Premium, Deductible, Star Rating, Rx Coverage, Rx Cost, Total Cost), visual star ratings, unavailable drug chips.
    5. **Cost & Charts** — Trajectory banner, all Chart.js charts in card containers (line, stacked bar, doughnut, projection), Medicare Expense Table, summary strip.
  - **LTC Tabs (2):** Active-tab primary color styling via `rec-detail-ltc.component.scss`. Profile, Cost Analysis (care config card, trajectory, categories, tips, assessment).
- **Helper Methods:** `fmtGender()`, `fmtHealth()`, `fmtTaxFiling()`, `starArray()` — format raw data values to human-readable labels.
- **Computed Getters:** `recBundleLabel`, `recExpenseTableRow`, `recPresentValue`, `recCoverageYear`, `recPlanSpecificLifetimeExpense`, `recTotalIrmaaSurcharge` — sourced from `rec.lastCostSnapshot`.
- **Imports:** `CommonModule`, `CurrencyPipe`, `DatePipe`, `DecimalPipe`, `MatTabsModule`, `MatCardModule`, `MatIconModule`, `MatButtonModule`, `MatProgressSpinnerModule`, `MatTooltipModule`.
- **Pattern:** Standalone, OnPush.

### `LtcShellComponent` (`long-term-care/ltc-shell.component.ts`, `.html`, `.scss`)
- **Role:** Parent wizard shell for the Long Term Care (LTC) cost projection flow. Routed at `/long-term-care`, guarded by `profileCompleteGuard`.
- **Layout:** Vertical flex — step indicator (top, 3 steps: Profile → Care Type → Projection), `<router-outlet>` (scrollable middle), Back/Calculate navigation bar (bottom).
- **Step Indicator:** 3 numbered badges (1·Profile, 2·Care Type, 3·Projection) with icons. Navigation computed from `LtcStateService.currentStep`.
- **`isProjectionRoute`:** Computed signal from `NavigationEnd` events — detects when user is on the Projection step to show/hide the "Calculate" button vs "Continue".
- **Back/Continue Logic:** `canGoBack` returns false on step 1 (Profile). Continue from step 2 navigates to `/long-term-care/projection`. On the Projection step, the primary action button triggers `calculateLtc()`.
- **`calculateLtc()`:** Builds `LtcProjectionRequest` from `LtcStateService` signals (healthProfile, adultDayYears, homeCareYears, nursingCareYears) and `ProfileService` profile data (DOB, gender, state, zip, countyCode, lifeExpectancy, tobaccoStatus). Calls `LtcService.calculate(request)`, stores result in `LtcStateService.ltcResult`. Sets `isCallingApi` during loading. Routes to `/long-term-care/projection` on success.
- **State:** Injects `LtcStateService`, `ProfileService`, `LtcService`, `ReferenceDataService`, `Router`.
- **Imports:** `RouterOutlet`, `MatIconModule`, `MatButtonModule`, `MatProgressSpinnerModule`.

### `LtcCareTypeStepComponent` (`long-term-care/care-type-step/`, `.ts`, `.html`)
- **Role:** LTC wizard step 2 — user selects quality of care level (health profile) and number of years for each care type (Adult Day Health Care, In-Home Care, Nursing Care).
- **State:** Reads/writes `LtcStateService` signals: `healthProfile` (1–5 quality of care scale), `adultDayYears`, `homeCareYears`, `nursingCareYears`.
- **OnInit:** Sets `LtcStateService.currentStep` to `2`.

### `LtcProjectionStepComponent` (`long-term-care/projection-step/`, `.ts`, `.html`)
- **Role:** LTC wizard step 3 — displays `LtcProjectionResponse` results from the Financial Planner LTC API.
- **State:** Reads `LtcStateService.ltcResult` (signal). Shows loading spinner while `LtcStateService.isCallingApi` is true.
- **OnInit:** Sets `LtcStateService.currentStep` to `3`.
- **Features:** Renders total LTC cost by category (Adult Day Health Care, In-Home Care, Assisted Care, Nursing Care) with present value figures. Shows year-by-year expense lists for each care type.

### `LtcStateService` (`long-term-care/ltc-state.service.ts`)
- **Role:** Signal-based state for the LTC wizard. `providedIn: 'root'`.
- **Signals:** `currentStep` (`1 | 2 | 3`), `healthProfile` (1–5 quality of care), `adultDayYears`, `homeCareYears`, `nursingCareYears`, `isCallingApi`, `ltcResult` (`LtcProjectionResponse | null`).

### `LtcService` (`long-term-care/ltc.service.ts`)
- **Role:** HTTP service for the LTC cost projection API.
- **Methods:** `calculate(request: LtcProjectionRequest)` → `Observable<LtcProjectionResponse>` — `POST /api/long-term-care`.

---

← [Medicare Analysis Components](ch02-04-components-medicare.md) | [Chapter 2 — Frontend Architecture (Index)](../ch02-frontend-architecture/ch02-frontend-architecture.md) | [Next → Services](ch02-06-services.md)
