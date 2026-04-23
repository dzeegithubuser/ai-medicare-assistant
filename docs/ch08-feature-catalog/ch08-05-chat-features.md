# Chapter 8.5 — Chat Features

> Chat intent routing, guided wizard, and all chat-based selection flows.

← [Feature Catalog Index](../ch08-feature-catalog/ch08-feature-catalog.md)

---

## ✅ Chat Intent Routing & Guided Wizard
- **What:** The chat panel now supports two interaction modes: (A) a **Guided Chat Sequence** (wizard) that walks the user through the Medicare or LTC analysis steps, and (B) **Free-form Intent Routing** where AI classifies user messages into 20 intents and routes them to navigation, plan section switching, LTC care-type input, actions, or the drug analysis flow.
- **Feature A — Guided Wizard:**
  - **Startup:** Chat shows greeting message with two mode selection cards: "Medicare Analysis" (starts wizard) and "Long Term Analysis" (starts LTC wizard). Mode buttons gated behind `isProfileComplete()` — only appear after profile API resolves.
  - **Immediate Start:** Clicking "Medicare Analysis" starts wizard flow immediately without recommendation chooser checks.
  - **Fresh Flow Reset:** On Medicare mode click, chat clears prior carried flow state (confirmed drugs, selected pharmacies, pharmacy-confirmed flag, and plan selections) before starting.
  - **Wizard Steps:** AWAITING_MODE → PROFILE → DRUGS_PHARMACIES → PLANS → ANALYSIS → COMPLETE. Each step is announced via an assistant message with auto-navigation to the relevant route.
  - **Auto-Advance:** `ChatWizardService.hasNewStep` computed signal detects when completion signals fire (profile saved, drugs confirmed, pharmacy selection confirmed, plan loaded, cost projection done). `ChatComponent` watches via `effect()` and auto-announces the next step. Wizard uses `pharmacySelectionConfirmed` (not `hasSelectedLookupPharmacies`) to prevent auto-advance on first pharmacy checkbox — user must explicitly click "Continue to Plans" or use chat intent.
  - **Reset:** `MedicareStateService.resetAll()` increments `wizardResetTrigger`, which `ChatComponent` watches to call `wizard.reset()` — returning to mode selection.
- **Feature B — Free-form Intent Routing:**
  - **AI Classification:** User types naturally → `POST /api/chat/intent` → `ChatIntentService` (backend) classifies into one of 20 intents using Anthropic Claude. System prompt loaded from `Prompts/system/chat-intent-system.txt` (file-based, not inline).
  - **Current intents:** Navigation (profile/drugs/pharmacies/plans/cost/saved/ltc-care-type), section switching, LTC care input, LTC projection, reset/save analysis/run analysis, sign out/help, and drug-input fallback intents.
  - **Intent Prerequisite Guards:** All navigation intents now enforce a profile-complete gate before proceeding. `NAVIGATE_ANALYSIS_DRUGS` and `NAVIGATE_PHARMACIES` each require profile complete (redirects to `/medicare-analysis/profile` with message if not). `NAVIGATE_PLANS`, `SWITCH_TO_PDP`, `SWITCH_TO_MA` enforce the full chain: (1) profile complete, (2) at least one drug confirmed, (3) at least one pharmacy selected. `NAVIGATE_COST_PROJECTIONS` adds a further gate: complete plan selection (`MedicareStateService.hasCompletePlanSelection()`). Each unmet prerequisite shows a descriptive assistant message and redirects to the appropriate step.
  - **Plan Section Switching:** `SWITCH_TO_PDP` / `SWITCH_TO_MA` intents (after passing prerequisites) set `activeSection` via `state.setActiveSection()`, set `pharmacySelectionConfirmed`, and navigate to `/medicare-analysis/plans`. If already on the requested section, shows "already viewing" message.
  - **Cross-Page Drug Search:** When a drug name (e.g. "add metformin") is typed on the pharmacy page, the `DRUG_INPUT` intent is detected and reclassified as `NAVIGATE_ANALYSIS_DRUGS`. `MedicareStateService.pendingCrossPageDrugSearch` is set to the original text before navigation. On `NavigationEnd` to `/medicare-analysis/drugs`, `ChatComponent` fires `runDrugFlow(text)` automatically — the user sees suggestion chips appear within 50 ms of page load. Pure navigation phrases ("go to drug") produce `NAVIGATE_ANALYSIS_DRUGS` directly — `pendingCrossPageDrugSearch` is NOT set, so the page opens blank with no search.
  - **Pharmacy-Save on Profile Redirect:** When `NAVIGATE_PROFILE` fires from the pharmacies page and the user has selected pharmacies, `recState.savePharmacySelection()` is called before navigation. The chat message prefixes a "Your N selected pharmacies have been saved." confirmation so the user knows their picks are preserved.
  - **Parameter Extraction:** AI extracts profile fields for chat-driven profile updates and analysis-save metadata.
  - **Confirmation Messages:** AI generates short, friendly confirmation text (max ~15 words) shown in chat.
  - **Fallback:** On classification error, falls back to drug name suggestion flow.
