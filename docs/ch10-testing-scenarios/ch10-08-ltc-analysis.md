# Chapter 10.8 — Long-Term Care (LTC) Analysis

> Routes: `/long-term-care/profile`, `/long-term-care/care-type`, `/long-term-care/projection`
> Covers LTC navigation, care type step, projection, backend API, selections persistence, chat wizard, chat resume, step navigation, AI intent classification, page context, and chat messages.

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

← [Testing Index](../ch10-testing-scenarios/ch10-testing-scenarios.md)
