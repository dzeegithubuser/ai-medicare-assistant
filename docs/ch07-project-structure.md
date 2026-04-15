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
  app.config.ts                   → Angular providers (router, httpClient + authInterceptor)
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
    chat-state.model.ts           → OrchestratorRequest, OrchestratorResponse, ChatMessage, ChatUiState, ChatSession interfaces
    orchestrator.model.ts         → OrchestratorIntentResult, DeltaResult, DisplayData (orchestrator response types)
  services/
    drug.service.ts               → HTTP service for /api/drug/suggest-names, /api/drug/analyze, /api/pharmacy/plan-search, /api/pharmacy/lookup
    drug-state.service.ts         → Signal-based shared state
    auth.service.ts               → Signal-based auth state (JWT token, user, signIn/signUp/signOut)
    profile.service.ts            → Signal-based profile state orchestrator (load + save + updateState)
    county-lookup.service.ts      → ZIP-based county code lookup with caching + Google Places key + MAGI tiers
    reference-data.service.ts     → Signal-based master data service (fetches + caches /api/reference-data)
    prescription.service.ts        → HTTP service for /api/prescription (save + list)
    plan-recommendation.service.ts → HTTP service for /api/plan-recommendation (recommend, checkLis, getGapAdvice, evaluateCosts)
    medicare-advantage-plan.service.ts → HTTP service for /api/MedicareAdvantagePlan/recommend
    medigap-plan.service.ts        → HTTP service for /api/MedigapPlan/quotes
    part-d-plan.service.ts         → HTTP service for /api/PartDPlan/recommend
    recommendation.service.ts      → HTTP service for /api/recommendation (CRUD: create, getActive, getAll, getById, updateProfile/drugs/pharmacy/plans/costSnapshot, delete)
    recommendation-state.service.ts → Signal-based active recommendation state (hydration, patch helpers, selection signals)
    chat-intent.service.ts         → HTTP service for /api/chat/intent (AI intent classification — 17 intents)
    chat-wizard.service.ts         → Reactive wizard state management (mode, step tracking, auto-advance signals)
    chat-plan-selection.service.ts  → HTTP service for /api/chat/extract-plan-selection (AI plan selection extraction)
    chat-orchestrator.service.ts    → HTTP service for /api/chat/orchestrate (AI chatbot orchestrator endpoint)
    chat-orchestrator-flow.service.ts → Client-side orchestrator response routing (maps OrchestratorResponse to UI actions)
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
    chat-router.service.ts          → Routes chat intent responses to correct flow handlers
    chat-router.constants.ts        → Constants for chat intent routing (intent → flow mapping)
    chat-router-summary.service.ts  → Builds context summaries injected into chat prompts (current page, selected drugs/pharmacies/plans)
    chat-session.service.ts         → HTTP client for /api/chat/session (messages + ui-state)
    chat-signal-r.service.ts        → SignalR WebSocket connection (connect, disconnect, syncMessages, session$ ReplaySubject)
    analysis-snapshot.service.ts    → Assembles full analysis snapshot (profile, drugs, pharmacy, plans, cost) and saves via RecommendationService
    plan-card-enrichment.service.ts → Pure computation service — derives display fields (formatted plan IDs, carrier names, surcharges, OOP, pharmacy/drug ratios, Medigap cents→dollars) from raw API responses for Part D, Medigap, and MA cards
    http-loader.service.ts          → Global HTTP loading state (signal-based — true when any HTTP request is in-flight)
  interceptors/
    auth.interceptor.ts           → HttpInterceptorFn — attaches Bearer token to requests
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
    change-password/
      change-password.component.ts/html/scss → Change password form (old + new + confirm, [Authorize])
  dashboard/
    dashboard.component.ts        → Authenticated shell (header + split layout + initial post-login route handling)
    dashboard.component.html      → Child router-outlet left panel + chat right panel
    dashboard.component.scss      → Host styling + slideIn animation
  user-profile/
    user-profile.component.ts     → Consolidated single-form profile (all fields in one form)
    user-profile.component.html   → Profile form template with Google Places Autocomplete
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
  pharmacy-list/
    pharmacy-list.component.ts    → Nearby pharmacies panel (collapsible, sortable, selectable)
    pharmacy-list.component.html  → Responsive card grid (1/2/3 cols) with inline drug price details on selection
    pharmacy-list.component.scss  → Pharmacy list host styling
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
    recommendation-compare.component.ts/html → Side-by-side comparison orchestrator (/saved/compare) — dispatches to CompareMedicare, CompareLtc, or CompareCross
    recommendation-detail.component.ts/html/scss → Full detail view of a single saved analysis (/saved/:id) — hero header, KPI strip, grouped profile/pharmacy/plans/cost tabs
    compare-medicare.component.ts/html → Medicare-vs-Medicare comparison (5-tab: Overview, Profile, Prescriptions, Plans & Pharmacy, Cost Analysis)
    compare-ltc.component.ts/html      → LTC-vs-LTC comparison (4-tab: Overview, Profile, Care Config, Cost Analysis)
    compare-cross.component.ts/html    → Medicare-vs-LTC cross-type comparison (3-tab: Overview, Profile, Cost Summary)
    compare-helpers.ts                 → Shared comparison utilities (deltaIcon, deltaLabel, deltaClass, getTrajectoryIcon/Color, getPriorityColor, starArray, typeBadgeClass, typeLabel, buildProfileRows, ProfileRow interface)
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
  Program.cs                      → App builder, DI, CORS, JWT, EF Core, Serilog, middleware pipeline
  Middleware/
    GlobalExceptionMiddleware.cs   → Global exception handler (AppException → HTTP status + JSON)
  Controllers/
    DrugController.cs             → Drug name suggestion + analysis REST endpoints
    PharmacyController.cs         → Nearby pharmacies with pricing + plan-aware search
    PlanRecommendationController.cs → Medicare plan recommendations + LIS check
    PrescriptionController.cs     → [Authorize] Save + list prescriptions (MongoDB)
    MigrationController.cs        → [AllowAnonymous] EF Core migration management
    AuthController.cs             → Sign up, sign in, forgot/reset password
    ReferenceDataController.cs    → Public master data endpoint
    ProfileController.cs         → [Authorize] consolidated profile GET/POST
    CountyLookupController.cs     → ZIP-based county code lookup + MAGI tiers endpoint
    FinancialPlannerDrugController.cs → [Authorize] Financial Planner drug search/detail/bulk-search with AI interactions
    ChatIntentController.cs       → [Authorize] AI-powered chat intent classification (POST api/chat/intent + 4 extract endpoints)
    ChatOrchestratorController.cs → [Authorize] Main chatbot FSM endpoint (POST api/chat/orchestrate)
    RecommendationController.cs   → [Authorize] Recommendation CRUD (GET/POST/PUT profile|drugs|pharmacy|plans|cost-snapshot/DELETE api/recommendation)
    LongTermCareController.cs     → [Authorize] LTC cost projection (POST api/long-term-care)
    LtcSelectionsController.cs    → [Authorize] LTC care-type selections persistence (PUT/GET api/ltc/current)
    MedicareAdvantagePlanController.cs → [Authorize] MA plan recommendations (POST api/MedicareAdvantagePlan/recommend)
    MedigapPlanController.cs      → [Authorize] Medigap plan quotes (POST api/MedigapPlan/quotes)
    PartDPlanController.cs        → [Authorize] Part D plan recommendations (POST api/PartDPlan/recommend)
  Prompts/                        → File-based prompt system (4 folders: system, tasks, schemas, templates)
    system/                       → System role prompts (pharma-system, drug-name-suggestion-system, pharmacy-pricing-system, plan-scoring-system, cost-evaluation-system, chat-intent-system)
    tasks/                        → Task instructions (drug-normalization, drug-name-suggestion, pharmacy-pricing, plan-scoring, cost-evaluation)
    schemas/                      → JSON output schemas (drug-json-schema, drug-name-suggestion-schema, pharmacy-pricing-schema, plan-scoring-schema, cost-evaluation-schema)
    templates/                    → User prompt templates with {{placeholders}} (prescription-analysis, drug-name-suggestion, pharmacy-pricing, plan-scoring, cost-evaluation)
  Logs/                           → Daily rolling log files (30 day retention)
  appsettings.json                → OpenAI + CMS + JWT + MySQL configuration

