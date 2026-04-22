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
- **Configuration** — `appsettings.json` → `Serilog:MinimumLevel` section: `Default: Information`, overrides for `Microsoft.AspNetCore: Warning`, `System.Net.Http.HttpClient: Warning`. `ReadFrom.Configuration()` picks up log level config at runtime.
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
| `CountyLookupController` | Public/Mixed | ZIP-based county code lookup and configuration. `POST api/county-lookup/getCountycodeList` — accepts `ZipCodeRequest`, returns `CountyCodeEntry[]` (county codes, names, state codes for a ZIP). `GET api/county-lookup/constants/magi-tiers` — accepts `filingStatus` and `coverageYear` query params, returns MAGI tier options from the Financial Planner constants API. |
| `FinancialPlannerDrugController` | `[Authorize]` | `POST api/FinancialPlannerDrug/search` — searches a single drug via Financial Planner `drugSearch` API, matches by `displayName`, fetches detail via `getDrugDetailAdvance`. `POST api/FinancialPlannerDrug/search-bulk` — accepts `BulkDrugSearchInput` with `List<string> DrugNames`, searches each drug, matches, fetches details, and if >1 drug calls AI to evaluate pairwise interactions and duplicate therapies. Returns `BulkDrugSearchResponse`. `POST api/FinancialPlannerDrug/detail` — fetches drug detail by rxcui. |
| `ChatIntentController` | `[Authorize]` | Routes at `api/chat`. 5 endpoints: `POST intent` — accepts `ChatIntentRequest` (message + isProfileComplete + **currentPage?**), delegates to `ChatIntentService`, returns `ChatIntentResponse` (intent, params, confirmationMessage). `POST extract-profile` — accepts `ProfileExtractRequest` (message + missingFields), delegates to `ProfileExtractService`, returns extracted profile fields + reply. `POST extract-drug-selection` — accepts `DrugSelectionExtractRequest` (message + availableDrugs), delegates to `DrugSelectionExtractService`, returns drug formulation extraction (drugName, type, dosageForm, strength, quantity, action, reply). `POST extract-pharmacy-selection` — accepts `PharmacySelectionExtractRequest` (message + availablePharmacies + selectedPharmacies), delegates to `PharmacySelectionExtractService`, returns pharmacy selection extraction (pharmacyName, action, searchTerm, reply). `POST extract-plan-selection` — accepts `PlanSelectionExtractRequest` (message + availablePlans + selectedPlans), delegates to `PlanSelectionExtractService`, returns plan selection extraction (planName, planType, action, section, reply). |
| `ChatSessionController` | `[Authorize]` | Routes at `api/chat/session` for phase-1 Mongo chat persistence. `POST start-new` starts a new session. |
| `RecommendationController` | `[Authorize]` | Routes at `api/recommendation`. `GET` — returns active recommendation for user. `GET {id}` — returns recommendation by ID. `GET all` — list all recommendations (summary response with drug/plan counts, cost totals). `POST` — creates new recommendation (`CreateRecommendationRequest` with profile, drugs, pharmacy, plans, costSnapshot; `?force=true` replaces existing). `PUT profile` — updates profile snapshot. `PUT drugs` — replaces drug list. `PUT pharmacy` — updates pharmacy + mail order. `PUT plans` — replaces plan selections. `PUT cost-snapshot` — saves cost projection snapshot. `DELETE` — removes active recommendation. |
| `LongTermCareController` | `[Authorize]` | Routes at `api/long-term-care`. `POST` — accepts `LongTermCareRequest` (age, pvAsOfYear, lifeExpectancy, healthProfile, location, zipcode, tobacco, care years, gender, alzheimers/heartStroke flags), delegates to `ILongTermCareService.GetProjectionAsync()`, returns `LongTermCareResponse` (year-by-year LTC expense breakdowns and present-value totals). |
| `LtcSelectionsController` | `[Authorize]` | Routes at `api/ltc`. `PUT current` — accepts `SaveLtcCurrentRequest` (healthProfile, care-type year counts, ltcResultJson), upserts `LtcCurrentSelectionsDocument` in MongoDB. `GET current` — returns `LtcCurrentResponse` (care-type selections + last projection result). |
| `MedicareAdvantagePlanController` | `[Authorize]` | Routes at `api/MedicareAdvantagePlan`. `POST recommend` — accepts `MedicareAdvantagePlanRequest` (county code model, prescriptions, pharmacies, sort/filter params, `MedicareAdvantage: true`), delegates to `IMedicareAdvantagePlanService.RecommendAsync()`. Returns ranked MA plan list from Financial Planner API. |
| `MedigapPlanController` | `[Authorize]` | Routes at `api/MedigapPlan`. `POST quotes` — accepts `MedigapPlanQuotesRequest` (zip5, gender, tobacco, birthDate, plan type, county, taxFilingStatus, magiTier, healthProfile, coverageYear, versionId?), delegates to `IMedigapPlanQuotesService.GetQuotesAsync()`. Returns `MedigapPlanQuotesResponse` with plan list, deductible, carrier map. |
| `PartDPlanController` | `[Authorize]` | Routes at `api/PartDPlan`. `POST recommend` — accepts `PartDPlanRecommendationRequest` (same shape as `MedicareAdvantagePlanRequest` minus `MedicareAdvantage` flag), delegates to `IPartDPlanRecommendationService.RecommendAsync()`. Returns ranked Part D plan list from Financial Planner API. |

