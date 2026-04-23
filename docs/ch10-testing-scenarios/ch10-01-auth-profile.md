# Chapter 10.1 — Authentication & Profile

> Routes: `/signin`, `/signup`, `/forgot-password`, `/reset-password`, `/verify-email`, `/change-password`, `/profile`

---

## 9. Authentication & Profile

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 9.1 | Sign up | Fill all fields → submit | JWT returned, redirected to dashboard, profile form shown. |
| 9.2 | Sign in | Valid email + password | JWT returned, redirected to dashboard, then auto-redirected to `/profile` (always). |
| 9.3 | Wrong password | Valid email + wrong password | Error: "Invalid credentials". |
| 9.4 | Profile completion | Complete the profile form and save | Left panel switches to analysis wizard (**`/medicare-analysis/profile`**, Profile as step 1). |
| 9.5 | Analyze without profile | Submit prescription early | Chat: "please complete your profile". Profile auto-opens. |
| 9.6 | Token expiry | Wait for JWT to expire | Redirected to sign-in. |

---

← [Testing Index](../ch10-testing-scenarios/ch10-testing-scenarios.md)
