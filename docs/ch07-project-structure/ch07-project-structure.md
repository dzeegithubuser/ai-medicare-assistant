# Chapter 7 — Project Structure

> Full directory tree for frontend and backend projects.

---

## Frontend (`ui-ai-medicare-assistant/src/`)

```
index.html                        → HTML shell with Google Fonts + Material Icons
main.ts                           → Bootstrap App with appConfig
styles.scss                       → Tailwind CSS import + theme color/font CSS custom properties (4 themes) + custom scrollbar styles
material-theme.scss               → Angular Material M3 theme (4 themes: Navy & Gold, Lavender Calm, Teal Medical, AiVante Professional) with per-theme color overrides + AiVante typography overrides
app/
  app.ts                          → Root component (router-outlet only)
  app.config.ts                   → Angular providers (router, httpClient + httpLoaderInterceptor + authInterceptor + httpErrorInterceptor)
  app.routes.ts                   → Lazy-loaded routes (signin, forgot-password, dashboard with role-aware children) — uses AppRoutes constants. Signup route removed. Dashboard parent runs authGuard + mustChangePasswordGuard; admin/fpg/fp children run roleGuard
  app-routes.const.ts              → Central route path registry (AppRoutes const with segment names + abs: { } absolute paths). All navigation and URL checks import from here — single place to rename any route. Includes ADMIN_HOME, FPG_HOME, FP_HOME for role landings
  models/
    drug.model.ts                 → Drug, Formulation, DrugAnalysisResponse, DrugNameSuggestionResult, DrugNameSuggestion, DrugCandidate, DrugPrice, PharmacyWithPricing, PlanPharmacySearchRequest, PlanCoverageInput, PharmacyLookupEntry, PharmacyLookupResponse interfaces
    auth.model.ts                 → Auth request/response, AuthUser (with role/fpgId/fpId/mustChangePassword), UserRole union ('admin'|'financial_planner_group'|'financial_planner'|'user'), ImpersonationResponse interface
    role-management.model.ts      → FpgSummary, UserSummary, FpSummary, EndUserSummary, RecommendationSummary, RecommendationByUser, plus Create/Update request types for admin / FPG / FP / end-user CRUD
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
    auth.service.ts               → Signal-based auth state (JWT token, user, currentRole signal, signIn/signOut). Impersonation methods: impersonate(targetUserId), refreshImpersonation(), exitImpersonation(), isImpersonating() signal-backed. Persists `auth_impersonation_expires` to sessionStorage so the banner+timer survive reload; gracefully restores FP creds when an expired impersonation is found at construction time
    admin.service.ts              → HTTP wrappers for /api/admin (list/create FPGs, create initial FPG-admin user)
    financial-planner-group.service.ts → HTTP wrappers for /api/financial-planner-group (group CRUD + read-only end-users / recommendations across the group)
    financial-planner.service.ts  → HTTP wrappers for /api/financial-planner (end-user create/list, recommendations grouped by user, delete)
    profile.service.ts            → Signal-based profile state orchestrator (load + save + updateState)
    county-lookup.service.ts      → ZIP-based county code lookup with caching + MAGI tiers (with response cache Map for dedup)
    reference-data.service.ts     → Signal-based master data service (fetches + caches /api/reference-data, with in-flight loading guard)
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
    chat-profile-edit-flow.service.ts → Chat-driven profile edit flow (collect fields, submit, with extraction cancellation guard)
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
    chart-builder.service.ts        → Centralized Chart.js registration and chart creation. Registers all controllers (Line, Bar, Doughnut), elements, scales, tooltip, legend, filler once. Components call buildChart(canvas, config) instead of manual Chart.register() + new Chart(). Used by CostProjections, LtcProjectionStep, RecDetailMedicare, RecDetailLtc, TabCostAnalysis
    session-storage.service.ts      → Typed sessionStorage wrapper with SESSION_KEYS registry. Methods: get<T>, getString, set, remove, removeMany, clear. Centralizes all 9+ session key names (AUTH_TOKEN, AUTH_USER, AUTH_TOKEN_TS, DRUG_STATE, CONFIRMED_DRUGS, CHAT_MESSAGES, FORMULATION_SEL, FP_DRUG_SEL, DRUG_QUANTITIES)
    font-size.service.ts            → User font size preference management
    theme.service.ts                → Theme management (4 themes: navy, lavender, teal, aivante) with signal-based switching + localStorage persistence
    error-notification.service.ts    → Opens ErrorDialogComponent via MatDialog for global API error popups (singleton, dedup guard)
    http-loader.service.ts          → Global HTTP loading state (signal-based — true when any HTTP request is in-flight)
  shared/
    auth-form-shell/
      auth-form-shell.component.ts → Shared auth form shell — reusable card layout with gradient background, icon, title, subtitle, form projection, and footer link. Used by Signin, Signup, ForgotPassword, ResetPassword, ChangePassword, VerifyEmail
    validators/
      password-match.validator.ts  → Shared cross-field password match validator (passwordMatchValidator). Used by Signup, ResetPassword, ChangePassword
    loading-spinner/
      loading-spinner.component.ts → Shared loading spinner with optional message text input. Replaces inline spinner markup across components
    empty-state/
      empty-state.component.ts    → Shared empty state card with icon, title, and subtitle inputs. Replaces inline empty-state markup
    error-alert/
      error-alert.component.ts    → Shared error alert banner with message input. Replaces inline error markup
    kpi-card/
      kpi-card.component.ts       → Shared KPI metric card with label, value, icon, and color inputs. Replaces inline KPI card markup
    section-header/
      section-header.component.ts → Shared section header with icon, title, and subtitle inputs. Replaces inline header markup
    error-dialog/
      error-dialog.component.ts   → Standalone Material Dialog for global API error popups (red icon, friendly message, collapsible technical details, themed OK button)
    confirm-delete-dialog/
      confirm-delete-dialog.component.ts/html → Standalone reusable type-to-confirm delete dialog. Accepts `{ title, subject, warning, confirmationToken, inputLabel?, confirmLabel? }` via `MAT_DIALOG_DATA`. The destructive button stays disabled until the user types the `confirmationToken` (case-insensitive, trimmed). Used by admin-home (delete FPG admin) and fp-home (delete end-user)
    impersonation-banner/
      impersonation-banner.component.ts → Amber banner shown above all dashboard routes while AuthService.isImpersonating() is true. "Exit impersonation" button restores FP creds + hard-reloads. Hosts the timer effect: schedules a warn dialog 5 min before expiry and an auto-exit at expiry, both reset when refresh updates the impersonationExpiresAt signal
    impersonation-continue-dialog/
      impersonation-continue-dialog.component.ts → Material dialog with live `m:ss` countdown. "Continue" → AuthService.refreshImpersonation(); "Exit" → AuthService.exitImpersonation()
    styles/
      _tab-active.scss            → Shared SCSS mixin for active mat-tab styling (cyan-600 background, white text/icon, rounded top corners). Used by compare-medicare, compare-ltc, compare-cross, rec-detail-medicare, rec-detail-ltc
      _chart-container.scss       → Shared SCSS mixin for chart container (position: relative, configurable height, default 320px). Used by cost-projections, ltc-projection-step, recommendation-detail, rec-detail-medicare, rec-detail-ltc
  interceptors/
    auth.interceptor.ts           → HttpInterceptorFn — attaches Bearer token to requests
    http-error.interceptor.ts     → HttpInterceptorFn — global API error handler (catches HttpErrorResponse, maps status codes to user-friendly messages, opens ErrorDialogComponent via ErrorNotificationService)
  guards/
    auth.guard.ts                 → CanActivateFn — redirects unauthenticated to /signin
    profile-complete.guard.ts     → CanActivateFn — protects /medicare-analysis, redirects to /profile
    dashboard-redirect.guard.ts   → CanActivateFn — role-driven landing (admin → /admin, FPG → /fpg, FP → /fp, user → /saved)
    must-change-password.guard.ts → CanActivateFn — redirects to /change-password while AuthUser.mustChangePassword is true (paired with server-side MustChangePasswordFilter)
    role.guard.ts                 → CanActivateFn factory `roleGuard(allowed: UserRole[])` — gates routes by role, redirects to / on mismatch
  auth/
    signin/
      signin.component.ts/html/scss   → Sign-in form (email + password). Signup link removed; the only paths into the app are admin-seeded → admin → FPG → FP → end-user
    forgot-password/
      forgot-password.component.ts/html/scss → Forgot password form (email)
    reset-password/
      reset-password.component.ts/html/scss  → Reset password form (reads ?token= from URL, 2 password fields)
    verify-email/
      verify-email.component.ts/html/scss    → Email verification page (reads token from URL)
    change-password/
      change-password.component.ts/html/scss → Change password form (old + new + confirm, [Authorize])
  dashboard/
    dashboard.component.ts        → Authenticated shell (header + split layout + initial post-login route handling + SignalR hydration guard). Imports ImpersonationBannerComponent
    dashboard.component.html      → Child router-outlet left panel + chat right panel. ImpersonationBanner mounted above main layout. "Recommendations" header button gated to `auth.currentRole() === 'user'`
    dashboard.component.scss      → Host styling + slideIn animation
  admin/
    admin-home.component.ts/html  → Admin landing (`/admin`). Lists FPG-admin users (welcome banner + search/sort/pagination card grid). "New FPG admin" opens a create dialog; per-card "Remove" opens the shared type-to-confirm delete dialog and calls `DELETE /api/admin/fpg-admin-users/{userId}`. Standalone, OnPush, signals
    create-admin-user-dialog.component.ts/html → MatDialog: creates an FPG admin via `POST /api/admin/fpg-admin-users`. No `groupId` injection — the group is hidden from the UI
  fpg/
    fpg-home.component.ts/html    → FPG landing (`/fpg`). Welcome banner + view-mode pills (Financial Planners / End-Users / Recommendations) + search/sort/pagination card grid. "Add planner" opens a dialog. Group end-users and group recommendations are lazy-loaded when their pill is selected
    create-fp-dialog.component.ts/html → MatDialog: creates a financial planner via `POST /api/financial-planner-group/me/financial-planners`
  fp/
    fp-home.component.ts/html     → FP landing (`/fp`). Welcome banner + filter chips (All / Has analyses / No analyses) + search/sort/pagination card grid. Each user card has "Continue as user" (auto-impersonate, lands on `/saved`), a red "Remove" that opens the shared type-to-confirm dialog and calls `DELETE /api/financial-planner/me/end-users/{userId}` (cascade), and an expandable recommendations list with per-rec delete. "New user" opens a dialog that creates the user, immediately impersonates them, and lands on `/saved`
    create-end-user-dialog.component.ts/html → MatDialog: creates an end-user via `POST /api/financial-planner/me/end-users` (FP supplies first/last/email/phone/password — same shape as admin/FPG/FP dialogs; `MustChangePassword=true`)
  user-profile/
    user-profile.component.ts     → Consolidated single-form profile (all fields in one form, with ZIP/MAGI subscription cancellation guards)
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
    plan-recommendation.component.ts/html → Plan recommendation shell (MA, Part D, Medigap plan slots + tabs) with PlanCardEnrichmentService integration (computed enrichment maps for all card types) + cancel-before-fire autosave guard
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
        compare-medicare.component.ts/html/scss → Medicare-vs-Medicare comparison shell (4-tab: Overview, Profile, Rx Pharmacy & Plans, Cost Analysis) with active-tab primary color styling
        compare-medicare-metrics.component.ts → Medicare KPI metrics cards (inline template — unified single grid via allMetrics() with orange/green labels)
        tab-overview/
          tab-overview.component.ts/html → Overview tab — 6 KPI delta cards, winner banner, profile diffs, Rx summary, pharmacy, plans, projections
        tab-cost-analysis/
          tab-cost-analysis.component.ts/html → Cost Analysis tab — Chart.js line + bar charts (orange/green series), year-by-year delta table, category comparison, assessment cards
        tab-rx-pharmacy-plans/
          tab-rx-pharmacy-plans.component.ts/html → Rx, Pharmacy & Plans tab — side-by-side prescription drug cards, pharmacy comparison cards, plan cards with star ratings
      ltc/
        compare-ltc.component.ts/html/scss      → LTC-vs-LTC comparison (4-tab: Overview, Profile, Care Config, Cost Analysis) with active-tab primary color styling
        compare-ltc-metrics.component.ts   → LTC KPI metrics cards (inline template — unified single grid via allMetrics() with orange/green labels)
      cross/
        compare-cross.component.ts/html/scss    → Medicare-vs-LTC cross-type comparison (3-tab: Overview, Profile, Cost Summary) with active-tab primary color styling
        compare-cross-metrics.component.ts → Cross-type KPI metrics cards (inline template — unified single grid via allMetrics(), dispatches to LTC/Medicare snapshot per side)
    detail/
      recommendation-detail.component.ts/html/scss → Full detail view of a single saved analysis (/saved/:id) — flat header (matching compare), KPI strip, grouped profile/details/cost tabs
      medicare/
        rec-detail-medicare.component.ts/html/scss → Medicare detail child — 3 tabs (Profile, Details, Cost & Charts) with active-tab primary color styling, Chart.js charts
      ltc/
        rec-detail-ltc.component.ts/html/scss     → LTC detail child — 2 tabs (Profile, Cost Analysis) with active-tab primary color styling, Chart.js charts
  constants/
    chat-messages.ts                  → Chat message constant strings
  pipes/
    markdown.pipe.ts                  → Markdown rendering pipe
  utils/
    pharmacy-chat-resolve.ts          → Pharmacy chat resolution utility

  ── Test Files (Vitest) ──
  services/
    auth.service.spec.ts              → AuthService unit tests (12 tests)
    drug-state.service.spec.ts        → MedicareStateService unit tests (22 tests)
  chat/
    chat-send-guards.spec.ts          → Chat send guard computed signal tests (5 tests)
    chat.component.spec.ts            → ChatComponent unit tests — creation, chatSendBlocked computed (12 tests)
  user-profile/
    user-profile.component.spec.ts    → UserProfileComponent unit tests — form validation, defaults, Medicare age, MAGI tiers, unsaved changes (23 tests)
  cost-projections/
    cost-projections.component.spec.ts → CostProjectionsComponent unit tests — creation, bundleLabel, expenseTableRow, presentValue, helpers (20 tests)
  recommendation/detail/medicare/
    rec-detail-medicare.component.spec.ts → RecDetailMedicareComponent unit tests — formatters, URL helpers, bundleLabel, computed props (23 tests)

environments/
  environment.ts                  → Production config
  environment.development.ts      → Dev config (apiUrl: localhost:5024)
```

