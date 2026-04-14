# Chapter 10 — Testing Scenarios

> Manual test matrix covering all features. Each section maps to a feature area.

---

## 1. Drug Name Suggestion — Two-Step Search

| # | Scenario | Input | Expected Result |
|---|----------|-------|-----------------|
| 1.0a | Single drug suggestion | `"Eliquis 50mg"` | Suggestion panel: "Eliquis" input → candidates include "Eliquis" (Brand) and "apixaban" (Generic). |
| 1.0b | Multiple drug suggestions | `"Eliquis 5mg, Ibuprofen 800mg, Naproxen 500mg"` | 3 suggestion rows. Each shows input name + candidate chips. |
| 1.0c | Misspelled drug | `"Eliqis 5mg"` | Suggestion with corrected candidate "Eliquis" (confidence < 1.0). |
| 1.0d | Non-drug input | `"hello world"` | Chat message: "No recognizable drug names found." No suggestion panel. |
| 1.0e | Auto-select high confidence | `"Metformin 500mg"` | Single candidate auto-selected (pre-filled cyan chip). |
| 1.0f | Select candidate | Click "apixaban" for Eliquis input | Chip turns cyan. Other chips deselected for that row. |
| 1.0g | Confirm all selected | Select all → click "Confirm & Analyze" | Loading spinner → full analysis results appear. |
| 1.0h | Confirm disabled | Leave one drug unselected | "Confirm & Analyze" button disabled. |
| 1.0i | Cancel suggestions | Click "Cancel" | Suggestion panel removed. Input re-enabled. Assistant message shown. |
| 1.0j | Input disabled during verify | Suggestion panel visible | Input field and send button are disabled. |
| 1.0k | Confirmed names in chat | Confirm selection | User message: "Confirmed drugs: Eliquis, Ibuprofen, Naproxen". |

---

## 1b. Drug Analysis — Backend Pipeline (Backend-Only, Not Used in UI)

> **Note:** The `POST /api/drug/analyze` endpoint and pipeline still exist on the backend, but the UI no longer calls it. Drug analysis in the UI uses the Financial Planner bulk search (`POST /api/FinancialPlannerDrug/search-bulk`). These scenarios are retained for backend API testing only.

| # | Scenario | Input | Expected Result |
|---|----------|-------|-----------------|
| 1.1 | Single valid drug | `"Eliquis 5mg"` | 1 drug card with normalized name "apixaban", brand "Eliquis", dosage forms, formulations with validated strength+packaging+NDC tuples. RxNormId populated. |
| 1.2 | Multiple valid drugs | `"Eliquis 5mg, Metformin 500mg, Lisinopril 10mg"` | 3 drug cards. Each has complete metadata with formulations. Interactions may be detected. |
| 1.3 | Invalid drug (typo) | `"eliq"` | No drug cards. Chat shows "No valid drugs could be identified" message. |
| 1.4 | Mixed valid + invalid | `"Eliquis 5mg, xyzfake"` | Only 1 drug card (Eliquis). Invalid drug filtered out. |
| 1.5 | Non-drug text | `"hello how are you"` | No drug cards. Message about no valid drugs. |
| 1.6 | Drug with dosage | `"Metformin 2000mg"` | 1 drug card. May trigger dosage alert (exceeds typical range of 500–2000mg). |
| 1.7 | Formulations populated | `"Naprosyn"` | Formulations include tablet (250mg, 375mg, 500mg) and suspension (125 mg/5 mL) with correct packaging per form. |
| 1.8 | NDC per formulation | `"Eliquis 5mg"` | Each formulation has a non-empty ndcCode (resolved by RxNorm enrichment, not AI) in XXXXX-XXXX-XX format. |
| 1.9 | Flat arrays from formulations | `"Metformin 500mg"` | `strengths`, `packaging`, `ndcCodes` flat arrays populated from formulations for backward compatibility. |

---

## 2. Drug Interaction Engine (via Bulk Search)

> **Note:** Drug interactions are now detected by the Financial Planner bulk search pipeline (`POST /api/FinancialPlannerDrug/search-bulk`), which calls AI for pairwise interaction evaluation when >1 drug is submitted. The old RxNorm-based interaction merging pipeline is backend-only.

| # | Scenario | Input | Expected Result |
|---|----------|-------|------------------|
| 2.1 | Known high-severity interaction | Confirm "Warfarin" and "Aspirin" in drugs step | Red interaction alert card in `InteractionAlertsComponent`: Warfarin ↔ Aspirin, severity "High", bleeding risk description. |
| 2.2 | Moderate interaction | Confirm "Lisinopril" and "Potassium Chloride" | Amber interaction alert card: hyperkalemia risk. |
| 2.3 | No interactions | Confirm "Metformin" and "Omeprazole" | No interaction alert cards shown. |
| 2.4 | Multiple interactions | Confirm "Warfarin", "Aspirin", "Ibuprofen" | Multiple interaction cards: Warfarin↔Aspirin, Warfarin↔Ibuprofen, Aspirin↔Ibuprofen. |
| 2.5 | Single drug — no interaction check | Confirm only 1 drug | No interaction cards shown (AI interaction evaluation skipped for single drug). |

---

## 3. Dosage Validation (Backend-Only, Not Displayed in UI)

> **Note:** Dosage validation is part of the old `POST /api/drug/analyze` pipeline. The current current UI flow does not display dosage alerts. These scenarios are retained for backend API testing only.

| # | Scenario | Input | Expected Result |
|---|----------|-------|-----------------|
| 3.1 | Normal dosage | `"Metformin 500mg"` | No dosage alert. |
| 3.2 | High dosage | `"Metformin 5000mg"` | Dosage alert: "Exceeds maximum recommended range" (max 2550mg/day). Severity "High". |
| 3.3 | Low dosage | `"Eliquis 0.5mg"` | Dosage alert: below recommended range (2.5–5mg). |

---

## 4. Duplicate Therapy Detection (via Bulk Search)

| # | Scenario | Input | Expected Result |
|---|----------|-------|------------------|
| 4.1 | Duplicate NSAIDs | Confirm "Ibuprofen" and "Naproxen" in drugs step | Orange duplicate therapy card in `DuplicateTherapyAlertsComponent`: "Both are NSAIDs". |
| 4.2 | Duplicate SSRIs | Confirm "Fluoxetine" and "Sertraline" | Duplicate therapy card: both are SSRIs. |
| 4.3 | No duplicates | Confirm "Eliquis" and "Metformin" | No duplicate therapy cards shown. |

---

## 4b. Formulation Cascading & Validation (FP Drugs Step)

> **Note:** Formulation selection is now done via `DrugSelectionPanelComponent` inside `DrugsStepComponent`. The 4-step flow is: Type (Generic/Branded) → Dosage Form → Strength → Quantity/Month. Data comes from the Financial Planner `getDrugDetailAdvance` API.

| # | Scenario | Steps | Expected Result |
|---|----------|-------|------------------|
| 4b.1 | Cascading: type → dosage forms | Confirm "Naprosyn" in drugs step, select "Branded" type | Step 2 shows dosage forms returned by `getDrugDetailAdvance` (e.g., "tablet", "suspension"). |
| 4b.2 | Cascading: dosage form → strengths | Select "Branded" → dosage form "tablet" | Step 3 shows tablet-only strengths (250mg, 375mg, 500mg). Suspension strengths not shown. |
| 4b.3 | Cascading: dosage form → strengths (suspension) | Select "Branded" → dosage form "suspension" | Step 3 shows only "125 mg/5 mL". Tablet strengths not shown. |
| 4b.4 | Cascading: strength → quantity | Select "tablet" → "375 mg" | Step 4 shows quantity/month options for the selected strength. |
| 4b.5 | Reset on dosage form change | Select "tablet" → "375 mg" → change to "suspension" | Strength and quantity selections cleared. New filtered strengths shown. |
| 4b.6 | Reset on strength change | Select "tablet" → "375 mg" → "30/month" → change strength to "250 mg" | Quantity selection cleared. New filtered quantities shown. |
| 4b.7 | Progressive reveal: strength hidden | Only type selected (no dosage form) | Step 3 (Strength) section not visible. |
| 4b.8 | Progressive reveal: quantity hidden | Type + dosage form selected, no strength | Step 4 (Quantity) section not visible. |
| 4b.9 | Generic vs Branded types | Confirm a drug that has both generic and branded | Step 1 shows both "Generic" and "Branded" radio buttons. Selecting each shows different formulation tree. |
| 4b.10 | Single type only | Confirm a drug with only generic available | Step 1 auto-selects "Generic". Only one option shown. |
| 4b.11 | Confirm with full selection | Complete all 4 steps → click "Select Drug" | Confirmed drug summary shows type, form, strength, quantity. Panel collapses. |
| 4b.12 | NDC code resolved | Complete all 4 steps | Selected NDC code appears in confirmed drug summary, matching the exact formulation. |
| 4b.13 | Confirmed drug keeps NDC mapping | Confirm selected formulations | Confirmed drug selection includes the exact formulation NDC mapping used by flow. |
| 4b.14 | drugs page input mirrors chat capability | On `/medicare-analysis/drugs`, enter "Eliquis 5mg, Metformin 500mg" in drugs page search | Suggestion chips appear on page. After Confirm & Analyze, drug details load and formulation cards render. |

---

## 5. Nearby Pharmacy Search & AI-Powered Pricing (Legacy Backend-Only)

> **Note:** The `GET /api/pharmacy/search` and `GET /api/pharmacy/nearby` endpoints still exist on the backend, but the UI no longer uses them. The analysis wizard uses `GET /api/pharmacy/lookup` (Financial Planner pharmacy lookup) via `PharmacyStepComponent`. These scenarios are retained for backend API testing only.

| # | Scenario | Precondition | Expected Result |
|---|----------|--------------|-----------------|
| 5.1 | Pharmacies found | User has zip "90210". Submit drugs, then click "Find Nearby Pharmacies" button | Pharmacies populated. PharmacyListComponent appears. Cheapest has "Best Price" chip. Button hides after load. |
| 5.2 | No zip code | No address profile. Click "Find Nearby Pharmacies" | Empty pharmacies. Panel not shown. Error handled gracefully. |
| 5.3 | Select pharmacy | Click a pharmacy row | Row highlights. Per-drug price grid expands (Retail/Medicare/Generic). |
| 5.4 | Sort toggle | Click sort icon | Re-sorts by name ↔ price. |
| 5.5 | Collapse/expand | Click panel header | Panel toggles. |
| 5.7 | Chat summary | Check chat message after drug analysis | Summary mentions "Use the buttons below the results to load Medicare plan recommendations or find nearby pharmacies." |
| 5.8 | Standalone search | `GET /api/pharmacy/search?zip=90210&drugs=1364430` | Returns pharmacies with pricing. |
| 5.9 | Missing zip | `GET /api/pharmacy/search?drugs=1364430` (no zip) | Returns 400. |
| 5.10 | NPI API timeout | NPI Registry down, click "Find Nearby Pharmacies" | Loading spinner stops. No pharmacy panel. No errors. |
| 5.11 | AI pricing fails | IChatClient timeout | Fallback to ParsePriceString prices. |
| 5.12 | Both pricing fail | AI + ParsePriceString fail | Prices null. UI shows "—". |
| 5.13 | Brand-only drug | `"Eliquis 5mg"` | `genericPrice` null. "—" in Generic column. |
| 5.14 | Generic drug | `"Metformin 500mg"` | All three price columns populated. |
| 5.15 | Multiple drugs | `"Eliquis 5mg, Metformin 500mg"` | Per-drug prices summed into TotalRetailCost. Sorted by total. |
| 5.16 | AI pricing cache | Same drugs+zip twice | Second request instant from 30-day cache. |
| 5.17 | NPI cache | Different drugs, same zip | NPI from 7-day cache. Only AI pricing call made. |

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
| 5b.9 | Profile incomplete | Access plans step with missing income/address | `profileCompleteGuard` redirects to `/profile` before reaching plans. |
| 5b.10 | AI failure | Timeout | Fallback message with Medicare.gov link. |
| 5b.11 | Quick LIS check | `GET /api/plan-recommendation/lis-check` | Returns `{ lisEligible, lisTier }`. |
| 5b.12 | Star rating | Ratings 3.0–5.0 | Full + half stars rendered. Tooltip shows rating. |
| 5b.13 | Plan Features toggle | Click "Plan Features" button | Drug coverage table + AI explanation expand. Click again to collapse. |
| 5b.14 | Cost Breakup toggle | Click "Cost Breakup (N)" button | Per-pharmacy cost breakdown cards expand. Badge shows pharmacy count. |
| 5b.15 | No pharmacy selected | Step 2 not completed (no pharmacies) | Continue button disabled. Cannot reach plans step. |
| 5b.16 | Cost breakdown preferred | Select CVS (chain) | CVS entry shows `isPreferredPharmacy: true`, ~20% lower copays, preferred badge. |
| 5b.17 | Multiple pharmacy costs | 3 pharmacies selected in step 2 | Each plan card shows 3 costBreakdown entries sorted cheapest-first. |
| 5b.18 | Calculate Lifetime Cost | Click "Calculate Lifetime Cost" on a plan card | Calls `POST /api/plan-recommendation/evaluate-costs`. Navigated to `/medicare-analysis/cost-projections`. |
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

## 5d. Pharmacy Selection (FP Lookup)