AI.MedicareAssistant.Domain/
  Entities/
    BaseEntity.cs                 → Abstract base (Id, CreatedDate, ModifiedDate, CreatedBy, ModifiedBy)
    User.cs                       → Email, Phone, PasswordHash + Profile navigation prop
    Profile.cs                    → CoverageYear, HealthCondition, TaxFilingStatus, MagiTier, Gender, TobaccoStatus, DateOfBirth, Concierge, ConciergeAmount, AlternateEmail, AlternateMobile, LifeExpectancy + Address fields
    Prescription.cs               → (DELETED — migrated to MongoDB PrescriptionDocument)
  Documents/
    PrescriptionDocument.cs       → MongoDB: named prescription with embedded drug list
    ChatSessionDocument.cs        → MongoDB: chat session messages + UI state
    ConvStateDocument.cs          → MongoDB: FSM conversation state (ConversationState enum, pendingChanges, collectedFields, TTL)
    RecommendationDocument.cs     → MongoDB: full recommendation (ProfileSnapshot, SelectedDrugDoc, SelectedPlanDoc, SelectedPharmacyDoc, MailOrderPharmacyDoc, CostSnapshotDoc and nested types)
    LtcCurrentSelectionsDocument.cs → MongoDB: per-user LTC care-type inputs + last projection result (collection ltcCurrentSelections)
    UserAnalysisSelectionsDocument.cs → MongoDB: per-user current analysis selections — drugs, pharmacies, plans, activeSection (collection userAnalysisSelections)
  Models/
    DrugAnalysisResult.cs         → DrugResult, DrugFormulation, MedicareCostEstimate models
    DrugNameSuggestion.cs         → DrugNameSuggestionResult, DrugNameSuggestion, DrugCandidate models
    Pharmacy/PharmacyModels.cs    → PharmacyResult, DrugPrice, PharmacyWithPricing, DrugPricingInput, PlanPharmacySearchRequest, PlanCoverageInput
    PlanRecommendation.cs         → LisTier, PlanType, PlanRecommendationRequest/Result, RankedPlan
    IndividualMedicare.cs         → IndividualMedicareRequest, IndividualMedicareResponse, IndividualMedicareDetail (Financial Planner API models)
    CostProjection.cs             → CostProjectionResult, LifetimeTotals, CostEvaluation, LifetimeSummary, YearlyHighlight, CostCategory, SavingsTip (combined Financial Planner + AI evaluation models)
    FinancialPlannerDrug.cs       → DrugSearchRequest/Response, DrugListItem, DrugDetailRequest/Response, DrugDetailAdvanceItem, DrugSearchResult, BulkDrugSearchResponse, DrugInteractionAnalysis (Financial Planner drug search + AI interaction models)
    LongTermCare.cs               → LongTermCareRequest, LongTermCareResponse, LtcExpenseEntry (Financial Planner LTC API models)
    PresentValue.cs               → PresentValueRequest, PresentValueResponse, YearExpense, PvEntry, PresentValueYears, RateOfReturns (Financial Planner expensesPresentValue API models)
    MedicareAdvantagePlan.cs      → MedicareAdvantagePlanRequest (MA plan recommendation request with MedicareAdvantage=true)
    MedigapPlanQuotes.cs          → MedigapPlanQuotesRequest, MedigapPlanQuotesResponse, MedigapPlanQuote + nested carrier/rate/discount types
    PartDPlanRecommendation.cs    → PartDPlanRecommendationRequest, CountyCodeModel, PrescriptionInput, PharmacyInput (Part D plan recommendation models)
  Exceptions/
    AppExceptions.cs              → AppException, NotFoundException, ValidationException, etc.
  Interfaces/
    ICmsPlanDataService.cs        → CMS SOCRATA plan/formulary data contract
    IDrugAiService.cs             → AI service contract (AnalyzePrescription, SuggestDrugNames)
    IFdaNdcService.cs             → FDA NDC Directory package info contract
    IFipsLookupService.cs         → ZIP-to-FIPS county code lookup contract (legacy)
    ICountyLookupService.cs       → ZIP-based county code lookup contract
    IConstantsService.cs          → Financial Planner constants API contract
    IMedicareCostService.cs       → Medicare cost service contract
    IMedicarePlanService.cs       → Medicare plan recommendation orchestrator contract
    IPharmacyPricingService.cs    → Pharmacy search + pricing contract
    IPharmacyLookupService.cs     → Financial Planner pharmacy lookup contract + request/response models
    IPlanPharmacyService.cs       → Plan-aware pharmacy search contract
    IPlanScoringAiService.cs      → AI plan scoring + explanation contract
    IIndividualMedicareService.cs  → Financial Planner individualMedicareR5 API contract
    IPresentValueService.cs       → Financial Planner expensesPresentValue API contract (CalculateAsync)
    IFinancialPlannerDrugService.cs → Financial Planner drug bulk-search contract (SearchBulkAsync)
    ICostEvaluationAiService.cs   → AI cost evaluation contract (EvaluateAsync → CostEvaluation)
    ILongTermCareService.cs       → LTC cost projection contract (GetProjectionAsync)
    IMedicareAdvantagePlanService.cs → MA plan recommendation contract (RecommendAsync)
    IMedigapPlanQuotesService.cs  → Medigap plan quotes contract (GetQuotesAsync)
    IPartDPlanRecommendationService.cs → Part D plan recommendation contract (RecommendAsync)
    IRepository.cs                → Generic repository contract
    IProfileRepositories.cs       → IProfileRepository marker interface
    IRxNormService.cs             → RxNorm normalization + interaction + NDC lookup contract
    IUserRepository.cs            → User data access contract
    IMongoRepositories.cs         → MongoDB repository interfaces: IPrescriptionDocRepository, IUserAnalysisSelectionsRepository (partial drug/pharmacy/plan updates), IRecommendationRepository (CRUD + GetAllByUserIdAsync), IConvStateRepository (upsert + TTL), ILtcSelectionsRepository

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
    DrugAnalysisService.cs        → Pipeline orchestrator (iterates IDrugAnalysisStep by Order)
    Pipeline/
      IDrugAnalysisStep.cs        → Interface + AnalysisContext record
      AiAnalysisStep.cs           → Step 1: AI drug analysis
      DrugValidationStep.cs       → Step 2: Filter invalid drugs
      CmsRxNormEnrichmentStep.cs  → Step 3: CMS + RxNorm enrichment
      InteractionMergingStep.cs   → Step 4: Merge RxNorm + AI interactions
      PharmacyPricingStep.cs      → Pharmacy pricing lookup (not registered in DI pipeline; pharmacy fetched on-demand via /api/pharmacy/search)
    AuthService.cs                → JWT auth logic
    ProfileService.cs             → Consolidated profile CRUD (Get/Save/Delete)
    MedicarePlanService.cs        → Plan recommendation orchestrator
    PlanPharmacyService.cs        → Plan-aware pharmacy search (overlays copay data)
    PrescriptionService.cs        → Prescription save/list logic (MongoDB)
    CostProjectionService.cs      → Orchestrates cost projections (profile → Financial Planner API → AI evaluation)
    ChatIntentService.cs          → AI-powered chat intent classification (17 intents, file-based prompt)
    ProfileExtractService.cs      → AI-powered profile field extraction from natural language
    DrugSelectionExtractService.cs → AI-powered drug formulation selection extraction from chat
    PharmacySelectionExtractService.cs → AI-powered pharmacy selection extraction from chat
    PlanSelectionExtractService.cs → AI-powered plan selection extraction from chat
    ChatSessionService.cs         → Chat session persistence (get/update messages + ui-state)
    RecommendationService.cs      → CRUD for MongoDB RecommendationDocument (GetActive, GetAll, Create, UpdateProfile/Drugs/Pharmacy/Plans/CostSnapshot, Delete)
    ConvStateService.cs           → FSM conversation state persistence (GetOrCreate, UpdateState, SetPendingChange, SetCollectedField, ClearPending, Reset)
    OrchestratorIntentService.cs  → 19-intent classifier via IChatClient + orchestrator-intent-system.txt + page-context injection
    ChatOrchestratorService.cs    → Core FSM router (ProcessMessageAsync, 19 handler methods, multi-turn collection wizards)
    DeltaCalculationService.cs    → Before/after cost comparisons (ComputeAsync → Financial Planner API diff + AI narrative)
    PageContextBuilder.cs         → Static helper — appends route-specific disambiguation to AI system prompts
    PlanCardEnrichmentService.cs  → Static utility — pure computation (no DI/IO). EnrichPartD, EnrichMedigap, EnrichMA methods compute derived display fields from raw API responses

