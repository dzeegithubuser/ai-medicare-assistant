# Chapter 2 — Frontend Architecture

> Component tree, components, services, interceptors, guards, models, configuration, styling, and UI flow.

---

## Component Tree

```
App (root) → <router-outlet />
 ├── SigninComponent (/signin)
 ├── SignupComponent (/signup)
 ├── ForgotPasswordComponent (/forgot-password)
 └── DashboardComponent (/ — guarded by authGuard)
      ├── Header (gradient toolbar with app branding, user menu dropdown, footer)
      └── Main Split Layout
           ├── Left Panel: <router-outlet> (child routes)
           │    ├── /profile → UserProfileComponent
           │    │    └── Consolidated single-form profile
           │    ├── /medicare-analysis → AnalysisShellComponent (4-step wizard, guarded by profileCompleteGuard)
           │    │    ├── Step Indicator (1·Profile → 2·Drugs → 3·Pharmacies → 4·Plans) + Back/Continue nav bar
           │    │    └── <router-outlet> (wizard step child routes)
           │    │         ├── /medicare-analysis/profile → UserProfileComponent (same component as `/profile`; analysis step 1)
           │    │         ├── /medicare-analysis/drugs → DrugsStepComponent (Financial Planner drug search, formulation selection, AI interactions, duplicate therapies)
           │    │         ├── /medicare-analysis/pharmacies → PharmacyStepComponent (Financial Planner pharmacy lookup with filters, pagination, multi-select)
           │    │         ├── /medicare-analysis/plans → PlansStepComponent
           │    │         │    └── PlanRecommendationComponent (MA / Part D / Medigap plan cards, section chooser, selected-plans summary)
           │    │         └── /medicare-analysis/cost-projections → CostProjectionsComponent (Chart.js dashboards + Save Analysis button)
           │    ├── /long-term-care → LtcShellComponent (2-step LTC wizard, guarded by profileCompleteGuard)
           │    │    ├── Step Indicator (1·Profile → 2·Care Type) + Back/Continue nav bar
           │    │    └── <router-outlet> (wizard step child routes)
           │    │         ├── /long-term-care/profile → UserProfileComponent (reused; LTC step 1)
           │    │         ├── /long-term-care/care-type → LtcCareTypeStepComponent (quality of care + LTC years + "Run Projection" button)
           │    │         └── /long-term-care/projection → LtcProjectionStepComponent (result page — chart.js cost breakdown + present-value summary; not a stepper step)
           │    ├── /saved → RecommendationComponent (saved analyses with filter/sort/pagination + compare basket)
           │    │    ├── /saved/compare → RecommendationCompareComponent (side-by-side comparison)
           │    │    └── /saved/:id → RecommendationDetailComponent (full detail view with 5 tabs + Chart.js)
           │    └── PharmacyListComponent (on-demand, triggered by user button click)
           └── ChatComponent (right panel, fixed 420px — visible on /medicare-analysis/* and /long-term-care/* routes)
```

---

## Components

### `App` (`app.ts`)
- **Role:** Minimal root shell — just renders `<router-outlet />`.
- **Imports:** `RouterOutlet`.
- **Template:** Inline `<router-outlet />` — all layout logic moved to DashboardComponent.

### `DashboardComponent` (`dashboard/dashboard.component.ts`, `.html`, `.scss`)
- **Role:** Authenticated shell — renders the header bar and split-panel layout with child `<router-outlet>`. Protected by `authGuard`.
- **Layout:** Full-height flex column. Header at top, main content below as horizontal flex.
- **State:** Injects `AuthService`, `ProfileService`, `Router`, `ChatSignalRService`. Local `bootstrapReady` signal.
- **Imports:** `RouterOutlet`, `ChatComponent`, Material modules (`MatIconModule`, `MatButtonModule`, `MatTooltipModule`, `MatMenuModule`). Does **not** import `DrugCardsComponent` or `UserProfileComponent` — these are loaded via child routes.
- **Bootstrap (`ngOnInit`):** `bootstrapDashboardState()` runs a `forkJoin` of three parallel operations: `profileService.loadProfile()`, `recommendationState.loadActiveRecommendation$()`, and `hydrateChatSession$()`. Then chains `selectionHydrator.hydrateAllFromActiveRecommendationSelectionForBootstrap$()`. Sets `bootstrapReady` once all complete.
- **`hydrateChatSession$()`:** Calls `chatSignalR.connect(token)` to open the WebSocket hub connection, then subscribes to `chatSignalR.session$` with `take(1)` and a 5 s timeout. When the hub fires `ReceiveSession` (on `OnConnectedAsync`), hydrates `DrugStateService.messages` from the pushed payload. Replaces the previous `GET /api/chat/session` HTTP call. The `ReplaySubject(1)` inside `ChatSignalRService` ensures that if the session push arrived before the dashboard subscribed (sign-in path), the value is replayed immediately.
- **Left Panel:** Renders `<router-outlet>` (shown after `profileLoaded()` is true). Child routes determine which component appears — no `@if` show/hide logic.
- **Template:** Gradient header with pharmacy icon, "AI Medicare Assistant" branding, **folder_open icon button** (navigates to `/saved` — always visible), and user menu dropdown button (account_circle icon). Dropdown shows "Welcome, {displayName}" header, **Saved Data** item (navigates to `/saved`, shown when profile complete), Edit Profile (if profile complete), Change Password, and Logout items. Below header: `<router-outlet>` left panel + chat. Footer bar at bottom shows "Powered by OpenAI".
- **`openRecommendations()`:** Navigates to `/saved`.
- **`displayName` Computed Signal:** Shows `"FirstName L"` (first name + last initial) when profile is complete, falls back to email otherwise.
- **Edit Profile:** `editProfile()` sets `profileService.editMode` to true and navigates to `/profile`.
- **Edit Profile from Analysis:** If current route is `/medicare-analysis/*`, stores `returnRoute` so profile save/close returns to the same analysis step.
- **Change Password:** `changePassword()` navigates to `/forgot-password`.

### `SigninComponent` (`auth/signin/signin.component.ts`, `.html`, `.scss`)
- **Role:** Sign-in page with email/password form.
- **Features:** ReactiveFormsModule form with email + password fields, password visibility toggle, loading/error signal states, links to sign up and forgot password.
- **Styling:** Centered card with cyan gradient background, pharmacy icon branding.
- **Flow:** Calls `authService.signIn()`, then `handleAuthSuccess()`, then navigates to `/`.

### `SignupComponent` (`auth/signup/signup.component.ts`, `.html`, `.scss`)
- **Role:** Registration page with email, phone, password, confirmPassword fields.
- **Features:** Password length validation (min 8), confirmPassword match validation, loading/error states.
- **Flow:** Calls `authService.signUp()`, then `handleAuthSuccess()`, then navigates to `/`.

### `ForgotPasswordComponent` (`auth/forgot-password/forgot-password.component.ts`, `.html`, `.scss`)
- **Role:** Password recovery page with email field.
- **Features:** Shows success (green) or error (red) messages after submission.
- **Styling:** Orange lock_reset icon, centered card layout.

### `UserProfileComponent` (`user-profile/user-profile.component.ts`, `.html`, `.scss`)
- **Role:** Consolidated single-form profile. Routed at **`/profile`** (dashboard, full-width profile) and **`/medicare-analysis/profile`** (same component embedded in `AnalysisShellComponent` as analysis step 1). When the URL contains `/medicare-analysis/profile`, `DrugStateService.currentStep` is set to `1`.
- **Landing Modes:**
  - **View mode (profile complete):** Opened by default after login when profile is complete. Form is read-only and shows a **Modify Profile** button.
  - **Create mode (profile incomplete):** Opened by default after login when profile is incomplete. Form is editable and focused on completing required onboarding fields.
  - **Edit mode:** Entered from view mode via **Modify Profile** or from explicit Edit Profile actions.
- **Fields:** First name (required, alphabetic + separators pattern), last name (required, same pattern), coverage year (radio, conditional on current month), health profile (dropdown, 1-5), tax filing status (radio), MAGI tier (dropdown, depends on tax filing + coverage year via constants API), gender (radio), tobacco status (radio), date of birth (datepicker, 18+ age validator), concierge (radio), concierge amount (conditional input), alternate email (optional, email validation), alternate mobile (optional, US phone validation), life expectancy (65-120, default 95), all address fields with county/city cascading dropdowns from ZIP lookup.
- **Name Validation Pattern:** `^[A-Za-z]+([' -][A-Za-z]+)*$` — alphabetic only, allows spaces/hyphens/apostrophes as separators, no leading/trailing separators, no consecutive separators. Supports names like John, Mary-Jane, O'Connor, Anne Marie.
- **Hardcoded Options:** Gender and yes/no options extracted to component variables (`genderOptions`, `yesNoOptions`) and iterated via `@for` in the template.
- **State:** `isEditMode` signal. Injects `ProfileService`, `Router`.
- **Flow:** On save, calls `POST /api/profile` with consolidated `ProfileDto`. Sets `profileService.isProfileComplete` to true and navigates to `/medicare-analysis`. In edit mode, stays on the panel — user closes manually via "Done" button. Note: the Save Profile button has been removed; saving is triggered by the wizard's Continue button when embedded at `/medicare-analysis/profile`.
- **Impact-aware invalidation:** If profile is opened from `/medicare-analysis/*` and impactful fields changed (demographic/tax/location/coverage assumptions), keeps drugs but clears downstream analysis state (pharmacy selection, plan selection, cost projection) and prompts user to continue from pharmacies.
- **OnInit:** Calls `ProfileService.loadProfile()` to populate form. Fetches MAGI tier options from constants service based on tax filing status and coverage year.

