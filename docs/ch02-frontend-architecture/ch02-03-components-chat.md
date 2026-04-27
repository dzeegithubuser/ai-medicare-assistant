# Chapter 2.3 — Components: Chat & Markdown

> Right-panel chat assistant, intent routing, guided wizard, drug analysis flow, and markdown rendering.

← [Chapter 2 — Frontend Architecture (Index)](../ch02-frontend-architecture/ch02-frontend-architecture.md)

---

### `ChatComponent` (`chat/chat.component.ts`, `.html`, `.scss`)
- **Role:** Right-side chat panel (420px fixed width, full height). Implements `OnInit`.
- **State:** Injects `MedicareStateService`, `ProfileService`, `AuthService`, `ChatIntentService`, `ChatWizardService`, `ChatRouterService`, `ChatNavigationFlowService`, `ChatDrugFlowService`, `ChatProfileService`, `ChatDrugSelectionService`, `ChatPharmacySelectionService`, `RecommendationStateService`, `Router`.
- **Features:**
  - Chat header with AI Assistant branding, icon, and **wizard step indicator** (Profile › Drugs & Pharmacy › Plans › Analysis) — visible when wizard mode is `MEDICARE_ANALYSIS`. Current step highlighted cyan, completed steps green with check icon.
  - Startup greeting on first load ("Hello! I'm your Medicare Assistant. What would you like to do today?").
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
      2. **Profile-page message routing** — when on `/profile` or `/medicare-analysis/profile`, classifies intent first via AI. **`DRUG_INPUT` keyword-gated strategy:** when the classifier returns `DRUG_INPUT`, profile extraction is attempted first (via `routeToProfileExtraction` with an `onEmptyExtraction` callback). If extraction finds profile fields (e.g. "magitier is 150" → `{ magiTier: "150" }`), they are applied normally — the user stays on the profile page. When extraction returns empty, the **keyword gate** decides: if the text contains an explicit drug keyword (`DRUG_KEYWORD_PATTERN`: drug, medication, medicine, prescription, rx, meds, pill, tablet, capsule), `pendingCrossPageDrugSearch` is set and the intent reclassified as `NAVIGATE_ANALYSIS_DRUGS` — navigating to drugs where the search auto-fires. If no drug keyword is found (bare drug name like "metformin"), a guidance hint is shown instead ("Navigate to the Drugs step or type 'go to drugs'"). `UNKNOWN` and `NAVIGATE_PROFILE` with field params fall through to `ChatProfileService.extractProfile()` for profile field extraction. All other intents (navigation, actions) pass through to `handleIntent()` normally.
      3. **Plan selection extraction** — when on `/medicare-analysis/plans` with loaded plan data, routes to `ChatPlanSelectionService.extractSelection()`. Supports select plans (Part D, Medigap, MA), remove plan selections, and switch between PDP/MA sections. **`DRUG_INPUT` keyword gate:** if `DRUG_INPUT` is classified, checks for drug keyword — with keyword, redirects to drugs with auto-search; without keyword, shows guidance hint. **Action bypass:** messages matching `ACTION_PATTERNS` (save prescription, save analysis, run analysis, help, sign out, etc.) fall through to the intent classifier.
      4. **Drug selection extraction** — when on `/medicare-analysis/drugs` with loaded drug details, routes to `ChatDrugSelectionService.extractSelection()`. Supports select, confirm_all, remove (with confirmation), edit (with confirmation). **Action bypass:** same `ACTION_PATTERNS` guard.
      5. **Pharmacy selection extraction** — when on `/medicare-analysis/pharmacies` with loaded pharmacy lookup, classifies intent first (single API call — pre-classified result forwarded directly, no second call). Routes to `ChatPharmacySelectionService.extractSelection()` only for `UNKNOWN` / `NAVIGATE_PHARMACIES` intents. **`DRUG_INPUT` keyword gate:** if a drug name is typed on the pharmacy page, checks `DRUG_KEYWORD_PATTERN` — with keyword ("add drug eliquis"), `pendingCrossPageDrugSearch` is set and navigates to drugs; without keyword ("eliquis"), shows guidance hint. Supports select, remove (with confirmation), search (filters by name), list. **Action bypass:** same `ACTION_PATTERNS` guard.
      6. **Intent classifier** — default fallback to `ChatIntentService.classify()` → `handleIntent()` routes 17 intents.
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
  - **Return Route:** `saveReturnRoute()` helper captures `router.url` when navigating away from a `/medicare-analysis/*` route (e.g., to profile). Stored in `MedicareStateService.returnRoute`. Profile component reads it on save/close to return the user to their previous analysis step instead of the default `/medicare-analysis`.
  - **Drug Analysis Flow (`runDrugFlow`):**
    - Profile gate: checks `profileService.isProfileComplete()`, redirects to `/profile` if incomplete.
    - Calls `DrugService.suggestNames()` → AI returns candidates → interactive selection panel with clickable chips. High-confidence (≥0.95) or single-candidate drugs auto-selected.
    - "Confirm & Analyze" → confirmed names sent to `DrugService.searchDrugsBulk()` → drug details loaded → navigates to drugs step.
  - **Drug Name Selection Panel:** Inline panel with per-drug rows showing input name + candidate buttons (cyan when selected). "Confirm & Analyze" enabled only when all drugs have a selection. "Cancel" clears suggestions.
