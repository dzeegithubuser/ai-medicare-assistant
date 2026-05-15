# Chapter 10.10 — Role Management & Tear-down

> Routes: `/admin`, `/fpg`, `/fp`. Covers admin → FPG → FP → end-user creation, impersonation, the type-to-confirm delete dialog, and the bottom-up tear-down chain.

← [Testing Index](ch10-testing-scenarios.md)

---

## 31. Admin — create FPG admin (auto-group)

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 31.1 | Open admin landing | Sign in as `admin@aivante.com`, complete first-login password change | Lands on `/admin`. Header logo + "Medicare Assistant". Welcome banner reads "Admin Console / FPG Admin Users". No reference to "groups" anywhere in the UI. |
| 31.2 | Create FPG admin | Click "New FPG admin" → fill first/last/email/phone/initial password → Create | Dialog closes. New card appears at the top of the list with an amber "Pending" badge. Phone normalized to 10 digits server-side. Backend silently created a `FinancialPlannerGroup` named `"{First} {Last}"` and assigned `fpgId` to the user. Duplicate email or duplicate phone → inline 409 message, no record created. |
| 31.3 | Auto-named group collision | Create two FPG admins with identical first+last (e.g., "Jane Doe" twice but different emails) | Both succeed. Second group is named `"Jane Doe 2"` (collision suffix). Inspect `db.financialPlannerGroups.find()` to confirm. |
| 31.4 | Duplicate email | Create FPG admin with an email that already exists anywhere in `users` | Dialog stays open with inline error: "Email already registered." |
| 31.5 | Search / sort / pagination | After ~7 FPG admins exist: type a name in search, switch sort to "Name A-Z", change page size | Filtering is client-side and immediate. Pagination footer shows `Showing N–M of total`. |
| 31.6 | Header logo for admin role | Click the Medicare Assistant logo while on any sub-page | Returns to `/admin` (role-based redirect via `dashboardRedirectGuard`). |

---

## 32. FPG — create / delete financial planners

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 32.1 | First sign-in | Sign in as the FPG-admin user created in 31.2 with the initial password | Forced to `/change-password`. After change, lands on `/fpg`. Welcome banner shows the auto-generated group name. |
| 32.2 | View pills | Click each of the three view pills | "Financial Planners" (default), "Group End-Users", "Group Recommendations". Last two lazy-load on first click and show empty state until data exists. |
| 32.3 | Create planner | Click "Add planner" → fill all five fields (first/last/email/phone/password) → Create | Dialog closes. New FP card appears with amber "Pending" badge. Email is unique-validated; bad phone format keeps the submit button disabled. |
| 32.4 | Delete planner with no end-users | Click "Delete" on a freshly created FP card → browser `confirm()` → OK | FP card removed from list. |
| 32.5 | Delete planner with end-users | Have the FP create at least one end-user (sign in as FP, do 33.2), come back as FPG, try to Delete | 409 toast / banner: "Cannot delete a financial planner who still has end-users assigned. Reassign or delete the users first." FP card remains. |

---

