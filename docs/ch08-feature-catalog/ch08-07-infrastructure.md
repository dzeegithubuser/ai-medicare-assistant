# Chapter 8.7 — Infrastructure & Miscellaneous Features

> MongoDB, logging, session lifecycle, reactivity fixes, disclaimers, and deprecated features.

← [Feature Catalog Index](../ch08-feature-catalog/ch08-feature-catalog.md)

---

## ✅ MongoDB (Single Database)
- **Driver:** `MongoDB.Driver` 3.4.0 with `MongoDB.Bson` 3.4.0.
- **User Document:** `UserDocument` merges user credentials and profile fields into a single document in the `users` collection. Unique indexes on `Email`, `Phone`, and `UserId`.
- **Collections:** `users`, `prescriptions`, `chat_sessions`, `userAnalysisSelections`, `recommendations`, `ltcCurrentSelections`, `logs`.
- **Repositories:** `MongoUserRepository`, `MongoProfileRepository` (both operate on `users` collection), plus `PrescriptionDocRepository`, `ChatSessionRepository`, `UserAnalysisSelectionsRepository`, `RecommendationRepository`, `LtcSelectionsRepository`.

---

## ✅ MongoDB Document Store
- **What:** MongoDB is used for document-oriented persistence — prescriptions, chat sessions, recommendations, analysis selections, FSM state, LTC selections, and structured application logs.
- **Driver:** `MongoDB.Driver` 3.4.0 (Infrastructure), `MongoDB.Bson` 3.4.0 (Domain for `[BsonId]` / `[BsonRepresentation]` attributes).
- **Collections:** `prescriptions`, `chatSessions`, `userAnalysisSelections`, `recommendations`, `ltcCurrentSelections`, `logs` (Serilog structured BSON logs).
- **Indexes:** Compound indexes on user/timestamp fields for efficient per-user retrieval. `MongoIndexInitializer` (hosted service) creates indexes at startup.
- **Architecture:** `MongoDbContext` includes typed collections for both prescriptions and chat sessions.
- **DI Registration:** `IMongoClient` + `IMongoDatabase` as singletons. `MongoDbContext` as singleton. Repository as scoped.

---

## ✅ Global Exception Handling & Structured Logging
- **Custom Exception Hierarchy:** `AppException` (abstract base), `NotFoundException` (404), `ValidationException` (400), `UnauthorizedException` (401), `ConflictException` (409).
- **GlobalExceptionMiddleware:** Maps exceptions to HTTP status codes. Logs 5xx as `Error`; 4xx as `Warning`. Returns `{ status, message, traceId, errors? }`.
- **Serilog (3-tier sink hierarchy):**
  1. **MongoDB (primary)** — `Serilog.Sinks.MongoDB` v6, structured BSON logs to `logs` collection, 5-second batch period.
  2. **Console** — development convenience.
  3. **File (fallback)** — daily rolling file (`Logs/log-.txt`), 30-day retention.
  - Bootstrap logger (console + file only) before DI host builds. `appsettings.json` → `Serilog:MinimumLevel` config (Default: Information, overrides: Microsoft.AspNetCore/EFCore/HttpClient → Warning).
  - `UseSerilogRequestLogging()` for automatic HTTP request logging.
- **Service-Level Logging:** All application + infrastructure services inject `ILogger<T>` with structured logging. 29 catch blocks pass the exception object (full stack trace preserved). Fixed silent catches: `AuthService` bare catch → `LogWarning`; `ChatSessionRepository` → added `ILogger`, `BsonSerializationException` logged with warning.

---

## ✅ Global API Error Popup (Frontend)
- **What:** When any API call fails, users see a Material Dialog popup with a user-friendly error message instead of silent failures or empty screens.
- **Components:**
  - `httpErrorInterceptor` (`interceptors/http-error.interceptor.ts`) — catches all `HttpErrorResponse`, maps HTTP status codes to human-readable messages, and triggers the popup.
  - `ErrorNotificationService` (`services/error-notification.service.ts`) — opens the dialog via `MatDialog`. Guards against stacking (only one popup at a time).
  - `ErrorDialogComponent` (`shared/error-dialog/error-dialog.component.ts`) — standalone Material Dialog with red error icon, friendly message, collapsible "Technical details" section (method, URL, status code), and themed "OK" button.
