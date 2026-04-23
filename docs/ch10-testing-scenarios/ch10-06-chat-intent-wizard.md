# Chapter 10.6 — Chat Intent Routing & Guided Wizard

> Cross-cutting: Chat panel, wizard flow, intent classification, free-form routing, SignalR, and chat stability.

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
| 12c.39 | Cross-page: profile field on pharmacies — no pharmacies selected | On `/medicare-analysis/pharmacies` (0 selected) → type `"change magitier 4"` | No pharmacy save call. Chat: AI confirmation message only (no "pharmacies saved" prefix). Navigated to `/medicare-analysis/profile`. MAGI tier pre-filled. |
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

### 12j. Orchestrator URL Guard (Dead — UI Never Reaches Orchestrator)

> **Note:** These scenarios are no longer reachable. The chat panel is hidden on `/medicare-analysis/cost-projections` (see `DashboardComponent.isChatRoute()`) — the only page where the orchestrator guard would have passed. On all active wizard pages (`/drugs`, `/pharmacies`, `/plans`, `/profile`), `routeToOrchestrator()` already returns false by design. The `POST /api/chat/orchestrate` endpoint is never called by the frontend under any normal navigation path. Retained for backend API testing only.

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 12j.0 | Orchestrator skips on analysis profile | Recommendation exists + on `/medicare-analysis/profile` → type a message | `routeToOrchestrator()` returns false (same guard pattern as other analysis wizard routes). |
| 12j.1 | Orchestrator skips on drugs page | Recommendation exists + on `/medicare-analysis/drugs` → type "select Metformin" | `routeToOrchestrator()` returns false. Message routed to `routeToDrugSelection()`. Drug selected correctly. |
| 12j.2 | Orchestrator skips on pharmacy page | Recommendation exists + on `/medicare-analysis/pharmacies` → type "select Walgreens" | `routeToOrchestrator()` returns false. Message routed to `routeToPharmacySelection()`. Pharmacy selected correctly. |
| 12j.3 | Orchestrator skips on plans page | Recommendation exists + on `/medicare-analysis/plans` → type "select the Humana plan" | `routeToOrchestrator()` returns false. Message routed to `routeToPlanSelection()`. Plan selected correctly. |
| 12j.4 | Orchestrator active on other pages | Recommendation exists + on `/medicare-analysis/cost-projections` → type any message | `routeToOrchestrator()` processes the message and sends to orchestrator. |
| 12j.5 | Orchestrator active on dashboard | Recommendation exists + on `/` → type any message | `routeToOrchestrator()` processes the message. |

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

← [Testing Index](../ch10-testing-scenarios/ch10-testing-scenarios.md)
