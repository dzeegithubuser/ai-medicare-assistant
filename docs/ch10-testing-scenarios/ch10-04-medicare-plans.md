# Chapter 10.4 — Medicare Analysis: Plans

> Route: `/medicare-analysis/plans`
> Covers plan recommendations (Part D, Medicare Advantage, Medigap), chat-based plan selection, plan section chooser, and Part D gap fill.

---

## 5b. Medicare Plan Recommendation (FP Flow)

> **Note:** Plan recommendations are now loaded via `PlanRecommendationComponent` (not the old `PlanRecommendationComponent`). The user selects PDP or MA section, then plans are loaded for the chosen section. The old "Load Medicare Plan Recommendations" single-button flow is superseded.

| # | Scenario | Input | Expected Result |
|---|----------|-------|-----------------|
| 5b.1 | Plans loaded (PDP) | Navigate to plans step, click "Part D (PDP) + Medigap" section | Part D plans loaded for user's confirmed drugs + pharmacies. Plan cards with cost grids displayed. |
| 5b.2 | Plans loaded (MA) | Navigate to plans step, click "Medicare Advantage" section | MA plans loaded. Plan cards displayed. |
| 5b.3 | LIS Full | Income $20,000, household size 1 | Amber LIS banner. Copays reduced to $0. |
| 5b.4 | LIS Partial | Income $30,000, household size 1 | Amber LIS banner. Copays $4.50/$11.20. |
| 5b.5 | LIS None | Income $55,000, household size 1 | No LIS banner. Standard copays. |
| 5b.6 | Drug not covered | Specialty drug | Red "Not Covered" chip. Plan ranked lower. |
| 5b.7 | Prior auth | Drug like Eliquis | Amber "PA" tag. |
| 5b.8 | Select plan | Click "Select This Plan" on a Part D plan | Plan stored in `selectedPartDPlan` state. System message: "Selected Part D plan: {name}". |
| 5b.8a | Summary panel appears | Select any MA or Part D plan | `SelectedPlansSummaryComponent` appears immediately below the plan list showing the selected plan's name and cost. |
| 5b.8b | Calculate button disabled — MA no gap | Select an MA plan that has no Part D coverage and no gap Part D selected | Summary panel visible. Calculate button is disabled (opacity-50, cursor-not-allowed). Amber hint: "Select a Part D gap plan below to calculate your total cost." |
| 5b.8c | Calculate button enabled — MA with Part D | Select MA plan that includes Part D coverage | Calculate button enabled. No amber hint. |
| 5b.8d | Calculate button enabled — MA + gap Part D | Select MA plan + gap Part D plan | Calculate button enabled. No amber hint. |
| 5b.9 | Profile incomplete | Access plans step with missing income/address | `profileCompleteGuard` redirects to `/profile` before reaching plans. |
| 5b.10 | AI failure | Timeout | Fallback message with Medicare.gov link. |
| 5b.11 | Quick LIS check | LIS tier determination logic | Returns `{ lisEligible, lisTier }` based on profile income/assets. |
| 5b.12 | Star rating | Ratings 3.0–5.0 | Full + half stars rendered. Tooltip shows rating. |
| 5b.13 | Plan Features toggle | Click "Plan Features" button | Drug coverage table + AI explanation expand. Click again to collapse. |
| 5b.14 | Cost Breakup toggle | Click "Cost Breakup (N)" button | Per-pharmacy cost breakdown cards expand. Badge shows pharmacy count. |
| 5b.15 | No pharmacy selected | Step 2 not completed (no pharmacies) | Continue button disabled. Cannot reach plans step. |
| 5b.16 | Cost breakdown preferred | Select CVS (chain) | CVS entry shows `isPreferredPharmacy: true`, ~20% lower copays, preferred badge. |
| 5b.17 | Multiple pharmacy costs | 3 pharmacies selected in step 2 | Each plan card shows 3 costBreakdown entries sorted cheapest-first. |
| 5b.18 | Calculate Lifetime Cost — dialog opens | Click "Calculate Lifetime Cost" in the Selected Plans summary panel (button enabled) | `SavePrescriptionDialogComponent` opens with title "Name this recommendation", pre-populated name `"{FirstName} Medicare Advantage – MM/DD/YYYY"` (e.g. `"John Medicare Advantage – 04/18/2026"`) or `"{FirstName} Part D + Medigap – MM/DD/YYYY"` for PDP section. |
| 5b.18a | Dialog — pre-populated name | Dialog opens when logged-in user has first name "John" and section is MA | Name field shows `"John Medicare Advantage – 04/18/2026"`. Field is editable. |
| 5b.18b | Dialog — name fallback | Profile not loaded / no first name | Name field shows `"Medicare Advantage – 04/18/2026"` (no prefix). |
| 5b.18c | Dialog — cancel | Click Cancel in name dialog | Dialog closes. No API calls. No navigation. |
| 5b.18d | Dialog — empty name | Clear name field in dialog | Save button is disabled. |
| 5b.18e | Save flow — full sequence | Enter name → click Save | 1) `POST /api/plan-recommendation/evaluate-costs` called. 2) `saveCurrentPlans` saves selections to `userAnalysisSelections`. 3) `AnalysisSnapshotService.save(name)` saves full recommendation to `recommendations` collection. 4) Chat: `"Plan recommendation \"[name]\" was saved to your account."`. 5) Navigate to `/medicare-analysis/cost-projections`. |
| 5b.18f | Save flow — 409 conflict | Name already exists in recommendations | Auto-retries with `force: true`. Saves successfully. Chat confirms "saved (updated existing)". Navigates to cost-projections. |
| 5b.18g | Save flow — API error | Recommendation save returns 500 | Chat message: "Could not save your plan recommendation. Please try again from the chat." Navigates to cost-projections. |
| 5b.19 | Section switcher | Click "Switch to Medicare Advantage" while viewing PDP | Section switches. Warning dialog if plan already selected. |