- **Status-Code Mapping:** `0` → network/offline, `401` → session expired, `403` → forbidden, `404` → not found, `408/504` → timeout, `429` → rate limit, `5xx` → server error.
- **Silent URLs:** Auth endpoints (`/api/auth/`) are excluded so login/signup errors are handled inline.
- **Error Propagation:** The error is re-thrown after showing the popup so component-level error handlers still execute.
- **Registration:** Interceptor chain: `httpLoaderInterceptor` → `authInterceptor` → `httpErrorInterceptor` (in `app.config.ts`).

---

## ✅ Signal-Based Confirmed Drugs (Reactivity Fix)
- **What:** `MedicareStateService.confirmedDrugs` changed from plain `Set<string>` to `signal(new Set<string>())` to fix Angular signal reactivity. Plain `Set.add/delete` mutations are invisible to `computed()` signals — the reference doesn't change, so dependents never recompute.
- **Fix:** `confirmDrug(name)` and `unconfirmDrug(name)` helper methods create a new `Set` reference on each call (copy + add/delete + set). `resetAll()` uses `confirmedDrugs.set(new Set())` instead of `.clear()`.
- **Impact:** Fixed the Continue button not enabling after drug selection (step 1 → step 2 gate uses `hasConfirmedDrugs()` computed signal). All callers updated — `DrugsStepComponent` reads via `confirmedDrugs()` (signal access), mutates via `state.confirmDrug()`/`state.unconfirmDrug()`.

---

## ✅ Prescription APIs (Legacy / Backend-Only)
- **What:** Prescription persistence endpoints remain in backend for compatibility, but are no longer part of current UI flows.
- **Current UX:** Main UI and chat flows do not expose save/load prescription actions. Saved Data page shows analyses only.
- **Backend:** `PrescriptionController` still exposes authorized `POST /api/prescription` and `GET /api/prescription` endpoints.

---

## ✅ Chat Session Lifecycle (Logout/New Login)
- **What:** Chat session persistence keeps historical conversation context while starting a fresh active session on each login.
- **Logout Behavior:** Frontend performs local sign-out cleanup only (no server-side session deletion).
- **Login Behavior:** After successful sign-in, frontend calls `POST /api/chat/session/start-new`. Backend archives previous active session content and resets active messages/UI state.
- **Next Dashboard Load:** `GET /api/chat/session` returns the new empty active session.

---

## ✅ Financial Disclaimers

- **What:** All cost projection and funding outputs include a disclaimer: "⚠️ These are estimates based on current CMS data and actuarial assumptions. Actual costs may vary. Consult a licensed financial advisor or Medicare counselor before making decisions."
- **Applied to:** `HandleViewProjections`, `HandleViewFunding`, and `FormatSummary` (cost snapshot section).

---

## ~~Orchestrator URL Guard~~ (Removed)

> **Note:** The chatbot orchestrator (`ChatOrchestratorService`, `ChatOrchestratorController`, `ConvStateService`, `DeltaCalculationService`) has been fully removed. Chat coordination is now handled by `ChatRouterService` with `ChatIntentService` (20 intents), page-specific extraction services, and `ChatNavigationFlowService`. This feature section is retained for historical reference only.

- **What:** The orchestrator handler now skips routing when the user is on wizard step pages, allowing page-specific drug/pharmacy/plan selection handlers to process messages correctly.
- **Problem:** `routeToOrchestrator()` had no URL guard and ran before all page-specific handlers. When a saved recommendation existed, _all_ messages were captured by the orchestrator, preventing chat-based pharmacy selection, drug selection, and plan selection from working.
- **Fix:** `routeToOrchestrator()` returns `false` when `router.url` starts with `/medicare-analysis/profile`, `/medicare-analysis/drugs`, `/medicare-analysis/pharmacies`, or `/medicare-analysis/plans`.

---

← [Feature Catalog Index](../ch08-feature-catalog/ch08-feature-catalog.md) | [← LTC](ch08-06-ltc.md)
