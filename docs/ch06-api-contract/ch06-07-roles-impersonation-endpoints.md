# Chapter 6.7 — Roles & Impersonation Endpoints

> Admin, Financial Planner Group (FPG), Financial Planner (FP), and impersonation routes. All endpoints require a Bearer JWT and most are role-gated via `[Authorize(Roles = …)]`. Service-layer ownership checks (`target.FpgId == caller.FpgId`, `target.FpId == caller`) re-verify on every mutation.

← [API Contract Index](ch06-api-contract.md)

---

## Admin (`/api/admin`)

`[Authorize(Roles = admin)]` — only the seeded `admin@aivante.com` can hit these. See [ADMIN_SETUP.md](../ADMIN_SETUP.md) for how the admin is bootstrapped.

> **The group is invisible to the admin UI.** The FPG entity (`financialPlannerGroups`) still backs the role hierarchy, but it's auto-managed: creating an FPG admin user via `POST /api/admin/fpg-admin-users` atomically creates the underlying `FinancialPlannerGroup` (named `"{FirstName} {LastName}"`, with a numeric suffix on collision). The legacy two-step endpoints below remain for backward compatibility but the UI no longer uses them.

### `GET api/admin/fpg-admin-users`

List every user with role `financial_planner_group`, newest first. Returns `UserSummaryDto[]`.

```json
[
  {
    "userId": "…",
    "email": "fpg@example.com",
    "firstName": "Jane", "lastName": "Doe",
    "phone": "5550112345",
    "role": "financial_planner_group",
    "fpgId": "…",
    "fpId": null,
    "mustChangePassword": true,
    "createdAt": "2026-05-14T01:23:45Z"
  }
]
```

### `POST api/admin/fpg-admin-users`

Create an FPG-admin user without exposing the group. Behind the scenes the backend creates a `FinancialPlannerGroupDocument` named `"{FirstName} {LastName}"` (or `"{First} {Last} 2"`, `" 3"`, … on collision, fails after 50) and assigns the new user to it. Phone is normalized to a 10-digit string and checked against the unique-phone index. The user is created with `Role = financial_planner_group`, `FpgId = <auto-group>`, `MustChangePassword = true`, `IsEmailVerified = true`.

**Request:**
```json
{ "email": "fpg@example.com", "firstName": "Jane", "lastName": "Doe", "phone": "(555) 123-4567", "password": "InitialPassword!" }
```

**Response:** `UserSummaryDto` with `role: "financial_planner_group"` and the auto-assigned `fpgId`.

**Errors:**
- `409 Conflict` — duplicate email, duplicate phone, or 50 group-name suffixes already taken.

---

### `DELETE api/admin/fpg-admin-users/{userId}`

Remove an FPG-admin user and their auto-created `FinancialPlannerGroup`. The admin must first clear out the group's planners via the FPG home (and each planner must clear out their end-users via the FP home — see below). Returns `204 No Content`.

**Errors:**
- `409 Conflict` — the group still has financial planners. Response body includes a message naming the count and pointing the admin at the FPG home.
- `401 Unauthorized` — target user exists but is not an FPG admin.
- `404 Not Found` — target user does not exist.

---

### Legacy endpoints (kept for backward compatibility, no longer used by the UI)

| Verb | Path | Notes |
|---|---|---|
| GET | `/api/admin/financial-planner-groups` | List `FpgSummaryDto[]`. |
| POST | `/api/admin/financial-planner-groups` | Create FPG (unique name required). |
| POST | `/api/admin/financial-planner-groups/{fpgId}/admin-user` | Create an FPG-admin scoped to an existing group. Prefer `POST /api/admin/fpg-admin-users` above. |

---

## Financial Planner Group (`/api/financial-planner-group`)

`[Authorize(Roles = financial_planner_group)]`. The caller's `fpgId` is read from the JWT — never accepted from the request body.

| Verb | Path | Body | Returns |
|---|---|---|---|
| GET | `/me` | — | `FpgSummaryDto` of the caller's group |
| GET | `/me/financial-planners` | — | `FpSummaryDto[]` (FPs in the group) |
| POST | `/me/financial-planners` | `CreateFpRequest` (email, first/last, password, phone) | `FpSummaryDto` — created with `MustChangePassword = true` |
| PUT | `/me/financial-planners/{fpUserId}` | `UpdateFpRequest` (first/last, phone) | `FpSummaryDto` — verifies `target.FpgId == callerFpgId` |
| DELETE | `/me/financial-planners/{fpUserId}` | — | `204 No Content`. **Rejected with 409** if the FP still has end-users assigned (avoids orphaning). |
| GET | `/me/end-users` | — | `EndUserSummaryDto[]` — all end-users created by FPs in the group (read-only) |
| GET | `/me/recommendations` | — | `RecommendationByUserDto[]` — recs grouped by end-user, latest first |

**`CreateFpRequest`:**
```json
{ "email": "fp@example.com", "firstName": "Sam", "lastName": "Lee", "password": "InitialPwd!", "phone": "(555) 123-4567" }
```

---

## Financial Planner (`/api/financial-planner`)