### `ChatComponent` (`chat/chat.component.ts`, `.html`, `.scss`)
- **Role:** Right-side chat panel (420px fixed width, full height). Implements `OnInit`.
- **State:** Injects `DrugStateService`, `ProfileService`, `AuthService`, `ChatIntentService`, `ChatWizardService`, `ChatOrchestratorService`, `ChatProfileService`, `ChatDrugSelectionService`, `ChatPharmacySelectionService`, `RecommendationStateService`, `Router`.
- **Features:**
  - Chat header with AI Assistant branding, icon, and **wizard step indicator** (Profile › Drugs & Pharmacy › Plans › Analysis) — visible when wizard mode is `MEDICARE_ANALYSIS`. Current step highlighted cyan, completed steps green with check icon.
  - Startup greeting on first load ("Hello! I'm your AI Medicare Assistant. What would you like to do today?").
  - **Mode Selection Buttons:** Two Material cards appear after the greeting — "Medicare Analysis" (starts guided wizard) and "Long Term Analysis" (coming soon). Cards hidden after selection. Only shown when `profileService.isProfileComplete()` is true — a constructor `effect()` watches the profile signal and shows buttons once the profile API resolves.
  - **Medicare Analysis Start:** Clicking "Medicare Analysis" starts the wizard immediately (no saved-analysis/prescription pre-check UI).
  - **Fresh Start Enforcement:** On mode selection, chat clears carry-forward flow state (`drugDetails`, `confirmedDrugNames`, `selectedLookupPharmacies`, `pharmacySelectionConfirmed`, plan selections) before starting wizard.
  - Message list with auto-scroll via `viewChild` + `effect()`.
  - User messages: right-aligned cyan bubbles. Assistant messages: left-aligned white bubbles with bot icon badge.
  - Loading state: spinner. Input area: Material outline form field + mini FAB send button. Enter key to send. **`chatInputDisabled`** disables input and send when **`chatSendBlocked()`** (loading, verification, profile save, etc.) **or** **`wizard.showModeButtons()`** — while startup mode cards are visible, the user must pick a card instead of typing.
  - **Guided Wizard Mode (Medicare Analysis):**
    - `selectMode('MEDICARE_ANALYSIS')` starts the wizard via `ChatWizardService.startMedicareAnalysis()` without recommendation pre-check branching.
    - `announceNextWizardStep()` recursively walks steps: PROFILE → DRUGS_PHARMACIES → PLANS → ANALYSIS → COMPLETE. Each step posts an assistant message and navigates to the appropriate route.
    - Auto-advance: `effect()` watches `wizard.hasNewStep()` — when a completion signal fires (e.g., profile saved, drugs confirmed), the next step is automatically announced.
    - Reset: `effect()` watches `state.wizardResetTrigger()` — calls `wizard.reset()` to return to mode selection.
  - **Free-form Intent Routing (via `ChatRouterService`):**
    - `send()` delegates to `ChatRouterService.route()` which routes messages through 7 contextual branches in priority order:
      0. **Back/previous guard** — `BACK_PATTERN` catches "back", "go back", "previous", "previous step" at the start of input. Shows guidance: "Use the Back button on the left side of the page or the stepper above." No navigation occurs. Compound phrases like "go back to profile" are NOT matched (pattern is anchored).
      1. **Pending confirmation handlers** — checks `pendingDrugAction()`, `pendingProfileUpdate()`, `pendingPharmacyAction()`, `pendingPlanAction()`, `pendingRunAnalysisConfirm()`, and `pendingSaveAnalysisOverwrite()` signals. If any are set, user's yes/no response confirms or cancels the pending action.
      2. **Orchestrator mode** — when `recState.hasRecommendation()`, routes to `ChatOrchestratorService`. **Guarded by URL:** skips when on wizard step pages (`/medicare-analysis/profile`, `/medicare-analysis/drugs`, `/medicare-analysis/pharmacies`, `/medicare-analysis/plans`) so page-specific handlers can take over.
      3. **Profile-page message routing** — when on `/profile` or `/medicare-analysis/profile`, classifies intent first via AI. **`DRUG_INPUT` keyword-gated strategy:** when the classifier returns `DRUG_INPUT`, profile extraction is attempted first (via `routeToProfileExtraction` with an `onEmptyExtraction` callback). If extraction finds profile fields (e.g. "magitier is 150" → `{ magiTier: "150" }`), they are applied normally — the user stays on the profile page. When extraction returns empty, the **keyword gate** decides: if the text contains an explicit drug keyword (`DRUG_KEYWORD_PATTERN`: drug, medication, medicine, prescription, rx, meds, pill, tablet, capsule), `pendingCrossPageDrugSearch` is set and the intent reclassified as `NAVIGATE_ANALYSIS_DRUGS` — navigating to drugs where the search auto-fires. If no drug keyword is found (bare drug name like "metformin"), a guidance hint is shown instead ("Navigate to the Drugs step or type 'go to drugs'"). `UNKNOWN` and `NAVIGATE_PROFILE` with field params fall through to `ChatProfileService.extractProfile()` for profile field extraction. All other intents (navigation, actions) pass through to `handleIntent()` normally.
      4. **Plan selection extraction** — when on `/medicare-analysis/plans` with loaded plan data, routes to `ChatPlanSelectionService.extractSelection()`. Supports select plans (Part D, Medigap, MA), remove plan selections, and switch between PDP/MA sections. **`DRUG_INPUT` keyword gate:** if `DRUG_INPUT` is classified, checks for drug keyword — with keyword, redirects to drugs with auto-search; without keyword, shows guidance hint. **Action bypass:** messages matching `ACTION_PATTERNS` (save prescription, save analysis, run analysis, help, sign out, etc.) fall through to the intent classifier.
      5. **Drug selection extraction** — when on `/medicare-analysis/drugs` with loaded drug details, routes to `ChatDrugSelectionService.extractSelection()`. Supports select, confirm_all, remove (with confirmation), edit (with confirmation). **Action bypass:** same `ACTION_PATTERNS` guard.
      6. **Pharmacy selection extraction** — when on `/medicare-analysis/pharmacies` with loaded pharmacy lookup, classifies intent first (single API call — pre-classified result forwarded directly, no second call). Routes to `ChatPharmacySelectionService.extractSelection()` only for `UNKNOWN` / `NAVIGATE_PHARMACIES` intents. **`DRUG_INPUT` keyword gate:** if a drug name is typed on the pharmacy page, checks `DRUG_KEYWORD_PATTERN` — with keyword ("add drug eliquis"), `pendingCrossPageDrugSearch` is set and navigates to drugs; without keyword ("eliquis"), shows guidance hint. Supports select, remove (with confirmation), search (filters by name), list. **Action bypass:** same `ACTION_PATTERNS` guard.
      7. **Intent classifier** — default fallback to `ChatIntentService.classify()` → `handleIntent()` routes 17 intents.
    - **`ACTION_PATTERNS`:** Regex array that detects app-level commands (save/load prescription, save/run/reset analysis, sign out, help, show saved). These bypass page-specific selection handlers so they always reach the intent classifier regardless of current page.
      - `NAVIGATE_PROFILE` — sets edit mode, applies `pendingPrefill` if name params extracted, saves current analysis route to `returnRoute`, navigates to `/medicare-analysis/profile`. **Pharmacy-save on redirect:** if the user is on `/medicare-analysis/pharmacies` with pharmacies selected when this intent fires, `recState.savePharmacySelection()` is called (fire-and-forget) before navigation and a "Your N selected pharmacies have been saved." prefix is prepended to the chat message.
      - `NAVIGATE_ANALYSIS_DRUGS` — requires profile complete (redirects to `/medicare-analysis/profile` with message if not). Navigates to `/medicare-analysis/drugs`. **Cross-page auto-search:** if `pendingCrossPageDrugSearch` is set (placed there by the keyword-gated `DRUG_INPUT` interception on profile, pharmacy, or plans pages — only when the text contains an explicit drug keyword like "drug", "medication", "prescription"), `ChatComponent` picks it up on `NavigationEnd` and fires `runDrugFlow(text)` automatically within 50 ms.
      - `NAVIGATE_PHARMACIES` — requires profile complete (same gate as `NAVIGATE_ANALYSIS_DRUGS`). Navigates to `/medicare-analysis/pharmacies`.
      - `NAVIGATE_PLANS` — navigates to `/medicare-analysis/plans`. Prerequisite chain: profile complete → drugs confirmed → pharmacy selected. Redirects to the first unmet prerequisite with a descriptive message. Sets `pharmacySelectionConfirmed` on success.
      - `NAVIGATE_COST_PROJECTIONS` — navigates to `/medicare-analysis/cost-projections`. Prerequisite chain: profile complete → drugs confirmed → pharmacy selected → plan selected. Redirects to the first unmet prerequisite.
      - `SWITCH_TO_PDP` / `SWITCH_TO_MA` — prerequisites are enforced by **`ChatNavigationFlowService.checkDrugPharmacyPrereqs()`**: profile complete; drugs satisfied by **in-memory** `confirmedDrugNames` **or** at least one drug on **`activeRecommendation().drugList`**; pharmacy satisfied by **selected lookup pharmacies** **or** a pharmacy on the active saved recommendation. If the URL is already **`/medicare-analysis/plans`** and the profile is complete, the check **returns true** so Part D ↔ MA switching works even when drug state signals briefly lag behind the UI. On success, sets `activeSection` to `'partd'` or `'ma'` via `state.setActiveSection()`, sets `pharmacySelectionConfirmed`, navigates to `/medicare-analysis/plans`. If already on the requested section, shows "already viewing" message.
      - `ACTION_RESET_ANALYSIS` — calls `state.resetAll()` (triggers wizard reset via wizardResetTrigger).
      - `ACTION_SAVE_PRESCRIPTION` — no longer available in UI/chat flow; assistant responds that this action is not available.
      - `ACTION_SIGN_OUT` — calls `authService.signOut()` after 800ms delay.
      - `ACTION_LOAD_PRESCRIPTIONS` — navigates to `/saved` (saved data page with prescriptions and analyses tabs).
      - `ACTION_SAVE_ANALYSIS` — saves the current analysis as a named recommendation. AI extracts `analysisName` from message. Opens `SavePrescriptionDialogComponent` (with custom title/subtitle/icon) if no name provided. Checks prerequisites via `AnalysisSnapshotService.canSave()`. Handles 409 conflict with overwrite confirmation. On success, calls `state.resetAll()` and navigates to `/medicare-analysis/profile`.
      - `NAVIGATE_SAVED_ANALYSES` — navigates to `/saved`.
      - `ACTION_HELP` — shows formatted help menu with navigation, actions, and drug input guidance.
      - `DRUG_INPUT` / `UNKNOWN` — falls through to `runDrugFlow()`.
    - On classify error, falls back to `runDrugFlow()` (drug name suggestion pipeline).
  - **Return Route:** `saveReturnRoute()` helper captures `router.url` when navigating away from a `/medicare-analysis/*` route (e.g., to profile). Stored in `DrugStateService.returnRoute`. Profile component reads it on save/close to return the user to their previous analysis step instead of the default `/medicare-analysis`.
  - **Drug Analysis Flow (`runDrugFlow`):**
    - Profile gate: checks `profileService.isProfileComplete()`, redirects to `/profile` if incomplete.
    - Calls `DrugService.suggestNames()` → AI returns candidates → interactive selection panel with clickable chips. High-confidence (≥0.95) or single-candidate drugs auto-selected.
    - "Confirm & Analyze" → confirmed names sent to `DrugService.searchDrugsBulk()` → drug details loaded → navigates to drugs step.
  - **Drug Name Selection Panel:** Inline panel with per-drug rows showing input name + candidate buttons (cyan when selected). "Confirm & Analyze" enabled only when all drugs have a selection. "Cancel" clears suggestions.
