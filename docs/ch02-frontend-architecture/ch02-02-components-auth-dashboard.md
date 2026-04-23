# Chapter 2.2 — Components: Auth, Dashboard & Profile

> App root, authenticated shell, authentication pages, and user profile.

← [Chapter 2 — Frontend Architecture (Index)](../ch02-frontend-architecture/ch02-frontend-architecture.md)

---

## Components

### `App` (`app.ts`)
- **Role:** Minimal root shell — just renders `<router-outlet />`.
- **Imports:** `RouterOutlet`.
- **Template:** Inline `<router-outlet />` — all layout logic moved to DashboardComponent.

### `DashboardComponent` (`dashboard/dashboard.component.ts`, `.html`, `.scss`)
- **Role:** Authenticated shell — renders the header bar and split-panel layout with child `<router-outlet>`. Protected by `authGuard`.
- **Layout:** Full-height flex column. Header at top, main content below as horizontal flex.
- **State:** Injects `AuthService`, `ProfileService`, `Router`, `ChatSignalRService`. Local `bootstrapReady` signal.
- **Imports:** `RouterOutlet`, `ChatComponent`, Material modules (`MatIconModule`, `MatButtonModule`, `MatTooltipModule`, `MatMenuModule`). Does **not** import `DrugCardsComponent` or `UserProfileComponent` — these are loaded via child routes.
- **Bootstrap (`ngOnInit`):** `bootstrapDashboardState()` runs a `forkJoin` of three parallel operations: `profileService.loadProfile()`, `recommendationState.loadActiveRecommendation$()`, and `hydrateChatSession$()`. Then chains `selectionHydrator.hydrateAllFromActiveRecommendationSelectionForBootstrap$()`. Sets `bootstrapReady` once all complete.
- **`hydrateChatSession$()`:** Calls `chatSignalR.connect(token)` to open the WebSocket hub connection, then subscribes to `chatSignalR.session$` with `take(1)` and a 5 s timeout. When the hub fires `ReceiveSession` (on `OnConnectedAsync`), hydrates `MedicareStateService.messages` from the pushed payload. Replaces the previous `GET /api/chat/session` HTTP call. The `ReplaySubject(1)` inside `ChatSignalRService` ensures that if the session push arrived before the dashboard subscribed (sign-in path), the value is replayed immediately.
- **Left Panel:** Renders `<router-outlet>` (shown after `profileLoaded()` is true). Child routes determine which component appears — no `@if` show/hide logic.
- **Template:** Gradient header with pharmacy icon, "AI Medicare Assistant" branding, **folder_open icon button** (navigates to `/saved` — always visible), and user menu dropdown button (account_circle icon). Dropdown shows "Welcome, {displayName}" header, **Saved Data** item (navigates to `/saved`, shown when profile complete), Edit Profile (if profile complete), Change Password, and Logout items. Below header: `<router-outlet>` left panel + chat. Footer bar at bottom shows "Powered by OpenAI".
- **`openRecommendations()`:** Navigates to `/saved`.
- **`displayName` Computed Signal:** Shows `"FirstName L"` (first name + last initial) when profile is complete, falls back to email otherwise.
- **Edit Profile:** `editProfile()` sets `profileService.editMode` to true and navigates to `/profile`.
- **Edit Profile from Analysis:** If current route is `/medicare-analysis/*`, stores `returnRoute` so profile save/close returns to the same analysis step.
- **Change Password:** `changePassword()` navigates to `/change-password`.

### `SigninComponent` (`auth/signin/signin.component.ts`, `.html`, `.scss`)
- **Role:** Sign-in page with email/password form.
- **Features:** ReactiveFormsModule form with email + password fields, password visibility toggle, loading/error signal states, links to sign up and forgot password.
- **Styling:** Centered card with cyan gradient background, pharmacy icon branding.
- **Flow:** Calls `authService.signIn()`, then `handleAuthSuccess()`, then navigates to `/`.

### `SignupComponent` (`auth/signup/signup.component.ts`, `.html`, `.scss`)
- **Role:** Registration page with email, phone, password, confirmPassword fields.
- **Features:** Password length validation (min 8), confirmPassword match validation, loading/error states.
- **Flow:** Calls `authService.signUp()`, then `handleAuthSuccess()`, then navigates to `/`.

### `ForgotPasswordComponent` (`auth/forgot-password/forgot-password.component.ts`, `.html`, `.scss`)
- **Role:** Password recovery page with email field.
- **Features:** Shows success (green) or error (red) messages after submission.
- **Styling:** Orange lock_reset icon, centered card layout.