`[Authorize(Roles = financial_planner)]`. Caller's user id is read from the JWT.

| Verb | Path | Body | Returns |
|---|---|---|---|
| GET | `/me/end-users` | — | `EndUserSummaryDto[]` — users created by the caller |
| POST | `/me/end-users` | `CreateEndUserRequest` (email, first/last, phone, password) | `EndUserSummaryDto` — created with the FP-supplied phone (normalized + checked against the unique-phone index) and password (BCrypt-hashed), `MustChangePassword = true`, `IsEmailVerified = true`, `FpId = caller.UserId`. Returns `409 Conflict` on duplicate email or duplicate phone. |
| DELETE | `/me/end-users/{endUserId}` | — | `204 No Content`. **Cascade delete:** verifies `target.FpId = caller.UserId`, then deletes every per-user document — `userProfiles`, `chatSessions`, `recommendations`, `userAnalysisSelections`, `ltcCurrentSelections` — before deleting the user itself. Required to make the FPG-admin delete usable (the FPG-admin delete refuses while planners have end-users, and the FP delete refuses while planners have end-users). |
| GET | `/me/recommendations` | — | `RecommendationByUserDto[]` — recs grouped by user, latest rec per user on top |
| DELETE | `/me/recommendations/{recommendationId}` | — | `204 No Content`. Verifies that the recommendation's user has `FpId = caller.UserId` before deleting. |

**`CreateEndUserRequest`:**
```json
{ "email": "user@example.com", "firstName": "Alex", "lastName": "Kim", "phone": "(555) 123-4567", "password": "InitialPwd!" }
```

**`RecommendationByUserDto`:**
```json
{
  "userId": "…",
  "email": "user@example.com",
  "firstName": "Alex",
  "lastName": "Kim",
  "recommendations": [
    { "id": "…", "name": "2026 Plan A", "status": "active", "type": "medicare", "createdAt": "…", "updatedAt": "…" }
  ]
}
```

---

## Impersonation (`/api/impersonate`)

Class-level `[Authorize]`. Initial impersonation requires the FP role; refresh accepts any authenticated principal (including the impersonation token itself).

### `POST api/impersonate`

`[Authorize(Roles = financial_planner)]`.

Issues a 60-minute JWT for the FP to act as one of their end-users. Verifies `target.Role == user && target.FpId == caller.UserId` — else `401 Unauthorized`.

**Request:**
```json
{ "targetUserId": "…" }
```

**Response (`ImpersonationResponse`):**
```json
{
  "token": "<impersonation-jwt>",
  "expiresAt": "2026-05-08T15:00:00Z",
  "actingAsUserId": "<fpUserId>",
  "targetUserId": "<endUserId>",
  "targetEmail": "user@example.com",
  "targetFirstName": "Alex",
  "targetLastName": "Kim"
}
```

The token's claim shape:
- `NameIdentifier` = end-user id (so all existing controllers read the right userId)
- `Role = "user"`
- `actingAs` = FP user id (audit + refresh)
- `mustChangePassword = "false"` (overridden so the FP can act even when the end-user still has the flag set)

### `POST api/impersonate/refresh`

`[Authorize]` (no role restriction — accepts the impersonation token).

Re-validates ownership using the `actingAs` claim and the current `NameIdentifier`, then reissues a fresh 60-minute token. Used by the UI's 55-minute "Continue impersonating?" prompt.

**Request:** _(empty body)_

**Response:** same shape as `POST /api/impersonate`.

---

## Tear-down chain

Tenant removal is admin-driven but bottom-up because each layer's delete endpoint refuses while it still has dependents. The chain:

| Step | Caller | Endpoint | Refuses if… |
|---|---|---|---|
| 1 | FP (or FP impersonator) | `DELETE /api/financial-planner/me/end-users/{endUserId}` | (no refusal) — cascade removes user + per-user docs |
| 2 | FPG | `DELETE /api/financial-planner-group/me/financial-planners/{fpUserId}` | the FP still has end-users (`409`) |
| 3 | Admin | `DELETE /api/admin/fpg-admin-users/{userId}` | the group still has financial planners (`409`) |

The `409` messages name the dependent type + count and point at the next role to handle the cleanup.

---

## Authorization summary

| Caller role | Endpoints |
|---|---|
| Anonymous | `POST /api/auth/signin`, `forgot-password`, `reset-password`, `verify-email`, `resend-verification` (`signup` was removed) |
| Any authenticated, `mustChangePassword=true` | Only `POST /api/auth/change-password` (everything else 401 via `MustChangePasswordFilter`) |
| `admin` | `/api/admin/*` |
| `financial_planner_group` | `/api/financial-planner-group/*` |
| `financial_planner` | `/api/financial-planner/*`, `POST /api/impersonate` |
| `user` (or FP impersonating) | `/api/profile`, `/api/drug/*`, `/api/pharmacy/*`, `/api/recommendation/*`, `/api/long-term-care/*`, `/api/chat/*`, `/hubs/chat`, `POST /api/impersonate/refresh` |

---

← [Previous: LTC Endpoints](ch06-06-ltc-endpoints.md) | [API Contract Index](ch06-api-contract.md)