AI.MedicareAssistant.Infrastructure/
  AI/
    DrugAiService.cs              → AI drug analysis integration (IDrugAiService)
    PromptBuilder.cs              → Prompt assembly from files
    PlanScoringAiService.cs       → AI plan scoring (IPlanScoringAiService)
    CostEvaluationAiService.cs    → AI cost evaluation (ICostEvaluationAiService)
  Anthropic/
    AnthropicMeaiChatClient.cs    → Anthropic IChatClient (M.E.AI) adapter, registered via "AiProvider" config switch
  RxNorm/
    RxNormService.cs              → NIH RxNorm API integration (IRxNormService)
  Pharmacy/
    CmsPharmacyPricingService.cs  → NPI Registry + AI pricing (IPharmacyPricingService)
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
  Data/
    AppDbContext.cs               → EF Core DbContext (MySQL)
    AppDbContextFactory.cs        → Design-time factory for migrations
    MongoDbContext.cs             → MongoDB typed collection accessor (prescriptions, chatSessions, userAnalysisSelections, recommendations, convStates, ltcCurrentSelections + indexes)
    Migrations/                   → EF Core migration files
  Repositories/
    Repository.cs                 → Generic EF Core repository base
    ProfileRepositories.cs        → ProfileRepository
    UserRepository.cs             → User repository (no eager loading)
    MongoRepositories.cs          → MongoDB repository implementations (prescriptions, chat sessions, userAnalysisSelections, ltcSelections)
    RecommendationRepository.cs   → MongoDB: recommendation CRUD (get/create/replace/delete) + unique userId index
    ConvStateRepository.cs        → MongoDB: conversation state (get/upsert/delete) + userId unique index + TTL index
