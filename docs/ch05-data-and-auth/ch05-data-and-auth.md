# Chapter 5 — Data & Authentication

> MongoDB database schema and JWT authentication flow.

---

## Database — MongoDB (Single Store)

All application data lives in a single MongoDB database (`ai_medicare_assistant`). There is no MySQL or EF Core dependency.

**Driver:** `MongoDB.Driver` 3.4.0 (Infrastructure project), `MongoDB.Bson` 3.4.0 (Domain project for BSON attributes).

**Connection:** Connection string configured in `appsettings.json` → `ConnectionStrings:MongoDb`. Database name configured via `MongoDb:DatabaseName` (default: `ai_medicare_assistant`).

### Collections & Indexes

| Collection | Document | Index | Purpose |
|------------|----------|-------|---------|
| `users` | `UserDocument` | unique sparse `Email`, unique sparse `Phone`, unique sparse `UserId`, non-unique `FpId`, non-unique `FpgId`, non-unique `Role` | **Login / identity only:** email, phone, passwordHash, isEmailVerified, mustChangePassword, firstName, lastName, **role**, **fpgId**, **fpId**, timestamps. Decorated `[BsonIgnoreExtraElements]` so legacy unified documents read cleanly during the transition window. |
| `userProfiles` | `ProfileDocument` | unique `UserId` | **Personal / medical / address:** coverageYear, healthCondition, taxFilingStatus, magiTier, gender, tobaccoStatus, dateOfBirth, lifeExpectancy, concierge / conciergeAmount, alternateEmail / alternateMobile, full address (addressLine1, city, state, zipCode, county, countyCode, lat, lng), currentPrescriptionDocumentId, isProfileComplete, audit. Linked to `users` by `userId`. Created lazily on the first profile save. |
| `schemaMigrations` | _(BsonDocument)_ | `_id` (migration name) | One-doc-per-migration marker collection. Holds `{ appliedAt, migrated, skipped }` for the one-shot user/profile split migration so it never runs twice. |
| `financialPlannerGroups` | `FinancialPlannerGroupDocument` | unique `GroupId`, unique `Name` | Tenant entity — owns one or more users with role `financial_planner` |
| `prescriptions` | `PrescriptionDocument` | `(userId ASC, createdAt DESC)` | Named prescriptions with embedded drug list |
| `chatSessions` | `ChatSessionDocument` | **unique sparse** `userId`, `(userId ASC, updatedAt DESC)` | Chat/AI conversation history with rolling 200-message window. The unique index is `Sparse=true` so a missing/null `userId` never participates in the constraint (defends against the legacy-doc class of bug that was tripped during impersonation). |
| `userAnalysisSelections` | `UserAnalysisSelectionsDocument` | `(userId ASC)` unique | Per-user current analysis selections — confirmed drugs, selected pharmacies + plans, activeSection |
| `recommendations` | `RecommendationDocument` | `(userId ASC, createdAt DESC)` | Full analysis snapshots — profile, drugs, pharmacies, plan selections, cost snapshots |
| `ltcCurrentSelections` | `LtcCurrentSelectionsDocument` | `(userId ASC)` unique | Per-user LTC care-type inputs (health profile, care-year counts) + last projection result JSON |
| `logs` | _(Serilog-managed)_ | _(auto-created)_ | Structured application logs — written by `Serilog.Sinks.MongoDB` v6, 5-second batch period. During impersonation every log line is enriched with `ImpersonatedBy={fpUserId}` via `ImpersonationLoggingMiddleware`. |

### Data Flow

```
User signs up / logs in            → MongoDB (users — UserDocument created top-down with login fields only)
User completes profile screen      → MongoDB (userProfiles — ProfileDocument upserted on first save, IsProfileComplete=true)
User completes drug wizard         → MongoDB (userAnalysisSelections — drugs)
User selects pharmacies            → MongoDB (userAnalysisSelections — pharmacies)
User selects plans                 → MongoDB (userAnalysisSelections — plans + activeSection)
User saves analysis snapshot       → MongoDB (recommendations — full profile+drug+pharmacy+plan+cost doc)
Chat messages exchanged            → MongoDB (chatSessions — rolling 200-message window)
User saves prescription            → MongoDB (prescriptions — named drug list)
LTC wizard selections              → MongoDB (ltcCurrentSelections — care-type inputs + last result)
Application logs                   → MongoDB (logs — Serilog structured BSON logs, 5-sec batch) + File fallback (Logs/log-*.txt)
```