- **Return Route:** When navigating away from analysis (e.g., to profile), `ChatComponent.saveReturnRoute()` captures the current `/medicare-analysis/*` URL in `MedicareStateService.returnRoute`. Header-initiated profile edit from analysis also stores the same return route. `UserProfileComponent` reads this on save/close and navigates back to the saved route instead of the default `/medicare-analysis`.
- **Impact-aware profile change handling:** If profile changes affect analysis assumptions (demographic/tax/location/coverage inputs), downstream state is invalidated after save (pharmacy/plans/cost), while confirmed drugs are retained.
- **Pharmacy Selection Gating:** `pharmacySelectionConfirmed` signal prevents the wizard from auto-advancing to plans when the first pharmacy is selected. User must explicitly click "Continue to Plans" in the analysis shell or use a chat intent to proceed.
- **Backend:**
  - **DTOs:** `ChatIntentRequest` (Message, IsProfileComplete, CurrentPage?), `ChatIntentResponse` (Intent, Params, ConfirmationMessage), `ChatIntentParams` (profile-related fields + analysis metadata + 4 LTC fields: LtcHealthProfile, LtcAdultDayYears, LtcHomeCareYears, LtcNursingCareYears) — in `Application/DTOs/ChatIntentDtos.cs`. `CurrentPage` carries the Angular `router.url` so the backend can apply page-specific disambiguation via `PageContextBuilder`.
  - **Service:** `ChatIntentService` in `Application/Services/` — injects `IChatClient` (M.E.AI), loads system prompt from `Prompts/system/chat-intent-system.txt` at construction time, calls `IChatClient.GetResponseAsync()`, parses JSON response (strips markdown fences), returns `ChatIntentResponse`. Falls back to `UNKNOWN` on error. Works with whichever AI provider is active.
  - **Controller:** `ChatIntentController` at `api/chat` route — `[Authorize]`, `POST intent` endpoint, thin delegation to `ChatIntentService`.
  - **DI:** `AddScoped<ChatIntentService>()` in `Program.cs`.
