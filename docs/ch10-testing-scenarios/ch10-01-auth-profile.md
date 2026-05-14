# Chapter 10.1 — Authentication & Profile

> Routes: `/signin`, `/forgot-password`, `/reset-password`, `/verify-email`, `/change-password`, `/profile`
>
> **Public sign-up was removed.** End-user accounts are created top-down by their FP (see [ch10-10](ch10-10-role-management.md) for the role-management flow). This file covers the auth surface that every role uses.

---

## 9. Authentication & Profile

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 9.1 | Sign in (end-user) | Valid email + password for a user with `role=user` and `mustChangePassword=false` | JWT returned. Dashboard root redirect (`dashboardRedirectGuard`) lands on `/saved`. Header logo navigates to `/saved`. |
| 9.2 | Sign in (admin / FPG / FP) | Valid email + password for `role=admin` / `financial_planner_group` / `financial_planner` | JWT returned. Redirect guard lands on `/admin` / `/fpg` / `/fp` respectively. The "Recommendations" header button is hidden (only end-users see it). |
| 9.3 | Wrong password | Valid email + wrong password | Inline error: "Invalid credentials". No JWT issued. |
| 9.4 | First-login forced password change | Sign in with a user that has `mustChangePassword=true` (any role) | Redirected to `/change-password` by `mustChangePasswordGuard`. Server-side `MustChangePasswordFilter` rejects every other endpoint with 401. After change, fresh JWT issued with the flag cleared and user lands on their role-based home. |
| 9.5 | Forgot password | Submit any email at `/forgot-password` | Always returns success (anti-enumeration). If the email exists, a reset link is emailed. |
| 9.6 | Reset password | Click the email link → fill new password + confirm | Token validated (`purpose: password-reset`, expires 30 min). Password updated. Auto-navigate to `/signin` after 2 s. |
| 9.7 | Profile completion (end-user only) | Sign in as end-user with `isProfileComplete=false`, complete the form, save | Profile saved. End-user can now navigate to `/medicare-analysis/*` (`profileCompleteGuard` releases). Admin/FPG/FP roles never see this — they have no profile flow. |
| 9.8 | Analyze without profile | End-user submits a prescription before completing profile | Chat: "please complete your profile". Profile auto-opens. |
| 9.9 | Token expiry | Wait for JWT to expire (1 h default in sessionStorage tracker) | Next API call returns 401 → interceptor signs out → redirect to `/signin`. |

---

← [Testing Index](ch10-testing-scenarios.md)
