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

- ✅ **Early Plans Summary Panel** — `hasAnyPlanSelected` computed signal in `PlanRecommendationComponent` shows `SelectedPlansSummaryComponent` as soon as any plan is selected (MA or Part D) — before the full selection is complete. `canCalculate` input disables the Calculate button and shows an amber hint (e.g., "Select a Part D gap plan below to calculate your total cost.") until `hasCompletePlanSelection()` is true.
- ✅ **Inline Recommendation Save from Plan Page** — `runLifetimeCostEvaluation(recommendationName?)` saves the full recommendation (profile + drugs + pharmacy + plans + cost) inline after `saveCurrentPlans` succeeds, before navigating to cost-projections. Eliminates the deferred `pendingCostRunRecommendationName` / `tryAutoSaveRecommendation` path for UI-triggered saves. Chat-triggered saves still use the deferred path.
- ✅ **Pre-Populated Save Dialog Name** — `SavePrescriptionDialogComponent` accepts `defaultName?: string` via `SavePrescriptionDialogData`. `PlanRecommendationComponent.buildDefaultRecommendationName()` reads the user's first name from `ProfileService` and formats today's date as `MM/DD/YYYY`, producing e.g. `John Medicare Advantage – 04/18/2026`. Falls back to plan-type-only if profile not loaded.
- ✅ **Serilog MongoDB Logging** — 3-tier sink hierarchy: MongoDB primary (BSON `logs` collection, 5-sec batch), console (dev), file (30-day rolling fallback). `appsettings.json` MinimumLevel config with ASP.NET/EFCore/HttpClient overrides
- ✅ **Silent Exception Catch Fixes** — `AuthService` bare catch → `LogWarning`; `ChatSessionRepository` added `ILogger`, `BsonSerializationException` logged with warning and userId
- ✅ **Part D Gap Fill for MA Plans** — `ensurePartDGapLoadForMA()` helper triggers Part D plan loading when MA plan is pre-populated from a saved recommendation
- ✅ **Cost Projections Navigation Guard** — Removed broken `isPageReload()` (PerformanceNavigationTiming bug); merged into single `hasCostProjection()` guard; removed dead `startNewAnalysis()`
- ✅ **Recommendation Detail Redesign** — Hero header with type badge, 6-card Medicare KPI strip, 5 Medicare tabs (Profile/Prescriptions/Pharmacy/Plans/Cost & Charts), 3 LTC tabs (Profile/Care Config/Cost Analysis)
- ✅ **Compare-LTC Redesign** — 4-tab comparison matching compare-medicare design: Overview (KPI strip + winner + profile diffs + care config), Profile, Care Config, Cost Analysis
- ✅ **Compare-Cross Redesign** — 3-tab cross-type comparison: Overview (disclaimer + KPI strip + winner + profile diffs), Profile, Cost Summary with side-by-side evaluation cards
- ✅ **Uppercase Recommendation Names** — All `rec.name` displays on saved data page rendered in uppercase CSS class
- ✅ **Chat-Based Recommendation Management** — Intent-classified NLU routing through `ChatIntentService` with 20 intents; recommendation lifecycle via `RecommendationService`
- ✅ **Recommendation Lifecycle** — Create / view / update / delete recommendation via conversational commands; profile, drugs, pharmacy, and plan snapshots stored in MongoDB `RecommendationDocument`
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

---

← [Chapter 8 — Feature Catalog](ch08-feature-catalog.md) | [Table of Contents](APPLICATION_BLUEPRINT.md) | [Chapter 10 → Testing Scenarios](ch10-testing-scenarios.md)