> **Note:** Pharmacy selection now uses `PharmacyStepComponent` with the Financial Planner `GET /api/pharmacy/lookup` API. The old NPI-based "Find Nearby Pharmacies" flow is no longer used in the UI.

| # | Scenario | Precondition | Expected Result |
|---|----------|--------------|-----------------|
| 5d.1 | Pharmacies auto-load | Navigate to `/medicare-analysis/pharmacies` with profile containing lat/lng | Pharmacies loaded from API. Cards displayed with name, address, distance, zipcode. |
| 5d.2 | Filter by name | Type "CVS" in name filter → click Search | Only pharmacies matching "CVS" shown. |
| 5d.3 | Filter by radius | Change radius dropdown to 10 miles | Pharmacies within 10 miles shown. |
| 5d.4 | Clear filters | Click Clear button | Name cleared, radius reset to 25 mi, page reset to 1. |
| 5d.5 | Pagination | 50+ pharmacies, page size 20 | 3 pages shown. Next/Prev buttons. Page number window. |
| 5d.6 | Select pharmacy | Click pharmacy card checkbox | Pharmacy marked selected (emerald highlight + check). Counter updates ("1/5 selected"). System message posted. |
| 5d.7 | Unselect pharmacy | Click selected pharmacy checkbox | Pharmacy unmarked. Counter decreases. System message: "Deselected pharmacy: {name}". |
| 5d.8 | 5-pharmacy cap | Select 5 pharmacies → attempt 6th | 6th click silently ignored. Counter stays at 5/5. |
| 5d.9 | Selected pharmacies review panel | Select 2+ pharmacies | Emerald summary panel shows selected pharmacies with name, address, distance, remove (×) buttons. |
| 5d.10 | Remove from review panel | Click × on a pharmacy in review panel | Pharmacy deselected. Counter decreases. |
| 5d.11 | Google Maps: Spot on Map | Click map icon on pharmacy card | Opens Google Maps centered on pharmacy address in new tab. |
| 5d.12 | Google Maps: Directions | Click directions icon on pharmacy card | Opens Google Maps directions to pharmacy in new tab. |
| 5d.13 | No lat/lng in profile | Profile without latitude/longitude | Pharmacies request fails. Error handled gracefully. |
| 5d.14 | Page size change | Change per-page dropdown from 20 → 50 | More pharmacies shown per page. Pagination updated. |
| 5d.15 | Empty results | Filter by name "XYZNONEXIST" | No pharmacies found. Empty state suggests adjusting filters. |

---

## 5c. Plan-Aware Pharmacy Search (Legacy Backend-Only)

> **Note:** `PharmacyListComponent` in `planMode` still exists in the codebase but is only rendered in the unreachable "regular" (non-FP) flow path of `PlansStepComponent`. Since the UI always uses the flow, these scenarios are effectively dead. Retained for backend API testing only.

| # | Scenario | Precondition | Expected Result |
|---|----------|--------------|-----------------|
| 5c.1 | Plan pharmacies loaded | Select a plan → click "Find Plan Pharmacies" | Pharmacies shown with copay columns instead of AI-estimated prices. |
| 5c.2 | Preferred network | Plan with preferred pharmacy network | Preferred pharmacies marked with badge. Lower copays for preferred pharmacies. |
| 5c.3 | Non-covered drug | Drug not on plan formulary | Drug row shows "Not Covered" with no copay. |
| 5c.4 | Prior auth required | Drug requires prior authorization | Amber "PA" tag on drug row in pharmacy panel. |
| 5c.5 | Formulary tier display | Select plan with tiered formulary | Tier number (1-5) shown per drug per pharmacy. |
| 5c.6 | No plan selected | Click plan pharmacy search without selecting plan | Button not visible or disabled. |
| 5c.7 | Total plan copay | Multiple drugs with copays | Per-pharmacy total copay aggregated across all drugs. |

---

## 6b. Drug Selection Confirm/Edit/Remove Workflow

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 6b.1 | Confirm drug | Complete all 4 selection steps → click "Select Drug" | Panel collapses. Drug appears in confirmed summary with all selections shown. |
| 6b.2 | Edit confirmed drug | Click "Edit" on confirmed drug | Selection panel re-opens with prior selections preserved. |
| 6b.3 | Remove confirmed drug | Click "Remove" on confirmed drug | Drug removed from confirmed list. Selection panel resets. |
| 6b.4 | Auto-advance | Confirm drug 1 | Panel auto-advances to next unconfirmed drug's selection panel. |
| 6b.5 | Multiple drugs confirmed | Confirm 3 drugs | All 3 appear in SelectedDrugsSummaryComponent list with individual edit/remove. |
| 6b.6 | Confirmed summary actions only | Confirm at least 1 drug | Confirmed summary shows edit/remove controls only (no save-prescription action). |
| 6b.7 | Remove all confirmed drugs | Remove all confirmed drugs | Confirmed summary section empties correctly. |

---

## 6c. Legacy Prescription API (Backend-Only)

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 6c.1 | Save endpoint authorization | Call `POST /api/prescription` without JWT | 401 Unauthorized. |
| 6c.2 | List endpoint authorization | Call `GET /api/prescription` without JWT | 401 Unauthorized. |
| 6c.3 | List prescriptions (legacy) | Call `GET /api/prescription` with JWT | Returns prescription documents ordered by most recent. |
| 6c.4 | Save payload contract (legacy) | Call `POST /api/prescription` with valid payload | Persists prescription with embedded drug fields including `ndcCode`. |

---

## 7. Medicare Cost Data — CMS API (Backend-Only, Not Displayed in UI)

> **Note:** CMS enrichment still runs in the backend during the old `POST /api/drug/analyze` pipeline, but the current current UI flow does not render CMS cost cards for individual drugs. Plan-level cost breakdowns (from the Financial Planner API) are shown instead. These scenarios are retained for backend API testing only.

| # | Scenario | Input | Expected Result |
|---|----------|-------|------------------|
| 7.1 | Drug found | `"Eliquis 5mg"` via `/api/drug/analyze` | CMS data included in API response with source, data year, costs. |
| 7.2 | Drug not found | `"Famciclovir 250mg"` via `/api/drug/analyze` | CMS section empty in response. AI estimates present. |
| 7.3 | CMS timeout | Any drug, CMS slow | Response completes without CMS data. No errors. |

---

## 8. Clinical Intelligence (Backend-Only, Not Displayed in UI)

> **Note:** Clinical intelligence fields (alternatives, generic switch, contraindications, confidence score) are generated by the old `POST /api/drug/analyze` AI pipeline but are not rendered by the current current UI. No drug card components exist. These scenarios are retained for backend API testing only.

| # | Scenario | Input | Expected Result |
|---|----------|-------|------------------|
| 8.1 | Therapeutic alternatives | `"Eliquis 5mg"` via `/api/drug/analyze` | Response includes alternatives: Warfarin, Xarelto. |
| 8.2 | Generic switch | `"Lipitor 20mg"` via `/api/drug/analyze` | Response includes: "Lipitor → Atorvastatin, savings $X/year". |
| 8.3 | Contraindications | `"Eliquis 5mg"` via `/api/drug/analyze` | Response includes contraindications: "Active pathological bleeding", etc. |
| 8.5 | Confidence score | `"Metformin 500mg"` via `/api/drug/analyze` | Response includes confidence score value. |

---

## 9. Authentication & Profile

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 9.1 | Sign up | Fill all fields → submit | JWT returned, redirected to dashboard, profile form shown. |
| 9.2 | Sign in | Valid email + password | JWT returned, redirected to dashboard, then auto-redirected to `/profile` (always). |
| 9.3 | Wrong password | Valid email + wrong password | Error: "Invalid credentials". |
| 9.4 | Profile completion | Complete the profile form and save | Left panel switches to analysis wizard (**`/medicare-analysis/profile`**, Profile as step 1). |
| 9.5 | Analyze without profile | Submit prescription early | Chat: "please complete your profile". Profile auto-opens. |
| 9.6 | Token expiry | Wait for JWT to expire | Redirected to sign-in. |

---

## 10. Error Handling & Edge Cases

| # | Scenario | Trigger | Expected Result |
|---|----------|---------|-----------------|
| 10.1 | Network error | Kill backend → submit | Chat: "Sorry, something went wrong." |
| 10.2 | OpenAI failure | Invalid API key | 500 caught by middleware. Error in chat. |
| 10.3 | RxNorm timeout | RxNorm slow | Old analysis pipeline completes with AI-only data. (Backend-only) |
| 10.4 | CMS timeout | CMS slow | Old analysis pipeline renders without CMS data. (Backend-only) |
| 10.5 | API timeout | Financial Planner API slow | Loading spinner stops. Error message shown to user. Retry available. |
| 10.6 | AI pricing failure | IChatClient fails | Fallback to ParsePriceString prices. |
| 10.7 | Empty prescription | Submit empty text | Nothing happens (ignored). |
| 10.8 | Very long prescription | 20+ drug names | All valid drugs analyzed. Results rendered. |
| 10.9 | Unauthorized access | Access `/` without JWT | Redirected to `/signin`. |

---

## 11. Migration Endpoints

| # | Scenario | Endpoint | Expected Result |
|---|----------|----------|-----------------|
| 11.1 | List applied | `GET /api/migration/applied` | Returns array of applied migration names. |
| 11.2 | List pending | `GET /api/migration/pending` | Returns array of pending migration names (empty if up-to-date). |
| 11.3 | Apply pending | `POST /api/migration/apply` | Applies all pending migrations. Returns success message. |
| 11.4 | No auth required | All migration endpoints without JWT | Returns 200 (AllowAnonymous). |

---

## 12. Chat Intent Routing & Guided Wizard

### 12a. Guided Wizard — Happy Path

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 12a.1 | Startup greeting | Fresh page load, no messages | Assistant message: "Hello! I'm your AI Medicare Assistant..." + two mode selection cards visible. |
| 12a.2 | Long Term Analysis | Click "Long Term Analysis" | LTC wizard starts. User message "Long Term Analysis" added. Assistant announces LTC profile step. Navigated to `/long-term-care/profile` (if profile incomplete) or shows profile review (if complete). |
| 12a.3 | Start Medicare wizard | Click "Medicare Analysis" | User message "Medicare Analysis" added. Wizard starts immediately (no recommendation fetch gate). Mode buttons hidden. |
| 12a.4 | Fresh-state reset on click | Have prior confirmed drugs/pharmacies/plans in state, then click "Medicare Analysis" | Prior carried flow state is cleared (`drugDetails`, `confirmedDrugNames`, selected lookup pharmacies, pharmacy-confirmed flag, plan selections). |
| 12a.5 | Profile auto-skip | Click "Medicare Analysis" (profile already saved) | Skips profile step. Assistant: "profile is all set!" + immediately announces drugs/pharmacy step. |
| 12a.6 | Auto-advance after profile save | Save profile on `/profile` page | Chat auto-posts drugs/pharmacy prompt. Shell stepper shows Profile first (`/medicare-analysis/profile`); user can Continue to Drugs or rely on chat navigation to `/medicare-analysis/drugs`. |
| 12a.7 | Drugs step announced | Profile complete, no drugs confirmed | Assistant: "Please add your prescription drugs..." + navigated to `/medicare-analysis/drugs`. |
| 12a.7b | drugs page drug search available | Land on `/medicare-analysis/drugs` after Medicare Analysis click | Left panel shows direct drug search input with verification + Confirm & Analyze controls (same capability as chat). |
| 12a.7c | Hard refresh resume — drugs | On `/medicare-analysis/drugs`, refresh browser | Stays on `/medicare-analysis/drugs` (no redirect to profile when profile is complete). Prior progress remains available. Chat posts a resume-aware message for Drugs step. |
| 12a.7d | Hard refresh resume — pharmacies/plans | On `/medicare-analysis/pharmacies` or `/medicare-analysis/plans`, refresh browser | Stays on same route with persisted progress intact. Chat posts step-aware resume message instead of startup mode chooser. |
| 12a.7e | Wizard mode consistency on analysis refresh | Refresh on any `/medicare-analysis/*` route | Wizard resumes in `MEDICARE_ANALYSIS` mode without replaying step announcements; step indicator remains aligned with current progress. |
| 12a.8 | Pharmacy step announced | Drugs confirmed, no pharmacy selected | Assistant: "Drugs added! Now let's find your preferred pharmacy..." + navigated to `/medicare-analysis/pharmacies`. |
| 12a.9 | Pharmacy selection does NOT auto-advance | Select first pharmacy checkbox | Wizard does NOT advance to PLANS. `pharmacySelectionConfirmed` remains false. User stays on pharmacy step. |
| 12a.10 | Explicit pharmacy confirmation | Drugs confirmed, pharmacies selected, click "Continue to Plans" | `pharmacySelectionConfirmed` set to true. Wizard advances. Assistant: "Excellent! Both your drugs and pharmacy are set..." + navigated to `/medicare-analysis/plans`. |
| 12a.11 | Drugs before pharmacy | Neither done → drugs prompt first (default) | Navigated to `/medicare-analysis/drugs`, not pharmacies. |
| 12a.12 | Pharmacy done before drugs | Pharmacy selected, drugs not confirmed | Assistant: "Pharmacy selected! Now please add your prescription drugs." + navigated to `/medicare-analysis/drugs`. |
| 12a.13 | Plans step announced | Both drugs and pharmacies confirmed | Assistant: "Excellent! Both your drugs and pharmacy are set..." + navigated to `/medicare-analysis/plans`. Step indicator advances to "Plans". |
| 12a.14 | Analysis step announced | Plan recommendation loaded | Assistant: "Everything is ready!" + navigated to `/medicare-analysis/cost-projections`. Step indicator advances to "Analysis". |
| 12a.15 | Complete | Cost projection loaded | Assistant: "Your Medicare analysis is complete! Review your results. Type 'reset analysis' to start a new one." |
| 12a.16 | Step indicator visibility | Wizard mode NONE | Step indicator in header NOT visible. |
| 12a.17 | Step indicator visibility | Wizard mode MEDICARE_ANALYSIS | Step indicator visible: Profile › Drugs & Pharmacy › Plans › Analysis. |
| 12a.18 | Mode buttons gated by profile | Profile API not yet resolved | Mode buttons NOT shown until `isProfileComplete()` returns true. Placeholder greeting shown first. |