## 33. FP — create end-user + impersonation

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 33.1 | First sign-in | Sign in as the FP from 32.3, complete forced password change | Lands on `/fp`. Welcome banner reads "My Practice / Users & Recommendations". |
| 33.2 | New user → auto-impersonate → /saved | Click "New user" → fill first/last/email/phone/initial password → "Create & start" | Dialog closes. **Full page reload.** Lands on `/saved` (the impersonated user's empty saved-analyses page). Amber impersonation banner is visible across the top. The end-user must change the password on first non-impersonated sign-in. Duplicate phone → inline 409 message, no record created. |
| 33.3 | Continue as user | From the FP landing, click "Continue as user" on an existing end-user card | Full page reload. Lands on `/saved` for that user. Amber banner shows the impersonated user's name + countdown. |
| 33.4 | Filter chips | Switch filter to "Has analyses" / "No analyses" / "All" | Card grid filters client-side. Result count updates. Sorting and search still work within the filtered set. |
| 33.5 | Per-user rec drilldown | Click "View" on a user card with recommendations | Inline list of that user's recommendations expands inside the card. Each row has a small red delete icon. |

---

## 34. Type-to-confirm delete dialog

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 34.1 | Dialog opens | Trigger any "Remove" button (admin remove FPG admin, or FP remove end-user) | Material dialog appears with red warning icon, title, subject (name + email), warning text, and a text input. Confirm button is **disabled** initially. |
| 34.2 | Wrong token | Type a random string in the input | Confirm button stays disabled. |
| 34.3 | Right token | Type the user's email exactly (case-insensitive, trimmed) | Confirm button becomes enabled. Pressing Enter or clicking it submits. |
| 34.4 | Cancel | Click Cancel or X | Dialog closes, no API call. List unchanged. |

---

## 35. FP — cascade delete end-user

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 35.1 | Preflight data | Impersonate the target end-user, complete a profile, save at least one Medicare analysis, run an LTC projection, send a chat message. Exit impersonation back to `/fp`. | The user now has docs in every per-user collection: `userProfiles`, `chatSessions`, `recommendations`, `userAnalysisSelections`, `ltcCurrentSelections`, `users`. |
| 35.2 | Remove end-user | On the user's card, click "Remove" → type the user's email → "Remove user" | Card disappears. `DELETE /api/financial-planner/me/end-users/{id}` returns 204. |
| 35.3 | Verify cascade | Run `db.<each-collection>.find({ userId: <id> })` for all six collections above | Every collection returns empty for that user id. |
| 35.4 | Delete another FP's user | Try to delete by hitting the endpoint with someone else's `endUserId` | 401: "Target is not one of your end-users." (Direct API hit; UI doesn't expose this.) |
| 35.5 | Delete missing user | Delete a `userId` that no longer exists | 404: not found. |

---

## 36. Admin — remove FPG admin

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 36.1 | Remove with FPs still in group | Open admin, click Remove on an FPG admin whose group still has at least one FP → type email → confirm | 409 inline error: "Cannot remove this FPG admin while N financial planner(s) still belong to their group. Sign in as the FPG admin and remove each planner first…". Card remains. |
| 36.2 | Remove after cleanup | Walk through 35.2 (remove every end-user under each FP), then sign in as FPG and 32.4 (delete each FP), then come back to admin and Remove | Card disappears. `DELETE /api/admin/fpg-admin-users/{id}` returns 204. The auto-created `FinancialPlannerGroup` is also deleted (verify with `db.financialPlannerGroups.find({ groupId: <id> })`). |
| 36.3 | Remove non-FPG user | Direct API hit: `DELETE /api/admin/fpg-admin-users/<some-user-id-with-role=user>` | 401: "Target user is not an FPG admin." |

---

## 37. Tear-down chain end-to-end

Goal: bring a full tenant from "active" to "gone" without orphans.

| Step | Acting role | Action | Endpoint |
|---|---|---|---|
| 1 | FP (real or via "Continue as user") | Remove each end-user via fp-home | `DELETE /me/end-users/{id}` |
| 2 | FPG admin | Delete each FP via fpg-home | `DELETE /me/financial-planners/{id}` |
| 3 | Admin | Remove the FPG admin via admin-home | `DELETE /api/admin/fpg-admin-users/{id}` |

Expected at the end:
- `db.users.countDocuments({ fpgId: <id> })` → 0
- `db.users.countDocuments({ fpId: <fpUserId> })` → 0 (for each FP id)
- `db.financialPlannerGroups.countDocuments({ groupId: <id> })` → 0
- All per-end-user collections (`userProfiles`, `chatSessions`, `recommendations`, `userAnalysisSelections`, `ltcCurrentSelections`) → 0 docs for that user
- The seeded `admin@aivante.com` user is untouched

If any step gets a 409, the chain is broken — the message tells you which dependent is still attached.

---

## 38. Header logo & landing redirects

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 38.1 | Logo as admin | While on any sub-page (e.g., open a dialog then close) click the logo | Navigates to `/admin` |
| 38.2 | Logo as FPG | While on `/fpg` (e.g., on the End-Users pill view) click the logo | Navigates to `/fpg` (no change — the redirect guard resolves to the same path) |
| 38.3 | Logo as FP | While on `/fp` click the logo | Navigates to `/fp` |
| 38.4 | Logo as end-user | Sign in as a plain end-user (or impersonate one), navigate into `/medicare-analysis/profile`, then click the logo | Navigates to `/saved`. Drug + LTC state are reset. |
| 38.5 | "Recommendations" header button | Only visible when `auth.currentRole() === 'user'` (real end-user or active impersonation). Click it. | Navigates to `/saved` directly; not gated by the redirect guard. |

---

## 39. Admin seed configuration

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 39.1 | No password configured | Start the API with `ADMIN_PASSWORD` blank in `.env` and no `Seed:AdminPassword` user-secret | Logs: `Admin seed skipped: Seed:AdminPassword is not configured.` No admin row in `users`. |
| 39.2 | First-run seed | Set `ADMIN_PASSWORD=SomeStrongOne` in `.env`, start against a fresh DB | Logs: `Seeded admin user admin@aivante.com (id=…)`. Sign in as `admin@aivante.com` with that password → forced password change → lands on `/admin`. |
| 39.3 | Already exists | Restart the API with the same `ADMIN_PASSWORD` | Logs: `Admin user admin@aivante.com already exists; skipping seed.` No duplicate row. |
| 39.4 | Custom email | Set `ADMIN_EMAIL=ops@company.com`, `ADMIN_PASSWORD=Strong!`, fresh DB | Seeded admin email is `ops@company.com`. Email is lowercased + trimmed (`OPS@Company.com  ` → `ops@company.com`). |
| 39.5 | Blank password after seed | Once seeded, blank out `ADMIN_PASSWORD` again and restart | Seed step is a no-op. Existing admin user untouched. |

---

← [Testing Index](ch10-testing-scenarios.md)