### CORS
Configured for `localhost:4200` (dev) and `169.61.105.110:9600` (production). `AllowCredentials()` is required for the SignalR WebSocket handshake and must be combined with explicit `WithOrigins(...)` (not `AllowAnyOrigin`).

### DI Registration
`builder.Services.AddSignalR()` registers the SignalR runtime (no extra NuGet package — included in `Microsoft.AspNetCore`). `ChatHub` is mapped via `app.MapHub<ChatHub>("/hubs/chat")` after `app.MapControllers()`. `PromptBuilder` (singleton), `IDrugAiService`/`DrugAiService` (scoped), `IChatClient` (via config-driven `"AiProvider"` switch: `"Anthropic"` → `AnthropicMeaiChatClient` via `AddHttpClient`, `"Gemini"` → `GeminiChatClient` via `AddHttpClient`, `"OpenAI"` → OpenAI SDK via `AddChatClient`), `IMedicareCostService`/`CmsMedicareCostService` (via `AddHttpClient`), `IFdaNdcService`/`FdaNdcService` (via `AddHttpClient`), `IMemoryCache` (via `AddMemoryCache`), `IProfileRepository`/`MongoProfileRepository` (scoped), `IUserRepository`/`MongoUserRepository` (scoped), `ProfileService` (scoped), `PrescriptionService` (scoped), `ICountyLookupService`/`CountyLookupService` (via `AddHttpClient`), `IConstantsService`/`FinancialPlannerConstantsService` (via `AddHttpClient`), `IPlanScoringAiService`/`PlanScoringAiService` (scoped), `ICmsPlanDataService`/`CmsPlanDataService` (via `AddHttpClient`, 10s timeout), `IIndividualMedicareService`/`IndividualMedicareService` (via `AddHttpClient`, 30s timeout), `ICostEvaluationAiService`/`CostEvaluationAiService` (scoped), `ILtcEvaluationAiService`/`LtcEvaluationAiService` (scoped), `CostProjectionService` (scoped), `IPharmacyLookupService`/`FinancialPlannerPharmacyService` (via `AddHttpClient`, 15s timeout), `IFinancialPlannerDrugService`/`FinancialPlannerDrugService` (via `AddHttpClient`, 15s timeout), `ChatIntentService` (scoped — AI-powered chat intent classification), `IEmailService`/`EmailService` (scoped).

### DI Registration (continued — Financial Planner services)
`ILongTermCareService`/`LongTermCareService` (via `AddHttpClient`, 30s timeout), `IPresentValueService`/`PresentValueService` (via `AddHttpClient`, 30s timeout), `IMedicareAdvantagePlanService`/`MedicareAdvantagePlanService` (via `AddHttpClient`), `IMedigapPlanQuotesService`/`MedigapPlanQuotesService` (via `AddHttpClient`), `IPartDPlanRecommendationService`/`PartDPlanRecommendationService` (via `AddHttpClient`), `RecommendationService` (scoped).

### MongoDB DI Registration
`IMongoClient` (singleton via `MongoClient`), `IMongoDatabase` (singleton), `MongoDbContext` (singleton — typed collection accessor with index creation on startup), `IPrescriptionDocRepository`/`PrescriptionDocRepository` (scoped), `IChatSessionRepository`/`ChatSessionRepository` (scoped), `IUserAnalysisSelectionsRepository`/`UserAnalysisSelectionsRepository` (scoped), `IRecommendationRepository`/`RecommendationRepository` (scoped), `ILtcSelectionsRepository`/`LtcSelectionsRepository` (scoped).