### `ResetPasswordComponent` (`auth/reset-password/reset-password.component.ts`, `.html`, `.scss`)
- **Role:** Public page that email reset links land on (`/reset-password?token=...`). Allows users to choose a new password.
- **Features:** Reads `?token=` from `ActivatedRoute.queryParamMap`; redirects to `/forgot-password` if token is missing. Cross-field password match validator. Success banner followed by auto-redirect to `/signin` after 2 s.
- **Flow:** Calls `authService.resetPassword({ token, newPassword, confirmPassword })`. Signals: `loading`, `error`, `successMessage`, password visibility toggles.
- **Styling:** Cyan gradient background, orange lock_reset icon (same palette as ForgotPasswordComponent).

### `ChangePasswordComponent` (`auth/change-password/change-password.component.ts`, `.html`, `.scss`)
- **Role:** Authenticated page at `/change-password` where logged-in users update their password from the dashboard menu.
- **Features:** Three-field form: `oldPassword`, `newPassword` (min 8), `confirmPassword`. Cross-field password match validator. Success banner followed by auto-redirect to `/` (dashboard) after 2 s. Cancel button returns immediately to dashboard.
- **Flow:** Calls `authService.changePassword({ oldPassword, newPassword, confirmPassword })`. Backend verifies old password via BCrypt before updating. Auth interceptor automatically attaches Bearer token — no manual header needed.
- **Styling:** Cyan gradient background, indigo lock icon (visually distinct from the orange reset icon).

### `VerifyEmailComponent` (`auth/verify-email/verify-email.component.ts`, `.html`, `.scss`)
- **Role:** Public page at `/verify-email` where email verification links land. Verifies the user's email address token.
- **Flow:** Calls `authService.verifyEmail()` or `authService.resendVerification()` via `POST /api/auth/verify-email` and `POST /api/auth/resend-verification`.

### `UserProfileComponent` (`user-profile/user-profile.component.ts`, `.html`, `.scss`)
- **Role:** Consolidated single-form profile. Routed at **`/profile`** (dashboard, full-width profile) and **`/medicare-analysis/profile`** (same component embedded in `AnalysisShellComponent` as analysis step 1). When the URL contains `/medicare-analysis/profile`, `MedicareStateService.currentStep` is set to `1`.
- **Landing Modes:**
  - **View mode (profile complete):** Opened by default after login when profile is complete. Form is read-only and shows a **Modify Profile** button.
  - **Create mode (profile incomplete):** Opened by default after login when profile is incomplete. Form is editable and focused on completing required onboarding fields.
  - **Edit mode:** Entered from view mode via **Modify Profile** or from explicit Edit Profile actions.
- **Fields:** First name (required, alphabetic + separators pattern), last name (required, same pattern), coverage year (radio, conditional on current month), health profile (dropdown, 1-5), tax filing status (radio), MAGI tier (dropdown, depends on tax filing + coverage year via constants API), gender (radio), tobacco status (radio), date of birth (datepicker, 18+ age validator), concierge (radio), concierge amount (conditional input), alternate email (optional, email validation), alternate mobile (optional, US phone validation), life expectancy (65-120, default 95), all address fields with county/city cascading dropdowns from ZIP lookup.
- **Name Validation Pattern:** `^[A-Za-z]+([' -][A-Za-z]+)*$` — alphabetic only, allows spaces/hyphens/apostrophes as separators, no leading/trailing separators, no consecutive separators. Supports names like John, Mary-Jane, O'Connor, Anne Marie.
- **Hardcoded Options:** Gender and yes/no options extracted to component variables (`genderOptions`, `yesNoOptions`) and iterated via `@for` in the template.
- **State:** `isEditMode` signal. Injects `ProfileService`, `Router`.
- **Flow:** On save, calls `POST /api/profile` with consolidated `ProfileDto`. Sets `profileService.isProfileComplete` to true and navigates to `/medicare-analysis`. In edit mode, stays on the panel — user closes manually via "Done" button. Note: the Save Profile button has been removed; saving is triggered by the wizard's Continue button when embedded at `/medicare-analysis/profile`.
- **Impact-aware invalidation:** If profile is opened from `/medicare-analysis/*` and impactful fields changed (demographic/tax/location/coverage assumptions), keeps drugs but clears downstream analysis state (pharmacy selection, plan selection, cost projection) and prompts user to continue from pharmacies.
- **OnInit:** Calls `ProfileService.loadProfile()` to populate form. Fetches MAGI tier options from constants service based on tax filing status and coverage year.

---

← [Chapter 2 — Frontend Architecture (Index)](../ch02-frontend-architecture/ch02-frontend-architecture.md) | [Next → Chat Components](ch02-03-components-chat.md)