- **Local State:** `selectedNames` Map tracks candidate selection per input drug.
- **Effects:** (1) auto-scroll on message/loading/suggestion changes, (2) wizard auto-advance on `hasNewStep`, (3) wizard reset on `wizardResetTrigger`, (4) when `activeRecommendation` / loading state changes: `autoHydrateStoredDrugsIfNeeded()` and `autoHydrateStoredPharmacyIfNeeded()` (session-gated: auto-load saved drugs on `/medicare-analysis/drugs` and saved pharmacy on `/medicare-analysis/pharmacies` without a yes/no prompt). **`NavigationEnd` handler:** clears `pendingProfileModifyDetail` when not on `/medicare-analysis/profile`; re-runs the two auto-hydrate helpers; when the URL is **`/medicare-analysis/plans`**, calls **`ChatAnalysisSelectionHydrationService.hydratePlansFromActiveRecommendationSelection()`** so footer navigation and chat navigation both restore saved plan rows and post assistant summaries (Part D / Medigap / MA bullets; duplicate identical posts suppressed via fingerprints inside the hydration service); triggers cross-page drug auto-search when landing on `/medicare-analysis/drugs` with `pendingCrossPageDrugSearch` set.
- **Change Detection:** OnPush (signals drive all reactivity).
- **Orchestrator Mode (when recommendation exists):**
  - **Routing:** When `recState.hasRecommendation()` is true and user is NOT on a wizard step page (`/medicare-analysis/profile`, `/medicare-analysis/drugs`, `/medicare-analysis/pharmacies`, `/medicare-analysis/plans`), `send()` routes messages to `ChatOrchestratorService.sendMessage()` instead of the intent classifier. On wizard step pages, page-specific handlers take priority so drug/pharmacy/plan selection commands work correctly.
  - **Signals:** `pendingDelta` (DeltaResult | null), `awaitingConfirmation` (boolean), `activeDisplayData` (DisplayData | null), `deleteConfirmMode` (boolean), `pendingDrugAction` (ChatDrugSelectionCommand | null — holds remove/edit drug actions pending yes/no confirmation), `pendingProfileUpdate` (Record<string, unknown> | null — holds extracted profile fields pending confirmation), `pendingPharmacyAction` (ChatPharmacySelectionCommand | null — holds pharmacy remove actions pending confirmation), `pendingSaveAnalysisOverwrite` (string | null — holds analysis name for overwrite confirmation on 409 conflict).
  - **`handleOrchestratorResponse()`:** Sets delta, confirmation, displayData, and detects delete/create lifecycle events. Refreshes recommendation state after mutations.
  - **`confirmOrCancel(answer)`:** Sends "yes"/"no" to orchestrator, clears pending state.
  - **`onHelpAction(action)`:** Receives action string from HelpMenuComponent chip click, clears displayData, sends to orchestrator.
  - **Header:** Shows emerald "Orchestrator" pill when in orchestrator mode; wizard step indicator hidden.
  - **Markdown Rendering:** Assistant messages use `[innerHTML]="msg.content | markdown"` via MarkdownPipe; user messages stay plain text.
  - **Delete Banner:** Red instruction card shown when `deleteConfirmMode()` is true — prompts user to type "DELETE MY RECOMMENDATION".
  - **Error Handling:** Differentiates network timeout (`status === 0`) vs server error for user-friendly messages.
- **Chat-Driven Helpers:**
  - `buildAvailableDrugSummaries()` — extracts types, dosage forms, strengths per drug from loaded drug details for the AI drug selection extractor.
  - `buildPharmacySummaries()` — builds available + selected pharmacy summaries from `pharmacyLookup()` and `selectedLookupPharmacies()` for the AI pharmacy selection extractor.

### `DeltaDisplayComponent` (`chat/delta-display/delta-display.component.ts`)
- **Role:** Inline cost comparison card shown in chat after a profile/plan change is proposed.
- **Layout:** 3-column grid: Lifetime Total, This Year, Present Value. Each column shows before → after with trend icon.
- **Color Coding:** Red (cost increase, `trending_up`), Green (cost decrease, `trending_down`), Gray (no change, `trending_flat`).
- **Input:** `@Input() delta: DeltaResult` — bound from `pendingDelta()` signal.
- **Pattern:** Standalone, inline template, OnPush.

### `HelpMenuComponent` (`chat/help-menu/help-menu.component.ts`)
- **Role:** 5-category help menu rendered inline in chat when orchestrator returns `displayData.type === 'help_menu'`.
- **Categories:** Recommendation, Profile Updates, Drugs & Pharmacy, Medicare Plans, Projections & Funding.
- **UI:** Each category is a white card with icon + title + row of clickable `rounded-full` action chips (cyan theme).
- **Output:** `actionClicked` output emits the action string (e.g., "Add a drug") — handled by ChatComponent's `onHelpAction()`.
- **Pattern:** Standalone, OnPush.

### `MarkdownPipe` (`pipes/markdown.pipe.ts`)
- **Role:** Transforms markdown strings to safe HTML for rendering in assistant chat bubbles.
- **Implementation:** Uses `marked` library with GFM + breaks enabled. Output passed through `DomSanitizer.bypassSecurityTrustHtml()`.
- **Styling:** `.markdown-body` class in `chat.component.scss` provides heading, table, list, and code block styles.

### `AnalysisShellComponent` (`medicare-analysis/analysis-shell.component.ts`, `.html`, `.scss`)
- **Role:** Parent wizard shell for the Medicare analysis flow (four primary steps). Routed to at `/medicare-analysis`.
- **Guarded by:** `profileCompleteGuard`.
- **Layout:** Vertical flex — step indicator (top), `<router-outlet>` (scrollable middle), Back/Continue navigation bar (bottom).
- **Step Indicator:** Horizontal numbered badges (1·Profile → 2·Drugs → 3·Pharmacies → 4·Plans) connected by lines. Current step highlighted in cyan, completed steps show a check icon, future steps are grey. Forward navigation to a later step is blocked until prior prerequisites are met (e.g. cannot jump to Pharmacies before drugs are confirmed — `canNavigateToStep()`). `/medicare-analysis/cost-projections` is a fifth child route (cost dashboard) and does not add a stepper step.
- **Navigation Bar:** Back button (left, hidden on step 1) and Continue button (right, hidden on step 4 — Plans). Continue on step 1 (Profile) is always enabled when the guard allows analysis (profile complete). Continue on step 2 requires `hasDrugDetails()` and `hasConfirmedDrugs()`; step 3 requires selected lookup or legacy pharmacies. `goNext()` sets `pharmacySelectionConfirmed` when advancing from Pharmacies to Plans. Emits system messages on navigation and new analysis.
- **Step Tracking:** Reads/writes `DrugStateService.currentStep` (`1 | 2 | 3 | 4`). Persisted snapshots include `analysisStepSchemaVersion: 2`; older session data without that field migrates legacy steps 1–3 to new steps 2–4. Child step components set `currentStep` on init (`profile` → 1, `drugs` → 2, `pharmacies` → 3, `plans` → 4).
- **State:** Injects `DrugStateService`, `Router`. No local state beyond step definitions.

### `DrugsStepComponent` (`medicare-analysis/drug-step/drug-step.component.ts`, `.html`)
- **Role:** Drugs step (shell step 2) for the Financial Planner analysis wizard — supports both direct page-based drug search and detailed formulation selection workflow.
- **State:** Injects `DrugStateService`, `DrugService`, `PrescriptionService`, `MatSnackBar`, `MatDialog`. Local signals: `formulationSelections`, `drugSelections`, `drugQuantities`. Shared: `confirmedDrugNames` (delegated to `DrugStateService.confirmedDrugNames`).
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
- **State:** Injects `DrugStateService`, `DrugService`. Local signals: `nameFilter`, `radiusFilter` (default '25'), `pageSize` (default 20), `currentPage` (default 1). Readonly arrays: `radiusOptions` ['10', '25', '50', '100'], `pageSizeOptions` [10, 20, 50].
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
- **State:** Injects `DrugStateService`. Embeds only **`PlanRecommendationComponent`** via `<app-plan-recommendation>`.
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

### `PharmacyListComponent` (`pharmacy-list/pharmacy-list.component.ts`, `.html`, `.scss`)
- **Role:** Nearby pharmacies panel (collapsible, sortable, selectable). Used in both standalone mode and plan-aware mode.
- **State:** Injects `DrugStateService`. Local signals `collapsed`, `sortBy`.
- **Inputs:** `planMode` (boolean, default `false`) — when true, shows plan copays instead of retail prices and reads from `state.planAwarePharmacies()`.
- **Features:**
  - Collapsible panel with gradient header and sort toggle (price ↔ name).
  - **Responsive card grid:** `grid-cols-1 sm:grid-cols-2 lg:grid-cols-3` — pharmacy cards wrap automatically by screen width.
  - Compact pharmacy cards with name + cost badge at top, address + phone below. "Best Price" / "Best Copay" chip on cheapest. "Preferred" badge in plan mode.
  - Selected card gets emerald ring highlight (`ring-2 ring-emerald-300`) + background. Drug price detail rows expand inline within the card on selection.
  - Multi-select support — `isSelected()` checks via `state.isPharmacySelected(npi)`.
  - `pharmacySource()` returns appropriate list based on `planMode`.
  - `sortedPharmacies()` sorts by `totalPlanCopay` in plan mode, `totalRetailCost` otherwise.
  - `formatPhone()` formats 10-digit phone numbers.

### `PlanRecommendationComponent` (`plan-recommendation/plan-recommendation.component.ts`, `.html`, `.scss`)
- **Role:** Medicare plan recommendations panel. Loaded on-demand.
- **State:** Injects `DrugStateService`, `PlanRecommendationService`, `DrugService`.
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
  | `PlanRecommendationComponent` | `app-plan-recommendation` | Orchestrator — plan loading, compare state, LIS banner, recommended badge | — | — |
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
  - **Calculate Lifetime Cost:** Each plan card has a "Calculate Lifetime Cost" button at the bottom. `@Input() isCostLoading` shows a spinner during calculation. `@Output() calculateCost` emits the plan to the parent. Parent `PlanRecommendationComponent` handles the call to `PlanRecommendationService.evaluateCosts()`, stores result in `DrugStateService.costProjection`, and navigates to `/medicare-analysis/cost-projections`.

### `CostProjectionsComponent` (`cost-projections/cost-projections.component.ts`, `.html`, `.scss`)
- **Role:** Full-page cost projections dashboard with Chart.js visualizations and AI-generated insights. Routed at `/medicare-analysis/cost-projections`.
- **State:** Injects `DrugStateService` (reads `costProjection` signal), `Router`.
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

### `RecommendationComponent` (`recommendation/recommendation.component.ts`, `.html`)
- **Role:** Saved analyses list page with full client-side filter, sort, pagination, and compare basket. Routed at `/saved`.
- **State:** Injects `RecommendationService`, `Router`. Local signals: `recommendations` (`RecommendationSummaryResponse[]`), `loadingRecommendations`, `searchTerm`, `selectedType`, `sortBy`, `currentPage`, `pageSize`, `compareBasket` (up to 2 items).
- **OnInit:** Loads `RecommendationService.getAll()`.
- **Filter/Sort/Pagination:**
  - **Search:** Text input filters by analysis name (case-insensitive).
  - **Type pills:** All / Medicare / Long Term Care — filters `recommendation.type`.
  - **Sort:** 6 options — Newest First, Oldest First, Name A–Z, Name Z–A, Highest Cost, Lowest Cost.
  - **Pagination:** Configurable page size (10/25/50); Prev/Next and numbered page buttons.