### Backend: Plan Recommendation API Endpoints

> The frontend plan sections call three distinct backend endpoints. These scenarios verify the API contract independent of the UI.

| # | Scenario | Request | Expected Result |
|---|----------|---------|-----------------|
| 5b.20 | POST /api/PartDPlan/recommend — success | Valid `PartDPlanRecommendationRequest` with JWT | 200 OK. Returns Part D plan list with copays, formulary coverage, star ratings, and per-pharmacy cost breakdowns. |
| 5b.21 | POST /api/PartDPlan/recommend — unauthorized | Request without JWT | 401 Unauthorized. |
| 5b.22 | POST /api/PartDPlan/recommend — missing county | Request with missing `countyCode` | 400 Bad Request or validation error. |
| 5b.23 | POST /api/MedicareAdvantagePlan/recommend — success | Valid `MedicareAdvantagePlanRequest` with JWT | 200 OK. Returns Medicare Advantage plan list with premiums, OOP maximums, and drug coverage. |
| 5b.24 | POST /api/MedicareAdvantagePlan/recommend — unauthorized | Request without JWT | 401 Unauthorized. |
| 5b.25 | POST /api/MedigapPlan/quotes — success | Valid `MedigapPlanQuotesRequest` with JWT | 200 OK. Returns `MedigapPlanQuotesResponse` with Medigap plan options, plan types (A, B, C, D, F, G, K, L, M, N), and monthly premiums. |
| 5b.26 | POST /api/MedigapPlan/quotes — unauthorized | Request without JWT | 401 Unauthorized. |
| 5b.27 | API timeout — Part D | API unreachable | Error propagated. 500 or timeout response. No unhandled crash. |
| 5b.28 | API timeout — MA | API unreachable | Error propagated. 500 or timeout response. |
| 5b.29 | API timeout — Medigap | API unreachable | Error propagated. 500 or timeout response. |

---

