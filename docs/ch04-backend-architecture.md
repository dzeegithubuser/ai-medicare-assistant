# Chapter 4 — Backend Architecture

> API, Domain, Application, and Infrastructure layers — the Clean Architecture stack.

---

## Dependency Graph

```
Api → Application → Domain
Api → Infrastructure → Domain
```

Application and Infrastructure both depend on Domain for shared models and interfaces, but never reference each other — eliminating cyclic dependencies.

---

## API Layer (`AI.MedicareAssistant.Api`)

### Middleware
- **GlobalExceptionMiddleware:** Catches all exceptions. Maps custom `AppException` subtypes to HTTP status codes (400, 401, 404, 409). Unhandled → 500. Logs 5xx as `Error` with stack trace; 4xx as `Warning`. Returns JSON `{ status, message, traceId, errors? }`.

### Serilog Integration
- **3-tier sink hierarchy:**
  1. **MongoDB (primary)** — `Serilog.Sinks.MongoDB` v6, writes structured BSON logs to `logs` collection in the `ai_medicare_assistant` database, 5-second batch period.
  2. **Console** — development convenience, same output template.
  3. **File (fallback)** — daily rolling file (`Logs/log-.txt`), 30-day retention.
- **Bootstrap logger** — file + console only (MongoDB not available before DI host builds). `try/catch/finally` around `app.Run()` with `Log.Fatal` + `Log.CloseAndFlush()`.
- **Configuration** — `appsettings.json` → `Serilog:MinimumLevel` section: `Default: Information`, overrides for `Microsoft.AspNetCore: Warning`, `Microsoft.EntityFrameworkCore: Warning`, `System.Net.Http.HttpClient: Warning`. `ReadFrom.Configuration()` picks up log level config at runtime.
- **Request logging** — `UseSerilogRequestLogging()` with `RequestHost` + `UserAgent` enrichment.
- **Enrichment** — `FromLogContext()` + static `Application: AI.MedicareAssistant` property on all log entries.

### SignalR Hub

`ChatHub` (`Hubs/ChatHub.cs`) — `[Authorize]` — persistent WebSocket endpoint at `/hubs/chat`.

- **`SyncMessages(List<ChatSessionMessageDto> messages)`** — invoked by the Angular client on every message-list change instead of `PATCH /api/chat/session/messages`. Delegates to `ChatSessionService.UpdateMessagesAsync` (preserves the 200-message rolling window). Acks the caller with `MessagesSynced`.
- **`OnConnectedAsync`** — pushes the stored session to the caller via `ReceiveSession(messages, uiState)` immediately on connect, replacing the `GET /api/chat/session` HTTP call. Always fires even when no session exists (sends empty arrays) so the Angular bootstrap `forkJoin` never hangs on first login.
- **JWT on WebSocket** — browsers cannot set `Authorization` headers on WebSocket upgrade requests. The Angular client passes the token as `?access_token=<jwt>` in the hub URL. `JwtBearerEvents.OnMessageReceived` reads this query param and sets `context.Token` when the path starts with `/hubs/chat`.

### Controllers