- **Compare Basket:**
  - Each card shows an **Add to Compare** / **Remove** toggle button.
  - Sticky ribbon at screen bottom appears when ≥1 item is in the basket. At 2 items, **Compare** button navigates to `/saved/compare`.
  - Compare is type-aware — Medicare analyses and Long Term Care analyses are compared separately.
- **Cards (4-row grid layout):** Analysis name (uppercase CSS), creation date, type badge, drug count, plan count, lifetime total (when available), and status pill.
- **Empty State:** Shows "No saved analyses" guidance when the full list is empty; "No results" when filters return nothing.
- **Back button:** Navigates to `/medicare-analysis`.
- **Pattern:** Standalone, OnPush.

### `RecommendationCompareComponent` (`recommendation/recommendation-compare.component.ts`, `.html`)
- **Role:** Orchestrator for side-by-side comparison of two saved analyses. Routed at `/saved/compare?ids=id1,id2`.
- **State:** Reads `ids` from query params, `forkJoin` loads both full `RecommendationResponse` records. Determines comparison `mode` — `'medicare'` (both Medicare), `'longterm'` (both LTC), or `'cross'` (mixed types). Signals: `left`, `right`, `loading`, `error`.
- **Hero Header:** 3-column grid — Left rec card (name, date, lifetime cost, winner badge), VS badge (mode label, savings), Right rec card.
- **Mode Dispatch:** `@switch(mode())` delegates to `CompareMedicareComponent`, `CompareLtcComponent`, or `CompareCrossComponent`.
- **Pattern:** Standalone, OnPush.

### `CompareMedicareComponent` (`recommendation/compare-medicare.component.ts`, `.html`)
- **Role:** Medicare-vs-Medicare comparison — 5-tab deep dive with Chart.js charts.
- **Inputs:** `left`, `right` (`RecommendationResponse`).
- **Computed Signals:** `costDelta`, `premiumDelta`, `oopDelta`, `irmaaDelta`, `pvDelta`, `currentYearDelta`, `winner`, `winnerName`, `winnerSavings`, `profileRows` (grouped by personal/location/health/financial), `personalRows`, `locationRows`, `healthRows`, `financialRows`, `profileDiffs`, `drugMatches` (common/left/right via rxcui key), `leftPlanSummary`, `rightPlanSummary`, `yearlyRows`.
- **5 Tabs:**
  1. **Overview** — 6 KPI delta cards (Lifetime Total, Premiums, OOP, IRMAA, Present Value, Current Year), winner banner, 5 key-difference sections: Profile Differences (only differing rows), Prescriptions (count strip + drug chips), Pharmacy (side-by-side cards with Same/Different badge), Plans (side-by-side plan cards with type/carrier/premium), Projection Summary (6-row comparison table + trajectory cards).
  2. **Profile** — 4 grouped card sections (Personal, Location, Health, Financial) with human-readable labels (health condition → "Good Health", gender → "Male"/"Female", tobacco → "Yes"/"No", concierge → "Yes — $X/yr", tax filing → "Married Filing Jointly", life expectancy → "X yrs"). Diff summary banner, match column (check/cross icons).
  3. **Prescriptions** — Count strip (Common / Left-only / Right-only), shared drugs table (Drug Name, Dosage, Quantity, Refill columns), unique drug cards in bordered side-by-side panels.
  4. **Plans & Pharmacy** — Pharmacy comparison cards with storefront icons, full address, zip, phone, type badge, distance, Same/Different badge, mail-order pharmacy row. Plan cards with indigo/cyan left borders showing plan name, carrier, type badge, monthly premium, deductible, star rating (visual + numeric), Rx Coverage status, Rx Cost, Total Plan Cost, and unavailable drug chips.
  5. **Cost Analysis** — Chart.js line chart (yearly cost trend) + bar chart (cost breakdown), year-by-year delta table with advantage badges, category comparison (progress bars), overall assessment cards.
- **Helpers:** `compare-helpers.ts` — `deltaClass`, `deltaIcon`, `deltaLabel`, `getTrajectoryIcon/Color`, `getPriorityColor`, `starArray`, `typeBadgeClass`, `typeLabel`, `buildProfileRows` (returns `ProfileRow[]` grouped by `personal | location | health | financial`, with inline label formatters for health condition, gender, tobacco, concierge, tax filing status).
- **Pattern:** Standalone, OnPush, Chart.js (LineController, BarController).

### `CompareLtcComponent` (`recommendation/compare-ltc.component.ts`, `.html`)
- **Role:** LTC-vs-LTC comparison — 4-tab deep dive.
- **Inputs:** `left`, `right` (`RecommendationResponse`).
- **Computed Signals:** `costDelta`, `pvDelta`, `avgAnnualDelta`, `winner`, `winnerName`, `winnerSavings`, `profileRows`, `profileDiffs`, `careConfigRows` (Health Profile, Adult Day Years, Home Care Years, Nursing Care Years), `careConfigDiffs`.
- **4 Tabs:**
  1. **Overview** — 4 KPI delta cards (Total Cost, Present Value, Avg Annual, Projection Years), green gradient winner banner, profile differences table with count badge, care config summary grid cards with match indicators, trajectory comparison side-by-side.
  2. **Profile** — 4 grouped sections (Personal, Location, Health, Financial) with colored icon headers, match column (check/warning icons).
  3. **Care Config** — Config table with match column, side-by-side cost total cards (indigo left / cyan right) showing Total Cost + Present Value.
  4. **Cost Analysis** — Category comparison with progress bars and trend badges, savings recommendations with priority pills, side-by-side overall assessment cards with colored left borders.
- **Pattern:** Standalone, OnPush.

### `CompareCrossComponent` (`recommendation/compare-cross.component.ts`, `.html`)
- **Role:** Medicare-vs-LTC cross-type comparison — 3-tab layout.
- **Inputs:** `left`, `right` (`RecommendationResponse`).
- **Computed Signals:** `leftType`, `rightType` (inferred `RecommendationCategory`), `leftLifetime`, `rightLifetime` (via `lifetimeCost()` helper), `profileRows`, `profileDiffs`, `costDelta`, `deltaIcon`, `deltaLabel`.
- **3 Tabs:**
  1. **Overview** — Amber cross-type warning banner, 3-card KPI strip (left cost with type badge, right cost with type badge, cost difference delta), green gradient winner banner, profile differences table.
  2. **Profile** — 4 grouped sections (Personal, Location, Health, Financial) with colored icon headers, match column.
  3. **Cost Summary** — Side-by-side evaluation cards with type badges, trajectory indicators from `evaluation` sub-object, assessment cards with colored left borders, blue info note explaining cross-type comparison caveats.
- **Pattern:** Standalone, OnPush.

### `RecommendationDetailComponent` (`recommendation/recommendation-detail.component.ts`, `.html`, `.scss`)
- **Role:** Full detail view for a single saved recommendation. Routed at `/saved/:id`.
- **State:** Injects `RecommendationService`, `ActivatedRoute`, `Router`. Loads recommendation via `id` route param.
- **Chart.js:** Manually registers all chart controllers (LineController, BarController, DoughnutController) — same setup as `CostProjectionsComponent`.
- **Design:** Professional redesign matching the compare page design language:
  - **Hero Header:** Dark gradient bar with type badge (Medicare/LTC), back button, save date.
  - **Medicare KPI Strip:** 6 cards above tabs (Lifetime, Premiums, OOP, IRMAA, Present Value, Current Year).
  - **Medicare Tabs (5):**
    1. **Profile** — 3 grouped section cards (Personal, Location, Health & Financial) with colored icons and human-readable labels.
    2. **Prescriptions** — Drug count pill + clean HTML table (drug name, dosage, quantity, refill).
    3. **Pharmacy** — Storefront-style cards with type badge, phone/distance/NPI icons, mail-order card.
    4. **Plans** — Card-per-plan with colored type headers, 6-metric grid (Monthly Premium, Deductible, Star Rating, Rx Coverage, Rx Cost, Total Cost), visual star ratings, unavailable drug chips.
    5. **Cost & Charts** — Trajectory banner, all Chart.js charts in card containers (line, stacked bar, doughnut, projection), Medicare Expense Table, summary strip.
  - **LTC Tabs (3):** Profile, Care Config, Cost Analysis (trajectory, categories, tips, assessment).
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

## Services

### `DrugService` (`services/drug.service.ts`)
- **Methods:**
  - `suggestNames(input: string)` → `Observable<DrugNameSuggestionResult>` — Step 1: identifies correct drug names from user input.
  - `analyze(prescription: string)` → `Observable<DrugAnalysisResponse>` — Step 2: full drug analysis with confirmed names.
  - `searchNearbyPharmacies(zip?: string)` → `Observable<PharmacyResult[]>` — Lightweight NPI-only pharmacy lookup (no pricing) via `GET /api/pharmacy/nearby`. Used for multi-select step (up to 5).
  - `searchPharmacies(rxCuis: string)` → `Observable<PharmacyWithPricing[]>` — On-demand nearby pharmacy search with AI pricing via `GET /api/pharmacy/search`.
  - `searchPlanPharmacies(request: PlanPharmacySearchRequest)` → `Observable<PharmacyWithPricing[]>` — Plan-aware pharmacy search via `POST /api/pharmacy/plan-search`.
  - `searchDrugsBulk(drugNames: string[])` → `Observable<BulkDrugSearchResponse>` — Financial Planner bulk drug search via `POST /api/FinancialPlannerDrug/search-bulk`. Searches all drugs, matches by displayName, fetches formulation details, and evaluates AI interactions/duplicate therapies if >1 drug.
- **Endpoints:**
  - `POST ${environment.apiUrl}/api/drug/suggest-names` with `{ input }` body.
  - `POST ${environment.apiUrl}/api/drug/analyze` with `{ prescription }` body.
- **Note:** Zipcode is no longer sent from the frontend — the backend retrieves it from the user's saved address profile.