- **Frontend:**
  - **`ChatIntentService`** (`services/chat-intent.service.ts`) — HTTP service calling `POST /api/chat/intent`. Defines `ChatIntent` type (union of 20 strings, including `ACTION_HELP`, `NAVIGATE_LTC_CARE_TYPE`, `LTC_CARE_INPUT`, `ACTION_RUN_LTC_PROJECTION`), `ChatIntentResponse`, `ChatIntentParams` (11 profile fields + 4 LTC fields) interfaces.
  - **`ChatWizardService`** (`services/chat-wizard.service.ts`) — Reactive wizard state. `WizardMode` (`NONE`/`MEDICARE_ANALYSIS`/`LONG_TERM_ANALYSIS`). `WizardStep` (6 Medicare + 3 LTC values). Computed `currentStep` derived from mode-specific signals. For `LONG_TERM_ANALYSIS`: checks `isProfileComplete()` → `ltcProfileIntroComplete` → returns `LTC_PROFILE`/`LTC_PROFILE_REVIEW`/`LTC_CARE_TYPE`. For `MEDICARE_ANALYSIS`: checks profile, drugs, pharmacy, plans, cost. `startLtcAnalysis()` / `resumeLtcAnalysis()` for LTC mode. `hasNewStep` triggers auto-advance for both modes. `markStepAnnounced()` prevents duplicate messages.
  - **`ChatComponent` / `ChatRouterService`** — `ChatRouterService` handles all message routing. `send()` delegates to `route()` which dispatches through contextual branches: pending confirmations, orchestrator (guarded by route), profile extraction, plan/drug/pharmacy selection handlers, then intent classifier. **Action bypass:** app-level commands (save/run/reset analysis, sign out, help, show saved) bypass page-specific handlers and go directly to intent routing. **LTC routing:** detects `onLtc` via URL prefix, dispatches targeted steps to `resolveLtcStepKeyword()` + `handleLtcStepNavigation()`, back to `handleLtcBackNavigation()`, and 3 new intents to `ChatLtcCareTypeFlowService`.
  - **`ChatLtcCareTypeFlowService`** (`services/chat-ltc-care-type-flow.service.ts`) — Handles chat-driven care-type form population and projection. `handleCareTypeInput()` uses `pendingChatCareType` signal on care-type page or direct state update + navigate if off-page. `handleRunProjection()` validates profile + careTypeVisited, builds LTC payload, calls API, saves, navigates to projection.
  - **`MedicareStateService`** — Maintains wizard/session signals including `wizardResetTrigger`, `pharmacySelectionConfirmed`, `returnRoute`, `pendingDrugSelection`, `pendingPharmacySelection`, and `pendingCrossPageDrugSearch` (drug text stored for cross-page auto-search after navigation to drugs).
  - **`AnalysisShellComponent`** — Four-step shell (Profile → Drugs → Pharmacies → Plans). `goNext()` sets `pharmacySelectionConfirmed` when advancing from Pharmacies to Plans. Default child route is `profile`. Emits system messages: "Navigated to {step}" on `goNext()`, "Started a new analysis" on `startNewAnalysis()` (resets state and navigates to `/medicare-analysis/profile`).
  - **`ProfileService`** — Added `pendingPrefill` signal (`Record<string, unknown> | null`) for chat-driven profile pre-fill, `pendingChatProfileData` signal for confirmed chat profile extraction, `missingRequiredFields` signal published by `UserProfileComponent`. Consumed by `UserProfileComponent` on init.
  - **`UserProfileComponent`** — Injects `MedicareStateService`. `save()` emits "Profile saved" system message, navigates to `returnRoute`. `effect()` watches `pendingChatProfileData` → patches form + triggers cascading lookups (ZIP→county, DOB→age, taxFiling→MAGI). `updateMissingFields()` publishes to `missingRequiredFields` signal. Prefill consumer handles all profile fields via `Record<string, unknown>`.
  - **`DrugsStepComponent`** — Watches `pendingDrugSelection` and applies chat-driven selection commands with fuzzy matching for drug names/forms/strengths. Supports select, confirm_all, remove, and edit actions.
  - **`PlanRecommendationComponent`** — Plan page redesigned: no default section on landing. Shows two choice buttons (PDP / MA). Single full-width section after selection. "Switch to..." button with warning popup only when a plan is already selected in the current section. Emits system messages on: plan selection (Part D, Medigap, MA), section switching, cost calculation.
- **UI Action Tracking (System Messages):**
  - **What:** Key UI button actions are tracked as `system` role messages in the chat panel, providing the AI and the user visibility into actions performed via the UI (not chat).
  - **Rendering:** System messages appear as centered pill-shaped badges with a `touch_app` icon, grey background, muted text — visually distinct from user (cyan, right-aligned) and assistant (white, left-aligned) bubbles.
  - **Tracked actions:** Profile saved, drug confirmed/removed, pharmacy selected/deselected, Part D/Medigap/MA plan selected, section switched, cost calculation started, wizard navigation (step transitions), new analysis started.

---

## ✅ Chat-Based Recommendation Management

- **What:** A conversational AI assistant that helps manage Medicare recommendations through natural language chat. Uses intent classification to route user messages to appropriate handlers.
- **Backend:**
  - `ChatIntentService` — IChatClient-based classifier with `chat-intent-system.txt`. Classifies into 20 intents (navigation, actions, plan switching, save/run analysis, LTC).
  - `RecommendationService` — Full CRUD: GetActive, Exists, Create (with force), UpdateProfile/Drugs/Pharmacies/Plans/CostSnapshot, Delete.
  - `RecommendationController` — 8 REST endpoints for recommendation CRUD.
- **Frontend:**
  - `RecommendationService` — HTTP CRUD for `/api/recommendation`.
  - `RecommendationStateService` — Signal-based: `activeRecommendation`, `hasRecommendation` (computed), `refreshAfterUpdate()`, `clear()`.
  - `ChatComponent` — Routes messages through `ChatRouterService` with 6 contextual branches. Signals: `pendingDrugAction`, `pendingProfileUpdate`, `pendingPharmacyAction`, `pendingPlanAction`, `pendingSaveAnalysisOverwrite`.
  - `DashboardComponent` — Loads recommendation on init.

---

## ✅ Chat-Based Profile Filling