```

---

## New Files — Chatbot Orchestrator

Files added across Phases 1–7 of the orchestrator implementation:

### Backend

```
AI.MedicareAssistant.Domain/
  Documents/
    RecommendationDocument.cs     → ProfileSnapshot, SelectedDrugDoc, SelectedPlanDoc, SelectedPharmacyDoc, MailOrderPharmacyDoc, CostSnapshotDoc
    ConvStateDocument.cs          → ConversationState enum (10 states), BsonDocument for pendingChanges/collectedFields, TTL
  Interfaces/
    IMongoRepositories.cs         → (extended) IRecommendationRepository, IConvStateRepository

AI.MedicareAssistant.Application/
  DTOs/
    OrchestratorDtos.cs           → OrchestratorRequest, OrchestratorResponse, DeltaResult, DisplayData, OrchestratorIntentResult
    RecommendationDtos.cs         → CreateRecommendationRequest (with expanded CostSnapshotDto + SelectedPlanDto), UpdateProfileRequest, UpdateDrugsRequest, UpdatePharmacyRequest, UpdatePlansRequest, RecommendationSummaryResponse, YearlyDetailDto, CostEvaluationDto, LifetimeSummaryDto, YearlyHighlightDto, CostCategoryDto, SavingsTipDto, PlanExpenseDto
  Services/
    RecommendationService.cs      → CRUD for recommendation documents (GetActive, GetAllAsync, Exists, Create, UpdateProfile/Drugs/Pharmacy/Plans/CostSnapshot, Delete)
    ConvStateService.cs           → FSM state persistence (GetOrCreate, UpdateState, SetPendingChange, SetCollectedField, ClearPending, Reset)
    OrchestratorIntentService.cs  → 19-intent classifier via IChatClient + orchestrator-intent-system.txt
    ChatOrchestratorService.cs    → Core FSM router (~1,300 lines) — ProcessMessageAsync, 19 handler methods, multi-turn collection wizards
    DeltaCalculationService.cs    → Before/after cost comparisons — ComputeAsync, BuildPreviewDelta, AI narrative
    PlanCardEnrichmentService.cs  → Static utility — pure computation (no DI/IO). EnrichPartD, EnrichMedigap, EnrichMA

