# Chapter 2.8 — Configuration, Styling & UI Flow

> App configuration, route definitions, environment settings, styling strategy, and end-to-end user flow.

← [Chapter 2 — Frontend Architecture (Index)](../ch02-frontend-architecture/ch02-frontend-architecture.md)

---

## Configuration

### `appConfig` (`app.config.ts`)
- Providers: `provideRouter(routes)`, `provideHttpClient(withInterceptors([httpLoaderInterceptor, authInterceptor, httpErrorInterceptor]))`, `provideBrowserGlobalErrorListeners()`, `provideAnimationsAsync()`.
- **Interceptor order:** `httpLoaderInterceptor` (loading spinner) → `authInterceptor` (JWT token) → `httpErrorInterceptor` (global error popup). Loader runs first so `finalize()` clears the spinner before the error dialog appears.
- SignalR client (`@microsoft/signalr`) is a plain npm package — no Angular provider registration needed. `ChatSignalRService` is `providedIn: 'root'` and manages its own `HubConnection` lifecycle.

### Routes (`app.routes.ts`)
- `/signin` → `SigninComponent` (lazy)
- `/signup` → `SignupComponent` (lazy)
- `/forgot-password` → `ForgotPasswordComponent` (lazy)
- `/` → `DashboardComponent` (authGuard) — child routes:
  - `''` → `dashboardRedirectGuard` auto-redirect
  - `profile` → `UserProfileComponent`
  - `medicare-analysis` → `AnalysisShellComponent` (profileCompleteGuard) — child routes:
    - `''` → redirects to `profile`
    - `profile` → `UserProfileComponent`
    - `drugs` → `DrugsStepComponent`
    - `pharmacies` → `PharmacyStepComponent`
    - `plans` → `PlansStepComponent`
    - `cost-projections` → `CostProjectionsComponent`
  - `long-term-care` → `LtcShellComponent` (profileCompleteGuard) — child routes:
    - `''` → redirects to `care-type`
    - `profile` → `UserProfileComponent`
    - `care-type` → `LtcCareTypeStepComponent`
    - `projection` → `LtcProjectionStepComponent`
  - `saved` → `RecommendationComponent`
  - `saved/compare` → `RecommendationCompareComponent`
  - `saved/:id` → `RecommendationDetailComponent`
- `**` → redirects to `/`

### Route Constants (`app-routes.const.ts`)
A centralized `AppRoutes` constant prevents hard-coded path strings across components and services. Includes relative path segments (e.g. `AppRoutes.LTC_CARE_TYPE = 'care-type'`) and absolute paths (e.g. `AppRoutes.abs.LTC_CARE_TYPE = '/long-term-care/care-type'`) for use in `router.navigate()` calls and `router.url` checks.

### Environments
- `environment.ts` / `environment.development.ts`: `apiUrl`, `appName`.

---

## Styling Strategy

- **Material Theme:** M3 theming via `mat.theme()` — cyan primary, orange tertiary, Roboto font.
- **Tailwind CSS 4:** Imported via `@import "tailwindcss"` in `styles.scss`, processed by `@tailwindcss/postcss`.
- **Custom Scrollbars:** Thin 6px scrollbars with subtle gray thumb.
- **Body:** `overflow: hidden` — each panel scrolls independently.
- **Component Hosts:** `:host { display: block/flex; height: 100% }` for proper flex layout.

---

## UI Flow

1. **Unauthenticated Access:** User lands on `/signin`. Can navigate to `/signup` or `/forgot-password`.
2. **Sign In / Sign Up:** JWT token + user info stored in sessionStorage (session ends on tab close). 1-hour expiry with auto-refresh on activity. Redirected to `/` (Dashboard).
3. **Post-Login Landing (All Users):** Dashboard loads profile and the default dashboard route redirects to `/profile` for both new and returning users. Left panel shows `UserProfileComponent` with consolidated single form (name, personal details, tax, concierge, planning, contact, address).
4. **Continue to Analysis:** From `/profile`, saving (or using existing completed data) navigates to `/medicare-analysis` (entry redirects to **`/medicare-analysis/profile`** — Profile is step 1 of the analysis shell). The guided chat wizard still routes users to **`/medicare-analysis/drugs`** when the chat `PROFILE` step is satisfied and drugs are needed, so deep-links and chat navigation may open Drugs directly without visiting the shell Profile step first. Edit Profile from the header also navigates to `/profile` in edit mode.
5. **Chat Startup:** Right chat adapts to profile status:
   - **Profile incomplete:** prompts user to complete profile first before analysis.
   - **Profile complete:** confirms profile is shown in view mode and asks whether user wants to modify anything.
   Mode selection cards ("Medicare Analysis", "Long Term Analysis") still appear only once profile API resolves and `isProfileComplete()` is true.
6. **Guided Wizard (Medicare Analysis):** User clicks "Medicare Analysis" → wizard starts immediately with fresh flow state (no saved-analysis/prescription chooser). The flow walks through steps: Profile → Drugs & Pharmacy → Plans → Analysis. Each step is announced via assistant message with auto-navigation. Steps auto-advance when completion signals fire (e.g., profile saved, drugs confirmed, pharmacies selected).
7. **Hard Refresh Resume:** Hard refresh on `/medicare-analysis/*` keeps the user on that same route (when profile is complete) and preserves in-progress state via persisted signals/session. Chat avoids startup mode prompts on analysis deep-links and shows a resume-aware message for the current step.
7. **Free-form Chat:** User can type naturally at any time. AI classifies intent (via `POST /api/chat/intent`) and routes to navigation, actions (reset, save, sign out), or drug analysis. Falls back to drug name suggestion flow for unrecognized input.
8. **Prescription (Profile Incomplete):** Chat shows assistant message and navigates to `/profile`.
9. **Prescription (Profile Complete):** Cyan bubble in chat. Spinner. Backend retrieves zipcode from user's saved address.
8. **Valid Drugs Response:** Drug cards render. Clinical alerts panel above cards. Chat summarizes findings.
9. **No Valid Drugs:** Chat shows message as assistant bubble. No drug cards rendered.
10. **User Configures Drug:** Selects Brand/Generic → Dosage Form → (filtered) Strength → (filtered) Packaging. Selection cascades. Progress dots fill.
11. **Confirm Drug:** "Select Drug" locks in selection. Panel collapses, auto-advances to next unconfirmed drug.
12. **Save Prescription:** "Continue" button opens save dialog. User names prescription → API saves it → snackbar + chat confirmation.
13. **Find Nearby Pharmacies:** Click button → lightweight NPI-only pharmacy list loads. User selects up to 5 pharmacies (toggle checkboxes). Counter shows X/5.
14. **Load Plan Recommendations:** Only appears after ≥1 pharmacy selected. Plans load with `costBreakdowns[]` per pharmacy. Each plan card shows compact view by default — "Plan Features" and "Cost Breakup" toggle buttons expand details.
15. **Calculate Lifetime Cost:** User clicks "Calculate Lifetime Cost" on a plan card → spinner shown on button → `POST /api/plan-recommendation/evaluate-costs` → AI evaluates cost data → navigates to `/medicare-analysis/cost-projections` → full dashboard with 4 Chart.js charts, summary cards, yearly highlights, category analysis, savings tips, and overall assessment. Back button returns to plans.
16. **Subsequent Queries:** Previous cards replaced.

---

← [Guards & Models](ch02-07-guards-models.md) | [Chapter 2 — Frontend Architecture (Index)](../ch02-frontend-architecture/ch02-frontend-architecture.md) | [Chapter 3 → Prompt Architecture](../ch03-prompt-architecture/ch03-prompt-architecture.md)
