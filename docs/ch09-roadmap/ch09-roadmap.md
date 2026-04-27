# Chapter 9 — Roadmap

> Future enhancements and planned capabilities.

---

## Planned Features

- Multi-Agent AI System (separate Drug Normalizer, Cost Estimator, Interaction Analyzer agents)
- User Preferences & Personalization Engine (brand preference, cost priority)
- Cost Comparison Charts (monthly vs annual, pharmacy-level, plan-level)
- Savings Dashboard (total savings, optimized plan recommendation)
- Export / Share Plan (PDF export, doctor share summary, email report)
- Vector search for drugs (semantic similarity)
- Dark mode toggle
- Responsive mobile layout
- Compare selected drugs side-by-side
- GoodRx API integration (real-time retail pricing for non-Medicare users)
- Pharmacy hours & GPS distance (Geocoding API for distance from user)

## Recently Completed

- ✅ **Global API Error Popup** — `httpErrorInterceptor` catches all `HttpErrorResponse` errors and opens a Material Dialog popup (`ErrorDialogComponent`) with user-friendly messages mapped from HTTP status codes, collapsible technical details, and dedup guard (one popup at a time). Auth URLs excluded. Replaces silent failures across all API flows.
- ✅ **Backend Logging & Exception Handling Hardening** — 24 files updated: safe `GetUserId()` in 7 controllers, `ILogger<T>` in services/repositories/hubs, try/catch in 3 AI services, HTTP response logging in 7 infrastructure services, error handling in `PromptBuilder`.
- ✅ **Early Plans Summary Panel** — `hasAnyPlanSelected` computed signal in `PlanRecommendationComponent` shows `SelectedPlansSummaryComponent` as soon as any plan is selected (MA or Part D) — before the full selection is complete. `canCalculate` input disables the Calculate button and shows an amber hint (e.g., "Select a Part D gap plan below to calculate your total cost.") until `hasCompletePlanSelection()` is true.
- ✅ **Inline Recommendation Save from Plan Page** — `runLifetimeCostEvaluation(recommendationName?)` saves the full recommendation (profile + drugs + pharmacies + plans + cost) inline after `saveCurrentPlans` succeeds, before navigating to cost-projections. Eliminates the deferred `pendingCostRunRecommendationName` / `tryAutoSaveRecommendation` path for UI-triggered saves. Chat-triggered saves still use the deferred path.
- ✅ **Pre-Populated Save Dialog Name** — `SavePrescriptionDialogComponent` accepts `defaultName?: string` via `SavePrescriptionDialogData`. `PlanRecommendationComponent.buildDefaultRecommendationName()` reads the user's first name from `ProfileService` and formats today's date as `MM/DD/YYYY`, producing e.g. `John Medicare Advantage – 04/18/2026`. Falls back to plan-type-only if profile not loaded.
- ✅ **Serilog MongoDB Logging** — 3-tier sink hierarchy: MongoDB primary (BSON `logs` collection, 5-sec batch), console (dev), file (30-day rolling fallback). `appsettings.json` MinimumLevel config with ASP.NET/HttpClient overrides
- ✅ **Silent Exception Catch Fixes** — `AuthService` bare catch → `LogWarning`; `ChatSessionRepository` added `ILogger`, `BsonSerializationException` logged with warning and userId
- ✅ **Part D Gap Fill for MA Plans** — `ensurePartDGapLoadForMA()` helper triggers Part D plan loading when MA plan is pre-populated from a saved recommendation
- ✅ **Cost Projections Navigation Guard** — Removed broken `isPageReload()` (PerformanceNavigationTiming bug); merged into single `hasCostProjection()` guard; removed dead `startNewAnalysis()`
- ✅ **Recommendation Detail Redesign** — Flat header matching compare page (back button + name + type badge + save date), 6-card Medicare KPI strip, 3 Medicare tabs (Profile/Details/Cost & Charts), 2 LTC tabs (Profile/Cost Analysis)
- ✅ **Compare-LTC Redesign** — 4-tab comparison matching compare-medicare design: Overview (KPI strip + winner + profile diffs + care config), Profile, Care Config, Cost Analysis
- ✅ **Compare-Cross Redesign** — 3-tab cross-type comparison: Overview (disclaimer + KPI strip + winner + profile diffs), Profile, Cost Summary with side-by-side evaluation cards
- ✅ **Uppercase Recommendation Names** — All `rec.name` displays on saved data page rendered in uppercase CSS class
- ✅ **Illustration Aliasing (A/B Labels)** — Left recommendation aliased as "Illustration A" (orange), right as "Illustration B" (green) across all compare views. Centralized via `LABEL_A` / `LABEL_B` constants in `compare-helpers.ts`. Header card shows recommendation name as primary title with alias below. All column headers, section labels, KPI sub-labels, and chart legends use aliases instead of raw names.
- ✅ **Detail Component Active Tab Styling** — SCSS files added to `RecDetailMedicareComponent` and `RecDetailLtcComponent` with same active-tab primary color (`--color-cyan-600`) pattern as compare shells.
- ✅ **Themed Background on Detail & Compare Shells** — Both `recommendation-detail.component.html` and `recommendation-compare.component.html` switched from `bg-slate-50` to `bg-[var(--app-bg)]` for theme-responsive page background.
- ✅ **Elderly-Accessible Color Coding** — Orange (`#c2410c`) / Green (`#15803d`) palette for Illustration A/B chosen for WCAG AA contrast (6:1+). Chart.js series colors centralized via `CHART_COLOR_A/B` constants. Replaced previous indigo/cyan scheme across all compare templates and inline metrics components.
- ✅ **Extracted Metrics Components** — Per-type KPI metrics cards (`CompareMedicareMetricsComponent`, `CompareLtcMetricsComponent`, `CompareCrossMetricsComponent`) extracted above compare tabs. Unified single-grid template pattern via `allMetrics()` computed signal (merged cost + profile metrics into one `grid-cols-2 md:grid-cols-3` grid) with orange/green sub-labels.
- ✅ **Medicare Tab Sub-Components** — 4 standalone tab components extracted from monolithic compare-medicare: `TabOverviewComponent`, `TabCostAnalysisComponent`, `TabRxPharmacyPlansComponent` (merged Rx + Pharmacy + Plans), `TabProfileComponent` (shared across all modes).
- ✅ **Compare Active Tab Styling** — SCSS files added to all three compare shell components (`compare-medicare.component.scss`, `compare-ltc.component.scss`, `compare-cross.component.scss`). Active/selected tab uses primary color (`--color-cyan-600`) background with white text/icon and rounded top corners.
- ✅ **Compare Table Column Alignment** — All comparison tables normalized to `table-fixed` layout with consistent percentage-based column widths (Metric 20% / A 30% / B 30% / Diff 20% for cost tables; Icon 5% / Field 15% / A 33-40% / B 33-40% for profile tables) so columns align vertically across sections.
- ✅ **Saved Recommendations Page Redesign** — Two-column card bottom section (stats + plan chips on left, stacked action buttons on right), themed background via `bg-[var(--app-bg)]`, equal-height cards via `items-stretch` + `flex-col`, pagination with 6/12/24 page sizes.
- ✅ **Cross Metrics Cleanup** — Removed Coverage Years from `CompareCrossMetricsComponent` (retained Projection Years in LTC metrics).
- ✅ **Chat-Based Recommendation Management** — Intent-classified NLU routing through `ChatIntentService` with 20 intents; recommendation lifecycle via `RecommendationService`
- ✅ **Recommendation Lifecycle** — Create / view / update / delete recommendation via conversational commands; profile, drugs, pharmacies, and plan snapshots stored in MongoDB `RecommendationDocument`
- ✅ **Cost Projection Snapshots** — Plan-specific lifetime cost data with present value computation for comparison
- ✅ **Chat Help System** — Intent-based help responses surfaced when user types "help"
- ✅ **Markdown Rendering** — `MarkdownPipe` (via `marked` + DomSanitizer) renders rich-text assistant messages with tables, lists, and headings
- ✅ **Financial Disclaimers** — Mandatory disclaimer appended to projection, funding, and summary outputs
- ✅ **Orchestrator Test Suite** — 18 unit tests covering all handler paths, FSM transitions, and edge cases (122 total tests passing)
- ✅ Lifetime Medicare Cost Projections (Financial Planner individualMedicareR5 API integration)
- ✅ Multi-Pharmacy Selection (up to 5) with toggle selection UI
- ✅ Per-Pharmacy Cost Breakdown in plan recommendations (preferred pharmacy discount)
- ✅ Lightweight nearby pharmacy lookup (NPI-only, no pricing)
- ✅ 4-Step Medicare Flow (profile → drugs → pharmacies → plan recommendation)
- ✅ Plan Card Toggle UI ("Plan Features" and "Cost Breakup" collapsible sections)
- ✅ Plan Optimization Engine (pharmacy-aware cost sorting + preferred discount)
- ✅ **UI Refactoring Phase 1 — Shared Presentation Components** — Extracted 5 reusable components (`LoadingSpinnerComponent`, `EmptyStateComponent`, `ErrorAlertComponent`, `KpiCardComponent`, `SectionHeaderComponent`) into `shared/` library. 16+ template replacements across components eliminated duplicated inline markup.
- ✅ **UI Refactoring Phase 2 — ChartBuilderService** — Centralized Chart.js 4.x registration into a single `ChartBuilderService`. Removed 5× duplicated `Chart.register()` blocks from `CostProjectionsComponent`, `LtcProjectionStepComponent`, `RecDetailMedicareComponent`, `RecDetailLtcComponent`, and `TabCostAnalysisComponent`. Components now call `chartBuilder.buildChart(canvas, config)`.
- ✅ **UI Refactoring Phase 4 — Auth Form Consolidation** — Created `AuthFormShellComponent` (shared card shell with gradient background, icon, title, form projection, footer link) and `passwordMatchValidator` (shared cross-field validator). All 6 auth components (Signin, Signup, ForgotPassword, ResetPassword, ChangePassword, VerifyEmail) refactored to use the shared shell.
- ✅ **UI Refactoring Phase 5 — SessionStorageService** — Created `SessionStorageService` with typed `get<T>/getString/set/remove/removeMany/clear` methods and `SESSION_KEYS` constant registry centralizing all 9+ session key names.
- ✅ **UI Refactoring Phase 6 — Shared SCSS Partials** — Created `_tab-active.scss` mixin (used by 5 components) and `_chart-container.scss` mixin (used by 5 components). Removed 2 inline `style="height: 350px"` attributes. 8 SCSS files updated to use `@use` + `@include`.
- ✅ **Component Test Coverage** — Added 78 new Vitest component tests across 4 spec files: `ChatComponent` (12 tests), `UserProfileComponent` (23 tests), `CostProjectionsComponent` (20 tests), `RecDetailMedicareComponent` (23 tests). Total frontend tests: 117 passing across 7 test files.

---

← [Chapter 8 — Feature Catalog](../ch08-feature-catalog/ch08-feature-catalog.md) | [Table of Contents](../APPLICATION_BLUEPRINT.md) | [Chapter 10 → Testing Scenarios](../ch10-testing-scenarios/ch10-testing-scenarios.md)