AI.MedicareAssistant.Api/
  Controllers/
    RecommendationController.cs   → GET, POST, PUT profile/drugs/pharmacy/plans, DELETE /api/recommendation
    ChatOrchestratorController.cs → POST /api/chat/orchestrate
  Prompts/system/
    orchestrator-intent-system.txt → 19 domain intents with parameter extraction + 55 few-shot examples
    delta-narrative-system.txt     → 2-4 sentence cost impact narrative prompt

AI.MedicareAssistant.Tests/
    ChatOrchestratorServiceTests.cs → 18 tests covering FSM routing, confirmation/delete flows, profile validation, error handling
```

### Frontend

```
ui-ai-medicare-assistant/src/app/
  models/
    recommendation.model.ts       → RecommendationResponse, RecommendationSummaryResponse, ProfileSnapshotDto, SelectedDrugDto, SelectedPlanDto (with deductible, starRating, totalPrescriptionCost, planExpenses, unavailableDrugs), CostSnapshotDto (with yearlyDetails, evaluation), YearlyDetailDto, CostEvaluationDto, LifetimeSummarySnapDto, YearlyHighlightDto, CostCategorySnapDto, SavingsTipSnapDto, PlanExpenseDto, CreateRecommendationRequest
    orchestrator.model.ts         → OrchestratorRequest, OrchestratorResponse, DeltaResult, DisplayData
  services/
    recommendation.service.ts     → HTTP CRUD for /api/recommendation
    recommendation-state.service.ts → Signal-based state (activeRecommendation, hasRecommendation, refreshAfterUpdate, clear)
    chat-orchestrator.service.ts  → sendMessage() → POST /api/chat/orchestrate
  pipes/
    markdown.pipe.ts              → marked + DomSanitizer for rendering markdown in chat bubbles
  chat/
    delta-display/
      delta-display.component.ts  → 3-column cost grid (lifetime/year/PV), color-coded ↑↓
    help-menu/
      help-menu.component.ts      → 5-category help with clickable action chips → orchestrator
