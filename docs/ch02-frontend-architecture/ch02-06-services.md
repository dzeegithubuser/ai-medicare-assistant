# Chapter 2.6 — Services

> All Angular services — HTTP clients, state management, SignalR, chat AI extraction, wizard orchestration, and snapshot persistence.

← [Chapter 2 — Frontend Architecture (Index)](../ch02-frontend-architecture/ch02-frontend-architecture.md)

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

### `MedicareStateService` (`services/drug-state.service.ts`)
- **Role:** Shared signal-based state management between chat and drug cards.
- **Message sync:** Injects `ChatSignalRService`. Every message mutation (`addUserMessage`, `addAssistantMessage`, `replaceLastAssistantMessage`, etc.) schedules a debounced SignalR sync via a 500 ms `setTimeout`. Rapid bursts (e.g. Medicare startup: greeting + profile review + mode buttons in < 200 ms) collapse into a single `ChatSignalRService.syncMessages()` WebSocket invoke. No HTTP call is made for message syncing.
- **Signals:** `drugs`, `interactions`, `dosageAlerts`, `duplicateTherapies`, `nearbyPharmacies`, `selectedPharmacies` (array, up to 5), `hasSelectedPharmacies` (computed), `messages` (`ChatMessage[]` with `'user' | 'assistant' | 'system'` role), `isLoading`, `currentStep` (`1 | 2 | 3 | 4` — analysis shell: Profile, Drugs, Pharmacies, Plans), `planRecommendation`, `selectedPlan`, `isPlanLoading`, `hasPlanRecommendation` (computed), `isLisEligible` (computed), `planAwarePharmacies`, `isPlanPharmacyLoading`, `hasPlanPharmacies` (computed), `drugSuggestions`, `hasSuggestions` (computed), `isVerifyingNames`, `confirmedDrugs` (signal wrapping `Set<string>`), `hasConfirmedDrugs` (computed), `prescriptionName`, `costProjection` (`EvaluateCostsResponse | null`), `hasCostProjection` (computed), `drugDetails` (`BulkDrugSearchResponse | null`), `isDrugDetailsLoading`, `hasDrugDetails` (computed), `wizardResetTrigger`, `pharmacySelectionConfirmed`, `returnRoute`, `pharmacyLookup`, `isPharmacyLookupLoading`, `hasPharmacyLookup` (computed), `selectedLookupPharmacies`, `hasSelectedLookupPharmacies` (computed), `confirmedDrugNames` (`Set<string>` signal), `hasConfirmedDrugs` (computed), `partDPlans`, `isPartDLoading`, `hasPartDPlans` (computed), `medigapQuotes`, `isMedigapLoading`, `hasMedigapQuotes` (computed), `maPlans`, `isMALoading`, `hasMAPlans` (computed), `selectedPartDPlan`, `selectedMedigapPlan`, `selectedMAPlan`, `selectedMAGapPartDPlan`, `activeSection`, `hasCompletePlanSelection` (computed — true when PDP+Medigap or MA(+gap PDP if needed) selected), `pendingDrugSelection` (`ChatDrugSelectionCommand | null` — chat-driven drug formulation command, watched by `DrugsStepComponent`), `pendingPharmacySelection` (`ChatPharmacySelectionCommand | null` — chat-driven pharmacy command, watched by `PharmacyStepComponent`), `pendingCrossPageDrugSearch` (`string | null` — drug search text stored when a drug input is typed on a non-drugs page, e.g. "add metformin" on pharmacies; picked up by `ChatComponent` on `NavigationEnd` to `/medicare-analysis/drugs` and cleared after `runDrugFlow()` fires).
- **Interfaces:** `ChatDrugSelectionCommand` (drugName, type, dosageForm, strength, quantity, action: select|options|confirm_all|remove|edit), `ChatPharmacySelectionCommand` (pharmacyName, action: select|remove|list|search, searchTerm).
- **Methods:** `addUserMessage()`, `addAssistantMessage()`, `addSystemMessage()` (UI action tracking — rendered as centered pill badges in chat), `hydrateMessagesFromServer()` (called by `DashboardComponent` with session data pushed from `ChatHub.OnConnectedAsync`), `togglePharmacy()` (max 5, returns false if limit reached), `isPharmacySelected(npi)`, `selectPharmacy()`, `setLoading()`, `setPlanRecommendation()`, `selectPlan()`, `setPlanLoading()`, `setPlanPharmacies()`, `setPlanPharmacyLoading()`, `clearPlanPharmacies()`, `setCostProjection()`, `setPharmacyLookup()`, `setPharmacyLookupLoading()`, `setDrugDetails()`, `setDrugDetailsLoading()`, `toggleLookupPharmacy()` (max 5, emits system messages), `isLookupPharmacySelected()`, `setDrugSuggestions()`, `setVerifyingNames()`, `clearSuggestions()`, `setPartDPlans()`, `setPartDLoading()`, `setMedigapQuotes()`, `setMedigapLoading()`, `setMAPlans()`, `setMALoading()`, `selectPartDPlan()`, `selectMedigapPlan()`, `selectMAPlan()`, `selectMAGapPartDPlan()`, `setActiveSection()`, `resetPlanSelections()`, `persistSelections()`, `resetAll()`.