- **What:** Users can fill their profile via natural language in the chat panel instead of manually filling form fields. Supports both one-shot ("I'm John Smith, male, born 01/15/1955, ZIP 80113") and conversational approaches.
- **Field Coverage:** Applies the same extract → confirm → apply flow across profile fields (name, DOB, gender, tobacco, health condition, tax filing, coverage year, MAGI tier, life expectancy, concierge + amount, alternate contact, ZIP/address).
- **Chat Actions:** After applying extracted fields, chat also provides a **Save Profile Now** button to trigger profile save directly from chat. (Note: the standalone Save button was removed from the profile form — the form saves via the Continue button in the analysis wizard, or automatically when the standalone `/profile` route saves on completion.)
- **Confirmation Flow:** AI extracts fields → shows formatted list of extracted fields → asks "Shall I apply these? (yes / no)" → user confirms or cancels. `pendingProfileUpdate` signal holds the pending data.
- **Cascading Lookups:** When applied, triggers automatic lookups: ZIP→county resolution, DOB→age check, taxFiling→MAGI tier options.
- **Backend:**
  - **Prompt:** `profile-extract-system.txt` — knows 13 profile fields, required vs optional, asks for remaining fields.
  - **DTOs:** `ProfileExtractRequest` (message, missingFields), `ProfileExtractResponse` (extractedFields dict, reply) — in `ProfileExtractDtos.cs`.
  - **Service:** `ProfileExtractService` — `IChatClient`-based, loads prompt from file, returns structured JSON. Fallback on error.
  - **Endpoint:** `POST /api/chat/extract-profile`.
- **Frontend:**
  - **`ChatProfileService`** — HTTP service for `POST /api/chat/extract-profile`.
  - **`ChatComponent`** — Routes to profile extraction when on `/profile` with incomplete profile. Intercepts response to show confirmation prompt.
  - **`ProfileService`** — `pendingChatProfileData` signal consumed by UserProfileComponent, `missingRequiredFields` signal sent to AI.
  - **`UserProfileComponent`** — `effect()` watches `pendingChatProfileData`, patches form, triggers cascading lookups.
  - **Startup Greeting:** New users see profile-fill guidance with example prompt.

---

## ✅ Chat-Based Drug Formulation Selection

- **What:** Users can select drug type, dosage form, strength, and quantity via chat instead of clicking through the 4-step formulation UI. Also supports removing and editing drugs via chat.
- **Confirmation Flow (remove/edit only):** Destructive actions (remove, edit) require yes/no confirmation. `pendingDrugAction` signal holds the pending command. Non-destructive actions (select, confirm_all, options) execute immediately.
- **Fuzzy Matching:** Drug names ("lipitor" → Atorvastatin), dosage forms ("tab" → Tablet), strengths ("10mg" → "10 MG"). Auto-confirms when all 4 selections are complete.
- **Backend:**
  - **Prompt:** `drug-selection-system.txt` — 6 actions (select, options, confirm_all, remove, edit), 8 few-shot examples.
  - **DTOs:** `DrugSelectionExtractRequest` (message, availableDrugs), `DrugSelectionExtractResponse` (drugName, type, dosageForm, strength, quantity, action, reply) — in `DrugSelectionDtos.cs`.
  - **Service:** `DrugSelectionExtractService` — `IChatClient`-based extraction.
  - **Endpoint:** `POST /api/chat/extract-drug-selection`.
- **Frontend:**
  - **`ChatDrugSelectionService`** — HTTP service.
  - **`ChatComponent`** — Routes to drug selection when on `/medicare-analysis/drugs` with loaded drug details. `buildAvailableDrugSummaries()` prepares data for AI. Remove/edit actions diverted to confirmation flow.
  - **`MedicareStateService`** — `pendingDrugSelection` signal + `ChatDrugSelectionCommand` interface.
  - **`DrugsStepComponent`** — `effect()` watches signal, `applyChatDrugSelection()` with `findMatchingDrugName()` (partial match), `fuzzyMatchForm()`, `fuzzyMatchStrength()`, `confirmAllReadyDrugs()`.

---

## ✅ Chat-Based Pharmacy Selection