```

### Modified Files

| File | Changes |
|------|---------|
| `MongoDbContext.cs` | Added `Recommendations` and `ConvStates` collections with unique + TTL indexes |
| `Program.cs` | Registered 5 new scoped services + 2 repository interfaces |
| `chat.component.ts` | Orchestrator mode routing, pendingDelta/awaitingConfirmation/activeDisplayData/deleteConfirmMode signals, handleOrchestratorResponse(), confirmOrCancel(), onHelpAction() |
| `chat.component.html` | Orchestrator mode pill, markdown rendering, help menu routing, delete banner, delta display, confirmation buttons |
| `chat.component.scss` | `.markdown-body` styles for tables, headings, lists, code |
| `dashboard.component.ts` | Loads recommendation on init via `recommendationState.loadActiveRecommendation()` + `openRecommendations()` method + folder_open header button + Recommendations menu item |
| `RecommendationDocument.cs` | Expanded `SelectedPlanDoc` (+7 fields), `CostSnapshotDoc` (+4 fields + nested collections). Added 8 new embedded document classes: `YearlyDetailDoc`, `CostEvaluationDoc`, `LifetimeSummaryDoc`, `YearlyHighlightDoc`, `CostCategoryDoc`, `SavingsTipDoc`, `PlanExpenseDoc` |
| `RecommendationDtos.cs` | Added `RecommendationSummaryResponse` + 8 new DTO classes. Expanded `SelectedPlanDto`, `CostSnapshotDto` |
| `RecommendationController.cs` | Added `GET /api/recommendation/all` endpoint. 8+ new mapping helpers for expanded plan/cost snapshot fields |
| `IMongoRepositories.cs` | Added `GetAllByUserIdAsync` to `IRecommendationRepository` |
| `RecommendationRepository.cs` | Implemented `GetAllByUserIdAsync` (sorted by CreatedAt desc) |
| `RecommendationService.cs` | Added `GetAllAsync(Guid userId)` |
| `chat-intent-system.txt` | Added `ACTION_SAVE_ANALYSIS` (+analysisName param), `ACTION_RUN_ANALYSIS`, `NAVIGATE_SAVED_ANALYSES` intents. Updated to 17 intents |
| `cost-projections.component.ts` | Added Save Analysis button + `saveAnalysis()` method with dialog + resetAll after save |
| `chat-router.service.ts` | Added `ACTION_SAVE_ANALYSIS`, `NAVIGATE_SAVED_ANALYSES` handlers, `pendingSaveAnalysisOverwrite` signal, overwrite confirmation flow, reset after save |

---

← [Chapter 6 — API Contract](ch06-api-contract.md) | [Table of Contents](APPLICATION_BLUEPRINT.md) | [Chapter 8 → Feature Catalog](ch08-feature-catalog.md)