> **Note:** All repositories — including user and profile — are backed by MongoDB. There is no EF Core or MySQL dependency.

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
| `Models/LtcCostEvaluation.cs` | AI-generated LTC cost evaluation models |
| `Models/MedicareAdvantagePlan.cs` | `MedicareAdvantagePlanRequest` (same shape as `PartDPlanRecommendationRequest` with `MedicareAdvantage: true` flag, paginated filters: `StarRatingFilter?, PrescriptionCoverageFilter?, ContractIdFilter?, MailOrderPharmacy`) |
| `Models/MedigapPlanQuotes.cs` | `MedigapPlanQuotesRequest` (zip5, gender, tobacco, birthDate MM-YYYY, plan type, county, taxFilingStatus, magiTier, healthProfile, coverageYear, versionId?), `MedigapPlanQuotesResponse` (contractIdCarrierMap, deductible, planList), `MedigapPlanQuote` (key, age, companyBase, contextualData, discounts, eAppLink, effectiveDate, expiresDate, fees, gender, rate, rateIncreases, rateType, plan), with nested `MedigapCompanyBase`, `MedigapContextualData`, `MedigapRate`, `MedigapDiscount`, `MedigapFee`, `MedigapRateIncrease` |
| `Models/PartDPlanRecommendation.cs` | `PartDPlanRecommendationRequest` (userId, sortRecommendations, `CountyCodeModel`, prescriptions `List<PrescriptionInput>`, beneficiaryCostDataRequired, pharmacyNetworkDataRequired, pharmacies `List<PharmacyInput>`, planRecommendName/Email, drugListingName, recommendationListId, taxFilingStatus, magiTier, healthGrade, birthDate, fullYearOOPCost, coverageYear, includePlanExpensesFullYear, pagination, `StarRatingFilter?`, filters, `MailOrderPharmacy`), `CountyCodeModel` (zipcode, state, stateCode, city, lat/lng, countyCode, countyName), `PrescriptionInput` (rxcui, refillDuration, prescriptionCount, ndc), `PharmacyInput` (pharmacyNumber, pharmacyName, latitude, longitude). **Response types:** `PartDPlanRecommendationResponse` (response-level `ContractIdCarrierMap`, `PartDPremiumSurcharge`, `PartBPremiumSurcharge`, `MonthsUsedForExpenseCalc`, pagination, `RecommendationList`), `RecommendationListItem` (contractId, planId, segmentId, `PharmacyWiseRecommendations[]`), `PharmacyWiseRecommendation` (premium, deductible, totalPlanCost, totalPrescriptionCost, starRating, `PrescriptionDrugCovered`, `PartAandBBenefitServiceCost`, `UnavailableDrugs[]`, `PharmacyNetworks[]`), `PharmacyNetwork` (pharmacyNumber, pharmacyName, pharmacyNetworkType, distance) |

### MongoDB Documents (`Documents/`)

| File | Key Types |
|------|-----------|
| `Documents/UserDocument.cs` | `UserDocument` — merged User+Profile in single MongoDB document (userId, email, phone, passwordHash, isEmailVerified, firstName, lastName, coverageYear, healthCondition, taxFilingStatus, magiTier, gender, tobaccoStatus, dateOfBirth, concierge, conciergeAmount, lifeExpectancy, address fields, currentPrescriptionDocumentId, isProfileComplete, timestamps) |
| `Documents/ChatSessionDocument.cs` | Chat session messages + UI state |
| `Documents/LtcCurrentSelectionsDocument.cs` | `LtcCurrentSelectionsDocument` (userId, name, healthProfile, numberOfAdultDayHealthCareYears, numberOfHomeCareYears, numberOfNursingCareYears, ltcResultJson, createdAt, updatedAt) — one document per user in collection `ltcCurrentSelections` |
| `Documents/PrescriptionDocument.cs` | Named prescriptions with embedded drug list |
| `Documents/RecommendationDocument.cs` | `RecommendationDocument` (root — userId, name, status, profile snapshot, planSelections, drugList, pharmacy, mailOrderPharmacy, lastCostSnapshot, timestamps), `ProfileSnapshot` (full profile mirror: name, DOB, gender, zip, county, state, city, address, coverage year, health, LIS), `SelectedDrugDoc`, `SelectedPlanDoc` (+deductible, starRating, totalPrescriptionCost, totalPlanCost, prescriptionDrugCovered, unavailableDrugs[], planExpenses[]), `SelectedPharmacyDoc`, `MailOrderPharmacyDoc`, `CostSnapshotDoc` (+supplementPlanType, supplementPlanPremium, yearlyDetails[], evaluation), `YearlyDetailDoc` (20 per-year financial fields including planG/F/NPremium, totalABGD/ABFD/ABND/ABCD), `CostEvaluationDoc`, `LifetimeSummaryDoc`, `YearlyHighlightDoc`, `CostCategoryDoc`, `SavingsTipDoc`, `PlanExpenseDoc` |
| `Documents/UserAnalysisSelectionsDocument.cs` | `UserAnalysisSelectionsDocument` (userId, name, drugs `List<PrescriptionDrugDoc>`, activeSection, selectedPharmacies `List<UserAnalysisPharmacyDoc>`, selectedPlans `List<UserAnalysisPlanDoc>`, createdAt, updatedAt) — one logical row per user in collection `userAnalysisSelections`. `UserAnalysisPharmacyDoc` (pharmacyNumber, name, address, distance, zipcode). `UserAnalysisPlanDoc` (slot, planId, planName, contractId, medigapKey?, medigapPlanType?) |