### 12a2. Saved Data Selection on Medicare Click

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 12a2.1 | No chooser UI shown | Any saved analyses/prescriptions exist → click "Medicare Analysis" | No path chooser or saved-item selection cards appear. Wizard starts immediately. |
| 12a2.2 | No copy from saved prescription | Saved prescriptions exist → click "Medicare Analysis" | No prescription loading/copying occurs; user begins at fresh wizard flow. |
| 12a2.3 | No copy from saved pharmacy | Saved analyses with pharmacy exist → click "Medicare Analysis" | No pharmacy auto-restore/copying occurs; selected pharmacies remain empty until user selects again. |

### 12b. Wizard Reset

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 12b.1 | Reset via chat | Type "reset analysis" at any wizard step | Intent classified as `ACTION_RESET_ANALYSIS`. `resetAll()` called. Wizard resets. Mode buttons re-appear. |
| 12b.2 | Reset clears state | Reset during active wizard | All drug state cleared. Wizard mode returns to NONE. Step indicator disappears. |
| 12b.3 | Reset trigger guard | First page load (trigger = 0) | Wizard reset effect does NOT fire on initial load. |

### 12c. Free-form Intent Routing

| # | Scenario | Input | Expected Result |
|---|----------|-------|-----------------|
| 12c.1 | Navigate to profile | `"I want to edit my profile"` | Intent: `NAVIGATE_PROFILE`. Navigated to `/profile`. Edit mode set. |
| 12c.2 | Profile with name extraction | `"change my first name to Krishna"` | Intent: `NAVIGATE_PROFILE`. Params: `firstName: "Krishna"`. Profile page opens with firstName pre-filled. |
| 12c.3 | Profile with both names | `"change my name to Krishna Test"` | Params: `firstName: "Krishna"`, `lastName: "Test"`. Both fields pre-filled on `/profile`. |
| 12c.4 | Prefill consumed | Navigate to profile with prefill → navigate away → return | Prefill is null on second visit (consumed and cleared on first init). |
| 12c.5 | Navigate to drugs | `"go to drug analysis"` (profile complete) | Intent: `NAVIGATE_ANALYSIS_DRUGS`. Navigated to `/medicare-analysis/drugs`. |
| 12c.5b | Navigate drugs — no profile | `"go to drug analysis"` (profile incomplete) | Assistant: "Please complete your profile before accessing pharmacies." Redirected to `/medicare-analysis/profile`. |
| 12c.6 | Navigate to pharmacies | `"find pharmacies near me"` | Intent: `NAVIGATE_PHARMACIES`. Navigated to `/medicare-analysis/pharmacies` (if profile complete). |
| 12c.7 | Navigate pharmacies — no profile | `"find pharmacies"` (profile incomplete) | Assistant: "Please complete your profile before accessing pharmacies." Navigated to `/profile`. |
| 12c.8 | Navigate to plans | `"show me the plans"` | Intent: `NAVIGATE_PLANS`. Prerequisite chain: profile → drugs → pharmacy. `pharmacySelectionConfirmed` set to true. Navigated to `/medicare-analysis/plans`. |
| 12c.9 | Navigate plans — no profile | `"show plans"` (profile incomplete) | Redirected to `/profile` with profile gate message. |
| 12c.9b | Navigate plans — no drugs | `"show plans"` (profile complete, no drugs confirmed) | Assistant: "Please add at least one drug before viewing plans." Redirected to `/medicare-analysis/drugs`. |
| 12c.9c | Navigate plans — no pharmacy | `"show plans"` (profile + drugs done, no pharmacy) | Assistant: "Please select at least one pharmacy before viewing plans." Redirected to `/medicare-analysis/pharmacies`. |
| 12c.10 | Navigate to cost projections | `"show cost projections"` | Intent: `NAVIGATE_COST_PROJECTIONS`. Prerequisite chain: profile → drugs → pharmacy → plan. Navigated to `/medicare-analysis/cost-projections`. |
| 12c.10b | Cost projections — no plan | `"show cost projections"` (all done except no plan) | Assistant: "Please select a plan before viewing cost projections." Redirected to `/medicare-analysis/plans`. |
| 12c.10c | Cost projections — no drugs | `"show cost projections"` (profile only) | Assistant: "Please add at least one drug before viewing cost projections." Redirected to `/medicare-analysis/drugs`. |
| 12c.11 | Reset analysis | `"reset everything"` | Intent: `ACTION_RESET_ANALYSIS`. All state cleared. Wizard reset triggered. |
| 12c.12 | Save-analysis intent supported | `"save analysis as Heart Plan"` | Intent: `ACTION_SAVE_ANALYSIS`. Params extracted and save flow initiated. |
| 12c.13 | Save-analysis without name | `"save analysis"` | Intent still resolves to `ACTION_SAVE_ANALYSIS`; dialog asks for name. |
| 12c.14 | Sign out | `"log me out"` | Intent: `ACTION_SIGN_OUT`. Assistant: confirmation message. `signOut()` called after 800ms delay. |
| 12c.15 | Load saved analyses | `"show my saved analyses"` | Intent: `NAVIGATE_SAVED_ANALYSES`. Navigated to `/saved`. |
| 12c.16 | Drug input | `"Eliquis 5mg daily, Metformin 500mg"` | Intent: `DRUG_INPUT`. Falls through to drug name suggestion flow. |
| 12c.17 | Switch to PDP | `"show me Part D plans"` | Intent: `SWITCH_TO_PDP`. Prerequisite chain: profile → drugs → pharmacy. `activeSection` set to `partd`. `pharmacySelectionConfirmed` set. Navigated to `/medicare-analysis/plans`. |
| 12c.18 | Switch to MA | `"switch to Medicare Advantage"` | Intent: `SWITCH_TO_MA`. Prerequisite chain: profile → drugs → pharmacy. `activeSection` set to `ma`. `pharmacySelectionConfirmed` set. Navigated to `/medicare-analysis/plans`. |
| 12c.18b | Switch — no profile | `"switch to PDP"` (profile incomplete) | Assistant: "Please complete your profile before viewing plans." Redirected to `/profile`. |
| 12c.18c | Switch — no drugs | `"switch to MA"` (no drugs confirmed) | Assistant: "Please add at least one drug before viewing plans." Redirected to `/medicare-analysis/drugs`. |
| 12c.18d | Switch — no pharmacy | `"switch to PDP"` (no pharmacy selected) | Assistant: "Please select at least one pharmacy before viewing plans." Redirected to `/medicare-analysis/pharmacies`. |
| 12c.19 | Switch — already active | `"show Part D plans"` (already viewing PDP section) | Assistant: "You're already viewing..." message. No navigation change. |
| 12c.20 | Unknown input | `"what's the weather?"` | Intent: `UNKNOWN`. Falls through to drug flow (which shows "No recognizable drug names found"). |
| 12c.21 | Classification error | Backend unreachable | Falls back to `runDrugFlow()` (drug name suggestion pipeline). |
| 12c.22 | Confirmation message | Any classified intent | AI-generated confirmation text (max ~15 words) shown as assistant message in chat. |
| 12c.23 | Save analysis via chat | `"save analysis as Heart Plan"` | Intent: `ACTION_SAVE_ANALYSIS`. Params: `analysisName: "Heart Plan"`. Save dialog opens with pre-filled name. Analysis saved. State reset. Navigated to `/medicare-analysis/profile`. |
| 12c.24 | Save analysis — no name | `"save my analysis"` | Intent: `ACTION_SAVE_ANALYSIS`. No `analysisName`. Save dialog opens for user to enter name. |
| 12c.25 | Save analysis — prerequisites not met | `"save analysis"` (no cost projection) | Assistant: "Please complete your analysis before saving." Lists missing prerequisites. |
| 12c.26 | Save analysis — overwrite conflict | `"save analysis as Heart Plan"` (name already exists) | 409 response. Overwrite confirmation: "Analysis 'Heart Plan' already exists. Overwrite? (yes/no)". |
| 12c.27 | Confirm overwrite | Type `"yes"` after overwrite prompt | Re-saves with `force: true`. Success message. State reset. Navigated to `/medicare-analysis/profile`. |
| 12c.28 | Cancel overwrite | Type `"no"` after overwrite prompt | Assistant: "Save cancelled." `pendingSaveAnalysisOverwrite` cleared. |
| 12c.29 | Navigate saved analyses | `"show my saved analyses"` | Intent: `NAVIGATE_SAVED_ANALYSES`. Navigated to `/saved`. |
| 12c.30 | Run analysis via chat | `"run the analysis"` | Intent: `ACTION_RUN_ANALYSIS`. Prerequisites checked (profile → drugs → pharmacy → plan). Triggers cost calculation. |
| 12c.31 | Help text updated | Type `"help"` | Help menu includes "Show saved analyses" and "Save analysis as [name]" in action list. |
| 12c.32 | Page-context: explicit zip change on drugs page | On `/medicare-analysis/drugs` → type `"change my zip to 80113"` | Intent: `NAVIGATE_PROFILE` with `params.zipCode: "80113"`. Profile page opens with zip pre-filled. (Explicit field phrase overrides drug-page context.) |
| 12c.33 | Page-context: bare number on drugs page | On `/medicare-analysis/drugs` → type `"80113"` | Intent: `UNKNOWN`. Falls through to drug name suggestion flow (bare number is not treated as a zip change on the drugs page). |
| 12c.34 | Page-context: drug name not misrouted to profile | On `/medicare-analysis/drugs` → type `"Metformin"` | Intent: `DRUG_INPUT` / routes to drug selection flow. Does NOT navigate to profile. |
| 12c.35 | Page-context: profile-field phrase on profile page | On `/medicare-analysis/profile` → type `"80113"` | Intent: `NAVIGATE_PROFILE`. ZIP pre-filled. (All field-like inputs on profile page treated as profile edits.) |
| 12c.36 | Cross-page: drug name typed on pharmacies page | On `/medicare-analysis/pharmacies` → type `"add metformin"` | Intent reclassified from `DRUG_INPUT` → `NAVIGATE_ANALYSIS_DRUGS`. `pendingCrossPageDrugSearch` set to `"add metformin"`. Navigate to `/medicare-analysis/drugs`. Drug name suggestion search fires automatically. |
| 12c.37 | Cross-page: navigation-only phrase on pharmacies page | On `/medicare-analysis/pharmacies` → type `"go to drug"` | Intent: `NAVIGATE_ANALYSIS_DRUGS` (true nav intent). `pendingCrossPageDrugSearch` NOT set. Navigate to `/medicare-analysis/drugs`. Page opens blank — no auto-search. |
| 12c.38 | Cross-page: profile field on pharmacies page — save pharmacies | On `/medicare-analysis/pharmacies` (3 pharmacies selected) → type `"change magitier 4"` | Selected pharmacies saved (`recState.savePharmacySelection()`). Chat: "Your 3 selected pharmacies have been saved. [AI confirmation message]". Navigated to `/medicare-analysis/profile`. MAGI tier pre-filled to 4. |
| 12c.39 | Cross-page: profile field on pharmacies — no pharmacies selected | On `/medicare-analysis/pharmacies` (0 selected) → type `"change magitier 4"` | No pharmacy save call. Chat: AI confirmation message only. Navigated to `/medicare-analysis/profile`. MAGI tier pre-filled. |
| 12c.40 | Cross-page: return route set from pharmacies | Navigate to profile via pharmacy-page profile intent | `returnRoute` saved as `/medicare-analysis/pharmacies`. Save/close on profile returns to pharmacy step. |
| 12c.41 | Cross-page: drug keyword on profile → redirect | On `/medicare-analysis/profile` (profile complete) → type `"add drug eliquis"` | Intent: `DRUG_INPUT`. Profile extraction tried first → returns empty. Text contains drug keyword ("drug") → `pendingCrossPageDrugSearch` set. Navigate to `/medicare-analysis/drugs`. Drug name suggestion search fires automatically. |
| 12c.42 | Cross-page: bare drug name on profile → hint | On `/medicare-analysis/profile` (profile complete) → type `"metformin"` | Intent: `DRUG_INPUT`. Profile extraction returns empty. No drug keyword found → shows hint: "It looks like you may be searching for a drug. Navigate to the **Drugs** step…" Stays on `/medicare-analysis/profile`. |
| 12c.43 | Cross-page: drug keyword on profile — incomplete | On `/medicare-analysis/profile` (profile incomplete) → type `"add medication eliquis"` | Intent: `DRUG_INPUT`. Extraction empty. Keyword "medication" found → redirect to `NAVIGATE_ANALYSIS_DRUGS`. Assistant: "Please complete your profile before accessing." Stays on `/medicare-analysis/profile`. |
| 12c.44 | Cross-page: multiple drugs with keyword from profile | On `/medicare-analysis/profile` → type `"add drugs eliquis 5mg and metformin 500mg"` | Profile extraction returns empty. Keyword "drugs" present → `pendingCrossPageDrugSearch` set. Navigate to `/medicare-analysis/drugs`. Both drugs appear as suggestion rows. |
| 12c.45 | Profile-field input not misrouted to drugs | On `/medicare-analysis/profile` → type `"80113"` | Intent: `UNKNOWN`. Falls through to profile extraction. ZIP pre-filled. Does NOT set `pendingCrossPageDrugSearch`. |
| 12c.46 | NAVIGATE_PROFILE with data still goes to extraction | On `/medicare-analysis/profile` → type `"change my name to John"` | Intent: `NAVIGATE_PROFILE` with `params.firstName: "John"`. Routed to profile extraction (reactive `pendingChatProfileData`). Name field updated in form. NOT treated as drug input. |
| 12c.47 | DRUG_INPUT but profile field found by extraction | On `/medicare-analysis/profile` → type `"magitier is 150"` | Intent: `DRUG_INPUT`. Profile extraction runs first → extracts `{ magiTier: "150" }`. MAGI validation normalizes to tier 2 (150-199% FPL via label-contains match). Profile updated. Does NOT redirect to drugs. No `pendingCrossPageDrugSearch`. |
| 12c.48 | DRUG_INPUT but profile field — invalid MAGI | On `/medicare-analysis/profile` → type `"magitier is 999"` | Intent: `DRUG_INPUT`. Extraction → `{ magiTier: "999" }`. Normalization fails (no match). MAGI tier picker buttons shown. Stays on profile. No drug search. |
| 12c.49 | Cross-page: bare drug on pharmacy page → hint | On `/medicare-analysis/pharmacies` → type `"eliquis"` | Intent: `DRUG_INPUT`. No drug keyword → shows hint: "It looks like you may be searching for a drug…" Stays on `/medicare-analysis/pharmacies`. |
| 12c.50 | Cross-page: drug keyword on pharmacy page → redirect | On `/medicare-analysis/pharmacies` → type `"add drug eliquis"` | Intent: `DRUG_INPUT`. Keyword "drug" found → `pendingCrossPageDrugSearch` set → navigate to `/medicare-analysis/drugs`. Auto-search fires. |
| 12c.51 | Cross-page: bare drug on plans page → hint | On `/medicare-analysis/plans` → type `"metformin"` | Intent: `DRUG_INPUT`. No drug keyword → shows hint: "It looks like you may be searching for a drug…" Stays on `/medicare-analysis/plans`. |
| 12c.52 | Cross-page: drug keyword on plans page → redirect | On `/medicare-analysis/plans` → type `"add prescription metformin"` | Intent: `DRUG_INPUT`. Keyword "prescription" found → `pendingCrossPageDrugSearch` set → navigate to `/medicare-analysis/drugs`. Auto-search fires. |
| 12c.53 | Back/previous blocked in chat | On any `/medicare-analysis/*` page → type `"go back"` | `BACK_PATTERN` matches. Assistant: "To go back, use the **Back** button on the left side of the page or the stepper above." No navigation. |
| 12c.54 | "previous step" blocked in chat | On `/medicare-analysis/pharmacies` → type `"previous step"` | `BACK_PATTERN` matches. Same back-button guidance message. Stays on pharmacies. |
| 12c.55 | "back" does not block compound phrases | On `/medicare-analysis/drugs` → type `"go back to profile"` | `BACK_PATTERN` does not match (pattern anchored). Falls through to intent classifier → `NAVIGATE_PROFILE`. Navigation proceeds normally. |