---

## Backend (`api-ai-medicare-assistant/`)

```
AI.MedicareAssistant.Api/
  Program.cs                      → App builder, DI, CORS, JWT, MongoDB, Serilog, middleware pipeline (UseAuthentication → UseMiddleware<ImpersonationLoggingMiddleware> → UseAuthorization)
  Middleware/
    GlobalExceptionMiddleware.cs   → Global exception handler (AppException → HTTP status + JSON)
    ImpersonationLoggingMiddleware.cs → Pushes Serilog `ImpersonatedBy={fpUserId}` LogContext property when the principal carries an `actingAs` claim, so every log line emitted while impersonating records the FP's id
  Filters/
    MustChangePasswordFilter.cs   → Global IAsyncActionFilter (registered via AddControllers options). Throws UnauthorizedException on every authenticated action other than `/api/auth/change-password` while the principal's `mustChangePassword` claim is `"true"`
  Controllers/
    DrugController.cs             → Drug name suggestion REST endpoint
    PharmacyController.cs         → Pharmacy lookup (Financial Planner API)
    PlanRecommendationController.cs → Medicare plan cost evaluation
    PrescriptionController.cs     → [Authorize] Save + update current analysis selections (MongoDB)
    AuthController.cs             → [Public] sign in, forgot/reset password, verify email, resend verification. [Authorize] change-password (clears MustChangePassword + reissues JWT). Sign-up endpoint removed
    AdminController.cs            → [Authorize(Roles=admin)]. Primary: `GET/POST/DELETE api/admin/fpg-admin-users[/{userId}]` — direct FPG-admin user CRUD (backend auto-creates the underlying group; DELETE refuses with 409 if the group still has FPs). Legacy: `GET/POST api/admin/financial-planner-groups`, `POST api/admin/financial-planner-groups/{fpgId}/admin-user`
    FinancialPlannerGroupController.cs → [Authorize(Roles=financial_planner_group)] FP CRUD scoped to caller's `fpgId` claim + read-only group end-users / recommendations
    FinancialPlannerController.cs → [Authorize(Roles=financial_planner)] end-user list/create + cascade DELETE end-user (`DELETE /me/end-users/{endUserId}` wipes profile/chat/recs/analysis/LTC) + recommendations grouped by user + delete recommendation
    ImpersonationController.cs    → [Authorize] base; POST `/api/impersonate` adds Roles=financial_planner. POST `/api/impersonate/refresh` accepts the impersonation token (Role=user) and reads `actingAs` claim to reissue
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
  Constants/
    UserRoles.cs                  → Role string constants: Admin, FinancialPlannerGroup, FinancialPlanner, User. Used throughout (controllers, services, attributes)
  Documents/
    UserDocument.cs               → MongoDB: login / identity document — `users` collection (email, phone, passwordHash, isEmailVerified, mustChangePassword, firstName, lastName, **role**, **fpgId**, **fpId**, audit). `[BsonIgnoreExtraElements]` for transition safety
    ProfileDocument.cs            → MongoDB: personal / medical / address document — `userProfiles` collection (userId FK, coverageYear, healthCondition, taxFilingStatus, magiTier, gender, tobaccoStatus, dateOfBirth, lifeExpectancy, concierge fields, alternate contact, address, currentPrescriptionDocumentId, isProfileComplete, audit)
    FinancialPlannerGroupDocument.cs → MongoDB: tenant entity (groupId, name, description, audit) — collection `financialPlannerGroups`
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
    IMongoRepositories.cs         → MongoDB repository interfaces: IPrescriptionDocRepository, IChatSessionRepository (with `DeleteByUserIdAsync`), IUserAnalysisSelectionsRepository (with `DeleteByUserIdAsync` for cascade), IRecommendationRepository (with `DeleteByUserIdAsync` for cascade), ILtcSelectionsRepository (with `DeleteByUserIdAsync` for cascade)
    IPartDPlanRecommendationService.cs → Part D plan recommendation contract (RecommendAsync)
    IPharmacyLookupService.cs     → Financial Planner pharmacy lookup contract + request/response models
    IPlanScoringAiService.cs      → AI plan scoring + explanation contract
    IPresentValueService.cs       → Financial Planner expensesPresentValue API contract (CalculateAsync)
    IProfileRepositories.cs       → IProfileRepository interface (operates on ProfileDocument against the `userProfiles` collection). Includes `DeleteByUserIdAsync` for end-user cascade delete
    IUserRepository.cs            → User data access contract — login / identity (operates on UserDocument). Methods: GetByEmail/Phone/Id, Create, Update, Delete, EmailExists, PhoneExists, plus role-hierarchy queries GetByFpIdAsync, GetByFpgIdAndRoleAsync, GetEndUsersByFpgAsync (two-step join), GetAllByRoleAsync (admin-scope list-all-by-role)
    IFinancialPlannerGroupRepository.cs → CRUD contract for FinancialPlannerGroupDocument (GetById, GetAll, Create, Update, Delete, ExistsByName)
    IJwtTokenIssuer.cs            → Single point of truth for JWT issuance (used by sign-in, post-change-password reissue, and impersonation). `Issue(UserDocument, actingAs?, ttl?)` returns (Token, ExpiresAt)

AI.MedicareAssistant.Application/
  Interfaces/
    IAuthService.cs               → Sign-in, password-flows, change-password, verify/resend (sign-up removed)
    IAdminService.cs              → List/create FPGs, create initial FPG-admin user
    IFinancialPlannerGroupService.cs → FP CRUD + group end-user/recommendation read-only views
    IFinancialPlannerService.cs    → End-user list, recommendations grouped by user, delete recommendation
    IEndUserService.cs            → Create end-user with FP-supplied first/last/email/phone/password (phone normalized + uniqueness-checked)
    IImpersonationService.cs      → Issue 60-min impersonation token (with mustChangePassword override)
    IChatIntentClassifier.cs, IDrugSelectionExtractor.cs, IPharmacySelectionExtractor.cs, IPlanSelectionExtractor.cs, IProfileExtractor.cs (existing AI extractor contracts)
  DTOs/
    AuthDtos.cs                   → SignInRequest, ForgotPassword, ResetPassword, ChangePassword, VerifyEmail, ResendVerification, AuthResponse, UserDto (now includes Role/FpgId/FpId/MustChangePassword). SignUpRequest removed
    FinancialPlannerGroupDtos.cs  → FpgSummaryDto, CreateFpgRequest, CreateFpgAdminUserRequest, UserSummaryDto
    FinancialPlannerDtos.cs       → FpSummaryDto, CreateFpRequest, UpdateFpRequest
    EndUserDtos.cs                → EndUserSummaryDto, CreateEndUserRequest, RecommendationSummaryDto, RecommendationByUserDto
    ImpersonationDtos.cs          → ImpersonateRequest, ImpersonationResponse
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
    AuthService.cs                → IAuthService impl. Sign-in, password flows, change-password (clears MustChangePassword + reissues JWT). Delegates token issuance to IJwtTokenIssuer
    JwtTokenIssuer.cs             → IJwtTokenIssuer impl. Builds claims (NameIdentifier, Email, Role, mustChangePassword, Jti, optional fpgId/fpId/actingAs), signs with HMAC-SHA256, configurable lifetime
    AdminService.cs               → IAdminService impl. `ListFpgAdminUsersAsync` + `CreateFpgAdminUserAsync` (atomically auto-creates a `FinancialPlannerGroup` named `"{First} {Last}"` with numeric suffix on collision, fails after 50) + `DeleteFpgAdminUserAsync` (deletes the FPG admin and their auto-created group; throws `ConflictException` if the group still has FPs). Randomized 55501XXXXX dummy phone. Legacy `ListGroupsAsync` / `CreateGroupAsync` / `CreateGroupAdminUserAsync` retained for back-compat
    FinancialPlannerGroupService.cs → IFinancialPlannerGroupService impl. Verifies `target.FpgId == callerFpgId` on every mutation. DeleteFinancialPlannerAsync rejects with 409 if FP still has end-users (no orphaning). Group views go through IRecommendationRepository.GetByUserIdsAsync
    FinancialPlannerService.cs    → IFinancialPlannerService impl. Queries `WHERE FpId = caller.UserId`. DeleteRecommendationAsync verifies the rec's user belongs to the caller before deleting. **DeleteEndUserAsync** cascades through `IProfileRepository`, `IChatSessionRepository`, `IRecommendationRepository`, `IUserAnalysisSelectionsRepository`, `ILtcSelectionsRepository` (all expose `DeleteByUserIdAsync`) before deleting the user via `IUserRepository.DeleteAsync` — verifies caller owns the target first
    EndUserService.cs             → IEndUserService impl. Creates end-user with first/last/email/phone/password from `CreateEndUserRequest`. Phone normalized via `PhoneNormalizer.NormalizeUsPhone` + uniqueness-checked. Password BCrypt-hashed. MustChangePassword=true, FpId=caller.UserId
    ImpersonationService.cs       → IImpersonationService impl. Verifies caller is FP and target is one of their end-users, then clones the target user with `MustChangePassword=false` and issues a 60-min token via IJwtTokenIssuer with `actingAs = fpUserId`
    ChatIntentService.cs          → AI-powered chat intent classification (file-based prompt)
    ChatSessionService.cs         → Chat session persistence (get/update messages + ui-state)
    CostProjectionService.cs      → Orchestrates cost projections (profile → Financial Planner API → AI evaluation)
    DrugSelectionExtractService.cs → AI-powered drug formulation selection extraction from chat
    PageContextBuilder.cs         → Static helper — appends route-specific disambiguation to AI system prompts
    PharmacySelectionExtractService.cs → AI-powered pharmacy selection extraction from chat
    PlanSelectionExtractService.cs → AI-powered plan selection extraction from chat
    PrescriptionService.cs        → Prescription save/list logic (MongoDB)
    ProfileExtractService.cs      → AI-powered profile field extraction from natural language
    ProfileService.cs             → Consolidated profile CRUD (Get/Save/Delete). Depends on IUserRepository (for firstName/lastName on the login doc) and IProfileRepository (for everything else on ProfileDocument). Lazily creates ProfileDocument on first save
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
    MongoDbContext.cs             → MongoDB typed collection accessor (users, userProfiles, financialPlannerGroups, prescriptions, chatSessions, userAnalysisSelections, recommendations, ltcCurrentSelections + indexes including unique UserId on userProfiles, unique sparse UserId on chatSessions, unique GroupId/Name on financialPlannerGroups, non-unique FpId/FpgId/Role on users). Also exposes `UsersRaw` (BsonDocument view) used by the split migration. `MongoIndexInitializer` runs two cleanup helpers before creating indexes: `DropLegacyPascalCaseIndexesAsync` (drops any index whose name starts with an uppercase letter — stale from before the camelCase convention was registered) and `DropOptionDriftedIndexesAsync` (drops indexes whose options have drifted; currently handles `chatSessions.userId_1` which used to be non-sparse)
    UserProfileSplitMigrationInitializer.cs → IHostedService. One-shot split migration. Scans legacy unified `users` documents, upserts profile fields into a `userProfiles` doc, then `$unset`s them on `users`. Idempotent — records completion in the `schemaMigrations` collection. Registered **before** MongoIndexInitializer so the unique-UserId index on `userProfiles` only sees post-split data
    AdminSeedInitializer.cs       → IHostedService. Seeds the singleton admin user on startup if `Seed:AdminPassword` config is set; otherwise skips silently (production-safe by default). Email and phone are configurable via `Seed:AdminEmail` (default `admin@aivante.com`) and `Seed:AdminPhone` (default `5550199999`); in docker these map to `ADMIN_EMAIL` / `ADMIN_PHONE` / `ADMIN_PASSWORD` env vars. Seeds with MustChangePassword=true so the admin must reset on first sign-in. `DefaultAdminEmail` / `DefaultAdminPhone` constants exposed for tests
  Repositories/
    ChatSessionRepository.cs      → MongoDB: chat session CRUD (IChatSessionRepository)
    MongoRepositories.cs          → MongoDB repository implementations (prescriptions, userAnalysisSelections, ltcSelections)
    MongoProfileRepository.cs     → MongoDB: profile CRUD on `userProfiles` collection (IProfileRepository). UpdateAsync is an upsert so ProfileDocument is created lazily on first save
    MongoUserRepository.cs        → MongoDB: login / identity CRUD on `users` collection (IUserRepository) — includes role-hierarchy queries (GetByFpIdAsync, GetByFpgIdAndRoleAsync, GetEndUsersByFpgAsync, GetAllByRoleAsync) and DeleteAsync
    MongoFinancialPlannerGroupRepository.cs → MongoDB: FPG CRUD on `financialPlannerGroups` collection (IFinancialPlannerGroupRepository)
    RecommendationRepository.cs   → MongoDB: recommendation CRUD (get/create/replace/delete) + unique userId index. Adds GetByIdAsync(string id) (no owner filter, used by FP delete-with-verification flow), GetByUserIdsAsync (for group views), DeleteByIdAsync
```

---

← [Chapter 6 — API Contract](../ch06-api-contract/ch06-api-contract.md) | [Table of Contents](../APPLICATION_BLUEPRINT.md) | [Chapter 8 → Feature Catalog](../ch08-feature-catalog/ch08-feature-catalog.md)