### `AuthService` (`services/auth.service.ts`)
- **Signals:** `currentUser`, `isAuthenticated` (computed).
- **Methods:** `signUp()`, `signIn()`, `forgotPassword()`, `resetPassword()`, `handleAuthSuccess()`, `signOut()`, `getToken()`.
- **Sign-out Cleanup:** `signOut()` calls `sessionStorage.clear()` (clears all keys) then resets all in-memory signals: `MedicareStateService` (25+ signals via `resetAll()` + explicit signal resets), `ProfileService` (profile, isProfileComplete, editMode, pendingPrefill, pendingChatProfileData, missingRequiredFields), `RecommendationStateService.clear()`, `ChatSignalRService.disconnect()` (closes the WebSocket so the next user gets a fresh connection). Uses `Injector.get()` for lazy service resolution to avoid circular dependencies.
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
- **Role:** Fetches ZIP-based county code data.
- **Methods:** `getCountyCodeList(zipcode)` → cached county entries.
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

### `RecommendationService` (`services/recommendation.service.ts`)
- **Role:** HTTP CRUD service for user recommendation documents.
- **Methods:** `getActive()` → recommendation or 404, `getAll()` → `Observable<RecommendationSummaryResponse[]>` — `GET /api/recommendation/all` (all saved analyses for the user), `getById(id: string)` → `Observable<RecommendationResponse>` — `GET /api/recommendation/{id}` (full recommendation by ID, used by saved analysis selection flow), `create(request)`, `updateProfile(profile)`, `updateDrugs(drugs)`, `updatePharmacy(pharmacies, mailOrder)`, `updatePlans(plans)`, `delete()`.
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
- **Role:** Restores drugs (async bulk search), pharmacy, and **plan selections** from **`RecommendationStateService.activeRecommendation()`** into **`MedicareStateService`**.
- **Plan hydration:** **`hydratePlansFromActiveRecommendationSelection(silent?)`** matches saved **`planSelections`** to live Part D / Medigap / MA lists when available; otherwise **`hydratePlansFallbackFromSavedSelection`** applies stubs and posts **`SAVED_PLANS_PENDING`**. Successful partial/full restores post **`RESTORE_ALL_MATCHED`** or **`RESTORE_PARTIAL_DETAIL`** with explicit **Part D / Medigap / Medicare Advantage** bullet lines. Fingerprint guards reduce duplicate identical assistant messages when hydration runs more than once.

### `ErrorNotificationService` (`services/error-notification.service.ts`)
- **Role:** Opens a Material Dialog popup (`ErrorDialogComponent`) to display API errors to the user.
- **Singleton:** `providedIn: 'root'`. Guards against stacking — only one error dialog at a time (`isOpen` flag).
- **Method:** `show(data: ErrorDialogData)` — opens the dialog with `{ title?, message, detail? }`. Dialog width: 440px.
- **Called by:** `httpErrorInterceptor` exclusively.

### `AnalysisSnapshotService` (`services/analysis-snapshot.service.ts`)
- **Role:** Assembles a full analysis snapshot from current state (profile, drugs, pharmacies, plans, cost projections) and saves it as a recommendation via `RecommendationService.create()`.
- **Methods:**
  - `canSave()` → `boolean` — checks 5 prerequisites: profile complete, drugs confirmed, pharmacies selected, plan selected, cost projection available.
  - `save(name: string, force?: boolean)` → `Observable<any>` — builds the full snapshot request and calls `create()`. `force=true` overwrites existing.
- **Helpers:** `buildPlans()` maps selected plans with expanded fields (deductible, starRating, totalPrescriptionCost, planExpenses, unavailableDrugs) from `PharmacyWiseRecommendation`. `buildCostSnapshot()` maps yearly details + full AI evaluation object.
- **Injection:** Injects `MedicareStateService`, `ProfileService`, `RecommendationService`.

---

← [Saved Data & LTC Components](ch02-05-components-saved-ltc.md) | [Chapter 2 — Frontend Architecture (Index)](../ch02-frontend-architecture/ch02-frontend-architecture.md) | [Next → Guards & Models](ch02-07-guards-models.md)
