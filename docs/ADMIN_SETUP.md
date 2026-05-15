# Admin & Role Bootstrapping

How to bring the four-role system online from a fresh database.

## Roles

| Role | Created by | Permissions |
|---|---|---|
| `admin` | Seeded once on API startup | Create FPG-admin users. The underlying `FinancialPlannerGroup` is auto-created behind the scenes — admin never sees or manages it. Nothing else. |
| `financial_planner_group` (FPG) | Admin | CRUD on Financial Planners in their group; read-only on end-users and recommendations across the group. |
| `financial_planner` (FP) | FPG | Create end-users; impersonate an end-user to walk the Medicare wizard on their behalf; view/delete their own recommendations. |
| `user` | FP (only) | Existing Medicare / LTC analysis flow. Public self-signup is disabled. |

## Step 1 — Seed the admin

[`AdminSeedInitializer`](../api-ai-medicare-assistant/AI.MedicareAssistant.Infrastructure/Data/AdminSeedInitializer.cs) reads three values from configuration:

| Config key | Default | Notes |
|---|---|---|
| `Seed:AdminEmail` | `admin@aivante.com` | Lowercased + trimmed before use |
| `Seed:AdminPhone` | `5550199999` | Trimmed before use |
| `Seed:AdminPassword` | _(unset)_ | **Gates the seed** — without this the seeder is a no-op (production-safe) |

### Local dev (.NET User Secrets, preferred)

```powershell
dotnet user-secrets set "Seed:AdminPassword" "PickAStrongOneHere" `
  --project api-ai-medicare-assistant/AI.MedicareAssistant.Api
```

Email and phone are pre-populated in `appsettings.Development.json` with the defaults; override there if you want a different local admin.

### Local dev (appsettings.Development.json)

```json
{
  "Seed": {
    "AdminEmail": "admin@aivante.com",
    "AdminPhone": "5550199999",
    "AdminPassword": "PickAStrongOneHere"
  }
}
```

### Docker / production (env vars)

`.env` (read by `docker-compose.yml`):

```env
ADMIN_EMAIL=admin@aivante.com
ADMIN_PHONE=5550199999
ADMIN_PASSWORD=PickAStrongOneHere
```

`docker-compose.yml` maps these to `Seed__AdminEmail` / `Seed__AdminPhone` / `Seed__AdminPassword` on the `api` service. See [DOCKER.md](DOCKER.md).

### What you should see in the logs

If `Seed:AdminPassword` is set and no matching user exists:
```
Seeded admin user admin@aivante.com (id=…)
```

If `Seed:AdminPassword` is missing:
```
Admin seed skipped: Seed:AdminPassword is not configured.
```

If the email already exists:
```
Admin user admin@aivante.com already exists; skipping seed.
```

The admin is seeded with `MustChangePassword=true`, `IsEmailVerified=true`, and `Role=admin`. Once seeded successfully you can blank `ADMIN_PASSWORD` again — subsequent restarts will skip silently.

## Step 2 — First sign-in (admin)

1. Open `http://localhost:4200/signin`.
2. Email **`admin@aivante.com`**, password = the value from step 1.
3. The first sign-in forces a password change:
   - JWT carries `mustChangePassword=true`.
   - `mustChangePasswordGuard` ([guards/must-change-password.guard.ts](../ui-ai-medicare-assistant/src/app/guards/must-change-password.guard.ts)) sends you to `/change-password`.
   - The server-side `MustChangePasswordFilter` ([Filters/MustChangePasswordFilter.cs](../api-ai-medicare-assistant/AI.MedicareAssistant.Api/Filters/MustChangePasswordFilter.cs)) blocks every other endpoint until the password is changed.
   - On success the API reissues a fresh token with the flag cleared and the guard releases.
4. `dashboardRedirectGuard` then routes role `admin` to `/admin`.

## Step 3 — Cascade the rest

From `/admin`:
- Click **New FPG admin** → fill in first / last / email / phone / initial password. One action creates both the FPG-admin user and an auto-named `FinancialPlannerGroup` (`"{First} {Last}"`, with a `" 2"`, `" 3"`, … suffix if the name is already taken). Phone is normalized to a 10-digit string and validated against the unique-phone index. The user is created with `MustChangePassword=true`.

> The legacy two-step flow (create group → add admin user inside it) still works at the API level via the `/api/admin/financial-planner-groups*` endpoints, but the admin UI no longer surfaces the group as a separate concept.

The FPG-admin signs in at `/signin`, is forced to change their password, lands on `/fpg`. From there:
- Create Financial Planners (email, first/last, real phone, initial password). Each FP also lands with `MustChangePassword=true`.

Each FP signs in, lands on `/fp`. From there:
- Click **New user** → fill first / last / email / phone / initial password. End-user is created with the supplied phone (normalized + checked against the unique-phone index), then the FP is auto-impersonated and dropped on `/saved` for that user. The user must change the password on first sign-in (`MustChangePassword=true`).
- Click **Continue as user** on any existing user to impersonate them.

## Impersonation lifecycle

- Token TTL = 60 minutes (set in [`ImpersonationService`](../api-ai-medicare-assistant/AI.MedicareAssistant.Application/Services/ImpersonationService.cs)).
- 55-minute mark → modal "Continue impersonating?" prompt with a live countdown.
  - **Continue** → `POST /api/impersonate/refresh` → fresh 60-min token.
  - **Exit** or no response by 60 min → silent auto-exit, hard-reload to `/fp`.
- Manual exit available any time via the amber banner at the top of every dashboard route.
- Every request made under impersonation logs `ImpersonatedBy={fpId}` via Serilog.

## Tearing down a tenant

The role-management UI lets each layer remove the layer below, but each delete refuses while dependents exist. This means tear-down is bottom-up:

1. **FP signs in → `/fp` → Remove on each end-user card.** Opens the type-to-confirm dialog (must type the user's email). On confirm, cascades through `userProfiles` → `chatSessions` → `recommendations` → `userAnalysisSelections` → `ltcCurrentSelections` → `users`. Calls `DELETE /api/financial-planner/me/end-users/{endUserId}`.
2. **FPG admin signs in → `/fpg` → Delete on each FP card.** Refuses with `409` if the FP still has end-users (do step 1 for each end-user first). Calls `DELETE /api/financial-planner-group/me/financial-planners/{fpUserId}`.
3. **Admin signs in → `/admin` → Remove on the FPG-admin card.** Type-to-confirm dialog (type the admin's email). Refuses with `409` if the group still has FPs. On success, deletes the user + their auto-created `FinancialPlannerGroup`. Calls `DELETE /api/admin/fpg-admin-users/{userId}`.

The 409 messages on each delete name the dependent type + count and tell whoever's stuck which earlier step to complete.

---

## Default credentials reference

| Account | Email | Initial password | Forced change? |
|---|---|---|---|
| Admin | `admin@aivante.com` | `Seed:AdminPassword` (config) | Yes |
| FPG-admin | (set by admin) | (set by admin) | Yes |
| FP | (set by FPG) | (set by FPG) | Yes |
| End-user | (set by FP) | (set by FP via the create-end-user dialog) | Yes |

Every role's initial password is chosen by the creator at the time of account creation. There is no hardcoded default for any role.
