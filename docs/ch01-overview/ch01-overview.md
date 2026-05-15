# Chapter 1 — Overview

> Purpose, tech stack, and high-level architecture.

---

## Purpose

ChatGPT-style Medicare healthcare assistant where users paste prescription lists and receive structured drug metadata with clinical intelligence. Features a professional split-panel UI with a two-step AI-powered drug search engine (drug name verification followed by full analysis), drug-drug interaction detection, dosage validation, therapeutic alternative suggestions, RxNorm-verified normalization, on-demand nearby pharmacy search with AI-generated per-drug pricing comparison (NPI Registry + IChatClient AI pricing), on-demand Medicare plan recommendations, JWT-authenticated user sessions, first-time user profile onboarding (name, personal details, income, health, address), and zipcode-aware drug cost estimation.

---

## Stack

### Frontend
- **Angular 21** (standalone components, signal-based reactivity)
- **Angular Material 21** (M3 theming with 4 switchable themes: Navy & Gold, Lavender Calm, Teal Medical, AiVante Professional)
- **Tailwind CSS 4** (utility-first styling via PostCSS plugin)
- **RxJS** (HTTP observables for API communication)
- **@microsoft/signalr** (WebSocket client for real-time chat session sync)
- **TypeScript 5.9**

### Backend
- **.NET 10 Web API**
- **Clean Architecture** (4-layer: API → Application → Domain ← Infrastructure)
- **ASP.NET Core SignalR** (persistent WebSocket hub at `/hubs/chat` for chat session sync)
- **Microsoft.Extensions.AI** (IChatClient abstraction)
- **OpenAI GPT-4.1** integration (AI model, selectable via `"AiProvider"` config switch)
- **Anthropic Claude Sonnet 4** integration (AI model via `IChatClient`, selectable via config)
- **Google Gemini** integration (AI model via `IChatClient`, selectable via config)
- **MongoDB.Driver 3.4** (single database for all data — `users` for login/identity, `userProfiles` for personal/medical/address, financial planner groups, sessions, prescriptions, plans)
- **JWT Authentication** (sign in, forgot/reset/change/verify password — public sign-up removed; hub auth via `access_token` query param). Tokens issued through `IJwtTokenIssuer` carry `Role`, `mustChangePassword`, `fpgId?`, `fpId?`, `actingAs?` claims.
- **Role-based access control** — 4 roles (`admin`, `financial_planner_group`, `financial_planner`, `user`) with `[Authorize(Roles=…)]` enforcement plus service-layer ownership checks. See [Chapter 5 — Roles & Authorization](../ch05-data-and-auth/ch05-data-and-auth.md#roles--authorization) and [ADMIN_SETUP.md](../ADMIN_SETUP.md).
- **Impersonation** — FPs act as their end-users via short-lived 60-min JWT swap (`/api/impersonate` + `/api/impersonate/refresh`); every request under impersonation is logged with `ImpersonatedBy={fpId}` via Serilog enrichment middleware.
- **First-login password change** — `MustChangePasswordFilter` (server) + `mustChangePasswordGuard` (UI) gate every other action until the default password is replaced.
- **BCrypt** password hashing
- **Serilog** structured logging (MongoDB primary sink via Serilog.Sinks.MongoDB v6 + console + daily rolling file fallback)
- **Global Exception Handling** (middleware with custom exception hierarchy)

---

## Architecture

```
User
 ↓
Angular Router (lazy-loaded routes)
 ├── /signin, /forgot-password (public — sign-up removed)
 └── / → DashboardComponent (authGuard + mustChangePasswordGuard) — child routes:
      ├── /admin → AdminHomeComponent (roleGuard ['admin']) — list FPG-admin users + create new ones (auto-creates the underlying group)
      ├── /fpg → FpgHomeComponent (roleGuard ['financial_planner_group']) — CRUD FPs in group + read-only group views
      ├── /fp → FpHomeComponent (roleGuard ['financial_planner']) — list/create end-users, list/delete recommendations, "Continue as user" impersonation
      ├── /change-password → ChangePasswordComponent (forced for users with MustChangePassword=true)
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
     MedicareStateService (signal-based shared state)
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
      ├── DrugController [Authorize] (POST suggest-names)
      ├── PharmacyController [Authorize] (GET lookup)
      ├── PlanRecommendationController [Authorize] (POST evaluate-costs)
      ├── PrescriptionController [Authorize] (POST/PUT/GET current selections)
      ├── RecommendationController [Authorize] (MongoDB recommendation CRUD)
      ├── ChatSessionController [Authorize] (MongoDB chat sessions — HTTP fallback for ui-state)
      ├── ChatHub [Authorize] (SignalR /hubs/chat — real-time message sync + session push)
      ├── CountyLookupController (ZIP-based county lookup + MAGI tiers)
      ├── AuthController (signin, forgot/reset-password, verify-email, resend-verification, change-password)
      ├── ReferenceDataController (public — master data for forms)
      ├── ProfileController [Authorize] (consolidated GET/POST)
      ├── LongTermCareController [Authorize] (POST /api/long-term-care — LTC projection)
      ├── LtcSelectionsController [Authorize] (PUT/GET /api/ltc/current — LTC selections persistence)
      ├── MedicareAdvantagePlanController [Authorize] (POST /api/MedicareAdvantagePlan/recommend)
      ├── MedigapPlanController [Authorize] (POST /api/MedigapPlan/quotes)
      ├── PartDPlanController [Authorize] (POST /api/PartDPlan/recommend)
      ├── AdminController [Authorize(Roles=admin)] (FPG-admin user CRUD — `GET/POST /api/admin/fpg-admin-users`; legacy group endpoints retained for back-compat)
      ├── FinancialPlannerGroupController [Authorize(Roles=fpg)] (FP CRUD + read-only group views)
      ├── FinancialPlannerController [Authorize(Roles=fp)] (end-user create/list, recommendations grouped by user, delete)
      ├── ImpersonationController [Authorize(+Role=fp on POST)] (POST /api/impersonate, POST /api/impersonate/refresh)
      ├── MustChangePasswordFilter (global filter — blocks every action except /api/auth/change-password while flag set)
      └── ImpersonationLoggingMiddleware (Serilog ImpersonatedBy enrichment)

           ↓
     Application Layer
      ├── IAuthService / AuthService (sign-in, password flows, JWT issuance via IJwtTokenIssuer)
      ├── IJwtTokenIssuer / JwtTokenIssuer (single source of truth for token claims: Role, mustChangePassword, fpgId, fpId, actingAs)
      ├── IAdminService / AdminService (FPG-admin user list + create — auto-creates the underlying FinancialPlannerGroup; legacy group CRUD retained for back-compat)
      ├── IFinancialPlannerGroupService / FinancialPlannerGroupService (FP CRUD scoped to group)
      ├── IFinancialPlannerService / FinancialPlannerService (recommendations grouped by user, delete)
      ├── IEndUserService / EndUserService (creator supplies first/last/email/phone/password; phone normalized + uniqueness-checked)
      ├── IImpersonationService / ImpersonationService (60-min JWT issuance with mustChangePassword override)
      ├── ProfileService (consolidated profile CRUD)
      ├── PrescriptionService
      ├── CostProjectionService
      ├── RecommendationService
      ├── ChatSessionService (MongoDB chat session CRUD)
      ├── ChatIntentService (intent classifier)
      ├── ProfileExtractService
      ├── DrugSelectionExtractService
      ├── PharmacySelectionExtractService
      └── PlanSelectionExtractService
           ↓  uses interfaces from
     Domain Layer (models + interfaces)
      ├── Constants/UserRoles
      ├── Documents/UserDocument (login/identity: email, phone, passwordHash, role, FpgId, FpId, MustChangePassword, firstName/lastName), Documents/ProfileDocument (personal/medical/address — `userProfiles` collection), FinancialPlannerGroupDocument
      ├── IProfileRepository (operates on ProfileDocument), IUserRepository (operates on UserDocument; + GetByFpId, GetByFpgIdAndRole, GetEndUsersByFpg, DeleteAsync), IFinancialPlannerGroupRepository
      ├── IJwtTokenIssuer
      ├── IDrugAiService, IChatClient, IMedicareCostService
      ├── IFdaNdcService
      ├── ICmsPlanDataService, ICountyLookupService, IConstantsService
      ├── IIndividualMedicareService, ICostEvaluationAiService, ILtcEvaluationAiService
      ├── ILongTermCareService, IMedicareAdvantagePlanService, IMedigapPlanQuotesService, IPartDPlanRecommendationService
      ├── IEmailService, IPresentValueService
      ├── IMongoRepositories (Prescription, ChatSession, UserAnalysisSelections, Recommendation, LtcSelections)
           ↑  implements interfaces
     Infrastructure Layer
      ├── MongoProfileRepository (operates on `userProfiles` collection)
      ├── MongoUserRepository (operates on `users` collection), MongoFinancialPlannerGroupRepository
      ├── Data/UserProfileSplitMigrationInitializer (IHostedService — one-shot migration that splits legacy unified user docs into `users` + `userProfiles`; idempotent via `schemaMigrations` marker collection)
      ├── Data/AdminSeedInitializer (IHostedService — seeds admin@aivante.com when Seed:AdminPassword is set)
      ├── DrugAiService, AnthropicMeaiChatClient, GeminiChatClient
      ├── CmsMedicareCostService
      ├── FdaNdcService
      ├── PlanScoringAiService, CmsPlanDataService
      ├── CostEvaluationAiService, LtcEvaluationAiService, IndividualMedicareService
      ├── CountyLookupService, FinancialPlannerConstantsService
      ├── LongTermCareService, MedicareAdvantagePlanService, MedigapPlanQuotesService, PartDPlanRecommendationService
      ├── EmailService
      ├── MongoRepositories (prescriptions, chatSessions, userAnalysisSelections, recommendations, ltcCurrentSelections)
      ├── ChatSessionRepository, RecommendationRepository
      └── PromptBuilder
           ↓
     PromptBuilder (file-based prompt assembly)
           ↓
     OpenAI GPT-4.1 / Anthropic Claude Sonnet 4 / Google Gemini / CMS Medicare API / FDA NDC Directory / Financial Planner API
```

---

← [Table of Contents](../APPLICATION_BLUEPRINT.md) | [Chapter 2 → Frontend Architecture](../ch02-frontend-architecture/ch02-frontend-architecture.md)
