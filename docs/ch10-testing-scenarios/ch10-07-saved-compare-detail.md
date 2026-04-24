# Chapter 10.7 — Saved Data, Detail & Compare

> Routes: `/saved`, `/saved/:id`, `/saved/compare`
> Covers saved data page, recommendation detail view, compare views, and uppercase naming.

---

## 16. Saved Data Page

### Page Layout & Navigation

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| SD.1 | Navigate via header button | Click folder_open icon in dashboard header | Navigated to `/saved`. RecommendationComponent loads. |
| SD.2 | Navigate via dropdown menu | Click user menu → "Saved Data" | Navigated to `/saved`. |
| SD.3 | Navigate via chat | Type `"show my saved analyses"` | Intent: `NAVIGATE_SAVED_ANALYSES`. Navigated to `/saved`. |
| SD.4 | Navigate via chat (saved analyses) | Type `"show my saved analyses"` | Intent: `NAVIGATE_SAVED_ANALYSES`. Navigated to `/saved`. |
| SD.5 | Back button | Click back button on saved data page | Navigated to `/medicare-analysis` (redirects to **`/medicare-analysis/profile`**). |
| SD.6 | Header layout | Load `/saved` page | Header with folder_open icon, "Saved Data" title, and back button. |

### Analyses List

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| SD.12 | Analyses loaded | Navigate to `/saved` with saved analyses | Analysis list shows cards with name, date, counts, and cost info. Themed background via `bg-[var(--app-bg)]`. |
| SD.13 | No tab count badge | 2 saved analyses | Page shows list directly (no tabs/badges). |
| SD.14 | Completed analysis card | Analysis with cost snapshot | Green status badge "completed". Lifetime total displayed. `hasCostSnapshot` indicator shown. |
| SD.15 | In-progress analysis card | Analysis without cost snapshot | Amber status badge "in-progress". No lifetime total. |
| SD.16 | Drug and plan counts | Analysis with 3 drugs, 2 plans | Card shows "3 drugs" and "2 plans" in left stats column. |
| SD.17 | Empty analyses | No saved analyses | Empty state message displayed on the page. |
| SD.18 | Loading state | Page loading | Loading spinner shown during API calls. |
| SD.19a | Two-column card layout | Load card with stats and plans | Bottom section has flex row: left column with stats + plan chips, right column with stacked "View details" and "Compare" buttons. |
| SD.19b | Equal-height cards | Load 2+ cards in same row | All cards in the same grid row stretch to equal height via `items-stretch` + `flex-col`. |

### Backend: GET /api/recommendation/all

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| SD.19 | Returns all for user | `GET /api/recommendation/all` with JWT | Returns array of `RecommendationSummaryResponse` sorted by CreatedAt desc. |
| SD.20 | Empty result | User with no saved analyses | Returns empty array `[]`. |
| SD.21 | Unauthorized | `GET /api/recommendation/all` without JWT | 401 Unauthorized. |
| SD.22 | Multiple users isolated | User A saves → User B queries | User B sees only their own analyses, not User A's. |
| SD.23 | Summary fields correct | Save analysis with 3 drugs, 2 plans, $485k lifetime | `drugCount: 3`, `planCount: 2`, `lifetimeTotal: 485000`, `hasCostSnapshot: true`. |

### Saved Recommendation Detail View

> `RecommendationDetailComponent` loaded at `/saved/:id`. 5-tab layout with Chart.js cost charts.

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| SD.30 | Navigate to detail view | Click a "View Details" / card action on `/saved` | Navigated to `/saved/:id`. `RecommendationDetailComponent` loads. |
| SD.31 | Detail loads recommendation | Land on `/saved/:id` | `RecommendationService.getById(id)` called with route param. Full recommendation data loaded. |
| SD.32 | 5-tab layout rendered | Recommendation detail loaded | 5 tabs visible: Profile, Drugs, Pharmacy, Plans, Cost. |
| SD.33 | Profile tab | Click "Profile" tab | Profile snapshot displayed: name, DOB, gender, zipcode, county, state, coverage year, LIS tier, health condition. |
| SD.34 | Drugs tab | Click "Drugs" tab | Confirmed drug list displayed with dosage, formulation, and pharmacy selections for each drug. |
| SD.35 | Pharmacy tab | Click "Pharmacy" tab | Selected pharmacies displayed: name, address, distance for each. |
| SD.36 | Plans tab | Click "Plans" tab | Selected plan(s) shown with deductible, star rating, total prescription cost; Medigap plan type (if applicable); MA plan (if applicable). |
| SD.37 | Cost tab — charts | Click "Cost" tab (`COST_TAB_INDEX = 4`) | Chart.js charts rendered showing year-by-year cost projection. Charts registered manually (ChartJS.register). |
| SD.38 | Cost tab — lifetime totals | Cost tab loaded | Lifetime total, current-year total, average annual premium vs. OOP breakdown shown from `costSnapshot`. |
| SD.39 | Cost tab — no snapshot | Recommendation without `costSnapshot` → navigate to `/saved/:id` | Cost tab shows empty state or "No cost data available" message. No Chart.js error. |
| SD.40 | Back navigation | Click back button | Navigated back to `/saved` list. |
| SD.41 | Invalid ID | Navigate to `/saved/nonexistent-id` | Error handled gracefully. User redirected to `/saved` or error state shown. |
| SD.42 | Unauthorized access | Access `/saved/:id` without JWT | Redirected to `/signin`. |