### `DrugStateService` (`services/drug-state.service.ts`)
- **Role:** Shared signal-based state management between chat and drug cards.
- **Message sync:** Injects `ChatSignalRService`. Every message mutation (`addUserMessage`, `addAssistantMessage`, `replaceLastAssistantMessage`, etc.) schedules a debounced SignalR sync via a 500 ms `setTimeout`. Rapid bursts (e.g. Medicare startup: greeting + profile review + mode buttons in < 200 ms) collapse into a single `ChatSignalRService.syncMessages()` WebSocket invoke. No HTTP call is made for message syncing.
- **Signals:** `drugs`, `interactions`, `dosageAlerts`, `duplicateTherapies`, `nearbyPharmacies`, `selectedPharmacies` (array, up to 5), `hasSelectedPharmacies` (computed), `messages` (`ChatMessage[]` with `'user' | 'assistant' | 'system'` role), `isLoading`, `currentStep` (`1 | 2 | 3 | 4` — analysis shell: Profile, Drugs, Pharmacies, Plans), `planRecommendation`, `selectedPlan`, `isPlanLoading`, `hasPlanRecommendation` (computed), `isLisEligible` (computed), `planAwarePharmacies`, `isPlanPharmacyLoading`, `hasPlanPharmacies` (computed), `drugSuggestions`, `hasSuggestions` (computed), `isVerifyingNames`, `confirmedDrugs` (signal wrapping `Set<string>`), `hasConfirmedDrugs` (computed), `prescriptionName`, `costProjection` (`EvaluateCostsResponse | null`), `hasCostProjection` (computed), `drugDetails` (`BulkDrugSearchResponse | null`), `isDrugDetailsLoading`, `hasDrugDetails` (computed), `wizardResetTrigger`, `pharmacySelectionConfirmed`, `returnRoute`, `pharmacyLookup`, `isPharmacyLookupLoading`, `hasPharmacyLookup` (computed), `selectedLookupPharmacies`, `hasSelectedLookupPharmacies` (computed), `confirmedDrugNames` (`Set<string>` signal), `hasConfirmedDrugs` (computed), `partDPlans`, `isPartDLoading`, `hasPartDPlans` (computed), `medigapQuotes`, `isMedigapLoading`, `hasMedigapQuotes` (computed), `maPlans`, `isMALoading`, `hasMAPlans` (computed), `selectedPartDPlan`, `selectedMedigapPlan`, `selectedMAPlan`, `selectedMAGapPartDPlan`, `activeSection`, `hasCompletePlanSelection` (computed — true when PDP+Medigap or MA(+gap PDP if needed) selected), `pendingDrugSelection` (`ChatDrugSelectionCommand | null` — chat-driven drug formulation command, watched by `DrugsStepComponent`), `pendingPharmacySelection` (`ChatPharmacySelectionCommand | null` — chat-driven pharmacy command, watched by `PharmacyStepComponent`), `pendingCrossPageDrugSearch` (`string | null` — drug search text stored when a drug input is typed on a non-drugs page, e.g. "add metformin" on pharmacies; picked up by `ChatComponent` on `NavigationEnd` to `/medicare-analysis/drugs` and cleared after `runDrugFlow()` fires).
- **Interfaces:** `ChatDrugSelectionCommand` (drugName, type, dosageForm, strength, quantity, action: select|options|confirm_all|remove|edit), `ChatPharmacySelectionCommand` (pharmacyName, action: select|remove|list|search, searchTerm).
- **Methods:** `addUserMessage()`, `addAssistantMessage()`, `addSystemMessage()` (UI action tracking — rendered as centered pill badges in chat), `hydrateMessagesFromServer()` (called by `DashboardComponent` with session data pushed from `ChatHub.OnConnectedAsync`), `togglePharmacy()` (max 5, returns false if limit reached), `isPharmacySelected(npi)`, `selectPharmacy()`, `setLoading()`, `setPlanRecommendation()`, `selectPlan()`, `setPlanLoading()`, `setPlanPharmacies()`, `setPlanPharmacyLoading()`, `clearPlanPharmacies()`, `setCostProjection()`, `setPharmacyLookup()`, `setPharmacyLookupLoading()`, `setDrugDetails()`, `setDrugDetailsLoading()`, `toggleLookupPharmacy()` (max 5, emits system messages), `isLookupPharmacySelected()`, `setDrugSuggestions()`, `setVerifyingNames()`, `clearSuggestions()`, `setPartDPlans()`, `setPartDLoading()`, `setMedigapQuotes()`, `setMedigapLoading()`, `setMAPlans()`, `setMALoading()`, `selectPartDPlan()`, `selectMedigapPlan()`, `selectMAPlan()`, `selectMAGapPartDPlan()`, `setActiveSection()`, `resetPlanSelections()`, `persistSelections()`, `resetAll()`.

### `AuthService` (`services/auth.service.ts`)
- **Signals:** `currentUser`, `isAuthenticated` (computed).
- **Methods:** `signUp()`, `signIn()`, `forgotPassword()`, `resetPassword()`, `handleAuthSuccess()`, `signOut()`, `getToken()`.
- **Sign-out Cleanup:** `signOut()` calls `sessionStorage.clear()` (clears all keys) then resets all in-memory signals: `DrugStateService` (25+ signals via `resetAll()` + explicit signal resets), `ProfileService` (profile, isProfileComplete, editMode, pendingPrefill, pendingChatProfileData, missingRequiredFields), `RecommendationStateService.clear()`, `ChatSignalRService.disconnect()` (closes the WebSocket so the next user gets a fresh connection). Uses `Injector.get()` for lazy service resolution to avoid circular dependencies.
- **Persistence:** Token and user JSON stored in `sessionStorage` keys `auth_token` and `auth_user` (not localStorage — session ends on tab close). Token timestamp stored in `auth_token_ts`.
- **Session Expiration:** 1-hour token expiry (`TOKEN_MAX_AGE_MS = 3,600,000`). `getToken()` checks timestamp age — if expired, calls `signOut()` and returns null. If valid, refreshes timestamp (auto-extending the session on activity). `loadUser()` also validates token age on startup.

### `ProfileService` (`services/profile.service.ts`)
- **Role:** Signal-based profile state orchestrator with consolidated HTTP save.
- **Signals:** `profile`, `isProfileComplete`, `editMode`, `pendingPrefill` (`Record<string, unknown> | null` — set by chat intent routing when user requests profile field changes via chat; consumed by `UserProfileComponent` on init to pre-fill form fields), `pendingChatProfileData` (`Record<string, unknown> | null` — set by chat profile extraction flow after user confirms; consumed by `UserProfileComponent` effect watcher to patch form + trigger cascading lookups), `missingRequiredFields` (`string[]` — published by `UserProfileComponent` for the AI to know which fields still need filling), `chatSaveRequestId`, `chatSaveInProgress`.
- **Methods:** `loadProfile()` → loads from `GET /api/profile`. `saveProfile(dto)` → `POST /api/profile` with consolidated `ProfileDto`. `updateState(p)` → updates signals from a `UserProfileResponse`.
- **Pattern:** Single consolidated save call replaces the previous per-section approach. Navigation handled by Angular Router (child routes), not signal toggling.

### `ChatSignalRService` (`services/chat-signal-r.service.ts`)
- **Role:** Manages the single persistent SignalR WebSocket connection to `/hubs/chat`. Replaces the repeated `PATCH /api/chat/session/messages` HTTP calls with a single lightweight hub invoke per message burst, and replaces the `GET /api/chat/session` HTTP call with a server push on connect.
- **Signals:** `isConnected` — true while the WebSocket is in `Connected` state.
- **`session$`** — `ReplaySubject<SignalRSessionPayload>(1)` getter. Emits once when the hub pushes `ReceiveSession` on connect. Late subscribers (e.g. `DashboardComponent.ngOnInit` after sign-in redirect) receive the replayed value immediately.
- **`connect(token: string)`** — builds `HubConnection` with `accessTokenFactory: () => token` (JWT via query-string, required because browsers cannot set headers on WebSocket upgrade requests). Transport forced to `WebSockets`. Auto-reconnect schedule: 0 / 2 / 5 / 10 / 30 s. Idempotent — returns `of(void)` if already Connected/Connecting/Reconnecting.
- **`disconnect()`** — stops the connection, resets `isConnected`, replaces `_sessionSubject` with a new `ReplaySubject` for the next login session.
- **`syncMessages(messages)`** — calls `connection.invoke('SyncMessages', messages)`. Silent no-op if not connected.
- **Types:** `SignalRChatMessage` (`role`, `content`, `timestamp`), `SignalRSessionPayload` (`messages[]`, `uiState`).

### `ChatSessionService` (`services/chat-session.service.ts`)
- **Role:** HTTP fallback for infrequent chat session operations. `updateMessages` is superseded by `ChatSignalRService.syncMessages` but kept for compatibility.
- **Methods:** `getSession()` (`GET /api/chat/session`), `updateMessages()` (`PATCH /api/chat/session/messages` — no longer called by the app), `updateUiState()` (`PATCH /api/chat/session/ui-state`), `startNewSession()` (`POST /api/chat/session/start-new`), `clearSession()` (`DELETE /api/chat/session`).

### `CountyLookupService` (`services/county-lookup.service.ts`)
- **Role:** Fetches ZIP-based county code data and Google Places API key.
- **Methods:** `getCountyCodeList(zipcode)` → cached county entries. `getGooglePlacesKey()` → Google Places API key.
- **Caching:** Results cached by ZIP code to avoid repeated API calls.

### `ReferenceDataService` (`services/reference-data.service.ts`)
- **Role:** Fetches and caches master/reference data for all profile forms. Loaded once via `load()`.
- **Computed Selectors:** `genders`, `maritalStatuses`, `taxFilingStatuses`, `incomeFilingStatuses`, `tobaccoStatuses`, `disabilityStatuses`, `chronicConditions`, `usStates`, `householdSizes`.
- **Dynamic:** `getMagiTiersForFiling(filingStatus)` → MAGI tiers for the given tax filing status.

### `PlanRecommendationService` (`services/plan-recommendation.service.ts`)
- **Methods:** `recommend(request)` → `Observable<PlanRecommendationResult>`, `checkLis()` → `Observable<LisCheckResult>`, `getGapAdvice(request: GapAdviceRequest)` → `Observable<GapCoverageResult>` — calls `POST /api/plan-recommendation/gap-advice` for AI-generated complementary plan recommendations (structured JSON with plan details, not text advice). `evaluateCosts(request: CalculateCostsRequest)` → `Observable<EvaluateCostsResponse>` — calls `POST /api/plan-recommendation/evaluate-costs` for combined Financial Planner + AI cost projections.

### `PrescriptionService` (`services/prescription.service.ts`)
- **Role:** HTTP service for saving and retrieving user prescriptions.
- **Methods:**
  - `save(request: SavePrescriptionRequest)` → `Observable<PrescriptionResponse>` — `POST /api/prescription`.
  - `getAll()` → `Observable<PrescriptionResponse[]>` — `GET /api/prescription`.
  - `getById(id: string)` → `Observable<PrescriptionResponse>` — `GET /api/prescription/{id}`. Used by saved prescription selection flow to load full drug details.
- **Types:** Defines `SavePrescriptionRequest`, `PrescriptionDrugDto`, `PrescriptionResponse` interfaces inline.