- **Local State:** `selectedNames` Map tracks candidate selection per input drug.
- **Effects:** (1) auto-scroll on message/loading/suggestion changes, (2) wizard auto-advance on `hasNewStep`, (3) wizard reset on `wizardResetTrigger`, (4) when `activeRecommendation` / loading state changes: `autoHydrateStoredDrugsIfNeeded()` and `autoHydrateStoredPharmacyIfNeeded()` (session-gated: auto-load saved drugs on `/medicare-analysis/drugs` and saved pharmacy on `/medicare-analysis/pharmacies` without a yes/no prompt). **`NavigationEnd` handler:** clears `pendingProfileModifyDetail` when not on `/medicare-analysis/profile`; re-runs the two auto-hydrate helpers; when the URL is **`/medicare-analysis/plans`**, calls **`ChatAnalysisSelectionHydrationService.hydratePlansFromActiveRecommendationSelection()`** so footer navigation and chat navigation both restore saved plan rows and post assistant summaries (Part D / Medigap / MA bullets; duplicate identical posts suppressed via fingerprints inside the hydration service); triggers cross-page drug auto-search when landing on `/medicare-analysis/drugs` with `pendingCrossPageDrugSearch` set.
- **Change Detection:** OnPush (signals drive all reactivity).
- **Markdown Rendering:** Assistant messages use `[innerHTML]="msg.content | markdown"` via MarkdownPipe; user messages stay plain text.
- **Error Handling:** Differentiates network timeout (`status === 0`) vs server error for user-friendly messages.
- **Chat-Driven Helpers:**
  - `buildAvailableDrugSummaries()` — extracts types, dosage forms, strengths per drug from loaded drug details for the AI drug selection extractor.
  - `buildPharmacySummaries()` — builds available + selected pharmacy summaries from `pharmacyLookup()` and `selectedLookupPharmacies()` for the AI pharmacy selection extractor.

### `MarkdownPipe` (`pipes/markdown.pipe.ts`)
- **Role:** Transforms markdown strings to safe HTML for rendering in assistant chat bubbles.
- **Implementation:** Uses `marked` library with GFM + breaks enabled. Output passed through `DomSanitizer.bypassSecurityTrustHtml()`.
- **Styling:** `.markdown-body` class in `chat.component.scss` provides heading, table, list, and code block styles.

---

← [Auth & Dashboard Components](ch02-02-components-auth-dashboard.md) | [Chapter 2 — Frontend Architecture (Index)](../ch02-frontend-architecture/ch02-frontend-architecture.md) | [Next → Medicare Analysis Components](ch02-04-components-medicare.md)