## 12g. Plan Section Chooser

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 12g.1 | No default section on landing | Navigate to `/medicare-analysis/plans` (first time or no active section) | Two large buttons displayed: "Part D (PDP) + Medigap" and "Medicare Advantage". No plan section visible. |
| 12g.2 | Select PDP section | Click PDP button on section chooser | `activeSection` set to `partd`. Part D + Medigap section displayed full-width. PDP button hidden, "Switch to Medicare Advantage" link visible. |
| 12g.3 | Select MA section | Click MA button on section chooser | `activeSection` set to `ma`. Medicare Advantage section displayed full-width. MA button hidden, "Switch to Part D" link visible. |
| 12g.4 | Switch with no plan selected | Viewing PDP section (no plan selected) → click "Switch to Medicare Advantage" | Immediately switches to MA section. No warning dialog. |
| 12g.5 | Switch with plan selected — confirm | Viewing PDP section (plan selected) → click "Switch to MA" → confirm dialog | Warning dialog appears. On confirm: section switched to MA. |
| 12g.6 | Switch with plan selected — cancel | Viewing PDP section (plan selected) → click "Switch to MA" → cancel dialog | Warning dialog dismissed. Section stays on PDP. |
| 12g.7 | Switch via chat intent | Chat "show me Medicare Advantage plans" | Intent: `SWITCH_TO_MA`. `activeSection` set to `ma`. Navigated to `/medicare-analysis/plans`. |
| 12g.8 | Section persisted in session | Select PDP → refresh page | `activeSection` restored as `partd` from sessionStorage. PDP section displayed. |

---

## 17. Chat-Based Plan Selection

| # | Scenario | Input | Expected Result |
|---|----------|-------|-----------------|
| PS.1 | Select Part D plan | `"select the SilverScript plan"` (on plans page) | AI extracts plan name. Plan selected in state. System message confirms selection. |
| PS.2 | Select Medigap plan | `"choose Plan G from Mutual of Omaha"` (on plans page) | AI extracts plan name + type. Medigap plan selected. System message confirms. |
| PS.3 | Select MA plan | `"I want the Humana Gold Plus plan"` (on plans page) | AI extracts MA plan. Plan selected in state. System message confirms. |
| PS.4 | Remove plan selection | `"remove the selected Part D plan"` | AI extracts remove action. Confirmation prompt: "Remove SilverScript? (yes/no)". |
| PS.5 | Confirm plan removal | `"yes"` after removal prompt | Plan deselected. Assistant confirms removal. |
| PS.6 | Cancel plan removal | `"no"` after removal prompt | Assistant: "Removal cancelled." Plan stays selected. |
| PS.7 | Switch section via plan chat | `"show me the Medicare Advantage plans"` | AI extracts switch_section action. `activeSection` set to `ma`. Section switches. |
| PS.8 | Fuzzy plan name matching | `"select silverscript"` (partial name) | AI matches to full plan name "SilverScript Choice". Plan selected. |
| PS.9 | No plans loaded | `"select a plan"` (no plans on page) | Assistant: "Please load plan recommendations first." |
| PS.10 | Not on plans page | `"select SilverScript"` (on drugs page) | Routed through intent classifier instead (not plan selection flow). |

### Backend: POST /api/chat/extract-plan-selection

| # | Scenario | Request | Expected Result |
|---|----------|---------|-----------------|
| PS.11 | Valid extraction | `{ "message": "select SilverScript", "availablePlans": [...] }` | 200 OK. `{ "planName": "SilverScript Choice", "action": "select", "reply": "..." }`. |
| PS.12 | Unauthorized | `POST /api/chat/extract-plan-selection` without JWT | 401 Unauthorized. |
| PS.13 | AI timeout | Anthropic API slow | Fallback response with error message. |

---

## 23. Part D Gap Fill for MA Plans

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 23.1 | MA pre-population triggers Part D gap | Save recommendation with MA plan → load it from saved page → navigate to plans | Part D gap section loads and displays PDP gap plans for the pre-populated MA plan. |
| 23.2 | MA already in list | Save rec with MA plan → reload plans page → reconcile finds MA in existing list | `ensurePartDGapLoadForMA()` fires, Part D gap section populated. |
| 23.3 | MA matched by planId | Save rec with MA plan → navigate away and back | MA plan matched by `planId`, Part D gap section populated. |
| 23.4 | PDP selection — no gap fill | Select a PDP plan (not MA) | Part D gap section does NOT load (only triggered for MA plans). |

---

← [Testing Index](../ch10-testing-scenarios/ch10-testing-scenarios.md)