### `ChatIntentService` (`services/chat-intent.service.ts`)
- **Role:** HTTP service for AI-powered chat intent classification.
- **Methods:** `classify(message: string, isProfileComplete: boolean, currentPage: string)` → `Observable<ChatIntentResponse>` — `POST /api/chat/intent`. The `currentPage` parameter is `router.url` from the calling service — it is sent to the backend so the AI can apply page-specific disambiguation rules (e.g. on `/medicare-analysis/drugs` a bare number is not classified as `NAVIGATE_PROFILE`).
- **Types:** Defines `ChatIntent` (union of 17 intent strings including `SWITCH_TO_PDP`, `SWITCH_TO_MA`, `ACTION_HELP`, `ACTION_SAVE_ANALYSIS`, `ACTION_RUN_ANALYSIS`, `NAVIGATE_SAVED_ANALYSES`), `ChatIntentResponse` (intent, params, confirmationMessage), `ChatIntentParams` (firstName, lastName, prescriptionName, analysisName, gender, dateOfBirth, tobaccoStatus, healthCondition, taxFilingStatus, coverageYear, zipCode, addressLine1, lifeExpectancy) inline.
- **Call sites:** `chat-router.service.ts` (2), `chat-drug-selection-flow.service.ts` (1), `chat-profile-edit-flow.service.ts` (1), `chat-plan-selection-flow.service.ts` (1), `chat-pharmacy-selection-flow.service.ts` (1) — all pass `this.router.url` as `currentPage`.

### `ChatWizardService` (`services/chat-wizard.service.ts`)
- **Role:** Reactive wizard state management for guided Medicare analysis flow.
- **Types:** `WizardMode` (`'NONE' | 'MEDICARE_ANALYSIS' | 'LONG_TERM_ANALYSIS'`), `WizardStep` (`'AWAITING_MODE' | 'PROFILE' | 'DRUGS_PHARMACIES' | 'PLANS' | 'ANALYSIS' | 'COMPLETE'`).
- **Signals:** `mode` (current wizard mode), `showModeButtons` (whether startup mode cards are visible).
- **Computed Signals:** `currentStep` (derived from `profileService.isProfileComplete()`, `state.hasConfirmedDrugs()`, `state.pharmacySelectionConfirmed()`, plan selection flags (`selectedPartDPlan` / `selectedMedigapPlan` / `selectedMAPlan`), `state.hasCostProjection()`), `hasNewStep` (true when currentStep differs from last announced step — triggers auto-advance), `isComplete` (true when currentStep is COMPLETE).
- **Methods:** `startMedicareAnalysis()` (sets mode, clears announced step, hides buttons), `resumeMedicareAnalysis()` (restores Medicare mode on hard refresh without re-announcing steps), `reset()` (returns to NONE mode, re-shows buttons), `markStepAnnounced()` (records current step to prevent duplicate messages).
- **Pattern:** Purely signal-driven — no subscriptions. ChatComponent watches `hasNewStep` via `effect()` to auto-advance wizard steps when completion signals fire.