### Exceptions (`Exceptions/AppExceptions.cs`)

| Exception | Status | Purpose |
|-----------|--------|---------|
| `AppException` | 500 (base) | Abstract base with `StatusCode` property |
| `NotFoundException` | 404 | Entity lookup fails |
| `ValidationException` | 400 | Supports message or error dictionary |
| `UnauthorizedException` | 401 | Auth-related denials |
| `ConflictException` | 409 | Duplicate resource conflicts |

### Interfaces (`Interfaces/`)
- Per-entity interfaces: `IProfileRepository` (GetByUserIdAsync, CreateAsync, UpdateAsync, ExistsByUserIdAsync — operates on `UserDocument`).
- `IUserRepository` — user-specific (GetByEmail, GetByPhone, GetById, Create, Update, EmailExists, PhoneExists — operates on `UserDocument`).
- `IDrugAiService` — AI drug analysis contract (`AnalyzePrescription`, `SuggestDrugNames`).
- `IMedicareCostService` — CMS Medicare cost lookup contract.
- `IRxNormService` — RxNorm normalization + interaction + NDC lookup contract. **Note: Interface exists but implementation was removed (`RxNorm/` is empty); NDC resolution handled by `FdaNdcService` directly.**
- `IFdaNdcService` — FDA NDC Directory package info contract (`GetPackageInfo`, `GetPackageInfoBatch`).
- `IPharmacyLookupService` — Financial Planner pharmacy lookup contract (`GetPharmaciesAsync`). Request/response models co-located: `PharmacyLookupRequest`, `PharmacyLookupResponse`, `PharmacyLookupEntry`.
- `IChatClient` — M.E.AI standard chat client interface. Single provider registered via `"AiProvider"` config switch (`"Anthropic"`, `"Gemini"`, or `"OpenAI"`).
- `ICountyLookupService` — ZIP-to-county lookup, county name, state code, county code list.
- `IConstantsService` — Financial Planner constants API (MAGI tiers, filing statuses, etc.).
- `IPlanScoringAiService` — AI plan scoring contract.
- `ICmsPlanDataService` — CMS open data plan lookups.
- `IIndividualMedicareService` — Financial Planner individualMedicareR5 API contract (`CalculateAsync`).
- `IFinancialPlannerDrugService` — Financial Planner drug search contract (`SearchBulkAsync`). Internally calls `SearchAndMatchAsync` per drug (search + match by displayName + detail), then AI pairwise interaction evaluation if >1 drug.
- `ICostEvaluationAiService` — AI cost evaluation contract. `EvaluateAsync()` takes `IndividualMedicareResponse` + plan/profile context + `supplementPlanType` + `supplementPlanPremium`, returns `CostEvaluation` with chart-ready insights, category breakdowns, savings tips, and trajectory.
- `ILtcEvaluationAiService` — AI LTC cost evaluation contract.
- `IEmailService` — Email delivery service contract.
- `IPrescriptionDocRepository` — MongoDB: save/get/list/delete prescription documents.
- `IChatSessionRepository` — MongoDB: chat session CRUD.
- `IRecommendationRepository` — MongoDB: recommendation CRUD including `GetAllByUserIdAsync` (sorted by CreatedAt descending), `ExistsByUserIdAsync`, `DeleteByUserIdAsync`.
- `IUserAnalysisSelectionsRepository` — MongoDB: per-user current analysis selections (`ReplaceCurrentForUserAsync`, `GetCurrentForUserAsync`, `UpdateDrugsAsync`, `UpdatePharmaciesAsync`, `UpdatePlansAsync`).
- `ILtcSelectionsRepository` — MongoDB: LTC care-type selections CRUD.
- `ILongTermCareService` — LTC cost projection contract (`GetProjectionAsync(request, userEmail, ct)`).
- `IMedicareAdvantagePlanService` — MA plan recommendation contract (`RecommendAsync(request, ct)`).
- `IMedigapPlanQuotesService` — Medigap plan quotes contract (`GetQuotesAsync(request, ct)`).
- `IPartDPlanRecommendationService` — Part D plan recommendation contract (`RecommendAsync(request, ct)`).
- `IPresentValueService` — Financial Planner expensesPresentValue API contract (`CalculateAsync`).