### Backend: GET /api/recommendation/:id

| # | Scenario | Request | Expected Result |
|---|----------|---------|-----------------|
| SD.43 | Returns recommendation by ID | `GET /api/recommendation/{id}` with valid JWT (owner) | 200 OK. Full `RecommendationDetailResponse` including profile snapshot, drugs, pharmacies, plans, costSnapshot. |
| SD.44 | Not found | `GET /api/recommendation/{id}` (id does not exist) | 404 Not Found. |
| SD.45 | Unauthorized | `GET /api/recommendation/{id}` without JWT | 401 Unauthorized. |
| SD.46 | Cross-user isolation | User B requests recommendation owned by User A | 404 or 403. Cannot access another user's recommendation. |

---

## 25. Recommendation Detail — Redesign

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 25.1 | Flat header (matches compare) | Open `/saved/:id` for a Medicare rec | Flat flex header row with back button, analysis name (bold, gray-900), type badge pill ("Medicare" in cyan-100/cyan-700), save date (gray-500). Themed page background via `bg-[var(--app-bg)]`. |
| 25.2 | KPI strip — Medicare | Open Medicare rec with cost snapshot | 6 KPI cards: Lifetime, Premiums, OOP, IRMAA, Present Value, Current Year — all populated from `lastCostSnapshot`. |
| 25.3 | Profile tab grouping | Click Profile tab | 3 grouped sections: Personal (name, DOB, age, gender), Location (state, ZIP, county, FIPS), Health & Financial (health, tobacco, tax filing, MAGI, concierge). |
| 25.4 | Prescriptions tab | Click Prescriptions tab with 3 drugs | Count pill shows "3", HTML table lists drug name, formulation, strength. |
| 25.5 | Pharmacy tab cards | Click Pharmacy tab | Storefront-style cards with type badge, phone, distance, NPI. Mail-order card if present. |
| 25.6 | Plans tab | Click Plans tab with Part D + Medigap | Card-per-plan with colored type headers, 6-metric grid, star ratings, unavailable drug chips. |
| 25.7 | Cost & Charts tab | Click Cost tab | Trajectory banner, 5 Chart.js charts, Medicare Expense Table, summary strip. |
| 25.8 | LTC 3-tab layout | Open `/saved/:id` for an LTC rec | 3 tabs: Profile, Care Config, Cost Analysis. No KPI strip, no Prescriptions/Pharmacy/Plans tabs. |

---

## 26. Compare-LTC — Redesign

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 26.1 | Overview tab KPI strip | Compare 2 LTC recs → Overview tab | `CompareLtcMetricsComponent` renders cost + profile KPI cards above tabs. KPI strip shows delta values with color-coded icons. Winner banner shown. |
| 26.2 | Profile diffs | Overview → profile diffs section | Side-by-side profile rows with green/red highlighting on differing fields. |
| 26.3 | Care config table | Click Care Config tab | Table rows: Health Profile, Adult Day Years, Home Care Years, Nursing Care Years with values for both recs. Cost totals shown below in orange (Illustration A) / green (Illustration B) cards. |
| 26.4 | Cost Analysis tab | Click Cost Analysis tab | Cost categories with progress bars, savings tips, trajectory assessment. |
| 26.5 | Illustration aliasing | Compare 2 LTC recs | All column headers, section labels, and KPI sub-labels show "Illustration A" (orange) and "Illustration B" (green) instead of recommendation names. |

---

## 27. Compare-Cross — Redesign

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 27.1 | Cross-type disclaimer | Compare Medicare vs LTC rec → Overview tab | Yellow disclaimer banner warning about cross-type comparison limitations. |
| 27.2 | KPI strip + winner | Overview tab | `CompareCrossMetricsComponent` renders KPI cards above tabs. Winner card shows savings amount with correct Illustration alias. |
| 27.3 | Profile diffs | Overview → profile section | Side-by-side profile comparison with grouped rows. |
| 27.4 | Cost Summary tab | Click Cost Summary tab | Side-by-side evaluation cards for each rec. Info note about comparison caveats. Orange/green section headers for Illustration A/B. |
| 27.5 | Illustration aliasing | Compare Medicare vs LTC rec | All labels use "Illustration A" (orange) / "Illustration B" (green). Winner banner uses `cheaperLabel` ternary. |

---

## 28. Uppercase Recommendation Names

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 28.1 | Saved list cards | Navigate to `/saved` page | All `rec.name` text on recommendation cards displayed in uppercase. |
| 28.2 | Compare slot 1 | Add rec to compare basket | Name in compare slot 1 displayed in uppercase. |
| 28.3 | Compare slot 2 | Add second rec to compare basket | Name in compare slot 2 displayed in uppercase. |

