# Chapter 8.6 — Long Term Care (LTC) Features

> LTC cost projection wizard and full chat integration for the LTC workflow.

← [Feature Catalog Index](../ch08-feature-catalog/ch08-feature-catalog.md)

---

## ✅ Long Term Care (LTC) Cost Projection Wizard

- **What:** A 2-step wizard with chat-driven navigation that projects lifetime Long Term Care costs using the Financial Planner LTC API. Users configure care type years and quality of care, then run a projection from the Care Type step. The projection result is displayed on a dedicated result page (not a stepper step).
- **Flow:**
  1. **Step 1 — Profile:** Reuses `UserProfileComponent` at `/long-term-care/profile`. Reads DOB, gender, state, ZIP, countyCode, lifeExpectancy, tobaccoStatus.
  2. **Step 2 — Care Type:** `LtcCareTypeStepComponent` — user selects quality of care level (1=Best … 5=Minimum) and number of years for each care type: Adult Day Health Care, In-Home Care, Nursing Care. Contains a **"Run Projection"** button that triggers the API call and navigates to the result page.
  3. **Projection (result page):** `LtcProjectionStepComponent` at `/long-term-care/projection` — reads `LtcProjectionResponse` from `LtcStateService.ltcResult`, shows totals and present values for each care category, plus year-by-year expense lists. Not a stepper step — the stepper only shows Profile and Care Type.
- **Projection Gating:** The "Run Projection" button is enabled only when the profile is complete AND the Care Type step has been visited (`careTypeVisited` signal). This ensures defaults are valid once the user has entered the step.
- **Auto-Save:** Step transitions auto-save the current step's data. Navigating away from Care Type saves care-type selections to the database via `LtcService.saveCurrent()`. Profile saves via `requestSaveFromChat()` on Continue.
- **Backend:**
  - **Domain:** `LongTermCareRequest`, `LongTermCareResponse`, `LtcExpenseEntry` in `Models/LongTermCare.cs`.
  - **Interface:** `ILongTermCareService.CalculateAsync(request, cancellationToken)`.
  - **Service:** `LongTermCareService` in `Infrastructure/FinancialPlanner/` — HTTP POST with Basic auth token to LTC endpoint.
  - **Controller:** `LongTermCareController` — `[Authorize]` `POST /api/long-term-care`. Reads user profile from DB, builds `LongTermCareRequest`, delegates to service.
- **Frontend:**
  - **Shell:** `LtcShellComponent` — 2-step stepper (Profile → Care Type), `<router-outlet>`, Back/Continue nav bar. Continue button triggers `profileService.requestSaveFromChat()` on step 1. No "Calculate" button in the footer — projection is triggered from Care Type step.
  - **Care Type Step:** `LtcCareTypeStepComponent` — health profile quality selector (1–5), care-type year inputs. `runProjection()` method builds `LtcProjectionRequest` from profile + state signals, calls `LtcService.calculate()`, saves result via `LtcService.saveCurrent()`, navigates to `/long-term-care/projection`. `canRunProjection` getter requires `profileComplete && careTypeVisited && !isCallingApi`. Consumes `pendingChatCareType` signal via `effect()` to apply chat-driven form patches.
  - **State:** `LtcStateService` — signals: `currentStep` (`1|2`), `healthProfile`, `adultDayYears`, `homeCareYears`, `nursingCareYears`, `careTypeVisited` (set true on step 2 entry), `returnRoute` (saved URL for return navigation), `pendingChatCareType` (chat-driven field updates consumed by Care Type component), `isCallingApi`, `ltcResult`. `resetAll()` resets all signals to defaults. `PendingChatCareType` interface defines optional fields: `healthProfile?`, `adultDayYears?`, `homeCareYears?`, `nursingCareYears?`.
  - **HTTP Service:** `LtcService.calculate(request)` → `POST /api/long-term-care`. `LtcService.saveCurrent()` → `PUT /api/ltc/current`.
  - **Models:** `LtcProjectionRequest`, `LtcProjectionResponse`, `LtcExpenseEntry` in `models/ltc.model.ts`.
- **Route:** `/long-term-care` — guarded by `profileCompleteGuard`. Visible in dashboard via route navigation.

---

## ✅ LTC Chat Integration (Wizard, Navigation & AI Intents)