### 12d. Wizard + Free-form Interaction

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 12d.1 | Drug input during wizard | Wizard at DRUGS_PHARMACIES step → type drug names | Drug flow runs normally. After confirmation, wizard auto-advances if drugs+pharmacies both done. |
| 12d.2 | Free-form nav during wizard | Wizard at PLANS step → type "go to profile" | Navigates to `/profile`. Return route saved. Wizard stays active. Returns to current step context. |
| 12d.3 | Save analysis during wizard | Type "save analysis as Heart Plan" during wizard | Routed to analysis snapshot save flow (no prescription save trigger exists). |

### 12e. Backend Intent Classification

| # | Scenario | Request | Expected Result |
|---|----------|---------|-----------------|
| 12e.1 | Valid classification | `POST /api/chat/intent` with `{ "message": "show plans", "isProfileComplete": true, "currentPage": "/medicare-analysis/plans" }` | 200 OK. `{ "intent": "NAVIGATE_PLANS", "confirmationMessage": "..." }`. |
| 12e.2 | Unauthorized | `POST /api/chat/intent` without JWT | 401 Unauthorized. |
| 12e.3 | Empty message | `{ "message": "", "isProfileComplete": true, "currentPage": "/medicare-analysis/drugs" }` | Returns `UNKNOWN` intent with fallback message. |
| 12e.4 | AI timeout | Anthropic API slow/down | Returns `UNKNOWN` intent with fallback: "I'm not sure what you'd like to do..." |
| 12e.5 | Markdown fence stripping | AI returns ```json wrapped response | JSON extracted correctly, intent classified. |
| 12e.6 | Save analysis intent | `{ "message": "save my analysis as Heart Plan", "currentPage": "/medicare-analysis/cost-projections" }` | Returns `ACTION_SAVE_ANALYSIS` with `params.analysisName: "Heart Plan"`. |
| 12e.7 | Navigate saved intent | `{ "message": "show my saved data", "currentPage": "/" }` | Returns `NAVIGATE_SAVED_ANALYSES`. |
| 12e.8 | Run analysis intent | `{ "message": "run the cost analysis", "currentPage": "/medicare-analysis/plans" }` | Returns `ACTION_RUN_ANALYSIS`. |
| 12e.9 | Page context — explicit profile change on drugs | `{ "message": "change my zip to 80113", "currentPage": "/medicare-analysis/drugs" }` | Returns `NAVIGATE_PROFILE` with `params.zipCode: "80113"`. (Explicit profile-field phrase → navigate profile even on drugs page.) |
| 12e.10 | Page context — bare number on drugs | `{ "message": "80113", "currentPage": "/medicare-analysis/drugs" }` | Returns `UNKNOWN`. (Bare number without field reference on drugs page → not treated as zip change.) |
| 12e.11 | Page context — drug name on drugs | `{ "message": "Metformin", "currentPage": "/medicare-analysis/drugs" }` | Returns `DRUG_INPUT` or `UNKNOWN` (falls through to drug flow). Does NOT return `NAVIGATE_PROFILE`. |
| 12e.12 | Page context — any message on profile page | `{ "message": "80113", "currentPage": "/medicare-analysis/profile" }` | Returns `NAVIGATE_PROFILE` with `params.zipCode: "80113"`. (All field-like input on profile page treated as profile edit.) |
| 12e.13 | Page context — null currentPage | `{ "message": "change my zip", "currentPage": null }` | Falls back to default classification with no page-specific guidance. |

### 12f. Return Route Navigation

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 12f.1 | Return route saved on profile nav | On `/medicare-analysis/plans` → chat "go to profile" | `returnRoute` set to `/medicare-analysis/plans`. Navigated to `/profile`. |
| 12f.2 | Return route saved on saved-analyses nav | On `/medicare-analysis/drugs` → chat "show saved analyses" | `returnRoute` set to `/medicare-analysis/drugs`. Navigated to `/saved`. |
| 12f.3 | Return after profile save | Return route set to `/medicare-analysis/plans` → save profile | Navigated back to `/medicare-analysis/plans` (not default ``/medicare-analysis``). `returnRoute` cleared to null. |
| 12f.4 | Return after close edit panel | Return route set → close edit panel without saving | Navigated back to saved route. `returnRoute` cleared. |
| 12f.5 | No return route — fallback | No return route set → save profile | Navigated to default ``/medicare-analysis`` (entry redirects to **``/medicare-analysis/profile``**). |
| 12f.6 | Return route not set for non-analysis | On `/profile` → navigate to profile (intent) | `returnRoute` NOT set (URL doesn't match `/medicare-analysis/*`). |
| 12f.7 | Return route persisted in session | Set return route → refresh page | `returnRoute` restored from sessionStorage. |
| 12f.8 | Return route cleared on reset | Set return route → reset analysis | `returnRoute` cleared to null via `resetAll()`. |
| 12f.9 | Header edit profile preserves return step | While on `/medicare-analysis/pharmacies`, click header/profile edit action | `returnRoute` saved as `/medicare-analysis/pharmacies`; save/close on profile returns to `/medicare-analysis/pharmacies`. |
| 12f.10 | Impact-aware invalidation after profile save | On `/medicare-analysis/plans`, edit impactful profile fields (e.g., ZIP/MAGI/coverage year) and save | Returns to prior analysis route; keeps drugs; clears downstream pharmacy/plans/cost state; assistant prompts user to continue from pharmacies. |
| 12f.11 | No invalidation for non-impactful profile edits | On `/medicare-analysis/plans`, edit alternate email/mobile only and save | Returns to prior analysis route; downstream pharmacy/plans/cost state remains intact. |

### 12g. Plan Section Chooser

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

### 12h. UI Action Tracking (System Messages)

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 12h.1 | System message rendering | Perform any tracked UI action | System message appears as centered pill badge with `touch_app` icon, grey background, muted text — distinct from user/assistant bubbles. |
| 12h.2 | Drug confirmed | Click "Confirm" on a drug on the Drugs step | System message: "Confirmed drug: {drugName}" |
| 12h.3 | Drug removed | Click "Remove" on a drug on the Drugs step | System message: "Removed drug: {drugName}" |
| 12h.4 | Pharmacy selected | Check a pharmacy checkbox on the Pharmacies step | System message: "Selected pharmacy: {pharmacyName}" |
| 12h.5 | Pharmacy deselected | Uncheck a pharmacy checkbox on the Pharmacies step | System message: "Deselected pharmacy: {pharmacyName}" |
| 12h.6 | Wizard navigation | On Drugs step, click "Continue to Pharmacies" | System message: "Navigated to Pharmacies" |
| 12h.7 | New analysis | Click "New Analysis" button | System message: "Started a new analysis" (before state reset) |
| 12h.8 | Part D plan selected | Click select on a Part D plan | System message: "Selected Part D plan: {planName}" |
| 12h.9 | Medigap plan selected | Click select on a Medigap plan | System message: "Selected Medigap plan: {plan}" |
| 12h.10 | MA plan selected | Click select on an MA plan | System message: "Selected MA plan: {planName}" |
| 12h.11 | Section switched | Click PDP or MA section button | System message: "Switched to Part D + Medigap plans" or "Switched to Medicare Advantage plans" |
| 12h.12 | Cost calculation | Click "Calculate Lifetime Cost" | System message: "Calculating lifetime cost projection" |
| 12h.13 | Profile saved | Click "Save" on profile page | System message: "Profile saved" |
| 12h.14 | System messages persisted | Perform actions → refresh page | System messages restored from sessionStorage along with user/assistant messages. |

### 12i. Action Intent Bypass

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 12i.1 | Save analysis on drugs page | On `/medicare-analysis/drugs` → type "save analysis as Heart Plan" | Message bypasses `routeToDrugSelection()` via `ACTION_PATTERNS`. Intent classified as `ACTION_SAVE_ANALYSIS`. |
| 12i.2 | Run analysis on plans page | On `/medicare-analysis/plans` → type "run the analysis" | Bypasses plan selection. Intent: `ACTION_RUN_ANALYSIS`. |
| 12i.3 | Help on pharmacy page | On `/medicare-analysis/pharmacies` → type "help" | Bypasses pharmacy selection. Intent: `ACTION_HELP`. Help menu shown. |
| 12i.4 | Sign out on drugs page | On `/medicare-analysis/drugs` → type "sign out" | Bypasses drug selection. Intent: `ACTION_SIGN_OUT`. |
| 12i.5 | Show saved on plans page | On `/medicare-analysis/plans` → type "show my saved analyses" | Bypasses plan selection. Intent: `NAVIGATE_SAVED_ANALYSES`. Navigated to `/saved`. |
| 12i.6 | Drug selection still works | On `/medicare-analysis/drugs` → type "select Lisinopril generic tablet 10mg" | Does NOT match `ACTION_PATTERNS`. Routed to `routeToDrugSelection()` as before. Drug selected. |
| 12i.7 | Pharmacy selection still works | On `/medicare-analysis/pharmacies` → type "select the Walgreens" | Does NOT match `ACTION_PATTERNS`. Routed to `routeToPharmacySelection()` as before. Pharmacy selected. |
| 12i.8 | Plan selection still works | On `/medicare-analysis/plans` → type "select the Humana plan" | Does NOT match `ACTION_PATTERNS`. Routed to `routeToPlanSelection()` as before. Plan selected. |

### 12k. Page-Context AI Injection (PageContextBuilder)

> The backend appends a page-specific guidance block to the AI system prompt on every intent classification call. These scenarios verify the injected context produces correct disambiguation.

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 12k.1 | drugs: explicit field → profile nav | On `/medicare-analysis/drugs`, type `"update my coverage year to 2026"` | Classified as `NAVIGATE_PROFILE`. Profile page opens with coverageYear pre-filled to 2026. |
| 12k.2 | drugs: drug action stays in drug flow | On `/medicare-analysis/drugs`, type `"select Lisinopril 10mg tablet"` | Routed to `routeToDrugSelection()`. Drug formulation extracted. Not navigated to profile. |
| 12k.3 | drugs: bare number not misrouted | On `/medicare-analysis/drugs`, type `"10mg"` or `"2026"` | Intent `UNKNOWN`. Falls through to drug name suggestion. No profile navigation. |
| 12k.4 | profile page: all field phrases go to profile | On `/medicare-analysis/profile`, type `"02649"` | Intent `NAVIGATE_PROFILE`. ZIP field updated. No drug navigation triggered. |
| 12k.5 | pharmacies: non-pharmacy message ignored | On `/medicare-analysis/pharmacies`, type `"change my MAGI tier"` | Intent `NAVIGATE_PROFILE`. Profile page opens. Not treated as a pharmacy command. |
| 12k.6 | plans page: plan phrases stay in plan flow | On `/medicare-analysis/plans`, type `"select the SilverScript plan"` | Routed to `routeToPlanSelection()`. Not misclassified as profile navigation. |
| 12k.7 | cost-projections: free-form message routed correctly | On `/medicare-analysis/cost-projections`, type `"save this as Heart Plan"` | Intent `ACTION_SAVE_ANALYSIS`. Save flow initiated. |
| 12k.8 | Null/missing currentPage | Backend called without `currentPage` | Falls back to base system prompt with no page guidance. Classification works as before. |

### 12l. Cross-Page Navigation from Pharmacies & Navigation Prerequisites

> Covers the pharmacy-page smart routing (profile edits, drug adds) and the profile-complete prerequisite now enforced for both Drugs and Pharmacies navigation.

#### 12l-a. Profile-Complete Prerequisite (Drugs & Pharmacies parity)

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 12l.1 | Navigate to drugs — profile complete | Any page → `"go to drugs"` (profile complete) | Navigated to `/medicare-analysis/drugs`. No redirect. |
| 12l.2 | Navigate to drugs — profile incomplete | Any page → `"go to drugs"` (profile incomplete) | Redirect to `/medicare-analysis/profile`. Assistant: "Please complete your profile before accessing." No drugs page shown. |
| 12l.3 | Navigate to pharmacies — profile complete | Any page → `"find pharmacies"` (profile complete) | Navigated to `/medicare-analysis/pharmacies`. |
| 12l.4 | Navigate to pharmacies — profile incomplete | Any page → `"find pharmacies"` (profile incomplete) | Redirect to `/medicare-analysis/profile`. Assistant: "Please complete your profile..." No pharmacy page shown. |
| 12l.5 | Drugs and pharmacies have same gate | Profile incomplete → try both `"go to drugs"` and `"find pharmacies"` | Both redirect to profile with identical message. Consistent UX. |

#### 12l-b. Cross-Page Profile Edit from Pharmacies

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 12l.6 | Profile field typed on pharmacies page | On `/medicare-analysis/pharmacies` (3 selected) → type `"change magitier 4"` | Pharmacy selection saved (fire-and-forget). Chat: `"Your 3 selected pharmacies have been saved. [AI message]"`. Navigated to `/medicare-analysis/profile`. MAGI tier field pre-filled to 4. |
| 12l.7 | Profile field — zero pharmacies selected | On `/medicare-analysis/pharmacies` (0 selected) → type `"change magitier 4"` | No save call (nothing to save). Chat: AI confirmation only (no "pharmacies saved" prefix). Navigated to `/medicare-analysis/profile`. MAGI tier pre-filled. |
| 12l.8 | Return route preserved from pharmacies | Navigate to profile via pharmacy-page intent | `returnRoute` set to `/medicare-analysis/pharmacies`. After profile save/close, router returns to pharmacies. |
| 12l.9 | Multiple profile fields from pharmacies | On `/medicare-analysis/pharmacies` → type `"change my zip to 80113 and name to John"` | Both fields extracted into `pendingPrefill`. Pharmacies saved. Navigated to profile with both fields pre-filled. |
| 12l.10 | No double classification | Any non-pharmacy intent on pharmacies page | Only ONE `POST /api/chat/intent` call made. The pre-classified result is forwarded directly to `handleIntent`. |

#### 12l-c. Cross-Page Drug Search from Pharmacies

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 12l.11 | Drug name on pharmacies page → auto-search | On `/medicare-analysis/pharmacies` → type `"add metformin"` | `DRUG_INPUT` detected. `pendingCrossPageDrugSearch = "add metformin"`. Navigate to `/medicare-analysis/drugs`. Drug suggestion search auto-fires within 50 ms of page load. Suggestion chips appear. |
| 12l.12 | Drug name — misspelled | On `/medicare-analysis/pharmacies` → type `"add metformi"` | Same flow as 12l.11. Drug suggestion AI handles typo (confidence < 1.0). |
| 12l.13 | Multiple drugs from pharmacies | On `/medicare-analysis/pharmacies` → type `"add eliquis and metformin"` | `pendingCrossPageDrugSearch = "add eliquis and metformin"`. Drug suggestion parses both names. Two suggestion rows appear on drugs. |
| 12l.14 | Pure navigation phrase — no auto-search | On `/medicare-analysis/pharmacies` → type `"go to drug"` | `NAVIGATE_ANALYSIS_DRUGS` (true nav intent). `pendingCrossPageDrugSearch` NOT set. Navigate to drugs. Page opens blank — no search fires. |
| 12l.15 | Pure navigation — variation | On `/medicare-analysis/pharmacies` → type `"show drugs"` or `"navigate to drugs page"` | Same as 12l.14. No auto-search. |
| 12l.16 | Pending search cleared after use | `pendingCrossPageDrugSearch` set → navigate → search fires | Signal cleared to null before `runDrugFlow()`. Refresh/re-navigation does NOT re-trigger search. |
| 12l.17 | Drug input not sent to pharmacy AI | On `/medicare-analysis/pharmacies` → type a drug name | Message does NOT reach `executePharmacySelectionChat()`. No "I could not apply that" message from pharmacy flow. |

#### 12l-d. Cross-Page Drug Search — Keyword Gating

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 12l.18 | Drug keyword on profile → auto-search | On `/medicare-analysis/profile` (profile complete) → type `"add drug eliquis"` | `DRUG_INPUT` detected. Profile extraction tried first → returns empty. Keyword "drug" found → `pendingCrossPageDrugSearch` set. Navigate to `/medicare-analysis/drugs`. Drug suggestion search auto-fires. |
| 12l.19 | Bare drug on profile → hint only | On `/medicare-analysis/profile` (profile complete) → type `"metformin"` | `DRUG_INPUT` detected. Profile extraction returns empty. No drug keyword → shows hint: "It looks like you may be searching for a drug…" Stays on profile. No `pendingCrossPageDrugSearch`. |
| 12l.20 | Drug keyword on profile — incomplete | On `/medicare-analysis/profile` (profile incomplete) → type `"add medication eliquis"` | `DRUG_INPUT` detected. Extraction empty. Keyword "medication" found → redirect to `NAVIGATE_ANALYSIS_DRUGS`. Blocked by profile-complete gate. Stays on profile. |
| 12l.21 | Drug keyword on profile — unsaved changes | On `/medicare-analysis/profile` (unsaved edits) → type `"add drug metformin"` | `DRUG_INPUT` detected. Extraction empty. Keyword "drug" → redirect to `NAVIGATE_ANALYSIS_DRUGS`. Shows "save first" message. Stays on profile. |
| 12l.22 | Multiple drugs with keyword from profile | On `/medicare-analysis/profile` → type `"add drugs eliquis 5mg and metformin"` | Extraction returns empty. Keyword "drugs" present → `pendingCrossPageDrugSearch` set. Navigate to drugs. Both drugs parsed. |
| 12l.23 | Profile field NOT misrouted to drugs | On `/medicare-analysis/profile` → type `"80113"` or `"John"` | Intent: `UNKNOWN`. Falls through to profile extraction (no drug fallback for UNKNOWN). No `pendingCrossPageDrugSearch`. Profile fields updated. |
| 12l.24 | NAVIGATE_PROFILE with data stays in extraction | On `/medicare-analysis/profile` → type `"change my zip to 80113"` | Intent: `NAVIGATE_PROFILE` with params. Routed to profile extraction (reactive path). NOT treated as drug input. |
| 12l.25 | DRUG_INPUT resolved by extraction (not drug) | On `/medicare-analysis/profile` → type `"magitier is 150"` | `DRUG_INPUT` detected. Profile extraction extracts `{ magiTier: "150" }` → normalized to tier 2. Profile updated. `onEmptyExtraction` callback NOT invoked. No cross-page navigation. |
| 12l.26 | Drug keyword on pharmacy page → redirect | On `/medicare-analysis/pharmacies` → type `"add drug eliquis"` | `DRUG_INPUT` detected. Keyword "drug" found → `pendingCrossPageDrugSearch` set → navigate to `/medicare-analysis/drugs`. Auto-search fires. |
| 12l.27 | Bare drug on pharmacy page → hint | On `/medicare-analysis/pharmacies` → type `"eliquis"` | `DRUG_INPUT` detected. No drug keyword → shows hint. Stays on pharmacies. |
| 12l.28 | Drug keyword on plans page → redirect | On `/medicare-analysis/plans` → type `"add prescription metformin"` | `DRUG_INPUT` detected. Keyword "prescription" found → `pendingCrossPageDrugSearch` set → navigate to `/medicare-analysis/drugs`. Auto-search fires. |
| 12l.29 | Bare drug on plans page → hint | On `/medicare-analysis/plans` → type `"metformin"` | `DRUG_INPUT` detected. No drug keyword → shows hint. Stays on plans. |

#### 12l-e. Back/Previous Blocked in Chat

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 12l.30 | "back" blocked | On any `/medicare-analysis/*` page → type `"back"` | `BACK_PATTERN` matches. Assistant: "To go back, use the **Back** button on the left side of the page or the stepper above." No navigation. |
| 12l.31 | "go back" blocked | On `/medicare-analysis/pharmacies` → type `"go back"` | `BACK_PATTERN` matches. Same guidance message. Stays on pharmacies. |
| 12l.32 | "previous step" blocked | On `/medicare-analysis/plans` → type `"previous step"` | `BACK_PATTERN` matches. Same guidance message. Stays on plans. |
| 12l.33 | "go back to profile" NOT blocked | On `/medicare-analysis/drugs` → type `"go back to profile"` | `BACK_PATTERN` does not match (pattern requires "back" at start or end of input, compound phrase has more text). Falls through to intent classifier → `NAVIGATE_PROFILE`. Navigation proceeds. |

### 12j. Orchestrator URL Guard

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 12j.0 | Orchestrator skips on analysis profile | Recommendation exists + on `/medicare-analysis/profile` → type a message | `routeToOrchestrator()` returns false (same guard pattern as other analysis wizard routes). |
| 12j.1 | Orchestrator skips on drugs page | Recommendation exists + on `/medicare-analysis/drugs` → type "select Metformin" | `routeToOrchestrator()` returns false. Message routed to `routeToDrugSelection()`. Drug selected correctly. |
| 12j.2 | Orchestrator skips on pharmacy page | Recommendation exists + on `/medicare-analysis/pharmacies` → type "select Walgreens" | `routeToOrchestrator()` returns false. Message routed to `routeToPharmacySelection()`. Pharmacy selected correctly. |
| 12j.3 | Orchestrator skips on plans page | Recommendation exists + on `/medicare-analysis/plans` → type "select the Humana plan" | `routeToOrchestrator()` returns false. Message routed to `routeToPlanSelection()`. Plan selected correctly. |
| 12j.4 | Orchestrator active on other pages | Recommendation exists + on `/medicare-analysis/cost-projections` → type any message | `routeToOrchestrator()` processes the message and sends to orchestrator. |
| 12j.5 | Orchestrator active on dashboard | Recommendation exists + on `/` → type any message | `routeToOrchestrator()` processes the message. |

---

## 13. Cost Projection — Extended Fields

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 13.1 | Supplement plan type sent | Select PDP+Medigap plan (e.g., Plan G) → Calculate Lifetime Cost | Request includes `supplementPlanType: "G"`. |
| 13.2 | Part D OOP full year | Calculate cost from any plan | `partDOOPFullYear` sourced from `PharmacyWiseRecommendation.totalPrescriptionCostFullYear` (not same as `partDOOP`). |
| 13.3 | Total IRMAA in response | Evaluate costs | Response `lifetimeTotals.totalIrmaa` = `lifeTimeBSurcharge + lifeTimeDSurcharge`. |
| 13.4 | Supplement data in response | Evaluate costs for PDP+Medigap plan | Response `lifetimeTotals.supplementPlanType` and `supplementPlanPremium` populated. |
| 13.5 | AI prompt includes IRMAA | Evaluate costs | AI evaluation prompt includes `{{TOTAL_IRMAA}}`, `{{SUPPLEMENT_PLAN_TYPE}}`, `{{SUPPLEMENT_PLAN_PREMIUM}}` data. |
| 13.6 | All request fields sent | Calculate cost | Request includes: `supplementDataProvided`, `partDDataProvided`, `reserveDaysUsed`, `dental`, `dentalHealthGrade`, `boughtPlanA`, `medicareAdvantageDataProvided`, `partDPremium`, `calculateForAdjustedMonth`, `supplementPlanType`. |

---

## 14. Save Analysis (Chat + UI Button)

### Save Analysis via UI Button

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| SA.1 | Save button visible | Navigate to `/medicare-analysis/cost-projections` with cost data loaded | "Save Analysis" button visible in header (mat-flat-button with assessment icon). |
| SA.2 | Save button opens dialog | Click "Save Analysis" button | `SavePrescriptionDialogComponent` opens with title "Save Analysis", subtitle "Enter a name for this analysis", icon "assessment". |
| SA.3 | Save with name | Enter "Heart Plan 2026" → click Save | `AnalysisSnapshotService.save("Heart Plan 2026")` called. Success: snackbar + chat confirmation. State reset. Navigated to `/medicare-analysis/profile`. |
| SA.4 | Cancel save dialog | Open dialog → click Cancel | Dialog closes. No API call. No state change. |
| SA.5 | Empty name disabled | Open dialog → leave name empty | Save button disabled. |
| SA.6 | Overwrite existing | Save → API returns 409 | Auto-retries with `force: true`. Saves successfully. State reset. |
| SA.7 | API error | Save → API returns 500 | Snackbar shows error. State NOT reset. User can retry. |

### Save Analysis via Chat

| # | Scenario | Input | Expected Result |
|---|----------|-------|-----------------|
| SA.8 | Save with name from chat | `"save analysis as Heart Plan"` | Intent: `ACTION_SAVE_ANALYSIS`. `analysisName: "Heart Plan"`. Saves directly (no dialog). State reset. |
| SA.9 | Save without name from chat | `"save my analysis"` | Intent: `ACTION_SAVE_ANALYSIS`. No `analysisName`. Dialog opens for name input. |
| SA.10 | Prerequisites not met | `"save analysis"` (no cost projection) | Assistant: "Please complete your analysis before saving." Lists unmet prerequisites. |
| SA.11 | Overwrite via chat | Save → 409 conflict | Assistant: "Analysis already exists. Overwrite? (yes/no)". `pendingSaveAnalysisOverwrite` set. |
| SA.12 | Confirm overwrite | `"yes"` after overwrite prompt | Re-saves with `force: true`. Success. State reset. `pendingSaveAnalysisOverwrite` cleared. |
| SA.13 | Cancel overwrite | `"no"` after overwrite prompt | Assistant: "Save cancelled." `pendingSaveAnalysisOverwrite` cleared. No state change. |

### Reset After Save

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| SA.14 | State reset on save success | Successfully save analysis (UI or chat) | `state.resetAll()` called. All signals cleared (drugs, pharmacies, plans, cost, data). SessionStorage cleared. |
| SA.15 | Navigate after reset | Save success | Router navigates to `/medicare-analysis/profile` (Profile step — step 1 of the analysis shell). |
| SA.16 | Wizard resets | Save success with wizard active | Wizard mode resets via `wizardResetTrigger`. Mode selection buttons re-appear. |
| SA.17 | Chat message after reset | Save success | Assistant message confirms save. New greeting may appear for fresh wizard. |

### AnalysisSnapshotService

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| SA.18 | canSave() — all met | Profile complete + drugs confirmed + pharmacies selected + plan selected + cost projection | `canSave()` returns `true`. |
| SA.19 | canSave() — missing profile | Profile incomplete | `canSave()` returns `false`. |
| SA.20 | canSave() — missing drugs | No confirmed drugs | `canSave()` returns `false`. |
| SA.21 | canSave() — missing pharmacy | No pharmacies selected | `canSave()` returns `false`. |
| SA.22 | canSave() — missing plan | No plan selected | `canSave()` returns `false`. |
| SA.23 | canSave() — missing cost | No cost projection | `canSave()` returns `false`. |
| SA.24 | Snapshot includes all data | Save analysis | Request body includes: profile snapshot, drugs array, pharmacy, plans (with expanded fields), costSnapshot (with yearlyDetails + evaluation). |

---

## 15. Expanded Analysis Persistence for PDF

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| EP.1 | SelectedPlanDto expanded fields | Save analysis with plan selected | Saved plan includes: `deductible`, `starRating`, `totalPrescriptionCost`, `totalPlanCost`, `prescriptionDrugCovered`, `unavailableDrugs[]`, `planExpenses[]`. |
| EP.2 | Plan expenses populated | Save analysis | Each plan's `planExpenses` array contains expense items with `name` and `amount`. |
| EP.3 | Unavailable drugs list | Save analysis with uncovered drugs | `unavailableDrugs` array lists drug names not on the plan's formulary. |
| EP.4 | CostSnapshot yearly details | Save analysis | `costSnapshot.yearlyDetails[]` contains one entry per projection year with 16 financial fields (partAPremium, partBPremium, partBSurcharge, maPremium, partDPremium, partDSurcharge, conciergePremium, partAOOP, partBOOP, partDOOP, totalABMA, dentalPremium, dentalOOP, reserveDaysLeft, monthsUsed, year). |
| EP.5 | CostSnapshot evaluation | Save analysis | `costSnapshot.evaluation` includes full AI analysis: planName, planBundleCode, lifetimeSummary (5 fields), costTrajectory, trajectoryExplanation, yearlyHighlights[], categories[], savingsTips[], overallAssessment. |
| EP.6 | Supplement plan data | Save PDP+Medigap analysis | `costSnapshot.supplementPlanType` (e.g., "G") and `supplementPlanPremium` populated. |
| EP.7 | Lifetime totals preserved | Save analysis | `costSnapshot` includes lifetimeTotal, currentYearTotal, averageAnnual, projectionYears, lifetimePremiums, lifetimeOOP, lifetimeIrmaa, costTrajectory. |
| EP.8 | Backend document mapping | `POST /api/recommendation` with expanded data | Controller maps to MongoDB documents: `SelectedPlanDoc` (+7 fields), `CostSnapshotDoc` (+4 fields), `YearlyDetailDoc`, `CostEvaluationDoc`, `LifetimeSummaryDoc`, `YearlyHighlightDoc`, `CostCategoryDoc`, `SavingsTipDoc`, `PlanExpenseDoc`. |
| EP.9 | Round-trip fidelity | Save → `GET /api/recommendation/all` | Summary response includes correct `drugCount`, `planCount`, `hasCostSnapshot`, `lifetimeTotal` derived from saved documents. |

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
| SD.12 | Analyses loaded | Navigate to `/saved` with saved analyses | Analysis list shows cards with name, date, counts, and cost info. |
| SD.13 | No tab count badge | 2 saved analyses | Page shows list directly (no tabs/badges). |
| SD.14 | Completed analysis card | Analysis with cost snapshot | Green status badge "completed". Lifetime total displayed. `hasCostSnapshot` indicator shown. |
| SD.15 | In-progress analysis card | Analysis without cost snapshot | Amber status badge "in-progress". No lifetime total. |
| SD.16 | Drug and plan counts | Analysis with 3 drugs, 2 plans | Card shows "3 drugs" and "2 plans". |
| SD.17 | Empty analyses | No saved analyses | Empty state message displayed on the page. |
| SD.18 | Loading state | Page loading | Loading spinner shown during API calls. |

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
| SD.35 | Pharmacy tab | Click "Pharmacy" tab | Selected pharmacy displayed: name, address, distance. |
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
| SD.43 | Returns recommendation by ID | `GET /api/recommendation/{id}` with valid JWT (owner) | 200 OK. Full `RecommendationDetailResponse` including profile snapshot, drugs, pharmacy, plans, costSnapshot. |
| SD.44 | Not found | `GET /api/recommendation/{id}` (id does not exist) | 404 Not Found. |
| SD.45 | Unauthorized | `GET /api/recommendation/{id}` without JWT | 401 Unauthorized. |
| SD.46 | Cross-user isolation | User B requests recommendation owned by User A | 404 or 403. Cannot access another user's recommendation. |

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

## 18. Chatbot Orchestrator

### 18a. Conversation State & Session

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 18a.1 | New conversation | First `POST /api/chat/orchestrate` for user | `ConvStateDocument` created in MongoDB with `state: "idle"`, empty `pendingChanges`, 30-min TTL expiry. |
| 18a.2 | Resume conversation | Send message within 30 minutes of last | Same `ConvStateDocument` reused. State preserved from previous turn. |
| 18a.3 | TTL expired | Send message after 30 minutes of inactivity | Old state discarded. Fresh `ConvStateDocument` created. Reply: "Your previous session expired..." |
| 18a.4 | State transitions | Send "add Lipitor" → confirm → complete | State: idle → awaiting_drug_name → awaiting_confirmation → idle. Each step persisted to MongoDB. |
| 18a.5 | Empty message | `POST /api/chat/orchestrate` with `{ "message": "" }` | 400 Bad Request. |

### 18b. Recommendation Lifecycle

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 18b.1 | Create recommendation | "Create a recommendation" (no existing rec) | Profile, drugs, pharmacy, plans snapshots captured. `RecommendationDocument` created. Reply: "I've created your recommendation..." |
| 18b.2 | Create — already exists | "Create a recommendation" (rec already exists) | Reply: "You already have a recommendation. Would you like to replace it?" Awaiting confirmation. |
| 18b.3 | View summary | "Show my recommendation" (rec exists) | Reply: Markdown summary with profile, drugs, pharmacy, plan details. Disclaimer appended. |
| 18b.4 | View summary — none exists | "Show my recommendation" (no rec) | Reply: "No recommendation found. Would you like to create one?" |
| 18b.5 | Delete recommendation | "Delete my recommendation" | Delete confirm mode activated. Reply: "Type DELETE MY RECOMMENDATION to confirm." |
| 18b.6 | Confirm delete | Type "DELETE MY RECOMMENDATION" | `RecommendationDocument` deleted. Reply: "Recommendation deleted." State reset to idle. |
| 18b.7 | Cancel delete | Type anything other than exact phrase | Reply: "Delete cancelled." State returns to idle. |

### 18c. Profile & Drug Updates via Orchestrator

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 18c.1 | Change profile field | "Change my coverage year to 2026" | Confirmation prompt with delta preview. `awaitingConfirmation: true`. |
| 18c.2 | Confirm profile change | "yes" after profile change prompt | Profile field updated in recommendation. Delta applied. Reply confirms change. |
| 18c.3 | Cancel profile change | "no" after profile change prompt | Pending changes discarded. Reply: "Change cancelled." State returns to idle. |
| 18c.4 | Add drug | "Add Metformin to my drugs" | Drug validated. Confirmation prompt shown with cost delta. |
| 18c.5 | Remove drug | "Remove Lipitor" | Drug found in recommendation. Confirmation prompt with delta. |
| 18c.6 | Change pharmacy | "Switch to CVS Pharmacy" | Pharmacy lookup performed. Confirmation prompt with delta. |
| 18c.7 | Change plan | "Switch to plan H5521-001-0" | Plan validated. Confirmation prompt with delta. |

### 18d. Delta Calculation & Display

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 18d.1 | Cost increase | Proposed change increases lifetime cost | `DeltaDisplayComponent` shows red `trending_up` icon. Lifetime, this-year, PV columns show before → after. |
| 18d.2 | Cost decrease | Proposed change decreases lifetime cost | Green `trending_down` icon. Values show savings. |
| 18d.3 | No change | Proposed change has no cost impact | Gray `trending_flat` icon. |
| 18d.4 | Delta in response | Orchestrator response with `delta` field | `pendingDelta` signal set. `DeltaDisplayComponent` rendered inline in chat. |
| 18d.5 | Delta cleared | Confirmation or cancellation | `pendingDelta` reset to null. Delta card disappears. |

### 18e. Financial Projections & Funding

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 18e.1 | View projections | "Show my cost projections" | Markdown table with year-by-year costs. Financial disclaimer appended. |
| 18e.2 | View funding | "Show funding sources" | Markdown funding breakdown. Financial disclaimer appended. |
| 18e.3 | View summary with disclaimer | "Show my recommendation summary" | Summary includes disclaimer: "These projections are estimates..." |

### 18f. Help Menu

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 18f.1 | Help command | "help" or "what can I do?" | `displayData.type === 'help_menu'`. `HelpMenuComponent` rendered with 5 categories. |
| 18f.2 | Help chip click | Click "Add a drug" chip | `actionClicked` emits "Add a drug". ChatComponent sends message to orchestrator. |
| 18f.3 | Help dismissed | Any non-help message after help shown | `activeDisplayData` cleared. Help menu disappears. |

### 18g. Orchestrator Mode Toggle

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 18g.1 | No recommendation | Initial state, no recommendation loaded | Chat uses wizard/intent mode. No "Orchestrator" pill visible. |
| 18g.2 | Recommendation exists | `RecommendationStateService.hasRecommendation()` true | Chat switches to orchestrator mode. Emerald "Orchestrator" pill visible in header. Messages routed to `ChatOrchestratorService`. |
| 18g.3 | After delete | Delete recommendation via orchestrator | Mode reverts to wizard/intent. "Orchestrator" pill disappears. |
| 18g.4 | After create | Create recommendation via orchestrator | Mode active. "Orchestrator" pill appears. |

### 18h. Error Handling

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 18h.1 | Network timeout | Orchestrator request times out (`status === 0`) | Assistant: "Request timed out. Please try again." |
| 18h.2 | Server error | 500 response from `/api/chat/orchestrate` | Assistant: "Something went wrong. Please try again." |
| 18h.3 | Top-level catch | Unhandled exception in `ProcessMessageAsync` | 500 response with generic error message. Exception logged. |
| 18h.4 | Affirmative/negative patterns | "yes", "sure", "yep", "go ahead", "absolutely" | All recognized as affirmative by `IsAffirmative()` (21 patterns). |
| 18h.5 | Negative patterns | "no", "nah", "cancel", "never mind", "skip" | All recognized as negative by `IsNegative()` (19 patterns). |

---

## 19. SignalR Message Context Attachment

> Every chat message synced via SignalR now carries a `context` field — the Angular `router.url` at the time the message was created. This enables backend AI to receive page-location metadata for intent disambiguation.

### 19a. Context Field on Outgoing Messages

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 19a.1 | User message stamped with context | Send any chat message on `/medicare-analysis/drugs` | Message object in SignalR payload includes `context: "/medicare-analysis/drugs"`. |
| 19a.2 | Assistant reply stamped with context | Bot replies on `/medicare-analysis/profile` | Assistant message `context` is `/medicare-analysis/profile` (URL at reply time). |
| 19a.3 | System message stamped | UI action (e.g., select pharmacy) fires a system message on `/medicare-analysis/pharmacies` | System message `context: "/medicare-analysis/pharmacies"`. |
| 19a.4 | Context changes per navigation | Send message on `/medicare-analysis/drugs`, navigate to `/medicare-analysis/plans`, send again | First message has context `/medicare-analysis/drugs`, second has `/medicare-analysis/plans`. |
| 19a.5 | Context absent for old messages | Load a session whose messages were saved before context was added | Missing `context` fields deserialized as `null` without error. |

### 19b. Context Persistence (MongoDB)

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 19b.1 | Context saved to MongoDB | Send messages from various pages | `ChatMessageDoc.Context` in MongoDB stores the correct URL for each message. |
| 19b.2 | Context round-trips via session hydration | Send messages → refresh page | On reconnect, `ReceiveSession` pushes messages back including `context` values; chat history restored with context intact. |
| 19b.3 | Null context does not break hydration | Session with mixed null/non-null context messages | All messages hydrated correctly; null `context` fields remain null. |

### 19c. Context Forwarded to Intent Classifier

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 19c.1 | currentPage in intent request | Send chat message from any page | `POST /api/chat/intent` request body includes `currentPage` equal to the page's `router.url`. |
| 19c.2 | currentPage in orchestrator request | Send message while in orchestrator mode | `POST /api/chat/orchestrate` request body includes `currentPage`. |
| 19c.3 | Classification improved by context | On `/medicare-analysis/drugs`, type `"change my zip to 80113"` | `currentPage` included in request; AI classifies as `NAVIGATE_PROFILE` (not `UNKNOWN`). |

---

## 20. Chat Stability Fixes

> Regression scenarios covering specific bugs that were fixed: back-button navigation, pendingProfileModifyDetail scope, and user-input preservation.

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 20.1 | Back button from drugs to profile | Navigate to `/medicare-analysis/drugs` → click "Back" button in the analysis shell | Navigates to `/medicare-analysis/profile`. Profile page renders. No immediate re-navigation back to drugs. |
| 20.2 | Back button does not trigger stale save | Navigate back to profile via Back button (no profile changes made) | No save API call fired. No redirect to drugs. Profile form displayed with existing values. |
| 20.3 | Chat input preserved on non-profile pages | On `/medicare-analysis/drugs`, type `"Metformin 500mg"` and send | Chat history shows `"Metformin 500mg"` (user's exact input). Not replaced by `"Next"`. |
| 20.4 | Chat input preserved on pharmacies page | On `/medicare-analysis/pharmacies`, type `"select Walgreens"` and send | Chat history shows `"select Walgreens"`. Not replaced by `"Next"`. |
| 20.5 | pendingProfileModifyDetail clears on navigation | Set a pending profile modify on `/medicare-analysis/profile`, then navigate to `/medicare-analysis/drugs` | `pendingProfileModifyDetail` flag is cleared. Next message on drugs page is not intercepted by profile logic. |
| 20.6 | pendingProfileModifyDetail only active on profile page | `pendingProfileModifyDetail` is true, user is on `/medicare-analysis/drugs` | Profile-modification branch in `send()` is skipped. Message is routed normally through the drug-page flow. |
| 20.7 | Profile re-mount does not re-fire stale save | Navigate away from profile → navigate back | `lastHandledChatSaveId` initialized to current value at component construction; stale save request is not re-processed. |
| 20.8 | Profile re-mount does not re-fire stale discard | Navigate away from profile → navigate back | `lastHandledChatDiscardId` initialized to current value; stale discard request is not re-processed. |

---

---

## 21. Long Term Care (LTC) Cost Projection Wizard

### 21a. Navigation & Access

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| LTC.1 | Navigate to LTC wizard | Click LTC link/button (profile complete) | Navigated to `/long-term-care`. Shell loads. Step indicator shows: 1·Profile › 2·Care Type. |
| LTC.2 | Profile step links to main profile | Click Step 1 (Profile) in LTC shell | Navigated to `/long-term-care/profile`. LTC step indicator remains visible. |
| LTC.3 | Step indicator icons | Load `/long-term-care` | Step 1 uses `person` icon, Step 2 `health_and_safety`. |
| LTC.4 | Direct URL access | Navigate to `/long-term-care/care-type` directly | `LtcCareTypeStepComponent` loads. Shell step indicator active at step 2. |
| LTC.5 | Continue from Profile | Click Continue in footer (step 1) | `requestSaveFromChat()` triggered. Profile saved. Navigated to `/long-term-care/care-type`. |
| LTC.6 | Back from Care Type | Click Back in footer (step 2) | Navigated to `/long-term-care/profile`. |
| LTC.7 | Projection not in stepper | Load any LTC route | Stepper shows only 2 steps (Profile, Care Type). Projection is not a step. |
| LTC.8 | Footer hidden on projection | Navigate to `/long-term-care/projection` | Back/Continue footer bar hidden (projection is a result page). |

### 21b. Step 2 — Care Type

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| LTC.9 | Care Type step renders | Navigate to `/long-term-care/care-type` | `LtcCareTypeStepComponent` loads. Health profile quality selector (1–5) and care type year inputs shown. "Run Projection" button visible at bottom. |
| LTC.10 | Health profile quality 1 (Best) | Select quality level 1 | `ltcStateService.healthProfile()` set to 1. Selection highlighted. |
| LTC.11 | Health profile quality 5 (Minimum) | Select quality level 5 | `ltcStateService.healthProfile()` set to 5. |
| LTC.12 | Adult Day Care years | Enter 3 | `ltcStateService.adultDayYears()` set to 3. |
| LTC.13 | In-Home Care years | Enter 5 | `ltcStateService.homeCareYears()` set to 5. |
| LTC.14 | Nursing Care years | Enter 2 | `ltcStateService.nursingCareYears()` set to 2. |
| LTC.15 | Default state | First visit to Care Type step | All year inputs default to 0. No health quality pre-selected (default 1). |
| LTC.16 | Saved selections restored | Prior LTC selections saved → navigate to Care Type step | `GET /api/ltc/current` called on load. Inputs pre-populated with saved healthProfile and care type years. |
| LTC.17 | careTypeVisited set on entry | Navigate to Care Type step | `ltcStateService.careTypeVisited()` set to `true`. |
| LTC.18 | Run Projection button disabled — not visited | Profile complete but `careTypeVisited` is false | "Run Projection" button disabled. |
| LTC.19 | Run Projection button disabled — API calling | Click "Run Projection" while API is loading | Button disabled. Spinner shown. |
| LTC.20 | Run Projection button enabled | Profile complete + careTypeVisited + not calling API | "Run Projection" button enabled. |

### 21c. Run Projection (from Care Type step)

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| LTC.21 | Run Projection succeeds | Click "Run Projection" | `isCallingApi()` set to true. Spinner shown. `POST /api/long-term-care` called. On success: `ltcResult` populated, care-type selections saved via `PUT /api/ltc/current`, navigated to `/long-term-care/projection`. |
| LTC.22 | Profile data in request | Click "Run Projection" | Request includes `age`, `gender`, `state`, `zipcode`, `countyCode`, `lifeExpectancy`, `tobaccoUsage` from `ProfileService`. |
| LTC.23 | Care type data in request | Click "Run Projection" | Request includes `healthProfile` (1–5), `adultDayYears`, `homeCareYears`, `nursingCareYears` from `LtcStateService`. |
| LTC.24 | API error handling | Backend returns 500 | `isCallingApi()` reset to false. Spinner hidden. Error message shown. No navigation. |

### 21d. Projection Result Page

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| LTC.25 | Projection step renders | Navigate to `/long-term-care/projection` | `LtcProjectionStepComponent` loads. Results visible. |
| LTC.26 | Result display | Projection result loaded | Care cost breakdown shown: Adult Day Health Care, In-Home Care, Assisted Living, Nursing Care. Present values shown per category. |
| LTC.27 | Year-by-year expenses | Projection result loaded | Year-by-year expense entries displayed (one `LtcExpenseEntry` per year: `{ year: number; expense: number }`). |
| LTC.28 | No result state | Navigate to `/long-term-care/projection` without running projection | Empty state or "No projection data" message. |
| LTC.29 | Recalculate | Return to Care Type → adjust values → Run Projection again | New API call. `ltcResult()` updated. Old result replaced on projection page. |

### 21e. Backend: POST /api/long-term-care

| # | Scenario | Request | Expected Result |
|---|----------|---------|-----------------|
| LTC.30 | Successful projection | Valid `LongTermCareRequest` with JWT | 200 OK. Response includes cost fields for all four care categories (`adultDayHealthCare`, `homeCare`, `assistedCare`, `nursingCare`), present values per category, and year-by-year expense arrays. |
| LTC.31 | Unauthorized | `POST /api/long-term-care` without JWT | 401 Unauthorized. |
| LTC.32 | Life expectancy in response | Request with `lifeExpectancy: 95` | Response `lifeExpectancy: 95`. Year-by-year arrays have entries up to that age. |
| LTC.33 | Health profile quality impact | Same profile, `healthProfile: 1` vs `healthProfile: 5` | `healthProfile: 5` (Minimum quality) produces higher care costs in all categories. |
| LTC.34 | All response fields present | Valid request | Response includes: `age`, `healthProfile`, `gender`, `state`, `zipcode`, `countyCode`, `lifeExpectancy`, `tobaccoUsage`, all four care cost scalars, present values, and yearly expense arrays. |
| LTC.35 | API proxied correctly | Valid request | Backend delegates to `ILongTermCareService.GetProjectionAsync(request, userEmail)` with user email from JWT claim. |

### 21f. LTC Selections Persistence (PUT/GET /api/ltc/current)

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| LTC.36 | Save LTC selections | Run Projection or navigate away from Care Type | `PUT /api/ltc/current` called. Body includes `healthProfile`, `numberOfAdultDayHealthCareYears`, `numberOfHomeCareYears`, `numberOfNursingCareYears`, and optionally `ltcResultJson`. |
| LTC.37 | Load LTC selections | Navigate to LTC wizard (prior selections saved) | `GET /api/ltc/current` called. `LtcStateService` signals populated from returned `LtcCurrentResponse`. |
| LTC.38 | No prior selections | `GET /api/ltc/current` (user has no saved LTC data) | 404 Not Found. `LtcStateService` signals remain at defaults (0 years, health profile 1). |
| LTC.39 | Selections round-trip | Save selections → refresh page → reload | Selections restored from server. Care Type step pre-populated. |
| LTC.40 | Save unauthorized | `PUT /api/ltc/current` without JWT | 401 Unauthorized. |
| LTC.41 | Load unauthorized | `GET /api/ltc/current` without JWT | 401 Unauthorized. |
| LTC.42 | Per-user isolation | User A saves LTC selections → User B queries | User B receives 404 (their own data not found). User A's data not returned. |
| LTC.43 | Upsert behavior | Save LTC selections twice | Second `PUT` overwrites the first. `GET` returns only the latest values. |
| LTC.44 | Auto-save on step transition | Navigate from Care Type to Profile via Back button | Care-type selections saved via `LtcService.saveCurrent()` before navigation. |

### 21g. LTC Chat Wizard — Guided Flow

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| LTC.50 | Start LTC wizard via chat | Click "Long Term Analysis" mode button | User message "Long Term Analysis" added. Wizard starts in `LONG_TERM_ANALYSIS` mode. |
| LTC.51 | Profile step announced | LTC wizard starts (profile incomplete) | Assistant: `LTC_MESSAGES.START_PROFILE`. Navigated to `/long-term-care/profile`. |
| LTC.52 | Profile review announced | LTC wizard starts (profile already complete) | Assistant: `LTC_MESSAGES.PROFILE_REVIEW`. No navigation (already reviewing profile). |
| LTC.53 | Care Type step announced | Profile complete + ltcProfileIntroComplete | Assistant: `LTC_MESSAGES.CARE_TYPE_PROMPT`. Navigated to `/long-term-care/care-type`. |
| LTC.54 | Profile save advances to Care Type | On `/long-term-care/profile`, save profile via Continue | `ltcProfileIntroComplete` set to true. Navigated to `/long-term-care/care-type`. Assistant announces Care Type step. |
| LTC.55 | Profile save — no changes | On `/long-term-care/profile` (profile already saved) → Continue | `ltcProfileIntroComplete` set to true. Navigated to Care Type. No save API call. |
| LTC.56 | "Next" on LTC profile | On `/long-term-care/profile` → type "next" in chat | Triggers `requestSaveFromChat()`. Profile saves and advances to Care Type. |
| LTC.57 | "Next" on LTC care type | On `/long-term-care/care-type` → type "next" in chat | Assistant: `LTC_MESSAGES.LAST_STEP`. "Configure your care preferences and click **Run Projection**." |
| LTC.58 | Mode buttons hidden during wizard | LTC wizard active | Mode selection cards not visible. |
| LTC.59 | Step indicator visible | LTC wizard mode active | Step indicator shows "Profile › Care Type" (not visible in NONE mode). |

### 21h. LTC Chat Resume (Hard Refresh)

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| LTC.60 | Resume on LTC profile | Refresh browser on `/long-term-care/profile` | Wizard resumes in `LONG_TERM_ANALYSIS` mode. Assistant: `LTC_MESSAGES.RESUME_PROFILE`. |
| LTC.61 | Resume on LTC care type | Refresh browser on `/long-term-care/care-type` | Wizard resumes. `ltcProfileIntroComplete` set. Assistant: `LTC_MESSAGES.RESUME_CARE_TYPE`. |
| LTC.62 | Resume on LTC projection | Refresh browser on `/long-term-care/projection` | Wizard resumes. Assistant: `LTC_MESSAGES.RESUME_PROJECTION`. |
| LTC.63 | No startup greeting on LTC resume | Refresh on any `/long-term-care/*` route | Startup greeting pending flag NOT set. No mode chooser shown. |

### 21i. LTC Chat Step Navigation

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| LTC.70 | Navigate to care type via chat | On any LTC page → type "go to care type" | `TARGETED_STEP_PATTERN` matches "care type". `resolveLtcStepKeyword()` → step 2. Navigate to `/long-term-care/care-type`. |
| LTC.71 | Navigate to profile via chat | On any LTC page → type "go to profile" | Resolves to step 1. Navigate to `/long-term-care/profile`. |
| LTC.72 | Back navigation via chat | On `/long-term-care/care-type` → type "go back" | `BACK_PATTERN` matches. `handleLtcBackNavigation()` → navigate to `/long-term-care/profile`. Care-type selections saved before navigation. |
| LTC.73 | Back on step 1 — no action | On `/long-term-care/profile` → type "go back" | `BACK_PATTERN` matches. No back possible from step 1. Guidance message shown. |
| LTC.74 | Forward on last step | On `/long-term-care/care-type` → type "next" | Assistant: `LTC_MESSAGES.LAST_STEP`. No further advancement. |
| LTC.75 | "care-type" keyword | Type "navigate to care-type" | Keyword "care-type" resolved to step 2. |
| LTC.76 | "caretype" keyword | Type "go to caretype" | Keyword "caretype" resolved to step 2. |
| LTC.77 | Return route saved | On `/long-term-care/care-type` → navigate to profile via intent | `LtcStateService.returnRoute` saved as `/long-term-care/care-type`. |
| LTC.78 | Return route consumed | Navigate back from profile after LTC return route set | Returns to saved route. `returnRoute` cleared to null. |
| LTC.79 | Auto-save on targeted navigation | On Care Type → type "go to profile" | Care-type selections saved via `saveLtcCurrentStepAndNavigate()` before navigating. |

### 21j. LTC AI Intent Classification

| # | Scenario | Input | Expected Result |
|---|----------|-------|-----------------|
| LTC.80 | NAVIGATE_LTC_CARE_TYPE | `"go to care type"` on any page | Intent: `NAVIGATE_LTC_CARE_TYPE`. Navigate to `/long-term-care/care-type` (profile required). |
| LTC.81 | NAVIGATE_LTC_CARE_TYPE — no profile | `"go to care type"` (profile incomplete) | Assistant: `LTC_MESSAGES.REQUIRE_PROFILE`. Stays on current page. |
| LTC.82 | LTC_CARE_INPUT — on care type page | `"set nursing care to 5 years"` on `/long-term-care/care-type` | Intent: `LTC_CARE_INPUT`. Params: `ltcNursingCareYears: 5`. `pendingChatCareType` signal set. Form patches via component effect. Assistant: AI confirmationMessage + `LTC_MESSAGES.CARE_TYPE_UPDATED`. |
| LTC.83 | LTC_CARE_INPUT — off care type page | `"set nursing care to 5 years"` on any other page | Intent: `LTC_CARE_INPUT`. State updated directly. Navigate to `/long-term-care/care-type`. |
| LTC.84 | LTC_CARE_INPUT — multiple fields | `"quality best and home care 3 years"` | Intent: `LTC_CARE_INPUT`. Params: `ltcHealthProfile: 1, ltcHomeCareYears: 3`. Both fields extracted and applied. |
| LTC.85 | LTC_CARE_INPUT — adult day + nursing | `"set adult day care to 2 years and nursing to 4"` | Intent: `LTC_CARE_INPUT`. Params: `ltcAdultDayYears: 2, ltcNursingCareYears: 4`. |
| LTC.86 | ACTION_RUN_LTC_PROJECTION — success | `"run projection"` on care-type page (profile complete + careTypeVisited) | Intent: `ACTION_RUN_LTC_PROJECTION`. Assistant: `LTC_MESSAGES.PROJECTION_RUNNING`. API called. On success: `LTC_MESSAGES.PROJECTION_COMPLETE`. Navigate to `/long-term-care/projection`. |
| LTC.87 | ACTION_RUN_LTC_PROJECTION — no profile | `"run projection"` (profile incomplete) | Assistant: `LTC_MESSAGES.REQUIRE_PROFILE`. No API call. |
| LTC.88 | ACTION_RUN_LTC_PROJECTION — care type not visited | `"run projection"` (profile ok, careTypeVisited=false) | Assistant: `LTC_MESSAGES.REQUIRE_CARE_TYPE`. No API call. |
| LTC.89 | ACTION_RUN_LTC_PROJECTION — API failure | `"run projection"` → API returns error | Assistant: `LTC_MESSAGES.PROJECTION_FAILED`. No navigation. |
| LTC.90 | Health quality natural language | `"set quality to good"` | Intent: `LTC_CARE_INPUT`. Params: `ltcHealthProfile: 2` ("good"→2). |
| LTC.91 | "calculate ltc costs" | `"calculate ltc costs"` | Intent: `ACTION_RUN_LTC_PROJECTION`. |
| LTC.92 | "project my care costs" | `"project my care costs"` | Intent: `ACTION_RUN_LTC_PROJECTION`. |

### 21k. LTC Page Context (Backend AI Disambiguation)

| # | Scenario | Request | Expected Result |
|---|----------|---------|-----------------|
| LTC.95 | LTC profile: field change | `{ "message": "change my zip to 80113", "currentPage": "/long-term-care/profile" }` | Returns `NAVIGATE_PROFILE` with `params.zipCode: "80113"`. |
| LTC.96 | LTC profile: next/continue | `{ "message": "continue", "currentPage": "/long-term-care/profile" }` | Returns `NAVIGATE_LTC_CARE_TYPE`. |
| LTC.97 | LTC care type: care values | `{ "message": "set nursing to 3 years", "currentPage": "/long-term-care/care-type" }` | Returns `LTC_CARE_INPUT` with `params.ltcNursingCareYears: 3`. |
| LTC.98 | LTC care type: run projection | `{ "message": "run projection", "currentPage": "/long-term-care/care-type" }` | Returns `ACTION_RUN_LTC_PROJECTION`. |
| LTC.99 | LTC care type: go back | `{ "message": "go back to profile", "currentPage": "/long-term-care/care-type" }` | Returns `NAVIGATE_PROFILE`. |

### 21l. LTC Chat Messages

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| LTC.100 | Start profile message | LTC wizard starts, profile incomplete | Assistant: "Let's start your Long-Term Care analysis. Please complete your profile information to proceed." |
| LTC.101 | Profile review message | LTC wizard starts, profile complete | Assistant: "Your profile looks good! Click **Continue to Care Type** in the footer to proceed, or edit the form if needed." |
| LTC.102 | Care type prompt | Navigate to care type step | Assistant: "Now configure your long-term care preferences. Set the quality of care and years for each care type, then click **Run Projection** when ready." |
| LTC.103 | Care type updated | Chat populates care-type field | Assistant: AI confirmation + "Care type updated in the form." |
| LTC.104 | Last step message | Forward navigation on care type | Assistant: "You're on the last step. Configure your care preferences and click **Run Projection**, or say **run projection** in the chat." |
| LTC.105 | Projection running | Run projection via chat | Assistant: "Running your long-term care projection…" |
| LTC.106 | Projection complete | Projection succeeds | Assistant: "Projection complete! Navigating to your results." |
| LTC.107 | Projection failed | Projection API fails | Assistant: "Failed to run projection. Please try again." |
| LTC.108 | Require profile | Attempt projection without profile | Assistant: "Please complete your profile first before running a projection." |
| LTC.109 | Require care type | Attempt projection without care type visited | Assistant: "Please configure your care type first before running a projection." |

---

## 22. Serilog MongoDB Logging

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 22.1 | Logs written to MongoDB | Start app, trigger any API request | `logs` collection in MongoDB receives structured BSON log entries within 5 seconds (batch period). |
| 22.2 | MinimumLevel filtering | Trigger EFCore debug-level log | Not written to MongoDB (override: Warning). Only Warning+ from EFCore appear. |
| 22.3 | File fallback | Stop MongoDB, start app | Logs written to `Logs/log-{date}.txt` file. Console output still present. |
| 22.4 | Bootstrap logger | Introduce startup error before host builds | Error logged to console + file (bootstrap logger), not to MongoDB (not yet configured). |
| 22.5 | Silent catch — AuthService | Submit invalid password-reset token | `LogWarning` entry: "Password-reset token validation failed" with exception stack trace in MongoDB `logs`. |
| 22.6 | Silent catch — ChatSessionRepository | Insert malformed BSON chat session doc, then fetch | `LogWarning` entry: "Malformed chat-session document for UserId=…" with `BsonSerializationException` in MongoDB `logs`. |

---

## 23. Part D Gap Fill for MA Plans

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 23.1 | MA pre-population triggers Part D gap | Save recommendation with MA plan → load it from saved page → navigate to plans | Part D gap section loads and displays PDP gap plans for the pre-populated MA plan. |
| 23.2 | MA already in list | Save rec with MA plan → reload plans page → reconcile finds MA in existing list | `ensurePartDGapLoadForMA()` fires, Part D gap section populated. |
| 23.3 | MA matched by planId | Save rec with MA plan → navigate away and back | MA plan matched by `planId`, Part D gap section populated. |
| 23.4 | PDP selection — no gap fill | Select a PDP plan (not MA) | Part D gap section does NOT load (only triggered for MA plans). |

---

## 24. Cost Projections Navigation Guard

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 24.1 | Direct URL with no projection | Navigate directly to `/medicare-analysis/cost-projections` with no prior cost evaluation | Redirected to `/medicare-analysis/plans`. State reset. |
| 24.2 | Valid projection | Run cost evaluation → navigate to cost-projections page | Page renders with all 5 charts + expense table + summary strip. |
| 24.3 | Browser refresh after evaluation | Run evaluation → F5 on cost-projections page | Redirected to `/medicare-analysis/plans` (signal state lost). |
| 24.4 | Back navigation | Use browser back from another page to cost-projections without state | Redirected to `/medicare-analysis/plans`. |

---

## 25. Recommendation Detail — Redesign

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 25.1 | Hero header | Open `/saved/:id` for a Medicare rec | Dark gradient hero bar with type badge ("Medicare"), back button, save date, recommendation name. |
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
| 26.1 | Overview tab KPI strip | Compare 2 LTC recs → Overview tab | KPI strip shows delta values (lifetime cost difference, care years, etc.) with color-coded icons. Winner banner shown. |
| 26.2 | Profile diffs | Overview → profile diffs section | Side-by-side profile rows with green/red highlighting on differing fields. |
| 26.3 | Care config table | Click Care Config tab | Table rows: Health Profile, Adult Day Years, Home Care Years, Nursing Care Years with values for both recs. Cost totals shown below. |
| 26.4 | Cost Analysis tab | Click Cost Analysis tab | Cost categories with progress bars, savings tips, trajectory assessment. |

---

## 27. Compare-Cross — Redesign

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 27.1 | Cross-type disclaimer | Compare Medicare vs LTC rec → Overview tab | Yellow disclaimer banner warning about cross-type comparison limitations. |
| 27.2 | KPI strip + winner | Overview tab | KPI delta cards + winner card with savings amount. |
| 27.3 | Profile diffs | Overview → profile section | Side-by-side profile comparison with grouped rows. |
| 27.4 | Cost Summary tab | Click Cost Summary tab | Side-by-side evaluation cards for each rec. Info note about comparison caveats. |

---

## 28. Uppercase Recommendation Names

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 28.1 | Saved list cards | Navigate to `/saved` page | All `rec.name` text on recommendation cards displayed in uppercase. |
| 28.2 | Compare slot 1 | Add rec to compare basket | Name in compare slot 1 displayed in uppercase. |
| 28.3 | Compare slot 2 | Add second rec to compare basket | Name in compare slot 2 displayed in uppercase. |

---

← [Chapter 9 — Roadmap](ch09-roadmap.md) | [Table of Contents](APPLICATION_BLUEPRINT.md)