---

## Application Layer (`AI.MedicareAssistant.Application`)

### Application Services

The Application layer contains service orchestrators that coordinate domain interfaces and infrastructure calls:

### Other Application Services

| Service | Role |
|---------|------|
| `ProfileService` | Consolidated profile CRUD via `IProfileRepository`. Get/Save/Delete operations. Maps `UserDocument` ↔ `ProfileDto`. |
| `AuthService` | JWT auth logic — signup, signin, forgot/reset password |
| `PrescriptionService` | Saves and retrieves named prescriptions. Maps `SavePrescriptionRequest` → `PrescriptionDocument` + `PrescriptionDrugDoc[]` via `IPrescriptionDocRepository` (MongoDB). Returns `PrescriptionResponse` with `PrescriptionDrugDto[]`. Supports delete and get-by-ID (`GetByIdAsync`). |
| `CostProjectionService` | Orchestrates cost projections: loads user profile via `ProfileService`, resolves state code to full name, formats DOB, computes remaining months, builds `IndividualMedicareRequest`, calls Financial Planner API, then runs AI evaluation via `ICostEvaluationAiService`. After FP calculation, calls `IPresentValueService` to compute present value (discount rate 6%) from yearwise expenses. Method: `EvaluateCostsAsync()` (combined `CostProjectionResult` with Financial Planner data + AI evaluation + present value). Contains `StateCodeToName` dictionary for state code→name resolution. Populates `LifetimeTotals` with `TotalIrmaa` (combined B+D surcharges), `SupplementPlanType`, `SupplementPlanPremium`, `ConciergeIncluded`, and 12 plan-specific lifetime fields (ABGD/ABFD/ABND/ABCD × Expenses/Premium/Oop). PV call is non-fatal (try/catch with warning log). |
| `ChatIntentService` | AI-powered chat intent classification. Injects `IChatClient` (M.E.AI) and `ILogger`. Loads system prompt from `Prompts/system/chat-intent-system.txt` at construction time (`File.ReadAllText`). Defines 17 intent categories with parameter extraction rules for 11 profile fields plus `prescriptionName` and `analysisName`. Intents include `ACTION_SAVE_ANALYSIS`, `ACTION_RUN_ANALYSIS`, `NAVIGATE_SAVED_ANALYSES`. **Page-context injection:** appends `PageContextBuilder.Build(request.CurrentPage)` to the system prompt at call time before invoking the AI — providing route-specific disambiguation without modifying the prompt file. Sends user message via `IChatClient.GetResponseAsync()` for JSON classification. Parses response (strips markdown fences if present). Returns `ChatIntentResponse` with intent, optional params, and confirmation message. Fallback returns `UNKNOWN` intent on errors. |
| `ProfileExtractService` | AI-powered profile field extraction from natural language. Injects `IChatClient` and `ILogger`. Loads system prompt from `Prompts/system/profile-extract-system.txt`. Accepts message + missing fields list. Returns `ProfileExtractResponse` with extracted fields (dict) + conversational reply. Supports 13 profile fields (firstName, lastName, gender, dateOfBirth, tobacco, health, taxFiling, magi, coverageYear, zipCode, address, lifeExpectancy, concierge). |
| `DrugSelectionExtractService` | AI-powered drug formulation selection extraction from chat. Injects `IChatClient` and `ILogger`. Loads system prompt from `Prompts/system/drug-selection-system.txt`. Accepts message + available drugs summary. Returns `DrugSelectionExtractResponse` with drugName, type, dosageForm, strength, quantity, action (select/options/confirm_all/remove/edit), and reply. Fuzzy matches drug names, forms, and strengths. |
| `PharmacySelectionExtractService` | AI-powered pharmacy selection extraction from chat. Injects `IChatClient` and `ILogger`. Loads system prompt from `Prompts/system/pharmacy-selection-system.txt`. Accepts message + available/selected pharmacy summaries. Returns `PharmacySelectionExtractResponse` with pharmacyName, action (select/remove/list/search), searchTerm, and reply. Fuzzy matches pharmacy names. |
| `PlanSelectionExtractService` | AI-powered plan selection extraction from chat. Injects `IChatClient` and `ILogger`. Loads system prompt from `Prompts/system/plan-selection-system.txt`. Accepts message + available/selected plan summaries. Returns `PlanSelectionExtractResponse` with planName, planType, action (select/remove/switch_section), section, and reply. Matches plan names and types. |
| `RecommendationService` | CRUD for `RecommendationDocument` in MongoDB. Methods: `GetActiveAsync`, `GetByIdAsync`, `GetAllAsync`, `ExistsAsync`, `CreateAsync` (with optional `force` flag to replace), `UpdateProfileAsync`, `UpdateDrugsAsync`, `UpdatePharmacyAsync`, `UpdatePlansAsync`, `UpdateCostSnapshotAsync`, `DeleteAsync`. Sets `status`, `UserId`, timestamps on create. |
| `PageContextBuilder` | Static internal helper (`Application/Services/PageContextBuilder.cs`). `Build(string? currentPage)` returns a page-specific guidance block appended to AI system prompts at call time. Maps Angular route prefixes (`/medicare-analysis/drugs`, `/medicare-analysis/profile`, `/medicare-analysis/pharmacies`, `/medicare-analysis/plans`, `/medicare-analysis/cost-projections`, `/saved`, `/profile`) to disambiguation rules. Returns empty string for null/unknown routes. Consumed by `ChatIntentService`. Prompt files are never modified — all page logic is centralized here. |

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