- **What:** Full chat-driven interaction for the LTC wizard — matching the Medicare analysis chat experience. Users can navigate between LTC steps, populate care-type fields, and trigger projections via natural language.
- **Chat Wizard (LONG_TERM_ANALYSIS mode):**
  - `ChatWizardService` extended with `LONG_TERM_ANALYSIS` mode and 3 LTC steps: `LTC_PROFILE`, `LTC_PROFILE_REVIEW`, `LTC_CARE_TYPE`.
  - `ltcProfileIntroComplete` signal — set when user clicks Continue from profile step on LTC route.
  - Computed `currentStep`: if profile incomplete → `LTC_PROFILE`; if profile complete but intro not done → `LTC_PROFILE_REVIEW`; else → `LTC_CARE_TYPE`.
  - `startLtcAnalysis()` / `resumeLtcAnalysis()` methods for starting and resuming the LTC wizard.
  - `ChatComponent` handles `selectMode('LONG_TERM_ANALYSIS')` → calls `beginLtcAnalysisFlow()` (resolves greeting + starts wizard). Auto-announces steps with appropriate `LTC_MESSAGES` constants.
  - Resume on hard refresh: detects LTC route, checks `ltcProfileIntroComplete` gate, posts resume-aware messages (`RESUME_PROFILE`, `RESUME_CARE_TYPE`, `RESUME_PROJECTION`).
  - `UserProfileComponent` — both save paths (no-changes + successful-save) check for LTC route → set `ltcProfileIntroComplete`, navigate to `LTC_CARE_TYPE` step instead of drugs.
- **Chat Step Navigation:**
  - `ChatNavigationFlowService` — `LTC_STEP_LABELS` (1→Profile, 2→Care Type), `LTC_KEYWORD_TO_STEP` (profile→1, care type/care-type/caretype→2), `LTC_STEP_ROUTES` (1→LTC_PROFILE, 2→LTC_CARE_TYPE).
  - `handleLtcStepNavigation(step)` — validate prerequisites (profile complete for forward nav), auto-save care-type on leave, navigate.
  - `handleLtcBackNavigation()` — step 2→1 with care-type save.
  - `handleLtcForwardNavigation()` — on step 2 shows "last step" message; on step 1 saves and advances.
  - `saveLtcReturnRoute()` — captures current LTC URL in `LtcStateService.returnRoute` for later return.
  - `saveLtcCurrentStepAndNavigate()` — saves care-type to DB when leaving step 2.
  - `handleReturnNavigation()` — checks both `MedicareStateService.returnRoute()` and `LtcStateService.returnRoute()`.
  - `TARGETED_STEP_PATTERN` regex updated to include `care[\s-]?type`.
- **Chat Intent Routing:**
  - `ChatRouterService.route()` — detects `onLtc = router.url.startsWith(AppRoutes.abs.LTC)`. Targeted step matches dispatch to `resolveLtcStepKeyword()` + `handleLtcStepNavigation()`. Back pattern dispatches to `handleLtcBackNavigation()`.
  - LTC "next" handling in `ChatComponent.send()`: on LTC profile → `requestSaveFromChat()`, on LTC care type → `handleLtcForwardNavigation()`.
- **3 New AI Intents:**
  - `NAVIGATE_LTC_CARE_TYPE` — navigate to care type step (with profile check).
  - `LTC_CARE_INPUT` — populate care-type fields from chat. Params: `ltcHealthProfile` (1–5), `ltcAdultDayYears` (0–20), `ltcHomeCareYears` (0–20), `ltcNursingCareYears` (0–20).
  - `ACTION_RUN_LTC_PROJECTION` — trigger projection from chat. Validates profile + careTypeVisited.
- **Care Type Flow Service (`ChatLtcCareTypeFlowService`):**
  - `handleCareTypeInput(params, confirmationMessage)` — on care-type page, uses `pendingChatCareType` signal (consumed by component `effect()`); off-page, updates state directly + navigates.
  - `handleRunProjection()` — validates prerequisites, builds LTC payload, calls API, saves result, navigates to projection page. Uses `LTC_MESSAGES.PROJECTION_RUNNING/COMPLETE/FAILED`.
- **LTC Messages (`LTC_MESSAGES` constant):** 12 entries covering the full wizard lifecycle: `START_PROFILE`, `PROFILE_REVIEW`, `CARE_TYPE_PROMPT`, `CARE_TYPE_UPDATED`, `REQUIRE_PROFILE`, `REQUIRE_CARE_TYPE`, `LAST_STEP`, `PROJECTION_RUNNING`, `PROJECTION_COMPLETE`, `PROJECTION_FAILED`, `RESUME_PROFILE`, `RESUME_CARE_TYPE`, `RESUME_PROJECTION`.
- **Backend (AI Prompt Updates):**
  - `chat-intent-system.txt` — 3 LTC intent definitions, LTC parameter extraction rules, 4 LTC params in JSON format, 7 LTC few-shot examples.
  - `PageContextBuilder.cs` — 2 new page context entries: `/long-term-care/profile` (profile field changes + next/continue guidance) and `/long-term-care/care-type` (care-type values → `LTC_CARE_INPUT`, run projection → `ACTION_RUN_LTC_PROJECTION`, go back → `NAVIGATE_PROFILE`).
  - `ChatIntentDtos.cs` — 4 nullable int properties: `LtcHealthProfile`, `LtcAdultDayYears`, `LtcHomeCareYears`, `LtcNursingCareYears`.

---

← [Feature Catalog Index](../ch08-feature-catalog/ch08-feature-catalog.md) | [← Chat Features](ch08-05-chat-features.md) | [Next: Infrastructure →](ch08-07-infrastructure.md)
