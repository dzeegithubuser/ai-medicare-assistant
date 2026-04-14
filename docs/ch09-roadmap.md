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

- ✅ **Serilog MongoDB Logging** — 3-tier sink hierarchy: MongoDB primary (BSON `logs` collection, 5-sec batch), console (dev), file (30-day rolling fallback). `appsettings.json` MinimumLevel config with ASP.NET/EFCore/HttpClient overrides
- ✅ **Silent Exception Catch Fixes** — `AuthService` bare catch → `LogWarning`; `ChatSessionRepository` added `ILogger`, `BsonSerializationException` logged with warning and userId
- ✅ **Part D Gap Fill for MA Plans** — `ensurePartDGapLoadForMA()` helper triggers Part D plan loading when MA plan is pre-populated from a saved recommendation
- ✅ **Cost Projections Navigation Guard** — Removed broken `isPageReload()` (PerformanceNavigationTiming bug); merged into single `hasCostProjection()` guard; removed dead `startNewAnalysis()`
- ✅ **Recommendation Detail Redesign** — Hero header with type badge, 6-card Medicare KPI strip, 5 Medicare tabs (Profile/Prescriptions/Pharmacy/Plans/Cost & Charts), 3 LTC tabs (Profile/Care Config/Cost Analysis)
- ✅ **Compare-LTC Redesign** — 4-tab comparison matching compare-medicare design: Overview (KPI strip + winner + profile diffs + care config), Profile, Care Config, Cost Analysis
- ✅ **Compare-Cross Redesign** — 3-tab cross-type comparison: Overview (disclaimer + KPI strip + winner + profile diffs), Profile, Cost Summary with side-by-side evaluation cards
- ✅ **Uppercase Recommendation Names** — All `rec.name` displays on saved data page rendered in uppercase CSS class
- ✅ **Chatbot Orchestrator & FSM** — 19-intent NLU classifier driving a 10-state finite state machine; MongoDB-backed conversation state with 30-minute TTL
- ✅ **Recommendation Lifecycle** — Create / view / update / delete recommendation via conversational commands; profile, drugs, pharmacy, and plan snapshots stored in MongoDB `RecommendationDocument`
- ✅ **What-If Delta Engine** — `DeltaCalculationService` computes before/after cost impact (lifetime, this-year, present-value) for any proposed change; inline `DeltaDisplayComponent` in chat
- ✅ **Interactive Help Menu** — 5-category chip menu (`HelpMenuComponent`) surfaces available orchestrator actions when user types "help"
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