| Controller | Auth | Purpose |
|------------|------|---------|
| `DrugController` | `[Authorize]` | `POST api/drug/suggest-names` — accepts `DrugNameRequest`, calls `IDrugAiService.SuggestDrugNames()`, returns `DrugNameSuggestionResult` with candidate names. `POST api/drug/analyze` — accepts `PrescriptionRequest`, extracts UserId from JWT, fetches zipcode from `ProfileService`, passes to `DrugAnalysisService`. Returns `DrugAnalysisResult`. |
| `PharmacyController` | `[Authorize]` | `GET api/pharmacy/search?zip=&drugs=` — pharmacy search with AI pricing. `GET api/pharmacy/nearby?zip=` — lightweight NPI-only nearby pharmacy lookup (no pricing, returns `PharmacyResult[]`; supports multi-select up to 5). `GET api/pharmacy/lookup?page=&size=&radius=&name=` — Financial Planner pharmacy lookup using user profile lat/lng, returns paginated `PharmacyLookupResponse` with distance, address, zipcode. `POST api/pharmacy/plan-search` — plan-aware search (Phase 3). |
| `PlanRecommendationController` | `[Authorize]` | `POST api/plan-recommendation` — ranked plan recommendations. `GET api/plan-recommendation/lis-check` — quick LIS eligibility check. `POST api/plan-recommendation/gap-advice` — AI gap coverage plans. `POST api/plan-recommendation/evaluate-costs` — calculate costs + AI evaluation returning chart-ready `CostProjectionResult` (yearly details, lifetime totals, AI evaluation with categories, savings tips, trajectory). Delegates to `CostProjectionService`. |
| `AuthController` | Public | signup, signin, forgot-password, reset-password |
| `ReferenceDataController` | Public | `GET api/reference-data` — all master data for profile form dropdowns (static/in-memory). |
| `ProfileController` | `[Authorize]` | Consolidated profile CRUD. `GET api/profile` returns `UserProfileResponse`. `POST api/profile` saves/updates `ProfileDto`. |
| `PrescriptionController` | `[Authorize]` | `POST api/prescription` — saves a named prescription with all confirmed drugs to MongoDB. `GET api/prescription` — returns all prescriptions for the authenticated user. `GET api/prescription/{id}` — returns a single prescription by ID (for loading saved prescriptions into the wizard). |
| `MigrationController` | `[AllowAnonymous]` | Database migration management (no auth required). `GET api/migration/applied` — lists all applied migrations as JSON. `GET api/migration/pending` — lists pending migrations. `POST api/migration/apply` — applies all pending migrations to the database. |
| `CountyLookupController` | Public/Mixed | ZIP-based county code lookup and configuration. `POST api/county-lookup/getCountycodeList` — accepts `ZipCodeRequest`, returns `CountyCodeEntry[]` (county codes, names, state codes for a ZIP). `GET api/county-lookup/config/google-places-key` — returns the Google Places API key. `GET api/county-lookup/constants/magi-tiers` — accepts `filingStatus` and `coverageYear` query params, returns MAGI tier options from the Financial Planner constants API. |
| `FinancialPlannerDrugController` | `[Authorize]` | `POST api/FinancialPlannerDrug/search` — searches a single drug via Financial Planner `drugSearch` API, matches by `displayName`, fetches detail via `getDrugDetailAdvance`. `POST api/FinancialPlannerDrug/search-bulk` — accepts `BulkDrugSearchInput` with `List<string> DrugNames`, searches each drug, matches, fetches details, and if >1 drug calls AI to evaluate pairwise interactions and duplicate therapies. Returns `BulkDrugSearchResponse`. `POST api/FinancialPlannerDrug/detail` — fetches drug detail by rxcui. |
| `ChatIntentController` | `[Authorize]` | Routes at `api/chat`. 5 endpoints: `POST intent` — accepts `ChatIntentRequest` (message + isProfileComplete + **currentPage?**), delegates to `ChatIntentService`, returns `ChatIntentResponse` (intent, params, confirmationMessage). Supports 20 intents including 3 LTC intents (`NAVIGATE_LTC_CARE_TYPE`, `LTC_CARE_INPUT`, `ACTION_RUN_LTC_PROJECTION`) with 4 LTC params (`LtcHealthProfile`, `LtcAdultDayYears`, `LtcHomeCareYears`, `LtcNursingCareYears`). `POST extract-profile` — accepts `ProfileExtractRequest` (message + missingFields), delegates to `ProfileExtractService`, returns extracted profile fields + reply. `POST extract-drug-selection` — accepts `DrugSelectionExtractRequest` (message + availableDrugs), delegates to `DrugSelectionExtractService`, returns drug formulation extraction (drugName, type, dosageForm, strength, quantity, action, reply). `POST extract-pharmacy-selection` — accepts `PharmacySelectionExtractRequest` (message + availablePharmacies + selectedPharmacies), delegates to `PharmacySelectionExtractService`, returns pharmacy selection extraction (pharmacyName, action, searchTerm, reply). `POST extract-plan-selection` — accepts `PlanSelectionExtractRequest` (message + availablePlans + selectedPlans), delegates to `PlanSelectionExtractService`, returns plan selection extraction (planName, planType, action, section, reply). |
| `ChatSessionController` | `[Authorize]` | Routes at `api/chat/session` for phase-1 Mongo chat persistence. `GET` returns session (`messages`, `uiState.editMode`), `PATCH messages` updates transcript, `PATCH ui-state` updates persisted UI flags. |
| `ChatOrchestratorController` | `[Authorize]` | `POST api/chat/orchestrate` — accepts `OrchestratorRequest` (message, currentPage?), delegates to `ChatOrchestratorService.ProcessMessageAsync()`, returns `OrchestratorResponse`. Core AI chatbot endpoint — routes through FSM multi-turn states and 19-intent classification. |
| `RecommendationController` | `[Authorize]` | Routes at `api/recommendation`. `GET` — returns active recommendation for user. `GET {id}` — returns recommendation by ID. `GET all` — list all recommendations (summary response with drug/plan counts, cost totals). `POST` — creates new recommendation (`CreateRecommendationRequest` with profile, drugs, pharmacy, plans, costSnapshot; `?force=true` replaces existing). `PUT profile` — updates profile snapshot. `PUT drugs` — replaces drug list. `PUT pharmacy` — updates pharmacy + mail order. `PUT plans` — replaces plan selections. `PUT cost-snapshot` — saves cost projection snapshot. `DELETE` — removes active recommendation. |
| `LongTermCareController` | `[Authorize]` | Routes at `api/long-term-care`. `POST` — accepts `LongTermCareRequest` (age, pvAsOfYear, lifeExpectancy, healthProfile, location, zipcode, tobacco, care years, gender, alzheimers/heartStroke flags), delegates to `ILongTermCareService.GetProjectionAsync()`, returns `LongTermCareResponse` (year-by-year LTC expense breakdowns and present-value totals). |
| `LtcSelectionsController` | `[Authorize]` | Routes at `api/ltc`. `PUT current` — accepts `SaveLtcCurrentRequest` (healthProfile, care-type year counts, ltcResultJson), upserts `LtcCurrentSelectionsDocument` in MongoDB. `GET current` — returns `LtcCurrentResponse` (care-type selections + last projection result). |
| `MedicareAdvantagePlanController` | `[Authorize]` | Routes at `api/MedicareAdvantagePlan`. `POST recommend` — accepts `MedicareAdvantagePlanRequest` (county code model, prescriptions, pharmacies, sort/filter params, `MedicareAdvantage: true`), delegates to `IMedicareAdvantagePlanService.RecommendAsync()`. Returns ranked MA plan list from Financial Planner API. |
| `MedigapPlanController` | `[Authorize]` | Routes at `api/MedigapPlan`. `POST quotes` — accepts `MedigapPlanQuotesRequest` (zip5, gender, tobacco, birthDate, plan type, county, taxFilingStatus, magiTier, healthProfile, coverageYear, versionId?), delegates to `IMedigapPlanQuotesService.GetQuotesAsync()`. Returns `MedigapPlanQuotesResponse` with plan list, deductible, carrier map. |
| `PartDPlanController` | `[Authorize]` | Routes at `api/PartDPlan`. `POST recommend` — accepts `PartDPlanRecommendationRequest` (same shape as `MedicareAdvantagePlanRequest` minus `MedicareAdvantage` flag), delegates to `IPartDPlanRecommendationService.RecommendAsync()`. Returns ranked Part D plan list from Financial Planner API. |

### CORS
Configured for `localhost:4200` (dev) and `169.61.105.110:9500` (production). `AllowCredentials()` is required for the SignalR WebSocket handshake and must be combined with explicit `WithOrigins(...)` (not `AllowAnyOrigin`).

### DI Registration
`builder.Services.AddSignalR()` registers the SignalR runtime (no extra NuGet package — included in `Microsoft.AspNetCore`). `ChatHub` is mapped via `app.MapHub<ChatHub>("/hubs/chat")` after `app.MapControllers()`. `PromptBuilder` (singleton), `IDrugAiService`/`DrugAiService` (scoped), `DrugAnalysisService` (scoped), **`IDrugAnalysisStep` pipeline steps** (4x scoped: `AiAnalysisStep`, `DrugValidationStep`, `CmsRxNormEnrichmentStep`, `InteractionMergingStep`), `IChatClient` (via config-driven `"AiProvider"` switch: `"Anthropic"` → `AnthropicMeaiChatClient` via `AddHttpClient`, `"OpenAI"` → OpenAI SDK via `AddChatClient`), `IMedicareCostService`/`CmsMedicareCostService` (via `AddHttpClient`), `IRxNormService`/`RxNormService` (via `AddHttpClient`), `IPharmacyPricingService`/`CmsPharmacyPricingService` (via `AddHttpClient`, 15s timeout), `IMemoryCache` (via `AddMemoryCache`), `IProfileRepository`/`ProfileRepository` (scoped), `ProfileService` (scoped), `PrescriptionService` (scoped), `ICountyLookupService`/`CountyLookupService` (via `AddHttpClient`), `IConstantsService`/`FinancialPlannerConstantsService` (via `AddHttpClient`), `IPlanScoringAiService`/`PlanScoringAiService` (scoped), `ICmsPlanDataService`/`CmsPlanDataService` (via `AddHttpClient`, 10s timeout), `MedicarePlanService` (scoped), `IPlanPharmacyService`/`PlanPharmacyService` (scoped), `IIndividualMedicareService`/`IndividualMedicareService` (via `AddHttpClient`, 30s timeout), `ICostEvaluationAiService`/`CostEvaluationAiService` (scoped), `CostProjectionService` (scoped), `IPharmacyLookupService`/`FinancialPlannerPharmacyService` (via `AddHttpClient`, 15s timeout), `IFinancialPlannerDrugService`/`FinancialPlannerDrugService` (via `AddHttpClient`, 15s timeout), `ChatIntentService` (scoped — AI-powered chat intent classification).

### DI Registration (continued — new services)
`ILongTermCareService`/`LongTermCareService` (via `AddHttpClient`, 30s timeout), `IPresentValueService`/`PresentValueService` (via `AddHttpClient`, 30s timeout), `IMedicareAdvantagePlanService`/`MedicareAdvantagePlanService` (via `AddHttpClient`), `IMedigapPlanQuotesService`/`MedigapPlanQuotesService` (via `AddHttpClient`), `IPartDPlanRecommendationService`/`PartDPlanRecommendationService` (via `AddHttpClient`), `RecommendationService` (scoped), `ConvStateService` (scoped), `OrchestratorIntentService` (scoped), `ChatOrchestratorService` (scoped), `DeltaCalculationService` (scoped).