- **What:** Users can select, remove, search, and list pharmacies via chat instead of clicking checkboxes. Supports fuzzy name matching.
- **Confirmation Flow (remove only):** Remove actions require yes/no confirmation. `pendingPharmacyAction` signal holds the pending command. Select and search actions execute immediately.
- **Backend:**
  - **Prompt:** `pharmacy-selection-system.txt` — 4 actions (select, remove, list, search), 6 few-shot examples. Prefers closest pharmacy on ambiguous matches.
  - **DTOs:** `PharmacySelectionExtractRequest` (message, availablePharmacies, selectedPharmacies), `PharmacySelectionExtractResponse` (pharmacyName, action, searchTerm, reply) — in `PharmacySelectionDtos.cs`.
  - **Service:** `PharmacySelectionExtractService` — `IChatClient`-based extraction.
  - **Endpoint:** `POST /api/chat/extract-pharmacy-selection`.
- **Frontend:**
  - **`ChatPharmacySelectionService`** — HTTP service.
  - **`ChatComponent`** — Routes to pharmacy selection when on `/medicare-analysis/pharmacies` with loaded lookup. `buildPharmacySummaries()` prepares data for AI. Remove actions diverted to confirmation flow. Search actions set `pendingPharmacySelection` with searchTerm.
  - **`MedicareStateService`** — `pendingPharmacySelection` signal + `ChatPharmacySelectionCommand` interface.
  - **`PharmacyStepComponent`** — `effect()` watches signal, `applyChatPharmacySelection()` with `findPharmacyByName()` (exact then partial match, prefers closest).

---

## ✅ Chat-Based Plan Selection

- **What:** Users can select, remove, and switch between plan sections (PDP/MA) via chat instead of clicking UI buttons. AI extracts plan selection commands from natural language.
- **Backend:**
  - **Prompt:** `plan-selection-system.txt` — 3 actions (select, remove, switch_section), matches plan names and types from available plans.
  - **DTOs:** `PlanSelectionExtractRequest` (message, availablePlans, selectedPlans), `PlanSelectionExtractResponse` (planName, planType, action, section, reply) — in `PlanSelectionDtos.cs`.
  - **Service:** `PlanSelectionExtractService` — `IChatClient`-based extraction.
  - **Endpoint:** `POST /api/chat/extract-plan-selection`.
- **Frontend:**
  - **`ChatPlanSelectionService`** — HTTP service for `POST /api/chat/extract-plan-selection`.
  - **`ChatComponent` / `ChatRouterService`** — Routes to plan selection extraction when on `/medicare-analysis/plans` with loaded plan data. Select/remove actions applied to state signals; switch_section sets `activeSection`.

---

## ✅ Chat-Based Run Analysis with Confirmation

- **What:** Users can trigger cost analysis calculation via chat using natural language (e.g., "run analysis", "calculate costs"). AI classifies the `ACTION_RUN_ANALYSIS` intent, which initiates the cost evaluation pipeline.
- **Flow:** User message → AI classifies as `ACTION_RUN_ANALYSIS` → prerequisite checks (profile, drugs, pharmacies, plan selected) → confirmation prompt → triggers `evaluateCosts()` → navigates to `/medicare-analysis/cost-projections`.

---

## ✅ Action Intent Bypass for Page-Specific Handlers

- **What:** App-level chat commands (save analysis, run analysis, help, sign out, etc.) now correctly reach the intent classifier even when the user is on a wizard step page where a page-specific selection handler would normally intercept all messages.
- **Problem:** On wizard pages, app-level commands could be captured by page-specific handlers and never reach intent routing.
- **Fix:** `ACTION_PATTERNS` — a regex array in `ChatRouterService` that matches app-level action phrases. All three page-specific selection handlers (`routeToDrugSelection`, `routeToPharmacySelection`, `routeToPlanSelection`) check this pattern first and return `false` to let matching messages fall through to `routeToIntentClassifier()`.
- **Patterns matched:** save analysis, run analysis, calculate cost, reset analysis, sign out, log out, show saved, help.

---

## ✅ Medicare Analysis Starts Fresh

- **What:** Clicking "Medicare Analysis" now starts wizard flow directly without checking saved analyses/prescriptions first.
- **Flow:** `selectMode('MEDICARE_ANALYSIS')` clears prior flow carry-over (`drugDetails`, `confirmedDrugNames`, `selectedLookupPharmacies`, `pharmacySelectionConfirmed`, plan selections) and starts wizard.
- **Result:** No path chooser, no saved-analysis/prescription copy-in behavior, and no pharmacy auto-restore from saved data.

---

← [Feature Catalog Index](../ch08-feature-catalog/ch08-feature-catalog.md) | [← Cost & Persistence](ch08-04-cost-persistence.md) | [Next: LTC →](ch08-06-ltc.md)
