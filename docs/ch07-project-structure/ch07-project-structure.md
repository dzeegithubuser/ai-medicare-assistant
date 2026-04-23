# Chapter 7 — Project Structure

> Full directory tree for frontend and backend projects.

---

## Frontend (`ui-ai-medicare-assistant/src/`)

```
index.html                        → HTML shell with Google Fonts + Material Icons
main.ts                           → Bootstrap App with appConfig
styles.scss                       → Tailwind CSS import + custom scrollbar styles
material-theme.scss               → Angular Material M3 theme (cyan/orange)
app/
  app.ts                          → Root component (router-outlet only)
  app.config.ts                   → Angular providers (router, httpClient + httpLoaderInterceptor + authInterceptor + httpErrorInterceptor)
  app.routes.ts                   → Lazy-loaded routes (signin, signup, forgot-password, dashboard) — uses AppRoutes constants
  app-routes.const.ts              → Central route path registry (AppRoutes const with segment names + abs: { } absolute paths). All navigation and URL checks import from here — single place to rename any route
  models/
    drug.model.ts                 → Drug, Formulation, DrugAnalysisResponse, DrugNameSuggestionResult, DrugNameSuggestion, DrugCandidate, DrugPrice, PharmacyWithPricing, PlanPharmacySearchRequest, PlanCoverageInput, PharmacyLookupEntry, PharmacyLookupResponse interfaces
    auth.model.ts                 → Auth request/response, AuthUser interfaces
    profile.model.ts              → UserProfileResponse, ProfileDto
    reference-data.model.ts       → ReferenceData, LabelValue, MagiTierOption, HouseholdSizeOption
    plan-recommendation.model.ts  → PlanRecommendationResult, RankedPlan (with extended benefit fields), PlanDrugCoverage
    cost-projection.model.ts      → CalculateCostsRequest, EvaluateCostsResponse, IndividualMedicareDetail, LifetimeTotals, CostEvaluation, LifetimeSummary, YearlyHighlight, CostCategory, SavingsTip
    ltc.model.ts                  → LongTermCareRequest, LongTermCareResponse, LtcExpenseEntry, SaveLtcCurrentRequest, LtcCurrentResponse interfaces
    medicare-advantage-plan.model.ts → MedicareAdvantagePlanRequest and MA plan response interfaces
    medigap-plan.model.ts         → MedigapPlanQuotesRequest, MedigapPlanQuotesResponse, MedigapPlanQuote and nested interfaces
    part-d-plan.model.ts          → PartDPlanRecommendationRequest and Part D plan response interfaces
    recommendation.model.ts       → CreateRecommendationRequest, RecommendationResponse, RecommendationSummaryResponse, SelectedDrugDto, SelectedPlanDto, SelectedPharmacyDto, CostSnapshotDto interfaces
    chat-state.model.ts           → ChatMessage, ChatUiState, ChatSession interfaces
  services/
    drug.service.ts               → HTTP service for /api/drug/suggest-names, /api/drug/analyze, /api/pharmacy/plan-search, /api/pharmacy/lookup
    drug-state.service.ts         → Signal-based shared state (class: MedicareStateService)
    auth.service.ts               → Signal-based auth state (JWT token, user, signIn/signUp/signOut)
    profile.service.ts            → Signal-based profile state orchestrator (load + save + updateState)
    county-lookup.service.ts      → ZIP-based county code lookup with caching + MAGI tiers
    reference-data.service.ts     → Signal-based master data service (fetches + caches /api/reference-data)
    prescription.service.ts        → HTTP service for /api/prescription (save + list)
    plan-recommendation.service.ts → HTTP service for /api/plan-recommendation (recommend, checkLis, getGapAdvice, evaluateCosts)
    medicare-advantage-plan.service.ts → HTTP service for /api/MedicareAdvantagePlan/recommend
    medigap-plan.service.ts        → HTTP service for /api/MedigapPlan/quotes
    part-d-plan.service.ts         → HTTP service for /api/PartDPlan/recommend
    recommendation.service.ts      → HTTP service for /api/recommendation (CRUD: create, getActive, getAll, getById, updateProfile/drugs/pharmacies/plans/costSnapshot, delete)
    recommendation-state.service.ts → Signal-based active recommendation state (hydration, patch helpers, selection signals)
    chat-intent.service.ts         → HTTP service for /api/chat/intent (AI intent classification)
    chat-wizard.service.ts         → Reactive wizard state management (mode, step tracking, auto-advance signals)
    chat-plan-selection.service.ts  → HTTP service for /api/chat/extract-plan-selection (AI plan selection extraction)
    chat-drug-selection.service.ts  → HTTP service for /api/chat/extract-drug-selection
    chat-drug-flow.service.ts       → Handles drug-step chat interactions (search, confirm, remove)
    chat-drug-selection-flow.service.ts → Drug selection flow state machine (guided type→form→strength→qty)
    chat-pharmacy-selection.service.ts → HTTP service for /api/chat/extract-pharmacy-selection
    chat-pharmacy-selection-flow.service.ts → Pharmacy selection flow (select, remove, list)
    chat-plan-selection-flow.service.ts → Plan selection flow (select, remove, switch section)
    chat-profile.service.ts         → Profile field extraction via /api/chat/extract-profile
    chat-profile-edit-flow.service.ts → Chat-driven profile edit flow (collect fields, submit)
    chat-navigation-flow.service.ts → Chat navigation actions (route user to steps)
    chat-analysis-selection-hydration.service.ts → Hydrates wizard selections from active recommendation or userAnalysisSelections MongoDB on bootstrap
    chat-intent-phrase.service.ts   → Generates human-readable chat replies for specific intents
    chat-ltc-care-type-flow.service.ts → Chat-driven LTC care type selection flow
    chat-router.service.ts          → Routes chat intent responses to correct flow handlers
    chat-router.constants.ts        → Constants for chat intent routing (intent → flow mapping)
    chat-router-summary.service.ts  → Builds context summaries injected into chat prompts (current page, selected drugs/pharmacies/plans)
    chat-session.service.ts         → HTTP client for /api/chat/session (messages + ui-state)
    chat-signal-r.service.ts        → SignalR WebSocket connection (connect, disconnect, syncMessages, session$ ReplaySubject)
    analysis-snapshot.service.ts    → Assembles full analysis snapshot (profile, drugs, pharmacies, plans, cost) and saves via RecommendationService
    ltc-analysis-snapshot.service.ts → Assembles LTC analysis snapshot and saves as recommendation
    plan-card-enrichment.service.ts → Pure computation service — derives display fields (formatted plan IDs, carrier names, surcharges, OOP, pharmacy/drug ratios, Medigap cents→dollars) from raw API responses for Part D, Medigap, and MA cards
    font-size.service.ts            → User font size preference management
    theme.service.ts                → Theme/dark mode management
    error-notification.service.ts    → Opens ErrorDialogComponent via MatDialog for global API error popups (singleton, dedup guard)
    http-loader.service.ts          → Global HTTP loading state (signal-based — true when any HTTP request is in-flight)
  shared/
    error-dialog/
      error-dialog.component.ts   → Standalone Material Dialog for global API error popups (red icon, friendly message, collapsible technical details, themed OK button)
  interceptors/
    auth.interceptor.ts           → HttpInterceptorFn — attaches Bearer token to requests
    http-error.interceptor.ts     → HttpInterceptorFn — global API error handler (catches HttpErrorResponse, maps status codes to user-friendly messages, opens ErrorDialogComponent via ErrorNotificationService)
  guards/
    auth.guard.ts                 → CanActivateFn — redirects unauthenticated to /signin
    profile-complete.guard.ts     → CanActivateFn — protects /medicare-analysis, redirects to /profile
    dashboard-redirect.guard.ts   → CanActivateFn — auto-redirect for default child route
  auth/
    signin/
      signin.component.ts/html/scss   → Sign-in form (email + password)
    signup/
      signup.component.ts/html/scss   → Registration form (email, phone, password, confirm)
    forgot-password/
      forgot-password.component.ts/html/scss → Forgot password form (email)
    reset-password/
      reset-password.component.ts/html/scss  → Reset password form (reads ?token= from URL, 2 password fields)
    verify-email/
      verify-email.component.ts/html/scss    → Email verification page (reads token from URL)
    change-password/
      change-password.component.ts/html/scss → Change password form (old + new + confirm, [Authorize])
  dashboard/
    dashboard.component.ts        → Authenticated shell (header + split layout + initial post-login route handling)
    dashboard.component.html      → Child router-outlet left panel + chat right panel
    dashboard.component.scss      → Host styling + slideIn animation
  user-profile/
    user-profile.component.ts     → Consolidated single-form profile (all fields in one form)
    user-profile.component.html   → Profile form template
    user-profile.component.scss   → Profile form styling
  chat/
    chat.component.ts             → Right-panel chat logic
    chat.component.html           → Chat bubbles + input template
    chat.component.scss           → Chat host styling
  medicare-analysis/
    analysis-shell.component.ts/html/scss → 4-step wizard shell (Profile → Drugs → Pharmacies → Plans: step indicator + router-outlet + Back/Continue nav). Routed at /medicare-analysis
    current-prescription.mapper.ts → Maps DrugStateService signals into CurrentPrescriptionSnapshot for snapshot saving
    cost-projection-messages.ts       → COST_PROJECTION_IMMUTABILITY_WARNING constant string
    drug-step/
      drug-step.component.ts   → Drugs step (shell step 2): Financial Planner drug orchestrator (state, confirm/edit/remove, save prescription, persistence)
      drug-step.component.html → Composes child sub-components inside expansion-panel accordion
      interaction-alerts/
        fp-interaction-alerts.component.ts/html → Drug interaction severity cards (High/Moderate/Low)
      duplicate-therapy-alerts/
        fp-duplicate-therapy-alerts.component.ts/html → Duplicate therapy warning cards
      drug-selection-panel/
        fp-drug-selection-panel.component.ts/html → 4-step guided selection (type→form→strength→qty) + confirm/edit per drug
      selected-drugs-summary/
        fp-selected-drugs-summary.component.ts/html → Confirmed drugs summary with edit/remove + save prescription button
      save-prescription-dialog/
        save-prescription-dialog.component.ts/html → MatDialog for naming + saving a prescription (accepts optional title/subtitle/icon via MAT_DIALOG_DATA)
    pharmacy-step/
      pharmacy-step.component.ts/html/scss → Pharmacies step (shell step 3): Financial Planner pharmacy lookup with filters (name, radius, page size), pagination, Google Maps links (spot on map + directions), multi-select (max 5)
    plans-step/
      plans-step.component.ts/html/scss → Plans step (shell step 4): Medicare plan recommendations + plan-aware pharmacies
  data/
    tooltips.ts                        → Centralized tooltip/description data (plan types, pharmacy types, formulary tiers, network types, coverage info, gap coverage)
  plan-recommendation/
    plan-recommendation.component.ts/html → Plan recommendation shell (MA, Part D, Medigap plan slots + tabs) with PlanCardEnrichmentService integration (computed enrichment maps for all card types)
    plan-recommendation.component.scss → Plan recommendation layout styling
    recommendation-card/
      recommendation-card.component.ts/html → Individual plan card (plan details, costs, benefits grid) — accepts `enriched` input (EnrichedPartDCard | EnrichedMACard) and `cardType` ('partd' | 'ma') for enriched display of Part D surcharge, Rx OOP, drugs covered, pharmacies in network, insurance carrier, formatted plan ID, MA surcharges, healthcare OOP
    medigap-card/
      medigap-card.component.ts/html         → Medigap supplemental plan card (rates, carrier info, discounts) — accepts `enriched` input (EnrichedMedigapCard) for carrier lookup from contractIdCarrierMap, cents→dollars premium, Part B surcharge, healthcare OOP, remaining months
    medigap-gap-section/
      medigap-gap-section.component.ts/html  → Medigap gap section sub-panel — passes enriched data to medigap cards via PlanCardEnrichmentService
    partd-gap-section/
      partd-gap-section.component.ts/html    → Part D gap section sub-panel — passes enriched data to recommendation cards via PlanCardEnrichmentService
    plan-detail-dialog/
      plan-detail-dialog.component.ts/html   → MatDialog: full plan detail view (benefits, cost breakdowns, drug coverage)
    selected-plans-summary/
      selected-plans-summary.component.ts/html → Summary of selected MA + Part D + Medigap plan slots
  long-term-care/
    ltc-shell.component.ts/html/scss → LTC wizard shell (Profile → Care Type → Projection: step indicator + router-outlet + Back/Continue nav). Routed at /long-term-care
    ltc-state.service.ts             → Signal-based LTC step state (current step, ltcResult, selections)
    ltc.service.ts                   → HTTP service for /api/long-term-care + /api/ltc (projection + selections CRUD)
    care-type-step/
      ltc-care-type-step.component.ts/html/scss → LTC step 2: care type year selection (adult day, home, nursing) + health profile
    projection-step/
      ltc-projection-step.component.ts/html/scss → LTC step 3: projection results dashboard (per-care-type totals, year-by-year charts, present-value breakdowns)
  cost-projections/
    cost-projections.component.ts/html/scss → Full-page cost projections dashboard with Chart.js charts (line, stacked bar, doughnut, surcharges bar), summary cards, yearly highlights, category analysis, savings tips, overall assessment, Save Analysis button
  recommendation/
    recommendation.component.ts/html        → Recommendations page with full client-side filter/sort/pagination and compare basket (uppercase analysis names)
    compare/
      recommendation-compare.component.ts/html → Side-by-side comparison orchestrator (/saved/compare) — dispatches to CompareMedicare, CompareLtc, or CompareCross. Shows recommendation name + Illustration A/B alias in orange/green
      compare-helpers.ts                 → Shared comparison utilities (LABEL_A/B aliases, CHART_COLOR_A/B orange/green palette, deltaIcon, deltaLabel, deltaClass, getTrajectoryIcon/Color, getPriorityColor, starArray, typeBadgeClass, typeLabel, buildProfileRows, ProfileRow interface)
      tab-profile/
        tab-profile.component.ts/html    → Shared profile comparison tab (4 grouped sections: Personal, Location, Health, Financial). Used by Medicare, LTC, and Cross compare
      medicare/
        compare-medicare.component.ts/html → Medicare-vs-Medicare comparison shell (4-tab: Overview, Profile, Rx Pharmacy & Plans, Cost Analysis)
        compare-medicare-metrics.component.ts → Medicare KPI metrics cards (inline template — cost + profile metrics grid with orange/green labels)
        tab-overview/
          tab-overview.component.ts/html → Overview tab — 6 KPI delta cards, winner banner, profile diffs, Rx summary, pharmacy, plans, projections
        tab-cost-analysis/
          tab-cost-analysis.component.ts/html → Cost Analysis tab — Chart.js line + bar charts (orange/green series), year-by-year delta table, category comparison, assessment cards
        tab-rx-pharmacy-plans/
          tab-rx-pharmacy-plans.component.ts/html → Rx, Pharmacy & Plans tab — side-by-side prescription drug cards, pharmacy comparison cards, plan cards with star ratings
      ltc/
        compare-ltc.component.ts/html      → LTC-vs-LTC comparison (4-tab: Overview, Profile, Care Config, Cost Analysis)
        compare-ltc-metrics.component.ts   → LTC KPI metrics cards (inline template — cost + profile metrics grid with orange/green labels)
      cross/
        compare-cross.component.ts/html    → Medicare-vs-LTC cross-type comparison (3-tab: Overview, Profile, Cost Summary)
        compare-cross-metrics.component.ts → Cross-type KPI metrics cards (inline template — dispatches to LTC/Medicare snapshot per side)
    recommendation-detail.component.ts/html/scss → Full detail view of a single saved analysis (/saved/:id) — hero header, KPI strip, grouped profile/pharmacy/plans/cost tabs
  constants/
    chat-messages.ts                  → Chat message constant strings
  pipes/
    markdown.pipe.ts                  → Markdown rendering pipe
  utils/
    pharmacy-chat-resolve.ts          → Pharmacy chat resolution utility

environments/
  environment.ts                  → Production config
  environment.development.ts      → Dev config (apiUrl: localhost:5024)
```