### MongoDB DI Registration
`IMongoClient` (singleton via `MongoClient`), `IMongoDatabase` (singleton), `MongoDbContext` (singleton — typed collection accessor with index creation on startup), `IPrescriptionDocRepository`/`PrescriptionDocRepository` (scoped), `IChatSessionRepository`/`ChatSessionRepository` (scoped), `IUserAnalysisSelectionsRepository`/`UserAnalysisSelectionsRepository` (scoped), `IRecommendationRepository`/`RecommendationRepository` (scoped), `IConvStateRepository`/`ConvStateRepository` (scoped), `ILtcSelectionsRepository`/`LtcSelectionsRepository` (scoped).

---

## Domain Layer (`AI.MedicareAssistant.Domain`)

**Purpose:** Shared models, DTOs, service interfaces, and MongoDB document models. Depends on `MongoDB.Bson` for BSON attributes.

### Models

| File | Key Types |
|------|-----------|
| `Models/DrugAnalysisResult.cs` | `DrugAnalysisResult` (root response with drugs, interactions, dosageAlerts, duplicateTherapies, message; nearbyPharmacies populated separately on-demand), `DrugResult` (includes `Formulations` list of `DrugFormulation`), `DrugFormulation` (validated `dosageForm` + `strength` + `packaging` + `ndcCode` tuple), `DrugInteraction`, `DosageAlert`, `DuplicateTherapy`, `DrugAlternative`, `GenericSwitchSuggestion`, `MedicareCostEstimate` |
| `Models/DrugNameSuggestion.cs` | `DrugNameSuggestionResult`, `DrugNameSuggestion`, `DrugCandidate` — models for the drug name verification step |
| `Models/Pharmacy/PharmacyModels.cs` | `PharmacyResult`, `DrugPrice`, `PharmacyWithPricing`, `DrugPricingInput`, `PlanDrugCoverageInput` (Phase 3) |
| `Interfaces/IPharmacyLookupService.cs` | `IPharmacyLookupService` (interface), `PharmacyLookupRequest` (Lat, Lng, Radius, Name, Page, Size), `PharmacyLookupResponse` (paginated), `PharmacyLookupEntry` (pharmacyNumber, pharmacyName, lat/lng, address, distance, zipcode) |
| `Models/PlanRecommendation.cs` | `LisTier` enum, `PlanType` enum, `PlanRecommendationRequest` (includes `List<SelectedPharmacy>? SelectedPharmacies`), `SelectedPharmacy` record (Npi, Name, PharmacyType), `DrugSummary`, `PlanRecommendationResult`, `RankedPlan` (includes `PlanCategory` field — `MA_ONLY`/`PDP_ONLY`/`PDP_MEDIGAP`/`MA_PDP`, `List<PlanCostBreakdown>? CostBreakdowns` + 12 extended benefit fields: `NetworkType`, `IncludesDental/Vision/Hearing/Fitness/Otc`, `OtcAllowancePerQuarter`, `GapCoverage`, `MailOrderSavings`, `ProviderNetworkSize`, `EmergencyCoverage`, `Pros`, `Cons`), `PlanCostBreakdown` (per-pharmacy cost totals + `List<DrugCopayDetail>`), `DrugCopayDetail` (per-drug copay with preferred discount flag), `PlanDrugCoverage`, `CmsPlanInfo` (Phase 2), `CmsFormularyEntry` (Phase 2) |
| `Models/IndividualMedicare.cs` | `IndividualMedicareRequest` (Financial Planner API payload — userEmail, versionId, birthDate, retirementYear, lifeExpectancy, healthGrade, stateName, zipcode, taxFilingStatus, tobacco, magiTier, coverageYear, concierge, planBundleCode, medicareAdvantagePremium, partDOOP, partABenefitServiceCost, partBBenefitServiceCost, calculateForAdjustedMonth, supplementPlanType, etc.), `IndividualMedicareResponse` (lifetime costs — lifeTimeABMedicareAdvantageExpenses/Premium/Oop, lifeTimeDSurcharge, lifeTimeBSurcharge, lifeTimeConciergePremium, conciergeIncluded, plan-specific lifetime fields: lifeTimeABGD/ABFD/ABND/ABCDExpenses/Premium/Oop, errorList, individualMedicares list), `IndividualMedicareDetail` (year-by-year: partAPremium, partBPremium, partBPremiumSurcharge, medicareAdvantagePremium, partDPremium, partDPremiumSurcharge, conciergePremium, partAOOP, partBOOP, partDOOP, totalABMedicareAdvantage, dentalPremium, dentalOOP, planGPremium, planFPremium, planNPremium, totalABGD, totalABFD, totalABND, totalABCD) |
| `Models/CostProjection.cs` | `CostProjectionResult` (combined financial planner + AI evaluation — yearlyDetails, lifetimeTotals, evaluation, presentValue from FP Present Value API), `LifetimeTotals` (lifetime AB/MA expenses/premium/OOP, surcharges, totalIrmaa, lifeTimeConciergePremium, supplementPlanType, supplementPlanPremium, conciergeIncluded, plan-specific lifetime fields: lifeTimeABGD/ABFD/ABND/ABCDExpenses/Premium/Oop), `CostEvaluation` (AI-generated — planName, planBundleCode, lifetimeSummary, costTrajectory, trajectoryExplanation, yearlyHighlights, categories, savingsTips, overallAssessment), `LifetimeSummary` (totalPremiums, totalOutOfPocket, totalCombined, projectionYears, averageAnnualCost), `YearlyHighlight` (year, totalCost, flag, explanation), `CostCategory` (name, lifetimeTotal, percentOfTotal, trend, insight), `SavingsTip` (title, description, estimatedSavings, priority) |
| `Models/FinancialPlannerDrug.cs` | `DrugSearchRequest`, `DrugSearchResponse` (webServiceTransactionId, webServiceStatus, drugName, drugList), `DrugListItem` (rxcui, displayName, prescription), `DrugDetailRequest`, `DrugDetailResponse` (rxcui, drugDetailAdvanceList), `DrugDetailAdvanceItem` (drugName, rxcui, genericDrugName, genericRxcui, newDoseForm, rxnDoseForm, strength, brandName, prescription, drugType — computed: "Generic" if brandName empty, else "Branded"), `DrugSearchResult` (drugName, search, matchedDrug, detail), `BulkDrugSearchResponse` (results, interactions, duplicateTherapies), `DrugInteractionAnalysis` (interactions, duplicateTherapies) |
| `Models/LongTermCare.cs` | `LongTermCareRequest` (age, pvAsOfYear, lifeExpectancy, transactionTypeFlag, healthProfile, location, zipcode, tobacco, currentLifeStyleExpenses, care-year counts, gender, alzheimersFlag, heartStorkeFlag), `LongTermCareResponse` (full LTC API response — region, county code, per-care-type totals, present values, year-by-year `LtcExpenseEntry` lists for adult day health, home care, assisted care, nursing care), `LtcExpenseEntry` (year + expense) |
| `Models/MedicareAdvantagePlan.cs` | `MedicareAdvantagePlanRequest` (same shape as `PartDPlanRecommendationRequest` with `MedicareAdvantage: true` flag, paginated filters: `StarRatingFilter?, PrescriptionCoverageFilter?, ContractIdFilter?, MailOrderPharmacy`) |
| `Models/MedigapPlanQuotes.cs` | `MedigapPlanQuotesRequest` (zip5, gender, tobacco, birthDate MM-YYYY, plan type, county, taxFilingStatus, magiTier, healthProfile, coverageYear, versionId?), `MedigapPlanQuotesResponse` (contractIdCarrierMap, deductible, planList), `MedigapPlanQuote` (key, age, companyBase, contextualData, discounts, eAppLink, effectiveDate, expiresDate, fees, gender, rate, rateIncreases, rateType, plan), with nested `MedigapCompanyBase`, `MedigapContextualData`, `MedigapRate`, `MedigapDiscount`, `MedigapFee`, `MedigapRateIncrease` |
| `Models/PartDPlanRecommendation.cs` | `PartDPlanRecommendationRequest` (userId, sortRecommendations, `CountyCodeModel`, prescriptions `List<PrescriptionInput>`, beneficiaryCostDataRequired, pharmacyNetworkDataRequired, pharmacies `List<PharmacyInput>`, planRecommendName/Email, drugListingName, recommendationListId, taxFilingStatus, magiTier, healthGrade, birthDate, fullYearOOPCost, coverageYear, includePlanExpensesFullYear, pagination, `StarRatingFilter?`, filters, `MailOrderPharmacy`), `CountyCodeModel` (zipcode, state, stateCode, city, lat/lng, countyCode, countyName), `PrescriptionInput` (rxcui, refillDuration, prescriptionCount, ndc), `PharmacyInput` (pharmacyNumber, pharmacyName, latitude, longitude). **Response types:** `PartDPlanRecommendationResponse` (response-level `ContractIdCarrierMap`, `PartDPremiumSurcharge`, `PartBPremiumSurcharge`, `MonthsUsedForExpenseCalc`, pagination, `RecommendationList`), `RecommendationListItem` (contractId, planId, segmentId, `PharmacyWiseRecommendations[]`), `PharmacyWiseRecommendation` (premium, deductible, totalPlanCost, totalPrescriptionCost, starRating, `PrescriptionDrugCovered`, `PartAandBBenefitServiceCost`, `UnavailableDrugs[]`, `PharmacyNetworks[]`), `PharmacyNetwork` (pharmacyNumber, pharmacyName, pharmacyNetworkType, distance) |