### `ChatOrchestratorService` (`services/chat-orchestrator.service.ts`)
- **Role:** HTTP service for the conversational orchestrator pipeline.
- **Methods:** `sendMessage(message: string, currentPage?: string)` → `Observable<OrchestratorResponse>` — `POST /api/chat/orchestrate`. Passes `currentPage` (caller's `router.url`) so the backend orchestrator's intent classifier can apply the same page-context disambiguation as `ChatIntentService`.
- **Types:** Uses `OrchestratorRequest` (message, currentPage?), `OrchestratorResponse` from `models/orchestrator.model.ts`.

### `RecommendationService` (`services/recommendation.service.ts`)
- **Role:** HTTP CRUD service for user recommendation documents.
- **Methods:** `getActive()` → recommendation or 404, `getAll()` → `Observable<RecommendationSummaryResponse[]>` — `GET /api/recommendation/all` (all saved analyses for the user), `getById(id: string)` → `Observable<RecommendationResponse>` — `GET /api/recommendation/{id}` (full recommendation by ID, used by saved analysis selection flow), `create(request)`, `updateProfile(profile)`, `updateDrugs(drugs)`, `updatePharmacy(pharmacy, mailOrder)`, `updatePlans(plans)`, `delete()`.
- **Endpoint:** All under `/api/recommendation`.

### `RecommendationStateService` (`services/recommendation-state.service.ts`)
- **Role:** Signal-based reactive state for the active recommendation.
- **Signals:** `activeRecommendation` (writable), `hasRecommendation` (computed — controls orchestrator mode routing in chat), `isLoading`.
- **Methods:** `loadActiveRecommendation()` (fetches from API, sets signal), `refreshAfterUpdate()` (reloads after mutations), `clear()` (nulls recommendation, used after delete).
- **Injection:** Injects `RecommendationService`. Called by `DashboardComponent.ngOnInit()` to preload.

### `ChatProfileService` (`services/chat-profile.service.ts`)
- **Role:** HTTP service for AI-powered profile field extraction from natural language.
- **Methods:** `extractProfile(request: { message, missingFields })` → `Observable<{ extractedFields, reply }>` — `POST /api/chat/extract-profile`.

### `ChatDrugSelectionService` (`services/chat-drug-selection.service.ts`)
- **Role:** HTTP service for AI-powered drug formulation selection extraction from chat.
- **Methods:** `extractSelection(request: { message, availableDrugs })` → `Observable<DrugSelectionExtractResponse>` — `POST /api/chat/extract-drug-selection`.
- **Types:** `AvailableDrugSummary` (name, types, dosageForms, strengths), `DrugSelectionExtractResponse` (drugName, type, dosageForm, strength, quantity, action: select|options|confirm_all|remove|edit, reply).

### `ChatPharmacySelectionService` (`services/chat-pharmacy-selection.service.ts`)
- **Role:** HTTP service for AI-powered pharmacy selection/removal extraction from chat.
- **Methods:** `extractSelection(request: { message, availablePharmacies, selectedPharmacies })` → `Observable<PharmacySelectionExtractResponse>` — `POST /api/chat/extract-pharmacy-selection`.
- **Types:** `AvailablePharmacySummary` (name, address, distance, zipcode), `SelectedPharmacySummary` (name, pharmacyNumber), `PharmacySelectionExtractResponse` (pharmacyName, action: select|remove|list|search, searchTerm, reply).

### `ChatPlanSelectionService` (`services/chat-plan-selection.service.ts`)
- **Role:** HTTP service for AI-powered plan selection extraction from chat.
- **Methods:** `extractSelection(request)` → `Observable<PlanSelectionExtractResponse>` — `POST /api/chat/extract-plan-selection`.
- **Types:** `PlanSelectionExtractResponse` (planName, planType, action: select|remove|switch_section, section, reply).

### `ChatNavigationFlowService` (`services/chat-navigation-flow.service.ts`)
- **Role:** Shared prerequisite checks and guarded navigation for analysis routes (used by **`ChatRouterService.handleIntent`** and related flows).
- **Methods:**
  - **`navigateWithPrerequisites(result, targetRoute, onSuccess?, requirePlan?)`** — ensures profile complete, drugs (**`hasDrugsForPlanPrereqs()`** — confirmed names **or** drugs on **`activeRecommendation`**), pharmacy (**`hasPharmacyForPlanPrereqs()`** — selected pharmacies **or** saved recommendation pharmacy), optional complete plan selection when `requirePlan`, then navigates.
  - **`checkDrugPharmacyPrereqs()`** — same drug/pharmacy rules; **short-circuits to `true`** when **`router.url`** starts with **`/medicare-analysis/plans`** and profile is complete (enables `SWITCH_TO_PDP` / `SWITCH_TO_MA` on the plans page without false "add drugs" errors when in-memory state lags saved analysis).

### `ChatAnalysisSelectionHydrationService` (`services/chat-analysis-selection-hydration.service.ts`)
- **Role:** Restores drugs (async bulk search), pharmacy, and **plan selections** from **`RecommendationStateService.activeRecommendation()`** into **`DrugStateService`**.
- **Plan hydration:** **`hydratePlansFromActiveRecommendationSelection(silent?)`** matches saved **`planSelections`** to live Part D / Medigap / MA lists when available; otherwise **`hydratePlansFallbackFromSavedSelection`** applies stubs and posts **`SAVED_PLANS_PENDING`**. Successful partial/full restores post **`RESTORE_ALL_MATCHED`** or **`RESTORE_PARTIAL_DETAIL`** with explicit **Part D / Medigap / Medicare Advantage** bullet lines. Fingerprint guards reduce duplicate identical assistant messages when hydration runs more than once.

### `AnalysisSnapshotService` (`services/analysis-snapshot.service.ts`)
- **Role:** Assembles a full analysis snapshot from current state (profile, drugs, pharmacy, plans, cost projections) and saves it as a recommendation via `RecommendationService.create()`.
- **Methods:**
  - `canSave()` → `boolean` — checks 5 prerequisites: profile complete, drugs confirmed, pharmacies selected, plan selected, cost projection available.
  - `save(name: string, force?: boolean)` → `Observable<any>` — builds the full snapshot request and calls `create()`. `force=true` overwrites existing.
- **Helpers:** `buildPlans()` maps selected plans with expanded fields (deductible, starRating, totalPrescriptionCost, planExpenses, unavailableDrugs) from `PharmacyWiseRecommendation`. `buildCostSnapshot()` maps yearly details + full AI evaluation object.
- **Injection:** Injects `DrugStateService`, `ProfileService`, `RecommendationService`.

---

## Interceptors & Guards

### `authInterceptor` (`interceptors/auth.interceptor.ts`)
- **Type:** `HttpInterceptorFn` (functional).
- **Behavior:** Attaches `Authorization: Bearer <token>` header to all outgoing HTTP requests.

### `authGuard` (`guards/auth.guard.ts`)
- **Type:** `CanActivateFn`.
- **Behavior:** Checks `AuthService.isAuthenticated()`. If false, redirects to `/signin`.

### `profileCompleteGuard` (`guards/profile-complete.guard.ts`)
- **Type:** `CanActivateFn`.
- **Behavior:** Protects `/medicare-analysis`. On deep-link/hard refresh, it loads profile first (if not already loaded) before deciding. If profile is incomplete, redirects to `/profile`; otherwise preserves the current analysis route.

### `dashboardRedirectGuard` (`guards/dashboard-redirect.guard.ts`)
- **Type:** `CanActivateFn`.
- **Behavior:** Auto-redirect for default dashboard child route (`''`). Always routes to `/profile` after login. Always returns false (pure redirect guard).

---

## Models

### Drug Models (`models/drug.model.ts`)

```typescript
interface DrugAnalysisResponse {
  drugs: Drug[];
  interactions: DrugInteraction[];
  dosageAlerts: DosageAlert[];
  duplicateTherapies: DuplicateTherapy[];
  nearbyPharmacies?: PharmacyWithPricing[]; // populated on-demand via separate API call
  message?: string;
}

interface Formulation {
  dosageForm: string;
  strength: string;
  packaging: string;
  ndcCode: string;
}

interface Drug {
  drugInput: string;
  normalizedDrugName: string;
  brandNames: string[];
  genericName: string;
  synonyms: string[];
  therapeuticCategory: string;
  drugClass: string;
  mechanismOfAction: string;
  dosageForms: string[];
  formulations: Formulation[];  // validated (dosageForm+strength+packaging+ndcCode) tuples
  strengths: string[];          // flat array (populated from formulations for backward compat)
  packaging: string[];          // flat array (populated from formulations for backward compat)
  rxNormId: string;
  ndcCodes: string[];           // flat array (populated from formulations for backward compat)
  estimatedRetailCostUSD: string;
  estimatedMedicarePartDCostUSD: string;
  medicareNegotiatedPriceUSD: string;
  confidenceScore?: number;
  alternatives: DrugAlternative[];
  genericSwitchSuggestion?: GenericSwitchSuggestion;
  contraindications: string[];
}

interface DrugInteraction { drugA, drugB, severity: 'High'|'Moderate'|'Low', description, clinicalConsequence, recommendation }
interface DosageAlert { drugName, inputDosage, recommendedRange, severity, message }
interface DuplicateTherapy { drugs: string[], therapeuticClass, message }
interface DrugAlternative { name, type, costDifference, clinicalNote }
interface GenericSwitchSuggestion { from, to, estimatedSavings }
```

### Pharmacy Models (`models/drug.model.ts`)

```typescript
interface PharmacyResult { npi, name, legalName, address, addressLine2, city, state, zipCode, phone, fax, pharmacyType, enumerationDate }
interface DrugPrice { drugName, ndc, rxCui, retailPrice: number|null, medicarePrice: number|null, genericPrice: number|null, planCopay?: number|null, formularyTier?: number|null, requiresPriorAuth?: boolean|null, isPreferredPharmacy?: boolean|null }
interface PharmacyWithPricing { pharmacy: PharmacyResult, drugs: DrugPrice[], totalRetailCost: number|null, totalMedicareCost: number|null, totalGenericCost: number|null, totalPlanCopay?: number|null, isPreferredNetwork?: boolean|null }
interface PlanPharmacySearchRequest { planId, zipCode?, drugs: {rxCui, drugName}[], planCoverages: PlanCoverageInput[] }
interface PlanCoverageInput { rxCui, drugName, formularyTier, monthlyCopay, isCovered, requiresPriorAuth }
```

### Auth Models (`models/auth.model.ts`)

```typescript
interface SignUpRequest { email, phone, password, confirmPassword }
interface SignInRequest { email, password }
interface ForgotPasswordRequest { email }
interface ResetPasswordRequest { token, newPassword, confirmPassword }
interface AuthResponse { success, message, token?, expiresAt?, user?: AuthUser }
interface AuthUser { id, email, phone }
```

### Profile Models (`models/profile.model.ts`)

```typescript
interface UserProfileResponse { profile: ProfileDto | null, isProfileComplete: boolean }
interface ProfileDto { firstName, lastName, coverageYear, healthCondition, taxFilingStatus, magiTier, gender, tobaccoStatus, dateOfBirth, concierge, conciergeAmount, alternateEmail, alternateMobile, lifeExpectancy, addressLine1, addressLine2, street, city, state, zipCode, county, countyCode, latitude, longitude }
```

### Reference Data Models (`models/reference-data.model.ts`)

```typescript
interface LabelValue { value: string; label: string }
interface MagiTierOption { value, label, description }
interface HouseholdSizeOption { value: number, label }
interface ReferenceData { genders, maritalStatuses, taxFilingStatuses, incomeFilingStatuses, magiTiersByFiling: Record<string, MagiTierOption[]>, tobaccoStatuses, disabilityStatuses, chronicConditions, usStates, householdSizes }
```

### Plan Recommendation Models (`models/plan-recommendation.model.ts`)

```typescript
interface PlanRecommendationRequest { drugs: DrugSummaryInput[], selectedPharmacies?: SelectedPharmacyInput[] }
interface SelectedPharmacyInput { npi, name, pharmacyType }
interface DrugSummaryInput { rxCui, drugName, genericName, ndc?, estimatedRetailPrice? }
interface PlanRecommendationResult { lisEligible, lisTier, recommendedPlanType, eligibilitySummary, lisCallToAction?, rankedPlans[] }
interface RankedPlan { planId, planName, planType, planCategory: 'MA_ONLY'|'PDP_ONLY'|'PDP_MEDIGAP'|'MA_PDP', insuranceName, monthlyPremium, annualDeductible, annualMoop, estimatedAnnualDrugCost, estimatedAnnualTotalCost, drugCoverages[], aiExplanation, starRating, hasPreferredPharmacyNetwork, planFinderUrl, networkType, includesDental, includesVision, includesHearing, includesFitness, includesOtc, otcAllowancePerQuarter, gapCoverage, mailOrderSavings, providerNetworkSize, emergencyCoverage, pros: string[], cons: string[], costBreakdowns?: PlanCostBreakdown[] }
interface PlanCostBreakdown { pharmacyName, pharmacyNpi, isPreferredPharmacy, annualPremium, annualDeductible, annualDrugCopay, annualTotal, drugCopays: DrugCopayDetail[] }
interface DrugCopayDetail { drugName, rxCui, formularyTier, monthlyCopay, annualCopay, isCovered, preferredDiscount }
interface PlanDrugCoverage { drugName, rxCui, isCovered, formularyTier, monthlyCopay, requiresPriorAuth, hasQuantityLimit, quantityLimitDetail? }
interface LisCheckResult { lisEligible, lisTier }
interface GapAdviceRequest { planId, planName, planType, missingCoverages: string[] }
interface GapCoverageResult { gapPlans: GapPlan[], comparisonTip: string }
interface GapPlan { category, planName, planType, carrier, monthlyPremiumRange, annualDeductible, coverageHighlights: string[], whyNeeded, enrollmentTip, priority: 'Essential'|'Recommended'|'Optional' }
```

### Cost Projection Models (`models/cost-projection.model.ts`)

```typescript
interface CalculateCostsRequest { planBundleCode, medicareAdvantagePremium, maWithPrescriptionBenefit, partDOOP, partDOOPFullYear, partABenefitServiceCost, partBBenefitServiceCost, planRecommendName, recommendationListId, supplementDataProvided, partDDataProvided, reserveDaysUsed, dental, dentalHealthGrade, boughtPlanA, medicareAdvantageDataProvided, partDPremium, calculateForAdjustedMonth, supplementPlanType }
interface EvaluateCostsResponse { yearlyDetails: IndividualMedicareDetail[], lifetimeTotals: LifetimeTotals, evaluation: CostEvaluation }
interface IndividualMedicareDetail { year, monthsUsedForExpenseCalc, partAPremium, partBPremium, partBPremiumSurcharge, medicareAdvantagePremium, partDPremium, partDPremiumSurcharge, conciergePremium, partAOOP, partBOOP, partDOOP, totalABMedicareAdvantage, reserveDaysLeft, dentalPremium, dentalOOP }
interface LifetimeTotals { lifeTimeABMedicareAdvantageExpenses, lifeTimeABMedicareAdvantagePremium, lifeTimeABMedicareAdvantageOop, lifeTimeDSurcharge, lifeTimeBSurcharge, totalIrmaa, supplementPlanType, supplementPlanPremium }
interface CostEvaluation { planName, planBundleCode, lifetimeSummary: LifetimeSummary, costTrajectory: 'Rising'|'Stable'|'Declining'|'Mixed', trajectoryExplanation, yearlyHighlights: YearlyHighlight[], categories: CostCategory[], savingsTips: SavingsTip[], overallAssessment }
interface LifetimeSummary { totalPremiums, totalOutOfPocket, totalCombined, projectionYears, averageAnnualCost }
interface YearlyHighlight { year, totalCost, flag: 'Highest'|'Lowest'|'Spike'|'Normal', explanation }
interface CostCategory { name, lifetimeTotal, percentOfTotal, trend: 'Rising'|'Stable'|'Declining', insight }
interface SavingsTip { title, description, estimatedSavings, priority: 'High'|'Medium'|'Low' }
```

### Chat Message (in `drug-state.service.ts`)

```typescript
interface ChatMessage { role: 'user' | 'assistant' | 'system', content: string, timestamp: Date }
```

### Orchestrator Models (`models/orchestrator.model.ts`)

```typescript
interface OrchestratorRequest  { message: string }
interface OrchestratorResponse {
  reply: string;
  state: string;            // FSM state (e.g. 'idle', 'awaiting_drug_name', 'awaiting_confirmation')
  awaitingConfirmation: boolean;
  delta?: DeltaResult;
  displayData?: DisplayData;
}
interface DeltaResult {
  lifetimeBefore: number; lifetimeAfter: number;
  thisYearBefore: number; thisYearAfter: number;
  pvBefore: number;       pvAfter: number;
}
interface DisplayData { type: string; payload: any }
```

### Recommendation Models (`models/recommendation.model.ts`)

```typescript
interface RecommendationSummaryResponse {
  id: string;
  name: string;
  status: string;              // 'completed' | 'in-progress'
  drugCount: number;
  planCount: number;
  hasCostSnapshot: boolean;
  lifetimeTotal: number | null;
  createdAt: string;
  updatedAt: string;
}

interface CreateRecommendationRequest {
  name: string;
  profile: ProfileSnapshotDto;
  drugs: SelectedDrugDto[];
  pharmacy: SelectedPharmacySnapDto | null;
  mailOrderPharmacy: MailOrderPharmacyDto | null;
  plans: SelectedPlanDto[];
  costSnapshot: CostSnapshotDto | null;
  force?: boolean;
}

interface SelectedPlanDto {
  planType: string;
  planName: string;
  carrier: string;
  monthlyPremium: number;
  planId: string;
  deductible: number;
  starRating: number;
  totalPrescriptionCost: number;
  totalPlanCost: number;
  prescriptionDrugCovered: boolean;
  unavailableDrugs: string[];
  planExpenses: PlanExpenseDto[];
}

interface PlanExpenseDto { name: string; amount: number }

interface CostSnapshotDto {
  lifetimeTotal: number;
  currentYearTotal: number;
  averageAnnual: number;
  projectionYears: number;
  lifetimePremiums: number;
  lifetimeOOP: number;
  lifetimeIrmaa: number;
  costTrajectory: string;
  supplementPlanType: string;
  supplementPlanPremium: number;
  yearlyDetails: YearlyDetailDto[];
  evaluation: CostEvaluationDto | null;
}

interface YearlyDetailDto {
  year: number;
  partAPremium: number; partBPremium: number;
  partBSurcharge: number; maPremium: number;
  partDPremium: number; partDSurcharge: number;
  conciergePremium: number;
  partAOOP: number; partBOOP: number; partDOOP: number;
  totalABMA: number;
  dentalPremium: number; dentalOOP: number;
  reserveDaysLeft: number; monthsUsed: number;
}

interface CostEvaluationDto {
  planName: string; planBundleCode: string;
  lifetimeSummary: LifetimeSummarySnapDto;
  costTrajectory: string; trajectoryExplanation: string;
  yearlyHighlights: YearlyHighlightDto[];
  categories: CostCategorySnapDto[];
  savingsTips: SavingsTipSnapDto[];
  overallAssessment: string;
}

interface LifetimeSummarySnapDto { totalPremiums: number; totalOutOfPocket: number; totalCombined: number; projectionYears: number; averageAnnualCost: number }
interface YearlyHighlightDto { year: number; totalCost: number; flag: string; explanation: string }
interface CostCategorySnapDto { name: string; lifetimeTotal: number; percentOfTotal: number; trend: string; insight: string }
interface SavingsTipSnapDto { title: string; description: string; estimatedSavings: string; priority: string }
```

### LTC Models (`models/ltc.model.ts`)

```typescript
interface LtcProjectionRequest { /* profile-derived fields: age, gender, state, zip, countyCode, healthProfile, lifeExpectancy, tobaccoUsage, adultDayYears, homeCareYears, nursingCareYears */ }
interface LtcProjectionResponse {
  age: number; healthProfile: number; gender?: string; state?: string; zipcode: number; countyCode: number;
  lifeExpenctancy: number; tobaccoUsage: boolean; currentLifeStyleExpenses: number;
  numberOfAdultDayHealthCareLTCYears: number; numberOfHomeCareLTCYears: number;
  numberOfAssistedCareLTCYears: number; numberOfNursingCareLTCYears: number;
  adultDayHealthCare: number; presentValueAdultDayHealthCare: number;
  homeCare: number; presentValueHomeCare: number;
  assistedCare: number; presentValueAssistedCare: number;
  nursingCare: number; presentValueNursingCare: number;
  futureAdultDayHealthCareExpenseList: LtcExpenseEntry[];
  futureHomeCareExpenseList: LtcExpenseEntry[];
  futureAssistedCareExpensesList: LtcExpenseEntry[];
  futureNursingCareExpensesList: LtcExpenseEntry[];
  expectedHomeCare: number; expectedNursingCare: number;
  presentValueExpectedHomeCare: number; presentValueExpectedNursingCare: number;
}
interface LtcExpenseEntry { year: number; expense: number }
```

### Plan Models

**`models/part-d-plan.model.ts`:**
```typescript
interface PartDPlanRecommendationRequest { userId, sortRecommendations, countycodeModel: CountyCodeModel, prescriptions: PrescriptionInput[], pharmacies: PartDPharmacyInput[], taxFilingStatus, magiTier, healthGrade, birthDate, coverageYear, planPage, planPageSize, recommendationPage, recommendationPageSize, starRatingFilter, prescriptionCoverageFilter, contractIdFilter, mailOrderPharmacy, ... }
interface CountyCodeModel { zipcode, state, stateCode, city, latitude, longitude, countyCode, countyName }
interface PrescriptionInput { /* drug selection fields */ }
interface PartDPharmacyInput { /* pharmacy fields */ }
```

**`models/medicare-advantage-plan.model.ts`:**
```typescript
// Extends PartDPlanRecommendationRequest with medicareAdvantage: true
interface MedicareAdvantagePlanRequest extends PartDPlanRecommendationRequest { medicareAdvantage: true }
type MedicareAdvantagePlanResponse = PartDPlanRecommendationResponse
```

**`models/medigap-plan.model.ts`:**
```typescript
interface MedigapPlanQuotesRequest { zip5, gender, tobacco, birthDate, plan, county, taxFilingStatus, magiTier, healthProfile, coverageYear, versionId }
interface MedigapPlanQuotesResponse { contractIdCarrierMap: Record<string,string>, deductible: number, planList: MedigapPlan[] }
interface MedigapPlan { key, age, plan, rate: MedigapRate|null, rate_type, company_base: MedigapCompanyBase|null, discounts, fees, ... }
```

---

## Configuration

### `appConfig` (`app.config.ts`)
- Providers: `provideRouter(routes)`, `provideHttpClient(withInterceptors([authInterceptor]))`, `provideBrowserGlobalErrorListeners()`, `provideAnimationsAsync()`.
- SignalR client (`@microsoft/signalr`) is a plain npm package — no Angular provider registration needed. `ChatSignalRService` is `providedIn: 'root'` and manages its own `HubConnection` lifecycle.

### Routes (`app.routes.ts`)
- `/signin` → `SigninComponent` (lazy)
- `/signup` → `SignupComponent` (lazy)
- `/forgot-password` → `ForgotPasswordComponent` (lazy)
- `/` → `DashboardComponent` (authGuard) — child routes:
  - `''` → `dashboardRedirectGuard` auto-redirect
  - `profile` → `UserProfileComponent`
  - `medicare-analysis` → `AnalysisShellComponent` (profileCompleteGuard) — child routes:
    - `''` → redirects to `profile`
    - `profile` → `UserProfileComponent`
    - `drugs` → `DrugsStepComponent`
    - `pharmacies` → `PharmacyStepComponent`
    - `plans` → `PlansStepComponent`
    - `cost-projections` → `CostProjectionsComponent`
  - `long-term-care` → `LtcShellComponent` (profileCompleteGuard) — child routes:
    - `''` → redirects to `care-type`
    - `profile` → `UserProfileComponent`
    - `care-type` → `LtcCareTypeStepComponent`
    - `projection` → `LtcProjectionStepComponent`
  - `saved` → `RecommendationComponent`
  - `saved/compare` → `RecommendationCompareComponent`
  - `saved/:id` → `RecommendationDetailComponent`
- `**` → redirects to `/`

### Route Constants (`app-routes.const.ts`)
A centralized `AppRoutes` constant prevents hard-coded path strings across components and services. Includes relative path segments (e.g. `AppRoutes.LTC_CARE_TYPE = 'care-type'`) and absolute paths (e.g. `AppRoutes.abs.LTC_CARE_TYPE = '/long-term-care/care-type'`) for use in `router.navigate()` calls and `router.url` checks.

### Environments
- `environment.ts` / `environment.development.ts`: `apiUrl`, `appName`.

---

## Styling Strategy

- **Material Theme:** M3 theming via `mat.theme()` — cyan primary, orange tertiary, Roboto font.
- **Tailwind CSS 4:** Imported via `@import "tailwindcss"` in `styles.scss`, processed by `@tailwindcss/postcss`.
- **Custom Scrollbars:** Thin 6px scrollbars with subtle gray thumb.
- **Body:** `overflow: hidden` — each panel scrolls independently.
- **Component Hosts:** `:host { display: block/flex; height: 100% }` for proper flex layout.

---

## UI Flow

1. **Unauthenticated Access:** User lands on `/signin`. Can navigate to `/signup` or `/forgot-password`.
2. **Sign In / Sign Up:** JWT token + user info stored in sessionStorage (session ends on tab close). 1-hour expiry with auto-refresh on activity. Redirected to `/` (Dashboard).
3. **Post-Login Landing (All Users):** Dashboard loads profile and the default dashboard route redirects to `/profile` for both new and returning users. Left panel shows `UserProfileComponent` with consolidated single form (name, personal details, tax, concierge, planning, contact, address).
4. **Continue to Analysis:** From `/profile`, saving (or using existing completed data) navigates to `/medicare-analysis` (entry redirects to **`/medicare-analysis/profile`** — Profile is step 1 of the analysis shell). The guided chat wizard still routes users to **`/medicare-analysis/drugs`** when the chat `PROFILE` step is satisfied and drugs are needed, so deep-links and chat navigation may open Drugs directly without visiting the shell Profile step first. Edit Profile from the header also navigates to `/profile` in edit mode.
5. **Chat Startup:** Right chat adapts to profile status:
   - **Profile incomplete:** prompts user to complete profile first before analysis.
   - **Profile complete:** confirms profile is shown in view mode and asks whether user wants to modify anything.
   Mode selection cards ("Medicare Analysis", "Long Term Analysis") still appear only once profile API resolves and `isProfileComplete()` is true.
6. **Guided Wizard (Medicare Analysis):** User clicks "Medicare Analysis" → wizard starts immediately with fresh flow state (no saved-analysis/prescription chooser). The flow walks through steps: Profile → Drugs & Pharmacy → Plans → Analysis. Each step is announced via assistant message with auto-navigation. Steps auto-advance when completion signals fire (e.g., profile saved, drugs confirmed, pharmacies selected).
7. **Hard Refresh Resume:** Hard refresh on `/medicare-analysis/*` keeps the user on that same route (when profile is complete) and preserves in-progress state via persisted signals/session. Chat avoids startup mode prompts on analysis deep-links and shows a resume-aware message for the current step.
7. **Free-form Chat:** User can type naturally at any time. AI classifies intent (via `POST /api/chat/intent`) and routes to navigation, actions (reset, save, sign out), or drug analysis. Falls back to drug name suggestion flow for unrecognized input.
8. **Prescription (Profile Incomplete):** Chat shows assistant message and navigates to `/profile`.
9. **Prescription (Profile Complete):** Cyan bubble in chat. Spinner. Backend retrieves zipcode from user's saved address.
8. **Valid Drugs Response:** Drug cards render. Clinical alerts panel above cards. Chat summarizes findings.
9. **No Valid Drugs:** Chat shows message as assistant bubble. No drug cards rendered.
10. **User Configures Drug:** Selects Brand/Generic → Dosage Form → (filtered) Strength → (filtered) Packaging. Selection cascades. Progress dots fill.
11. **Confirm Drug:** "Select Drug" locks in selection. Panel collapses, auto-advances to next unconfirmed drug.
12. **Save Prescription:** "Continue" button opens save dialog. User names prescription → API saves it → snackbar + chat confirmation.
13. **Find Nearby Pharmacies:** Click button → lightweight NPI-only pharmacy list loads. User selects up to 5 pharmacies (toggle checkboxes). Counter shows X/5.
14. **Load Plan Recommendations:** Only appears after ≥1 pharmacy selected. Plans load with `costBreakdowns[]` per pharmacy. Each plan card shows compact view by default — "Plan Features" and "Cost Breakup" toggle buttons expand details.
15. **Calculate Lifetime Cost:** User clicks "Calculate Lifetime Cost" on a plan card → spinner shown on button → `POST /api/plan-recommendation/evaluate-costs` → AI evaluates cost data → navigates to `/medicare-analysis/cost-projections` → full dashboard with 4 Chart.js charts, summary cards, yearly highlights, category analysis, savings tips, and overall assessment. Back button returns to plans.
16. **Subsequent Queries:** Previous cards replaced.

---

← [Chapter 1 — Overview](ch01-overview.md) | [Table of Contents](APPLICATION_BLUEPRINT.md) | [Chapter 3 → Prompt Architecture](ch03-prompt-architecture.md)
