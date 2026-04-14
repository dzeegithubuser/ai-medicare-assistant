# Chapter 1 — Overview

> Purpose, tech stack, and high-level architecture.

---

## Purpose

ChatGPT-style Medicare healthcare assistant where users paste prescription lists and receive structured drug metadata with clinical intelligence. Features a professional split-panel UI with a two-step AI-powered drug search engine (drug name verification followed by full analysis), drug-drug interaction detection, dosage validation, therapeutic alternative suggestions, RxNorm-verified normalization, on-demand nearby pharmacy search with AI-generated per-drug pricing comparison (NPI Registry + IChatClient AI pricing), on-demand Medicare plan recommendations, JWT-authenticated user sessions, first-time user profile onboarding (name, personal details, income, health, address), and zipcode-aware drug cost estimation.

---

## Stack

### Frontend
- **Angular 21** (standalone components, signal-based reactivity)
- **Angular Material 21** (M3 theming with cyan primary palette)
- **Tailwind CSS 4** (utility-first styling via PostCSS plugin)
- **RxJS** (HTTP observables for API communication)
- **@microsoft/signalr** (WebSocket client for real-time chat session sync)
- **TypeScript 5.9**

### Backend
- **.NET 10 Web API**
- **Clean Architecture** (4-layer: API → Application → Domain ← Infrastructure)
- **ASP.NET Core SignalR** (persistent WebSocket hub at `/hubs/chat` for chat session sync)
- **Microsoft.Extensions.AI** (IChatClient abstraction)
- **OpenAI GPT-4.1** integration (primary AI model)
- **Anthropic Claude Sonnet 4** integration (secondary AI model via `IAiChatClient`)
- **Entity Framework Core 9** (Code First with MySQL via Pomelo)
- **JWT Authentication** (sign up, sign in, forgot/reset password; hub auth via `access_token` query param)
- **BCrypt** password hashing
- **Serilog** structured logging (MongoDB primary sink via Serilog.Sinks.MongoDB v6 + console + daily rolling file fallback)
- **Global Exception Handling** (middleware with custom exception hierarchy)

---

## Architecture