### MongoDB Documents (`Documents/`)

| File | Key Types |
|------|-----------|
| `Documents/RecommendationDocument.cs` | `RecommendationDocument` (root — userId, name, status, profile snapshot, planSelections, drugList, pharmacy, mailOrderPharmacy, lastCostSnapshot, timestamps), `ProfileSnapshot` (full profile mirror: name, DOB, gender, zip, county, state, city, address, coverage year, health, LIS), `SelectedDrugDoc`, `SelectedPlanDoc` (+deductible, starRating, totalPrescriptionCost, totalPlanCost, prescriptionDrugCovered, unavailableDrugs[], planExpenses[]), `SelectedPharmacyDoc`, `MailOrderPharmacyDoc`, `CostSnapshotDoc` (+supplementPlanType, supplementPlanPremium, yearlyDetails[], evaluation), `YearlyDetailDoc` (20 per-year financial fields including planG/F/NPremium, totalABGD/ABFD/ABND/ABCD), `CostEvaluationDoc`, `LifetimeSummaryDoc`, `YearlyHighlightDoc`, `CostCategoryDoc`, `SavingsTipDoc`, `PlanExpenseDoc` |
| `Documents/ConvStateDocument.cs` | `ConvStateDocument` (userId, `ConversationState` enum: Idle/AwaitingConfirmation/AwaitingDeletePhrase/CollectingProfile/CollectingDrugs/CollectingPharmacy/CollectingPlans/Processing, activeIntent, awaitingConfirmationFor, pendingChanges `BsonDocument`, collectedFields `BsonDocument`, TTL expiresAt) |
| `Documents/LtcCurrentSelectionsDocument.cs` | `LtcCurrentSelectionsDocument` (userId, name, healthProfile, numberOfAdultDayHealthCareYears, numberOfHomeCareYears, numberOfNursingCareYears, ltcResultJson, createdAt, updatedAt) — one document per user in collection `ltcCurrentSelections` |
| `Documents/UserAnalysisSelectionsDocument.cs` | `UserAnalysisSelectionsDocument` (userId, name, drugs `List<PrescriptionDrugDoc>`, activeSection, selectedPharmacies `List<UserAnalysisPharmacyDoc>`, selectedPlans `List<UserAnalysisPlanDoc>`, createdAt, updatedAt) — one logical row per user in collection `userAnalysisSelections`. `UserAnalysisPharmacyDoc` (pharmacyNumber, name, address, distance, zipcode). `UserAnalysisPlanDoc` (slot, planId, planName, contractId, medigapKey?, medigapPlanType?) |
| `Documents/PrescriptionDocument.cs` | Named prescriptions with embedded drug list |
| `Documents/ChatSessionDocument.cs` | Chat session messages + UI state |

### Exceptions (`Exceptions/AppExceptions.cs`)

| Exception | Status | Purpose |
|-----------|--------|---------|
| `AppException` | 500 (base) | Abstract base with `StatusCode` property |
| `NotFoundException` | 404 | Entity lookup fails |
| `ValidationException` | 400 | Supports message or error dictionary |
| `UnauthorizedException` | 401 | Auth-related denials |
| `ConflictException` | 409 | Duplicate resource conflicts |