| `FdaNdcService` | `IFdaNdcService` | openFDA NDC Directory | 7 days (per NDC) | Returns null on failure. Resolves package descriptions (size + type) for each NDC. |
| `FinancialPlannerPharmacyService` | `IPharmacyLookupService` | Financial Planner (`getPharmacies`) | None | Fetches paginated pharmacies by lat/lng from Financial Planner API. Basic auth token from config. Returns `PharmacyLookupResponse` with pharmacy name, number, address, distance, zipcode. 15s timeout. |
| `CmsPlanDataService` | `ICmsPlanDataService` | CMS SOCRATA (Phase 2) | 7 days per state+county | Returns empty list on failure |
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
| `MongoUserRepository` | User queries against MongoDB `users` collection. Implements `IUserRepository`. |
| `MongoProfileRepository` | Profile queries against MongoDB `users` collection. Implements `IProfileRepository`. Profile “create” is a `ReplaceOne` on the existing user document (sets `IsProfileComplete = true`). |
| `MongoDbContext` | MongoDB typed collection accessor. Provides `Users`, `Prescriptions`, `ChatSessions`, `UserAnalysisSelections`, `Recommendations`, `LtcCurrentSelections` collections. Creates compound indexes at startup (including unique Email, Phone, UserId on `users`). |
| `PrescriptionDocRepository` | MongoDB: prescription CRUD (save, list by user, get by ID, delete). |
| `ChatSessionRepository` | MongoDB: chat session persistence (get/upsert by userId). |
| `UserAnalysisSelectionsRepository` | MongoDB: per-user current analysis selections — drugs/pharmacies/plans partial updates. |
| `RecommendationRepository` | MongoDB: recommendation CRUD (get by user, get all by user sorted by CreatedAt desc, get by ID, create, replace, delete). Unique userId compound index. |
| `LtcSelectionsRepository` | MongoDB: LTC care-type selections (get/upsert by userId). |

### Utilities
- **PromptBuilder:** Loads and combines prompt files from `Prompts/` directory using a consistent 4-layer pipeline (system + task + schema + template). Five methods: `Build()` for drug analysis, `BuildDrugNameSuggestion()` for name identification, `BuildPlanScoring()` for plan recommendations, `BuildCostEvaluation()` for cost evaluation, `BuildLtcEvaluation()` for LTC cost evaluation. All return `(system, user)` tuples. Scoring, cost evaluation, and LTC evaluation methods accept a `Dictionary<string, string>` for placeholder substitution.

---

