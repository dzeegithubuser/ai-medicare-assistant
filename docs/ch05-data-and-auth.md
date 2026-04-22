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
| `users` | `UserDocument` | unique sparse `Email`, unique sparse `Phone`, unique sparse `UserId` | Merged user + profile data (email, phone, passwordHash, isEmailVerified, firstName, lastName, coverageYear, healthCondition, taxFilingStatus, magiTier, gender, tobaccoStatus, dateOfBirth, concierge, conciergeAmount, lifeExpectancy, address fields, currentPrescriptionDocumentId, isProfileComplete, timestamps) |
| `prescriptions` | `PrescriptionDocument` | `(userId ASC, createdAt DESC)` | Named prescriptions with embedded drug list |
| `chatSessions` | `ChatSessionDocument` | unique `userId`, `(userId ASC, updatedAt DESC)` | Chat/AI conversation history with rolling 200-message window |
| `userAnalysisSelections` | `UserAnalysisSelectionsDocument` | `(userId ASC)` unique | Per-user current analysis selections — confirmed drugs, selected pharmacies + plans, activeSection |
| `recommendations` | `RecommendationDocument` | `(userId ASC, createdAt DESC)` | Full analysis snapshots — profile, drugs, pharmacies, plan selections, cost snapshots |
| `ltcCurrentSelections` | `LtcCurrentSelectionsDocument` | `(userId ASC)` unique | Per-user LTC care-type inputs (health profile, care-year counts) + last projection result JSON |
| `logs` | _(Serilog-managed)_ | _(auto-created)_ | Structured application logs — written by `Serilog.Sinks.MongoDB` v6, 5-second batch period |

### Data Flow

```
User signs up / logs in            → MongoDB (users — UserDocument created at signup, profile fields updated later)
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
- **DI:** `IMongoClient` (singleton), `IMongoDatabase` (singleton), `MongoDbContext` (singleton with index creation on startup)
- **Repositories (all scoped):** `IUserRepository`/`MongoUserRepository`, `IProfileRepository`/`MongoProfileRepository`, `IPrescriptionDocRepository`, `IChatSessionRepository`, `IUserAnalysisSelectionsRepository`, `IRecommendationRepository`, `ILtcSelectionsRepository`

---

## Authentication (JWT)

### Endpoints

| Method | Route | Body | Auth | Description |
|--------|-------|------|------|-------------|
| POST | `/api/auth/signup` | `{ email, phone, password, confirmPassword }` | Public | Create account, returns JWT |
| POST | `/api/auth/signin` | `{ email, password }` | Public | Login, returns JWT |
| POST | `/api/auth/forgot-password` | `{ email }` | Public | Generates reset token, sends email |
| POST | `/api/auth/reset-password` | `{ token, newPassword, confirmPassword }` | Public | Reset password with token |
| POST | `/api/auth/verify-email` | `{ token }` | Public | Verify email address with token |
| POST | `/api/auth/resend-verification` | `{ email }` | Public | Resend email verification link |
| POST | `/api/auth/change-password` | `{ oldPassword, newPassword, confirmPassword }` | `[Authorize]` JWT | Change password for authenticated user |

### Security

- Passwords stored as **BCrypt** hashes (never plaintext).
- JWT tokens signed with HMAC-SHA256, configurable expiry (default 24h).
- Password reset tokens expire in 30 minutes with `purpose: password-reset` claim.
- Forgot password sends a reset link via email (SMTP: smtp.1and1.com:587, `support@aivante.com`). Returns success regardless of email existence (prevents enumeration).
- **Change password** (`POST /api/auth/change-password`) requires a valid Bearer JWT (`[Authorize]`). `userId` is extracted from the `NameIdentifier` claim. BCrypt verifies the old password before writing the new hash.
- JWT config in `appsettings.json` → `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryHours`.

---

← [Chapter 4 — Backend Architecture](ch04-backend-architecture.md) | [Table of Contents](APPLICATION_BLUEPRINT.md) | [Chapter 6 → API Contract](ch06-api-contract.md)