### Interfaces (`Interfaces/`)
- `IRepository<T>` — generic repository contract (`GetByIdAsync`, `GetByUserIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `ExistsByUserIdAsync`).
- Per-entity marker interfaces: `IProfileRepository`.
- `IUserRepository` — user-specific (GetByEmail, GetByPhone, GetById, Create, Update, EmailExists, PhoneExists).
- `IDrugAiService` — AI drug analysis contract (`AnalyzePrescription`, `SuggestDrugNames`).
- `IMedicareCostService` — CMS Medicare cost lookup contract.
- `IRxNormService` — RxNorm normalization + interaction + NDC lookup contract (`NormalizeDrug`, `GetInteractions`, `GetNdcsByRxCui`).
- `IFdaNdcService` — FDA NDC Directory package info contract (`GetPackageInfo`, `GetPackageInfoBatch`).
- `IPharmacyPricingService` — pharmacy search + pricing contract.
- `IPharmacyLookupService` — Financial Planner pharmacy lookup contract (`GetPharmaciesAsync`). Request/response models co-located: `PharmacyLookupRequest`, `PharmacyLookupResponse`, `PharmacyLookupEntry`.
- `IChatClient` — M.E.AI standard chat client interface. Single provider registered via `"AiProvider"` config switch (`"Anthropic"` or `"OpenAI"`).
- `ICountyLookupService` — ZIP-to-county lookup, county name, state code, county code list.
- `IConstantsService` — Financial Planner constants API (MAGI tiers, filing statuses, etc.).
- `IMedicarePlanService` — Medicare plan recommendation contract.
- `IFipsLookupService` — synchronous ZIP-to-county-FIPS lookup (legacy, replaced by `ICountyLookupService`).
- `IPlanScoringAiService` — AI plan scoring contract.
- `ICmsPlanDataService` — CMS open data plan lookups (Phase 2).
- `IPlanPharmacyService` — plan-aware pharmacy search (Phase 3).
- `IIndividualMedicareService` — Financial Planner individualMedicareR5 API contract (`CalculateAsync`).
- `IFinancialPlannerDrugService` — Financial Planner drug search contract (`SearchBulkAsync`). Internally calls `SearchAndMatchAsync` per drug (search + match by displayName + detail), then AI pairwise interaction evaluation if >1 drug.
- `ICostEvaluationAiService` — AI cost evaluation contract. `EvaluateAsync()` takes `IndividualMedicareResponse` + plan/profile context + `supplementPlanType` + `supplementPlanPremium`, returns `CostEvaluation` with chart-ready insights, category breakdowns, savings tips, and trajectory.
- `IPrescriptionDocRepository` — MongoDB: save/get/list/delete prescription documents.
- `IRecommendationRepository` — MongoDB: recommendation CRUD including `GetAllByUserIdAsync` (sorted by CreatedAt descending), `ExistsByUserIdAsync`, `DeleteByUserIdAsync`.
- `IUserAnalysisSelectionsRepository` — MongoDB: per-user current analysis selections (`ReplaceCurrentForUserAsync`, `GetCurrentForUserAsync`, `UpdateDrugsAsync`, `UpdatePharmaciesAsync`, `UpdatePlansAsync`).
- `IConvStateRepository` — MongoDB: FSM conversation state per user (`GetByUserIdAsync`, `UpsertAsync`, `DeleteByUserIdAsync`).
- `ILongTermCareService` — LTC cost projection contract (`GetProjectionAsync(request, userEmail, ct)`).
- `IMedicareAdvantagePlanService` — MA plan recommendation contract (`RecommendAsync(request, ct)`).
- `IMedigapPlanQuotesService` — Medigap plan quotes contract (`GetQuotesAsync(request, ct)`).
- `IPartDPlanRecommendationService` — Part D plan recommendation contract (`RecommendAsync(request, ct)`).

---

## Application Layer (`AI.MedicareAssistant.Application`)

### DrugAnalysisService — Pipeline Orchestrator
Thin orchestrator. Injects `IEnumerable<IDrugAnalysisStep>` and `ILogger<DrugAnalysisService>`. Iterates steps ordered by `IDrugAnalysisStep.Order` — stops early if any step returns `false` (short-circuit). Logs pipeline start, short-circuit step name, and completion summary.

### Drug Analysis Pipeline (`Services/Pipeline/`)

| Step | Order | Class | Responsibility |
|------|-------|-------|----------------|
| 1 | 1 | `AiAnalysisStep` | Calls `IDrugAiService.AnalyzePrescription()`, deserializes AI JSON into `DrugAnalysisResult` |
| 2 | 2 | `DrugValidationStep` | Filters invalid drug entries. Populates flat arrays (`Strengths`, `Packaging`, `NdcCodes`, `DosageForms`) from `Formulations[]` for backward compatibility with downstream steps. Returns `false` to short-circuit if zero valid drugs remain |
| 3 | 3 | `CmsRxNormEnrichmentStep` | Parallel CMS + RxNorm enrichment per drug via `Task.WhenAll`. Resolves NDC codes from RxNorm, then calls `IFdaNdcService.GetPackageInfoBatch()` to match NDCs to formulations by package description (size + container type scoring). Falls back to index-based assignment if FDA API unavailable |
| 4 | 4 | `InteractionMergingStep` | Merges RxNorm-validated interactions with AI-identified ones (deduplicates by drug pair) |

> **Note:** `PharmacyPricingStep` exists in the codebase but is **not registered in the DI pipeline**. Pharmacy pricing is fetched on-demand via the standalone `GET /api/pharmacy/search` endpoint.

**Interface:** `IDrugAnalysisStep` — `int Order` (execution priority) + `Task<bool> ExecuteAsync(DrugAnalysisResult, AnalysisContext)`. Returns `false` to short-circuit.

**`AnalysisContext`** — immutable record: `Prescription` (string) + `ZipCode` (string?).

### Other Application Services

| Service | Role |
|---------|------|
| `ProfileService` | Consolidated profile CRUD via `IProfileRepository`. Get/Save/Delete operations. Maps `Profile` entity ↔ `ProfileDto`. |
| `AuthService` | JWT auth logic — signup, signin, forgot/reset password |
| `PrescriptionService` | Saves and retrieves named prescriptions. Maps `SavePrescriptionRequest` → `PrescriptionDocument` + `PrescriptionDrugDoc[]` via `IPrescriptionDocRepository` (MongoDB). Returns `PrescriptionResponse` with `PrescriptionDrugDto[]`. Supports delete and get-by-ID (`GetByIdAsync`). |
| `MedicarePlanService` | Orchestrates plan recommendations: FIPS lookup → LIS determination → AI scoring → CMS enrichment (Phase 2). `BuildRequestAsync` loads user profile via `ProfileService` (sequential for DbContext safety), accepts optional `List<SelectedPharmacy>`. `ComputePharmacyCostBreakdowns` computes per-plan cost at each selected pharmacy (up to 5) with ~20% preferred pharmacy discount; populates `CostBreakdowns` list sorted cheapest-first and re-sorts plans by best pharmacy cost. LIS thresholds: Full ≤$22,590, Partial ≤$33,240 (2025 FPL). |
| `PlanPharmacyService` | Plan-aware pharmacy search (Phase 3): overlays copay/formulary data, flags preferred networks, applies ~20% preferred discount. |
| `CostProjectionService` | Orchestrates cost projections: loads user profile via `ProfileService`, resolves state code to full name, formats DOB, computes remaining months, builds `IndividualMedicareRequest`, calls Financial Planner API, then runs AI evaluation via `ICostEvaluationAiService`. After FP calculation, calls `IPresentValueService` to compute present value (discount rate 6%) from yearwise expenses. Method: `EvaluateCostsAsync()` (combined `CostProjectionResult` with Financial Planner data + AI evaluation + present value). Contains `StateCodeToName` dictionary for state code→name resolution. Populates `LifetimeTotals` with `TotalIrmaa` (combined B+D surcharges), `SupplementPlanType`, `SupplementPlanPremium`, `ConciergeIncluded`, and 12 plan-specific lifetime fields (ABGD/ABFD/ABND/ABCD × Expenses/Premium/Oop). PV call is non-fatal (try/catch with warning log). |
| `ChatIntentService` | AI-powered chat intent classification. Injects `IChatClient` (M.E.AI) and `ILogger`. Loads system prompt from `Prompts/system/chat-intent-system.txt` at construction time (`File.ReadAllText`). Defines 17 intent categories with parameter extraction rules for 11 profile fields plus `prescriptionName` and `analysisName`. Intents include `ACTION_SAVE_ANALYSIS`, `ACTION_RUN_ANALYSIS`, `NAVIGATE_SAVED_ANALYSES`. **Page-context injection:** appends `PageContextBuilder.Build(request.CurrentPage)` to the system prompt at call time before invoking the AI — providing route-specific disambiguation without modifying the prompt file. Sends user message via `IChatClient.GetResponseAsync()` for JSON classification. Parses response (strips markdown fences if present). Returns `ChatIntentResponse` with intent, optional params, and confirmation message. Fallback returns `UNKNOWN` intent on errors. |
| `ProfileExtractService` | AI-powered profile field extraction from natural language. Injects `IChatClient` and `ILogger`. Loads system prompt from `Prompts/system/profile-extract-system.txt`. Accepts message + missing fields list. Returns `ProfileExtractResponse` with extracted fields (dict) + conversational reply. Supports 13 profile fields (firstName, lastName, gender, dateOfBirth, tobacco, health, taxFiling, magi, coverageYear, zipCode, address, lifeExpectancy, concierge). |
| `DrugSelectionExtractService` | AI-powered drug formulation selection extraction from chat. Injects `IChatClient` and `ILogger`. Loads system prompt from `Prompts/system/drug-selection-system.txt`. Accepts message + available drugs summary. Returns `DrugSelectionExtractResponse` with drugName, type, dosageForm, strength, quantity, action (select/options/confirm_all/remove/edit), and reply. Fuzzy matches drug names, forms, and strengths. |
| `PharmacySelectionExtractService` | AI-powered pharmacy selection extraction from chat. Injects `IChatClient` and `ILogger`. Loads system prompt from `Prompts/system/pharmacy-selection-system.txt`. Accepts message + available/selected pharmacy summaries. Returns `PharmacySelectionExtractResponse` with pharmacyName, action (select/remove/list/search), searchTerm, and reply. Fuzzy matches pharmacy names. |
| `PlanSelectionExtractService` | AI-powered plan selection extraction from chat. Injects `IChatClient` and `ILogger`. Loads system prompt from `Prompts/system/plan-selection-system.txt`. Accepts message + available/selected plan summaries. Returns `PlanSelectionExtractResponse` with planName, planType, action (select/remove/switch_section), section, and reply. Matches plan names and types. |
| `RecommendationService` | CRUD for `RecommendationDocument` in MongoDB. Methods: `GetActiveAsync`, `GetByIdAsync`, `GetAllAsync`, `ExistsAsync`, `CreateAsync` (with optional `force` flag to replace), `UpdateProfileAsync`, `UpdateDrugsAsync`, `UpdatePharmacyAsync`, `UpdatePlansAsync`, `UpdateCostSnapshotAsync`, `DeleteAsync`. Sets `status`, `UserId`, timestamps on create. |
| `ConvStateService` | FSM conversation state persistence via `IConvStateRepository`. Methods: `GetOrCreateAsync` (creates `Idle` state on first call), `UpdateStateAsync` (sets `State` + `activeIntent`), `SetPendingChangeAsync` (transitions to `AwaitingConfirmation` with description + `BsonDocument` pending changes), `SetCollectedFieldAsync` (appends a field to `collectedFields`), `ClearPendingAsync` (resets state to Idle), `ResetAsync` (full wipe). Refreshes TTL expiry on every access. |
| `OrchestratorIntentService` | AI-powered intent classification for the chatbot orchestrator. Injects `IChatClient` and `ILogger`. Loads system prompt from `Prompts/system/orchestrator-intent-system.txt`. Classifies messages into 19 domain intents (create_recommendation, modify_drugs, update_demographic, etc.). **Page-context injection:** appends `PageContextBuilder.Build(currentPage)` to the system prompt before the AI call — same mechanism as `ChatIntentService`. `ClassifyAsync(message, currentPage?)` receives `currentPage` threaded down from `ChatOrchestratorController` → `ChatOrchestratorService`. Returns `OrchestratorIntentResult` (intent + extracted params). |
| `ChatOrchestratorService` | Core FSM router (~1,300 lines). `ProcessMessageAsync(userId, message, currentPage?, ct)` — first checks FSM multi-turn states (AwaitingConfirmation, AwaitingDeletePhrase, CollectingProfile/Drugs/Pharmacy/Plans), then falls through to 19-intent dispatch via `_handlers` dictionary. Handler methods use `ConvStateService`, `RecommendationService`, `ProfileService`, `DeltaCalculationService`, `CostProjectionService`, `ICountyLookupService`, `IPharmacyLookupService`, `IDrugAiService`. Returns `OrchestratorResponse` (reply, displayData?, deltaResult?). |
| `DeltaCalculationService` | Before/after cost comparisons. `ComputeAsync(beforeDoc, afterDoc)` — runs Financial Planner cost projection for both states, computes deltas, calls AI for narrative summary. `BuildPreviewDelta()` — assembles `DeltaResult` (before/after lifetime costs, savings, AI explanation). |
| `PageContextBuilder` | Static internal helper (`Application/Services/PageContextBuilder.cs`). `Build(string? currentPage)` returns a page-specific guidance block appended to AI system prompts at call time. Maps Angular route prefixes (`/medicare-analysis/drugs`, `/medicare-analysis/profile`, `/medicare-analysis/pharmacies`, `/medicare-analysis/plans`, `/medicare-analysis/cost-projections`, `/saved`, `/profile`) to disambiguation rules. Returns empty string for null/unknown routes. Consumed by `ChatIntentService` and `OrchestratorIntentService`. Prompt files are never modified — all page logic is centralized here. |
| `PlanCardEnrichmentService` | Static utility (`Application/Services/PlanCardEnrichmentService.cs`). Pure computation — no DI, no I/O. Three static methods: `EnrichPartD(plan, response, selectedPharmacyNumbers, totalDrugs)` → `EnrichedPartDCard`, `EnrichMedigap(quote, response)` → `EnrichedMedigapCard`, `EnrichMA(plan, response, selectedPharmacyNumbers, totalDrugs)` → `EnrichedMACard`. Computes derived display fields from raw API responses: formatted plan IDs (`contractId-planId-segmentId`), insurance carrier from `ContractIdCarrierMap`, Part D surcharge (response-level), prescription OOP, pharmacy network ratio, drug coverage ratio, Medigap premium cents→dollars, Part B surcharge, healthcare OOP, remaining months, MA combined surcharges. Called by frontend enrichment service (mirrored logic); backend DTOs available for future controller-level enrichment. |

---

## Infrastructure Layer (`AI.MedicareAssistant.Infrastructure`)

### AI Services

| Service | Interface | External API | Notes |
|---------|-----------|-------------|-------|
| `DrugAiService` | `IDrugAiService` | OpenAI GPT-4.1 | Builds prompts via `PromptBuilder.Build()` and `BuildDrugNameSuggestion()`, calls `IChatClient.GetResponseAsync()`. Two methods: `AnalyzePrescription()` for full analysis, `SuggestDrugNames()` for name identification. |
| `PlanScoringAiService` | `IPlanScoringAiService` | OpenAI GPT-4.1 | Builds prompts via `PromptBuilder.BuildPlanScoring()` (system + task + schema + template). `BuildPharmacyContext()` renders numbered list of selected pharmacies for AI copay estimation. Cache key includes pharmacy NPIs. Cached 24h. |
| `CostEvaluationAiService` | `ICostEvaluationAiService` | OpenAI GPT-4.1 | Builds prompts via `PromptBuilder.BuildCostEvaluation()` (system + task + schema + template). Renders year-by-year breakdown text from `IndividualMedicareResponse`. Parses AI JSON response into `CostEvaluation` (strips markdown fences if present). Placeholder substitution: `{{PLAN_NAME}}`, `{{PLAN_BUNDLE_CODE}}`, `{{COVERAGE_YEAR}}`, `{{LIFE_EXPECTANCY}}`, `{{TAX_FILING_STATUS}}`, `{{STATE_NAME}}`, `{{LIFETIME_AB_MA_EXPENSES/PREMIUM/OOP}}`, `{{LIFETIME_D/B_SURCHARGE}}`, `{{TOTAL_IRMAA}}`, `{{SUPPLEMENT_PLAN_TYPE}}`, `{{SUPPLEMENT_PLAN_PREMIUM}}`, `{{YEARLY_BREAKDOWN}}`. |
| `AnthropicMeaiChatClient` | `IChatClient` (M.E.AI) | Anthropic Claude Sonnet 4 | Implements M.E.AI `IChatClient` interface (`GetResponseAsync`, `GetStreamingResponseAsync`). HTTP POST to Anthropic API (v2023-06-01). Handles system prompts separately from user messages. Extracts text content from response. Streaming falls back to full response. Configured via `Anthropic:ApiKey`, `Anthropic:Model`, `Anthropic:BaseUrl`, `Anthropic:MaxTokens` in appsettings. Registered conditionally when `"AiProvider": "Anthropic"`. |

### External Data Services

| Service | Interface | External API | Caching | Graceful Degradation |
|---------|-----------|-------------|---------|---------------------|
| `CmsMedicareCostService` | `IMedicareCostService` | CMS SOCRATA (`data.cms.gov`) | None | Returns `null` on timeout/error |
| `RxNormService` | `IRxNormService` | NIH RxNorm REST API | None | Returns null/empty on failure. `GetNdcsByRxCui()` calls `/rxcui/{id}/ndcs.json` — provides NDC list (no packaging detail) |
| `FdaNdcService` | `IFdaNdcService` | openFDA NDC Directory | 7 days (per NDC) | Returns null on failure. Resolves package descriptions (size + type) for each NDC. Used by enrichment step for package-accurate matching |
| `CmsPharmacyPricingService` | `IPharmacyPricingService` | NPI Registry + IChatClient AI pricing | NPI: 7 days, AI pricing: 30 days | Builds prompts via `PromptBuilder.BuildPharmacyPricing()` (system + task + schema + template). Fallback chain: AI prices → ParsePriceString → null ("—") |
| `FinancialPlannerPharmacyService` | `IPharmacyLookupService` | Financial Planner (`getPharmacies`) | None | Fetches paginated pharmacies by lat/lng from Financial Planner API. Basic auth token from config. Returns `PharmacyLookupResponse` with pharmacy name, number, address, distance, zipcode. 15s timeout. |
| `CmsPlanDataService` | `ICmsPlanDataService` | CMS SOCRATA (Phase 2) | 7 days per state+county | Returns empty list on failure |
| `FipsLookupService` | `IFipsLookupService` | None (static in-memory) | Singleton | Tries `Data/zip-fips.csv`, falls back to ~400+ built-in ZIPs |
| `CountyLookupService` | `ICountyLookupService` | Financial Planner API | 1 hour per ZIP | Fetches county code, name, state code via Financial Planner API. Used for county-based plan lookups |
| `FinancialPlannerConstantsService` | `IConstantsService` | Financial Planner API | In-memory cache | Fetches MAGI tiers, filing statuses, and other constants from Financial Planner constants API |
| `IndividualMedicareService` | `IIndividualMedicareService` | Financial Planner (`individualMedicareR5`) | None | Computes lifetime Medicare cost projections. Posts `IndividualMedicareRequest` to external API, returns year-by-year breakdown. Basic auth token from config. 30s timeout. |
| `FinancialPlannerDrugService` | `IFinancialPlannerDrugService` | Financial Planner (`drugSearch`, `getDrugDetailAdvance`) + OpenAI GPT-4.1 | None | Drug search & detail from Financial Planner API. Public method: `SearchBulkAsync` processes multiple drugs (internally calls private `SearchAndMatchAsync` per drug: search → match by displayName → detail), then calls `EvaluateInteractionsAsync` (private) via `IChatClient` for AI pairwise interaction + duplicate therapy analysis if >1 drug. Basic auth token from config. 15s timeout. |
| `LongTermCareService` | `ILongTermCareService` | Financial Planner LTC API | None | POSTs `LongTermCareRequest` to Financial Planner LTC endpoint. Returns `LongTermCareResponse` with per-care-type year-by-year expense lists and present-value totals. Basic auth token from config. 30s timeout. |
| `MedicareAdvantagePlanService` | `IMedicareAdvantagePlanService` | Financial Planner MA plan API | None | POSTs `MedicareAdvantagePlanRequest` (with `MedicareAdvantage: true`) to Financial Planner plan recommendation endpoint. Returns paginated ranked MA plan list. Basic auth from config. |
| `MedigapPlanQuotesService` | `IMedigapPlanQuotesService` | Financial Planner Medigap API | None | POSTs `MedigapPlanQuotesRequest` to Financial Planner Medigap quotes endpoint. Returns `MedigapPlanQuotesResponse` (carrier map, deductible, plan list with rates/discounts). Basic auth from config. |
| `PartDPlanRecommendationService` | `IPartDPlanRecommendationService` | Financial Planner Part D API | None | POSTs `PartDPlanRecommendationRequest` to Financial Planner Part D plan recommendation endpoint. Returns paginated ranked Part D plan list. Basic auth from config. |

### Data Access

| Class | Role |
|-------|------|
| `Repository<T>` | Generic EF Core base. Uses `EF.Property<Guid>(e, "UserId")` for dynamic queries. |
| Per-entity repositories | Extend `Repository<T>`, implement marker interface (`IProfileRepository`). Constructor forwarding only. |
| `UserRepository` | User-specific queries. No eager loading. |
| `AppDbContext` | EF Core DbContext (MySQL) with entity configurations. |
| `AppDbContextFactory` | Design-time factory for migration generation. |
| `MongoDbContext` | MongoDB typed collection accessor. Provides `Prescriptions`, `ChatSessions`, `UserAnalysisSelections`, `Recommendations`, `ConvStates`, `LtcCurrentSelections` collections. Creates compound indexes at startup. |
| `PrescriptionDocRepository` | MongoDB: prescription CRUD (save, list by user, get by ID, delete). |
| `ChatSessionRepository` | MongoDB: chat session persistence (get/upsert by userId). |
| `UserAnalysisSelectionsRepository` | MongoDB: per-user current analysis selections — drugs/pharmacies/plans partial updates. |
| `RecommendationRepository` | MongoDB: recommendation CRUD (get by user, get all by user sorted by CreatedAt desc, get by ID, create, replace, delete). Unique userId compound index. |
| `ConvStateRepository` | MongoDB: FSM conversation state (get/upsert/delete by userId). Unique userId index + TTL index on `expiresAt`. |
| `LtcSelectionsRepository` | MongoDB: LTC care-type selections (get/upsert by userId). |

### Utilities
- **PromptBuilder:** Loads and combines prompt files from `Prompts/` directory using a consistent 4-layer pipeline (system + task + schema + template). Five methods: `Build()` for drug analysis, `BuildDrugNameSuggestion()` for name identification, `BuildPharmacyPricing()` for pharmacy pricing, `BuildPlanScoring()` for plan recommendations, `BuildCostEvaluation()` for cost evaluation. All return `(system, user)` tuples. Pricing, scoring, and cost evaluation methods accept a `Dictionary<string, string>` for placeholder substitution.

---

## Chatbot Orchestrator Layer

The orchestrator layer manages the full lifecycle of a Medicare recommendation through a conversational interface. It uses a finite state machine (FSM) to route messages, a 19-intent classifier for natural language understanding, and a delta engine for cost impact previews.

### MongoDB Document Models

| Document | Collection | Key Fields |
|----------|-----------|------------|
| `RecommendationDocument` | `recommendations` | userId (unique index), `ProfileSnapshot`, `List<SelectedDrugDoc>`, `List<SelectedPlanDoc>`, `SelectedPharmacyDoc`, `MailOrderPharmacyDoc`, `CostSnapshotDoc` |
| `ConvStateDocument` | `conv_states` | userId (unique + TTL index), `ConversationState` enum, `BsonDocument PendingChanges`, `BsonDocument CollectedFields`, `AwaitingConfirmationFor` |
| `ChatSessionDocument` | `chat_sessions` | one active doc per user; `messages[]` (`role`, `content`, `timestamp`), `uiState.editMode`, `createdAt`, `updatedAt` |

**ConversationState enum:** `Idle`, `CollectingProfile`, `CollectingDrugs`, `CollectingPharmacy`, `CollectingPlans`, `AwaitingConfirmation`, `AwaitingDeletePhrase`, `ShowingComparison`, `ShowingProjections`, `WhatIfMode`

### MongoDB Repositories

| Repository | Interface | Methods |
|-----------|-----------|---------|
| `RecommendationRepository` | `IRecommendationRepository` | `GetByUserIdAsync`, `GetAllByUserIdAsync` (sorted by CreatedAt desc), `CreateAsync`, `ReplaceAsync`, `ExistsByUserIdAsync`, `DeleteByUserIdAsync` |
| `ConvStateRepository` | `IConvStateRepository` | `GetByUserIdAsync`, `UpsertAsync`, `DeleteByUserIdAsync` |
| `ChatSessionRepository` | `IChatSessionRepository` | `GetByUserIdAsync`, `UpsertAsync` |

Both registered in `MongoDbContext` with unique indexes on `userId`. ConvStates has a 30-minute TTL via `expiresAt`.

### Orchestrator Services

| Service | Responsibility |
|---------|---------------|
| `RecommendationService` | CRUD for recommendation documents — `GetActive`, `GetAllAsync` (returns all for user), `Exists`, `Create` (with force flag), `UpdateProfile`, `UpdateDrugs`, `UpdatePharmacy`, `UpdatePlans`, `UpdateCostSnapshot`, `Delete` |
| `ConvStateService` | FSM state persistence — `GetOrCreate`, `UpdateState`, `SetPendingChange`, `SetCollectedField`, `ClearPending`, `Reset`. Auto-refreshes 30-min TTL on every operation. |
| `OrchestratorIntentService` | 19-intent classifier via IChatClient + `orchestrator-intent-system.txt`. Returns `OrchestratorIntentResult { Intent, Params }`. Handles markdown fence stripping and JSON parse errors gracefully. |
| `ChatOrchestratorService` | Core FSM router (~1,300 lines). `ProcessMessageAsync` checks FSM state priority: Confirmation → DeletePhrase → CollectingProfile/Drugs/Pharmacy/Plans → Intent classification → Handler dispatch. Contains 19 handler methods + multi-turn collection wizards + helper methods. Top-level try/catch for error resilience. |
| `DeltaCalculationService` | Before/after cost comparisons — `ComputeAsync` (full recalculation via CostProjectionService), `BuildPreviewDelta` (lightweight from snapshot), AI narrative via `delta-narrative-system.txt`. `BuildSnapshotFromResult` uses plan-specific lifetime fields (ABGD for Plan G, ABFD for Plan F, etc.) based on `SupplementPlanType`, and stores real FP present value from `CostProjectionResult.PresentValue`. |

### Orchestrator Controllers

| Endpoint | Controller | Method |
|----------|-----------|--------|
| `POST /api/chat/orchestrate` | `ChatOrchestratorController` | Accepts `OrchestratorRequest { Message }`, extracts userId from JWT, calls `ProcessMessageAsync`, returns `OrchestratorResponse` |
| `GET /api/recommendation` | `RecommendationController` | Get active recommendation |
| `GET /api/recommendation/{id}` | `RecommendationController` | Get full recommendation by ID (for loading saved analyses into the wizard) |
| `GET /api/recommendation/all` | `RecommendationController` | Get all saved recommendations for the user (returns `RecommendationSummaryResponse[]` with id, name, status, drugCount, planCount, hasCostSnapshot, lifetimeTotal, dates; sorted by CreatedAt desc) |
| `POST /api/recommendation` | `RecommendationController` | Create recommendation (request includes expanded `CostSnapshotDto` with yearlyDetails + full evaluation, expanded `SelectedPlanDto` with deductible/starRating/totalPrescriptionCost/planExpenses/unavailableDrugs; supports `force` flag for overwrite) |
| `PUT /api/recommendation/profile` | `RecommendationController` | Update profile snapshot |
| `PUT /api/recommendation/drugs` | `RecommendationController` | Update drug list |
| `PUT /api/recommendation/pharmacy` | `RecommendationController` | Update pharmacy selection |
| `PUT /api/recommendation/plans` | `RecommendationController` | Update plan selections |
| `DELETE /api/recommendation` | `RecommendationController` | Delete (requires `confirmed=true`) |

### FSM Message Flow

```
User Message
  │
  ▼
ProcessMessageAsync
  │
  ├── AwaitingConfirmation? → HandleConfirmation (yes/no → commit or cancel)
  ├── AwaitingDeletePhrase? → HandleDeletePhrase (exact phrase match)
  ├── CollectingProfile?    → ContinueProfileCollection (12-step wizard)
  ├── CollectingDrugs?      → ContinueDrugCollection
  ├── CollectingPharmacy?   → ContinuePharmacyCollection
  ├── CollectingPlans?      → ContinuePlanCollection
  └── Idle                  → OrchestratorIntentService.ClassifyAsync
                                 │
                                 ▼
                            19-intent handler dispatch
```

### DI Registrations (Program.cs)

All orchestrator services registered as **Scoped**:
- `IRecommendationRepository` → `RecommendationRepository`
- `IConvStateRepository` → `ConvStateRepository`
- `IChatSessionRepository` → `ChatSessionRepository`
- `RecommendationService`, `ConvStateService`, `OrchestratorIntentService`, `ChatOrchestratorService`, `DeltaCalculationService`

---

← [Chapter 3 — Prompt Architecture](ch03-prompt-architecture.md) | [Table of Contents](APPLICATION_BLUEPRINT.md) | [Chapter 5 → Data & Authentication](ch05-data-and-auth.md)