---

## 29. Illustration Aliasing & Color Coding

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 29.1 | Header card — name + alias | Navigate to `/saved/compare` | Left side shows recommendation name (bold, gray-900) with "Illustration A" below in orange-700. Right side shows name with "Illustration B" below in green-700. |
| 29.2 | Medicare column headers | Compare 2 Medicare recs → any tab | All column headers show "Illustration A" / "Illustration B" in orange/green instead of raw recommendation names. |
| 29.3 | LTC column headers | Compare 2 LTC recs → any tab | Same aliasing as Medicare — Illustration A (orange), Illustration B (green). |
| 29.4 | Cross-type labels | Compare Medicare vs LTC rec → any tab | Illustration A (orange) / Illustration B (green) used consistently with type badges. |
| 29.5 | Metrics KPI sub-labels | Any compare view | KPI metrics cards show "Illustration A" in orange-600 and "Illustration B" in green-600 as sub-labels under cost/profile values. |
| 29.6 | Chart.js line chart colors | Medicare compare → Cost Analysis tab | Line chart uses orange series (`#c2410c`) for Illustration A and green series (`#15803d`) for Illustration B. Legend labels match. |
| 29.7 | Chart.js bar chart colors | Medicare compare → Cost Analysis tab | Bar chart uses orange fill (`rgba(234, 88, 12, 0.7)`) for Illustration A and green fill (`rgba(22, 163, 74, 0.7)`) for Illustration B. |
| 29.8 | Advantage badges | Medicare compare → Cost Analysis → yearly table | Winner column shows orange badge for "Illustration A" or green badge for "Illustration B". |
| 29.9 | Rx, Pharmacy & Plans colors | Medicare compare → Rx, Pharmacy & Plans tab | Rx card headers, pharmacy section labels, and plan column headers use orange for A and green for B. |
| 29.10 | WCAG AA contrast | Inspect color contrast ratio | Orange-700 (`#c2410c`) on white background: ≥6:1 ratio. Green-700 (`#15803d`) on white: ≥6:1 ratio. Passes WCAG AA for normal text. |
| 29.11 | Central constant change | Change `LABEL_A` in `compare-helpers.ts` to "Option 1" | All compare views across all modes update to show "Option 1" — single point of change. |

---

## 30. Compare Sub-Components (Extracted Architecture)

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 30.1 | Medicare metrics — unified grid | Compare 2 Medicare recs | `CompareMedicareMetricsComponent` renders unified `allMetrics()` grid (`grid-cols-2 md:grid-cols-3`) with 4 cost KPI cards (Lifetime, PV, IRMAA, Avg Annual) + 2 profile cards (Coverage Years, ZIP Code). |
| 30.2 | LTC metrics — unified grid | Compare 2 LTC recs | `CompareLtcMetricsComponent` renders unified `allMetrics()` grid with 3 cost KPI cards (Lifetime, PV, Avg Annual) + 2 profile cards (Projection Years, ZIP Code). |
| 30.3 | Cross metrics — unified grid | Compare Medicare vs LTC | `CompareCrossMetricsComponent` renders unified `allMetrics()` grid with cross-type aware KPI cards — dispatches to correct snapshot per side. Profile metrics: ZIP Code only (Coverage Years removed). |
| 30.4 | Tab Overview | Medicare compare → Overview tab | `TabOverviewComponent` renders 6 KPI deltas, winner banner, diffs, Rx, pharmacy, plans, projections. |
| 30.5 | Tab Cost Analysis charts | Medicare compare → Cost Analysis tab | `TabCostAnalysisComponent` renders Chart.js line + bar charts with orange/green colors, destroys charts on destroy. |
| 30.6 | Tab Rx, Pharmacy & Plans | Medicare compare → Rx, Pharmacy & Plans tab | `TabRxPharmacyPlansComponent` shows side-by-side Rx drug cards, pharmacy comparison cards, plan cards with star ratings. |
| 30.7 | Tab Profile (shared) | Any compare → Profile tab | `TabProfileComponent` renders 4 grouped sections with match column. Shared across all 3 compare modes. |
| 30.8 | Active tab styling — Medicare | Click any tab in Medicare compare | Active tab has primary color (`--color-cyan-600`) background, white text/icon, and rounded top corners (from `compare-medicare.component.scss`). |
| 30.9 | Active tab styling — LTC | Click any tab in LTC compare | Same primary-color active tab styling via `compare-ltc.component.scss`. |
| 30.10 | Active tab styling — Cross | Click any tab in Cross compare | Same primary-color active tab styling via `compare-cross.component.scss`. |
| 30.11 | Table column alignment — cost | Any compare → cost-related table | Table uses `table-fixed` with percentage widths: Metric 20%, A 30%, B 30%, Diff 20%. Columns vertically aligned across sections. |
| 30.12 | Table column alignment — profile | Any compare → Profile tab table | Table uses `table-fixed` with percentage widths: Icon 5%, Field 15%, A/B ~33-40% each. |

---

← [Testing Index](../ch10-testing-scenarios/ch10-testing-scenarios.md)