### .NET Integration
- **DI:** `IMongoClient` (singleton), `IMongoDatabase` (singleton), `MongoDbContext` (singleton with index creation on startup). Hosted services (executed in registration order on startup): `UserProfileSplitMigrationInitializer` → `MongoIndexInitializer` → `AdminSeedInitializer`. The split-migration runs first so the unique-`userId` index on `userProfiles` only ever sees post-split data.
- **Index cleanup on every startup.** `MongoIndexInitializer.StartAsync` runs two helpers before creating indexes: `DropLegacyPascalCaseIndexesAsync` (drops any index whose name starts with an uppercase letter — these are stale PascalCase-keyed indexes from before the `CamelCaseElementNameConvention` was registered, which silently indexed every doc as `null` and caused dup-key errors); `DropOptionDriftedIndexesAsync` (drops indexes whose options have drifted from the desired spec — currently handles `chatSessions.userId_1` which was non-sparse and is now sparse). Both are idempotent and safe to re-run.
- **Repositories (all scoped):** `IUserRepository`/`MongoUserRepository` (operates on `users`), `IProfileRepository`/`MongoProfileRepository` (operates on `userProfiles`), `IFinancialPlannerGroupRepository`/`MongoFinancialPlannerGroupRepository`, `IPrescriptionDocRepository`, `IChatSessionRepository`, `IUserAnalysisSelectionsRepository`, `IRecommendationRepository`, `ILtcSelectionsRepository`

---

## Authentication (JWT)

### Endpoints

| Method | Route | Body | Auth | Description |
|--------|-------|------|------|-------------|
| POST | `/api/auth/signin` | `{ email, password }` | Public | Login, returns JWT (with `role`, `fpgId?`, `fpId?`, `mustChangePassword` claims) |
| POST | `/api/auth/forgot-password` | `{ email }` | Public | Generates reset token, sends email |
| POST | `/api/auth/reset-password` | `{ token, newPassword, confirmPassword }` | Public | Reset password with token |
| POST | `/api/auth/verify-email` | `{ token }` | Public | Verify email address with token |
| POST | `/api/auth/resend-verification` | `{ email }` | Public | Resend email verification link |
| POST | `/api/auth/change-password` | `{ oldPassword, newPassword, confirmPassword }` | `[Authorize]` JWT | Change password for authenticated user; clears `MustChangePassword` and reissues a fresh JWT in the response |

> **Public sign-up was removed.** End-users are created exclusively by Financial Planners (see `POST /api/financial-planner/me/end-users` in [ch06-07](../ch06-api-contract/ch06-07-roles-impersonation-endpoints.md)). FP-admin and FP users are created top-down by Admin and FPG respectively.

### Security

- Passwords stored as **BCrypt** hashes (never plaintext).
- JWT tokens signed with HMAC-SHA256, configurable expiry (default 24h). Issued through `IJwtTokenIssuer` ([Application/Services/JwtTokenIssuer.cs](../../api-ai-medicare-assistant/AI.MedicareAssistant.Application/Services/JwtTokenIssuer.cs)) — single point of truth for sign-in tokens, post-change-password reissues, and impersonation tokens.
- **Claims on every JWT:** `NameIdentifier` (userId), `Email`, `Role` (`admin` | `financial_planner_group` | `financial_planner` | `user`), `mustChangePassword` (`"true"`/`"false"`), `Jti`. Plus `fpgId` for FPG/FP users, `fpId` for end-users, and `actingAs` (FP user id) on impersonation tokens.
- `TokenValidationParameters` explicitly sets `RoleClaimType = ClaimTypes.Role` so `[Authorize(Roles=…)]` works downstream ([AuthExtensions.cs](../../api-ai-medicare-assistant/AI.MedicareAssistant.Api/Extensions/AuthExtensions.cs)).
- Password reset tokens expire in 30 minutes with `purpose: password-reset` claim.
- Forgot password sends a reset link via email (SMTP: smtp.1and1.com:587, `support@aivante.com`). Returns success regardless of email existence (prevents enumeration).
- **Change password** verifies the old password with BCrypt, hashes the new one, **clears `MustChangePassword`**, and **reissues a fresh JWT** so the UI can swap tokens without re-login.
- JWT config in `appsettings.json` → `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryHours`.

### Admin seed (configuration)

The singleton admin user is seeded on startup by `AdminSeedInitializer` ([Infrastructure/Data/AdminSeedInitializer.cs](../../api-ai-medicare-assistant/AI.MedicareAssistant.Infrastructure/Data/AdminSeedInitializer.cs)) from three config keys:

| Key | Default | Notes |
|---|---|---|
| `Seed:AdminEmail` | `admin@aivante.com` | Lowercased + trimmed |
| `Seed:AdminPhone` | `5550199999` | Trimmed |
| `Seed:AdminPassword` | _(unset)_ | **Gates the seed.** Absent ⇒ no-op (production-safe by default). |

In docker, these are surfaced via `ADMIN_EMAIL` / `ADMIN_PHONE` / `ADMIN_PASSWORD` env vars mapped through `docker-compose.yml` to `Seed__AdminEmail` / `Seed__AdminPhone` / `Seed__AdminPassword`. Full walk-through in [ADMIN_SETUP.md](../ADMIN_SETUP.md) and [DOCKER.md](../DOCKER.md).

### Forced first-login password change

Every user created top-down (admin via seeder, FPG-admin via admin, FP via FPG, end-user via FP) is created with `MustChangePassword = true`. Two enforcement layers:

1. **Server:** [`MustChangePasswordFilter`](../../api-ai-medicare-assistant/AI.MedicareAssistant.Api/Filters/MustChangePasswordFilter.cs) — global `IAsyncActionFilter` registered in `CoreServicesExtensions`. Throws `UnauthorizedException` for any authenticated request other than `POST /api/auth/change-password` while the claim is `true`.
2. **UI:** [`mustChangePasswordGuard`](../../ui-ai-medicare-assistant/src/app/guards/must-change-password.guard.ts) — redirects every dashboard route to `/change-password` until the flag is cleared.

After a successful password change the API reissues the token (with `mustChangePassword=false`); the UI's change-password component calls `auth.handleAuthSuccess(res)` so the new token replaces the old one, releasing the guard.

---

## Roles & Authorization

Four roles, all stored as a string `Role` field on `UserDocument` (login document):

| Role constant | Created by | Lands on | What they can do |
|---|---|---|---|
| `admin` | Seeded once on API startup (see [ADMIN_SETUP.md](../ADMIN_SETUP.md)) | `/admin` | Create FPG-admin users (one click — backend auto-creates the underlying `FinancialPlannerGroup`). The group concept is hidden from the admin UI. No analysis access. |
| `financial_planner_group` (FPG) | Admin | `/fpg` | CRUD on Financial Planners in their group; read-only on end-users + recommendations across the group. |
| `financial_planner` (FP) | FPG | `/fp` | Create end-users; impersonate an end-user to walk the Medicare wizard on their behalf; view/delete their own recommendations. |
| `user` | FP | `/saved` | Existing Medicare / LTC analysis flow. |

Role constants live in [`Domain/Constants/UserRoles.cs`](../../api-ai-medicare-assistant/AI.MedicareAssistant.Domain/Constants/UserRoles.cs). `[Authorize(Roles = UserRoles.FinancialPlanner)]` etc. is enforced at the controller (or action) level. Service layer additionally re-fetches the target and verifies `target.FpId == caller` / `target.FpgId == caller.FpgId` for cross-tenant safety.

### Impersonation

An FP "acts as" one of their end-users to fill the Medicare wizard on their behalf. Implemented as a short-lived JWT swap:

- `POST /api/impersonate` — class-level `[Authorize]`, action-level `[Authorize(Roles = financial_planner)]`. Verifies caller is the target user's FP (`target.FpId == fpUserId`), then issues a 60-minute JWT with `NameIdentifier = endUserId`, `Role = "user"`, `actingAs = fpUserId`, `mustChangePassword = false` (overridden so the FP can act even if the end-user still hasn't reset their default password).
- `POST /api/impersonate/refresh` — class-level `[Authorize]` only. Reads `actingAs` from the impersonation token, reissues a fresh 60-minute token. Used by the UI's 5-minute "Continue impersonating?" prompt.
- All existing controllers (drug, profile, plans, etc.) keep working unchanged because they read `User.FindFirstValue(NameIdentifier)`, which now resolves to the impersonated user.
- Every request under impersonation is logged with `ImpersonatedBy={fpUserId}` via `ImpersonationLoggingMiddleware` (Serilog `LogContext.PushProperty`).

The UI auth-service swaps tokens via `auth.impersonate(targetUserId)` (saves FP creds to `*_original` sessionStorage keys) and restores them via `auth.exitImpersonation()`. Impersonation expiry is persisted to `auth_impersonation_expires` so the banner + timers survive page reload.

---

← [Chapter 4 — Backend Architecture](../ch04-backend-architecture/ch04-backend-architecture.md) | [Table of Contents](../APPLICATION_BLUEPRINT.md) | [Chapter 6 → API Contract](../ch06-api-contract/ch06-api-contract.md)
