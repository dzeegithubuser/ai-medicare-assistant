# Chapter 8.1 — Auth & Profile Features

> Authentication flows, email service, frontend auth components, and user profile onboarding.

← [Feature Catalog Index](../ch08-feature-catalog/ch08-feature-catalog.md)

---

## ✅ JWT Authentication (Sign Up / Sign In / Forgot Password / Reset Password / Change Password)
- **What:** Complete auth flow with JWT token-based authentication and full password lifecycle management.
- **Sign Up:** Validates uniqueness of email + phone, hashes password with BCrypt, creates user, returns JWT.
- **Sign In:** Validates email + BCrypt password verification, returns JWT on success.
- **Forgot Password:** Generates a 30-minute reset token. Sends an HTML reset-link email via SMTP (`EmailService`). Returns success regardless of email existence to prevent enumeration.
- **Reset Password:** Validates reset token (`purpose: password-reset` claim), updates password hash. Token expires in 30 minutes.
- **Change Password:** `[Authorize]` endpoint. Extracts `userId` from Bearer JWT `NameIdentifier` claim. BCrypt-verifies old password before writing new hash.
- **Security:** HMAC-SHA256 signed tokens, configurable expiry, ClockSkew=Zero.

---

## ✅ Email Service (SMTP)
- **What:** Transactional email delivery for password reset links.
- **Implementation:** `IEmailService` / `EmailService` in the Infrastructure layer. Registered as scoped in DI.
- **Transport:** SMTP via `smtp.1and1.com:587` with STARTTLS, sender `support@aivante.com`. Credentials in `appsettings.json` → `Email:SmtpHost`, `Email:SmtpPort`, `Email:Username`, `Email:Password`, `Email:FromAddress`, `Email:FromName`.
- **Usage:** `ForgotPasswordAsync` in `AuthService` calls `IEmailService.SendPasswordResetEmailAsync(to, resetLink)`. Sends an HTML email containing the `/reset-password?token=<jwt>` link.
- **Security:** Token is never returned in the HTTP response — it travels exclusively through the email channel.

---

## ✅ Frontend Auth Components & Routing
- **Components:** SigninComponent, SignupComponent, ForgotPasswordComponent, ResetPasswordComponent, ChangePasswordComponent, VerifyEmailComponent.
- **Shared Shell:** All auth components use `AuthFormShellComponent` — a reusable card shell providing consistent gradient background, icon, title, subtitle, form content projection, and footer link. Eliminates duplicated layout markup across 6 auth forms.
- **Shared Validator:** `passwordMatchValidator` (from `shared/validators/`) — cross-field `ValidatorFn` used by SignupComponent, ResetPasswordComponent, and ChangePasswordComponent.
- **ResetPasswordComponent:** Public route `/reset-password`. Reads `?token=` from URL; redirects to `/forgot-password` if missing. Two-field form (newPassword + confirmPassword with cross-field match validator). On success shows green banner then auto-navigates to `/signin` after 2 s.
- **ChangePasswordComponent:** Authenticated route `/change-password` (inside `authGuard` dashboard children). Three-field form (oldPassword + newPassword + confirmPassword). On success shows green banner then auto-navigates to `/` (dashboard) after 2 s. Cancel button returns immediately.
- **AuthService:** Signal-based state with `currentUser` and `isAuthenticated` signals, sessionStorage persistence (not localStorage — session ends on tab close). 1-hour token expiry with auto-refresh on activity.
- **Auth Interceptor:** `HttpInterceptorFn` that attaches `Authorization: Bearer <token>` header — no manual header management needed in components or services.
- **Auth Guard:** `CanActivateFn` protecting the Dashboard route.
- **Routing:** Lazy-loaded via `loadComponent`. App component simplified to `<router-outlet />`.
- **Styling:** Auth forms use `AuthFormShellComponent` for consistent centered card layout with gradient background and pharmacy branding.

---

## ✅ First-Time User Profile Onboarding
- **What:** Consolidated single-form profile completion shown before medicare analysis access.
- **Detection:** Dashboard calls `GET /api/profile` and the post-login dashboard redirect always lands on `/profile` (profile complete or incomplete).
- **Landing behavior by state:**
  - **Profile complete:** profile opens in **view mode** (read-only) with a **Modify Profile** action.
  - **Profile incomplete:** profile opens in **create mode** and chat instructs user to complete profile before analysis.
- **Fields:** First name (required, alphabetic with separators), last name (required, same pattern), coverage year (radio), health profile (dropdown), tax filing status (radio), MAGI tier (dropdown, depends on tax filing + coverage year), gender (radio), tobacco status (radio), date of birth (datepicker, 18+ validator), concierge (radio), concierge amount (conditional input), alternate email (optional), alternate mobile (optional, US phone), life expectancy (65-120, default 95), plus all address fields with ZIP-based county/city cascading dropdowns.
- **Name Validation:** Pattern `^[A-Za-z]+([' -][A-Za-z]+)*$` — supports names like John, Mary-Jane, O'Connor, Anne Marie.
- **Save Flow:** Single `POST /api/profile` saves all fields. Auto-navigates to `/medicare-analysis`. Note: the explicit "Save Profile" button has been removed from the profile form — saving is triggered by the wizard's Continue button when the user is embedded at `/medicare-analysis/profile`, keeping the form clean for the first-time onboarding flow.
- **Backend:** `ProfileController` extracts UserId from JWT. `ProfileService` creates or updates the consolidated `Profile` entity.

---

## ✅ Session State Leak Fix on Sign-Out

- **What:** Fixed critical bug where User A's drugs, pharmacies, chat messages, plans, and cost projections leaked to User B after sign-out/sign-in.
- **Root Cause:** `signOut()` was only clearing 3 auth keys from sessionStorage.
- **Fix:** `sessionStorage.clear()` + reset all in-memory signals: `MedicareStateService` (25+ signals), `ProfileService` (6 signals), `RecommendationStateService.clear()`. Uses `Injector.get()` for lazy service resolution to avoid circular dependencies.

---

← [Feature Catalog Index](../ch08-feature-catalog/ch08-feature-catalog.md) | [Next: Drug Analysis →](ch08-02-drug-analysis.md)