```
User
 ↓
Angular Router (lazy-loaded routes)
 ├── /signin, /signup, /forgot-password (public)
 └── / → DashboardComponent (authGuard) — child routes:
      ├── /profile → UserProfileComponent (onboarding or edit)
      ├── /medicare-analysis → AnalysisShellComponent (profileCompleteGuard) — 4-step shell (Profile → Drugs → Pharmacies → Plans) + cost projections route:
      │    ├── /medicare-analysis/profile → UserProfileComponent (same standalone component as `/profile`, shown inside the analysis stepper as step 1)
      │    ├── /medicare-analysis/drugs → DrugsStepComponent (Financial Planner drug search + formulation selection)
      │    ├── /medicare-analysis/pharmacies → PharmacyStepComponent (nearby pharmacy search + multi-select)
      │    ├── /medicare-analysis/plans → PlansStepComponent (Medicare plan recommendations + plan-aware pharmacies)
      │    └── /medicare-analysis/cost-projections → CostProjectionsComponent (lifetime cost dashboards)
      ├── /long-term-care → LtcShellComponent (profileCompleteGuard) — 3-step shell (Profile → Care Type → Projection)
      │    ├── /long-term-care/profile → UserProfileComponent (same standalone component as `/profile`)
      │    ├── /long-term-care/care-type → LtcCareTypeStepComponent (health profile + care-type year selection)
      │    └── /long-term-care/projection → LtcProjectionStepComponent (LTC projection results + per-care-type charts)
      └── Right Panel: Chat
           ↓
     DrugStateService (signal-based shared state)
      ├── Message mutations → ChatSignalRService.syncMessages() [WebSocket]
      └── Session hydration ← ChatHub.OnConnectedAsync() push [WebSocket]
           ↓
     DrugService (HttpClient + auth interceptor)
           ↓
     Step 1: POST /api/drug/suggest-names { input }
           ↓  (user confirms correct drug names)
     Step 2: POST /api/drug/analyze { prescription }
           ↓
     API Layer
      ├── DrugController
      ├── PharmacyController
      ├── PlanRecommendationController
      ├── PrescriptionController [Authorize]
      ├── RecommendationController [Authorize] (MongoDB recommendation CRUD)
      ├── ChatSessionController [Authorize] (MongoDB chat sessions — HTTP fallback for ui-state)
      ├── ChatHub [Authorize] (SignalR /hubs/chat — real-time message sync + session push)
      ├── ChatOrchestratorController [Authorize] (POST /api/chat/orchestrate — AI FSM chatbot)
      ├── CountyLookupController (ZIP-based county lookup + MAGI tiers + Google Places key)
      ├── AuthController
      ├── ReferenceDataController (public — master data for forms)
      ├── ProfileController [Authorize] (consolidated GET/POST)
      ├── LongTermCareController [Authorize] (POST /api/long-term-care — LTC projection)
      ├── LtcSelectionsController [Authorize] (PUT/GET /api/ltc/current — LTC selections persistence)
      ├── MedicareAdvantagePlanController [Authorize] (POST /api/MedicareAdvantagePlan/recommend)
      ├── MedigapPlanController [Authorize] (POST /api/MedigapPlan/quotes)
      ├── PartDPlanController [Authorize] (POST /api/PartDPlan/recommend)
      └── MigrationController [AllowAnonymous]
           ↓
     Application Layer
      ├── DrugAnalysisService
      ├── AuthService
      ├── ProfileService (consolidated profile CRUD)
      ├── PrescriptionService
      ├── MedicarePlanService
      ├── PlanPharmacyService
      ├── CostProjectionService
      ├── RecommendationService
      ├── ConvStateService (FSM state persistence)
      ├── ChatSessionService (MongoDB chat session CRUD)
      ├── OrchestratorIntentService (19-intent classifier)
      ├── ChatOrchestratorService (FSM router)
      └── DeltaCalculationService
           ↓  uses interfaces from
     Domain Layer (models + interfaces)
      ├── IRepository<T> (generic)
      ├── IProfileRepository
      ├── IDrugAiService, IChatClient, IMedicareCostService
      ├── IRxNormService, IPharmacyPricingService
      ├── IFdaNdcService
      ├── IMedicarePlanService, IPlanScoringAiService
      ├── ICmsPlanDataService, ICountyLookupService, IConstantsService
      ├── IPlanPharmacyService
      ├── IIndividualMedicareService, ICostEvaluationAiService
      ├── ILongTermCareService, IMedicareAdvantagePlanService, IMedigapPlanQuotesService, IPartDPlanRecommendationService
      ├── IMongoRepositories (Prescription, UserAnalysisSelections, Recommendation, ConvState, LtcSelections)
           ↑  implements interfaces
     Infrastructure Layer
      ├── Repository<T> (generic base)
      ├── ProfileRepository
      ├── UserRepository
      ├── DrugAiService, AnthropicMeaiChatClient
      ├── CmsMedicareCostService, CmsPharmacyPricingService
      ├── FdaNdcService, RxNormService
      ├── PlanScoringAiService, CmsPlanDataService
      ├── CostEvaluationAiService, IndividualMedicareService
      ├── PlanPharmacyService
      ├── CountyLookupService, FinancialPlannerConstantsService
      ├── LongTermCareService, MedicareAdvantagePlanService, MedigapPlanQuotesService, PartDPlanRecommendationService
      ├── MongoRepositories (prescriptions, chatSessions, userAnalysisSelections, recommendations, convStates, ltcCurrentSelections)
      └── PromptBuilder
           ↓
     PromptBuilder (file-based prompt assembly)
           ↓
     OpenAI GPT-4.1 / Anthropic Claude Sonnet 4 / CMS Medicare API / NPI Registry / RxNav NDC / FDA NDC Directory / Financial Planner API
```

---

← [Table of Contents](APPLICATION_BLUEPRINT.md) | [Chapter 2 → Frontend Architecture](ch02-frontend-architecture.md)