---

## Backend (`api-ai-medicare-assistant/`)

```
AI.MedicareAssistant.Api/
  Program.cs                      → App builder, DI, CORS, JWT, MongoDB, Serilog, middleware pipeline
  Middleware/
    GlobalExceptionMiddleware.cs   → Global exception handler (AppException → HTTP status + JSON)
  Controllers/
    DrugController.cs             → Drug name suggestion REST endpoint
    PharmacyController.cs         → Pharmacy lookup (Financial Planner API)
    PlanRecommendationController.cs → Medicare plan cost evaluation
    PrescriptionController.cs     → [Authorize] Save + update current analysis selections (MongoDB)
    AuthController.cs             → Sign up, sign in, forgot/reset password, verify email, resend verification, change password
    ReferenceDataController.cs    → Public master data endpoint
    ProfileController.cs         → [Authorize] consolidated profile GET/POST
    CountyLookupController.cs     → ZIP-based county code lookup + MAGI tiers endpoint
    FinancialPlannerDrugController.cs → [Authorize] Financial Planner drug bulk-search with AI interactions
    ChatIntentController.cs       → [Authorize] AI-powered chat intent classification (POST api/chat/intent + 4 extract endpoints)
    ChatSessionController.cs      → [Authorize] Chat session start-new endpoint
    RecommendationController.cs   → [Authorize] Recommendation CRUD (GET/POST/PUT profile|drugs|pharmacy|plans|cost-snapshot/DELETE api/recommendation)
    LongTermCareController.cs     → [Authorize] LTC cost projection (POST api/long-term-care)
    LtcSelectionsController.cs    → [Authorize] LTC care-type selections persistence (PUT/GET api/ltc/current)
    MedicareAdvantagePlanController.cs → [Authorize] MA plan recommendations (POST api/MedicareAdvantagePlan/recommend)
    MedigapPlanController.cs      → [Authorize] Medigap plan quotes (POST api/MedigapPlan/quotes)
    PartDPlanController.cs        → [Authorize] Part D plan recommendations (POST api/PartDPlan/recommend)
  Prompts/                        → File-based prompt system (4 folders: system, tasks, schemas, templates)
    system/                       → System role prompts (chat-intent-system, cost-evaluation-system, drug-name-suggestion-system, drug-selection-system, ltc-evaluation-system, pharma-system, pharmacy-selection-system, plan-scoring-system, plan-selection-system, profile-extract-system)
    tasks/                        → Task instructions (cost-evaluation, drug-name-suggestion, drug-normalization, ltc-evaluation, plan-scoring)
    schemas/                      → JSON output schemas (cost-evaluation-schema, drug-json-schema, drug-name-suggestion-schema, ltc-evaluation-schema, plan-scoring-schema)
    templates/                    → User prompt templates with {{placeholders}} (cost-evaluation, drug-name-suggestion, ltc-evaluation, plan-scoring, prescription-analysis)
  Logs/                           → Daily rolling log files (30 day retention)
  appsettings.json                → OpenAI + CMS + JWT + MongoDB configuration

AI.MedicareAssistant.Domain/
  Documents/
    UserDocument.cs               → MongoDB: merged user + profile document (email, phone, passwordHash, isEmailVerified, profile fields, isProfileComplete, timestamps)
    ChatSessionDocument.cs        → MongoDB: chat session messages + UI state
    LtcCurrentSelectionsDocument.cs → MongoDB: per-user LTC care-type inputs + last projection result (collection ltcCurrentSelections)
    PrescriptionDocument.cs       → MongoDB: named prescription with embedded drug list
    RecommendationDocument.cs     → MongoDB: full recommendation (ProfileSnapshot, SelectedDrugDoc, SelectedPlanDoc, SelectedPharmacyDoc, MailOrderPharmacyDoc, CostSnapshotDoc and nested types)
    UserAnalysisSelectionsDocument.cs → MongoDB: per-user current analysis selections — drugs, pharmacies, plans, activeSection (collection userAnalysisSelections)
  Models/
    AnthropicModels.cs            → Anthropic API request/response models
    ConstantItem.cs               → Financial Planner constants item model
    CostProjection.cs             → CostProjectionResult, LifetimeTotals, CostEvaluation, LifetimeSummary, YearlyHighlight, CostCategory, SavingsTip (combined Financial Planner + AI evaluation models)
    DrugAnalysisResult.cs         → DrugResult, DrugFormulation, MedicareCostEstimate models
    DrugNameSuggestion.cs         → DrugNameSuggestionResult, DrugNameSuggestion, DrugCandidate models
    FinancialPlannerDrug.cs       → DrugSearchRequest/Response, DrugListItem, DrugDetailRequest/Response, DrugDetailAdvanceItem, DrugSearchResult, BulkDrugSearchResponse, DrugInteractionAnalysis (Financial Planner drug search + AI interaction models)
    GeminiModels.cs               → Google Gemini API request/response models
    IndividualMedicare.cs         → IndividualMedicareRequest, IndividualMedicareResponse, IndividualMedicareDetail (Financial Planner API models)
    LongTermCare.cs               → LongTermCareRequest, LongTermCareResponse, LtcExpenseEntry (Financial Planner LTC API models)
    LtcCostEvaluation.cs          → AI-generated LTC cost evaluation models
    MedicareAdvantagePlan.cs      → MedicareAdvantagePlanRequest (MA plan recommendation request with MedicareAdvantage=true)
    MedigapPlanQuotes.cs          → MedigapPlanQuotesRequest, MedigapPlanQuotesResponse, MedigapPlanQuote + nested carrier/rate/discount types
    PartDPlanRecommendation.cs    → PartDPlanRecommendationRequest, CountyCodeModel, PrescriptionInput, PharmacyInput (Part D plan recommendation models)
    PlanRecommendation.cs         → LisTier, PlanType, PlanRecommendationRequest/Result, RankedPlan
    PresentValue.cs               → PresentValueRequest, PresentValueResponse, YearExpense, PvEntry, PresentValueYears, RateOfReturns (Financial Planner expensesPresentValue API models)
    Pharmacy/PharmacyModels.cs    → PharmacyResult, DrugPrice, PharmacyWithPricing, DrugPricingInput, PlanPharmacySearchRequest, PlanCoverageInput
  Exceptions/
    AppExceptions.cs              → AppException, NotFoundException, ValidationException, etc.
  Interfaces/
    ICmsPlanDataService.cs        → CMS SOCRATA plan/formulary data contract
    IConstantsService.cs          → Financial Planner constants API contract
    ICostEvaluationAiService.cs   → AI cost evaluation contract (EvaluateAsync → CostEvaluation)
    ICountyLookupService.cs       → ZIP-based county code lookup contract
    IDrugAiService.cs             → AI service contract (AnalyzePrescription, SuggestDrugNames)
    IEmailService.cs              → Email delivery service contract
    IFdaNdcService.cs             → FDA NDC Directory package info contract
    IFinancialPlannerDrugService.cs → Financial Planner drug bulk-search contract (SearchBulkAsync)
    IIndividualMedicareService.cs  → Financial Planner individualMedicareR5 API contract
    ILongTermCareService.cs       → LTC cost projection contract (GetProjectionAsync)
    ILtcEvaluationAiService.cs    → AI LTC cost evaluation contract
    IMedicareAdvantagePlanService.cs → MA plan recommendation contract (RecommendAsync)
    IMedicareCostService.cs       → Medicare cost service contract
    IMedigapPlanQuotesService.cs  → Medigap plan quotes contract (GetQuotesAsync)
    IMongoRepositories.cs         → MongoDB repository interfaces: IPrescriptionDocRepository, IChatSessionRepository, IUserAnalysisSelectionsRepository, IRecommendationRepository, ILtcSelectionsRepository
    IPartDPlanRecommendationService.cs → Part D plan recommendation contract (RecommendAsync)
    IPharmacyLookupService.cs     → Financial Planner pharmacy lookup contract + request/response models
    IPlanScoringAiService.cs      → AI plan scoring + explanation contract
    IPresentValueService.cs       → Financial Planner expensesPresentValue API contract (CalculateAsync)
    IProfileRepositories.cs       → IProfileRepository interface (operates on UserDocument)
    IUserRepository.cs            → User data access contract (operates on UserDocument)

AI.MedicareAssistant.Application/
  DTOs/
    AuthDtos.cs                   → SignUpRequest, SignInRequest, ForgotPassword, ResetPassword, AuthResponse
    PrescriptionDtos.cs           → SavePrescriptionRequest, PrescriptionDrugDto, PrescriptionResponse
    ProfileDtos.cs                → ProfileDto, UserProfileResponse
    ProfileExtractDtos.cs         → ProfileExtractRequest, ProfileExtractResponse (extracted fields + reply)
    ChatIntentDtos.cs             → ChatIntentRequest, ChatIntentResponse, ChatIntentParams
    ChatSessionDtos.cs            → ChatSessionMessageDto, ChatUiStateDto (session persistence DTOs)
    DrugSelectionDtos.cs          → DrugSelectionExtractRequest, DrugSelectionExtractResponse (drug formulation selection extraction)
    PharmacySelectionDtos.cs      → PharmacySelectionExtractRequest, PharmacySelectionExtractResponse (pharmacy selection extraction)
    PlanSelectionDtos.cs          → PlanSelectionExtractRequest, PlanSelectionExtractResponse (plan selection extraction)
    OrchestratorDtos.cs           → OrchestratorRequest, OrchestratorResponse, DeltaResult, DisplayData, OrchestratorIntentResult
    RecommendationDtos.cs         → CreateRecommendationRequest, UpdateProfileRequest, UpdateDrugsRequest, UpdatePharmacyRequest, UpdatePlansRequest, UpdateCostSnapshotRequest, RecommendationSummaryResponse + nested doc DTOs
    LongTermCareDtos.cs           → LongTermCareRequest, LongTermCareResponse, LtcExpenseEntry (Application-layer validated versions)
    LtcSelectionsDtos.cs          → SaveLtcCurrentRequest (validation attributes), LtcCurrentResponse
    PlanCardEnrichmentDtos.cs     → EnrichedPartDCard (planIdDisplay, insuranceCarrier, partDSurcharge, prescriptionOOP, pharmaciesInNetwork, drugsCovered), EnrichedMedigapCard (premiumMonthly, premiumAnnual, insuranceCarrier, partBSurcharge, healthcareOOP, remainingMonths), EnrichedMACard (planIdDisplay, insuranceCarrier, surcharges, prescriptionOOP, healthcareOOP, pharmaciesInNetwork, drugsCovered)
  Services/
    Pipeline/                     → (empty — pipeline steps removed)
    AuthService.cs                → JWT auth logic
    ChatIntentService.cs          → AI-powered chat intent classification (file-based prompt)
    ChatSessionService.cs         → Chat session persistence (get/update messages + ui-state)
    CostProjectionService.cs      → Orchestrates cost projections (profile → Financial Planner API → AI evaluation)
    DrugSelectionExtractService.cs → AI-powered drug formulation selection extraction from chat
    PageContextBuilder.cs         → Static helper — appends route-specific disambiguation to AI system prompts
    PharmacySelectionExtractService.cs → AI-powered pharmacy selection extraction from chat
    PlanSelectionExtractService.cs → AI-powered plan selection extraction from chat
    PrescriptionService.cs        → Prescription save/list logic (MongoDB)
    ProfileExtractService.cs      → AI-powered profile field extraction from natural language
    ProfileService.cs             → Consolidated profile CRUD (Get/Save/Delete)
    RecommendationService.cs      → CRUD for MongoDB RecommendationDocument (GetActive, GetAll, Create, UpdateProfile/Drugs/Pharmacies/Plans/CostSnapshot, Delete)

AI.MedicareAssistant.Infrastructure/
  AI/
    CostEvaluationAiService.cs    → AI cost evaluation (ICostEvaluationAiService)
    DrugAiService.cs              → AI drug analysis integration (IDrugAiService)
    LtcEvaluationAiService.cs     → AI LTC cost evaluation (ILtcEvaluationAiService)
    PlanScoringAiService.cs       → AI plan scoring (IPlanScoringAiService)
    PromptBuilder.cs              → Prompt assembly from files
  Anthropic/
    AnthropicMeaiChatClient.cs    → Anthropic IChatClient (M.E.AI) adapter, registered via "AiProvider" config switch
  Gemini/
    GeminiChatClient.cs           → Google Gemini IChatClient adapter
  RxNorm/                         → (empty)
  Pharmacy/
    FinancialPlannerPharmacyService.cs → Financial Planner getPharmacies API (IPharmacyLookupService)
  Medicare/
    CmsMedicareCostService.cs     → CMS open data API (IMedicareCostService)
    CmsPlanDataService.cs         → CMS SOCRATA plan/formulary data (ICmsPlanDataService)
  Fda/
    FdaNdcService.cs              → openFDA NDC Directory (package-accurate NDC resolution)
  FinancialPlanner/
    IndividualMedicareService.cs  → Financial Planner individualMedicareR5 lifetime cost projections
    PresentValueService.cs        → Financial Planner expensesPresentValue present value calculations (IPresentValueService)
    FinancialPlannerConstantsService.cs → Financial Planner constants API (MAGI tiers, filing statuses)
    FinancialPlannerDrugService.cs → Financial Planner drug search + AI interaction evaluation
    LongTermCareService.cs        → Financial Planner LTC cost projection API (ILongTermCareService)
    MedicareAdvantagePlanService.cs → Financial Planner MA plan recommendations (IMedicareAdvantagePlanService)
    MedigapPlanQuotesService.cs   → Financial Planner Medigap plan quotes (IMedigapPlanQuotesService)
    PartDPlanRecommendationService.cs → Financial Planner Part D plan recommendations (IPartDPlanRecommendationService)
  CountyLookup/
    CountyLookupService.cs        → ZIP-based county code lookup via Financial Planner API (ICountyLookupService, 1-hour cache)
  Email/
    EmailService.cs               → Email delivery service (IEmailService)
    EmailSettings.cs              → Email configuration model
  Data/
    MongoDbContext.cs             → MongoDB typed collection accessor (users, prescriptions, chatSessions, userAnalysisSelections, recommendations, ltcCurrentSelections + indexes)
  Repositories/
    ChatSessionRepository.cs      → MongoDB: chat session CRUD (IChatSessionRepository)
    MongoRepositories.cs          → MongoDB repository implementations (prescriptions, userAnalysisSelections, ltcSelections)
    MongoProfileRepository.cs     → MongoDB: profile CRUD on users collection (IProfileRepository)
    MongoUserRepository.cs        → MongoDB: user CRUD on users collection (IUserRepository)
    RecommendationRepository.cs   → MongoDB: recommendation CRUD (get/create/replace/delete) + unique userId index
```

---

← [Chapter 6 — API Contract](../ch06-api-contract/ch06-api-contract.md) | [Table of Contents](../APPLICATION_BLUEPRINT.md) | [Chapter 8 → Feature Catalog](../ch08-feature-catalog/ch08-feature-catalog.md)
