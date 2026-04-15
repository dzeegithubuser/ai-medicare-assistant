# Chapter 5 — Data & Authentication

> Database schema (MySQL / EF Core Code First + MongoDB document store) and JWT authentication flow.

---

## Database Schema

### Base Entity
All tables inherit: `Id` (GUID PK), `CreatedDate`, `ModifiedDate`, `CreatedBy`, `ModifiedBy`.

### Tables & Relationships

```
users (1) ──── (1) profiles
```

| Table | Key Columns | Constraints |
|-------|-------------|-------------|
| `users` | Email, Phone, PasswordHash | Email UNIQUE, Phone UNIQUE |
| `profiles` | UserId (FK), FirstName, LastName, CoverageYear, HealthCondition, TaxFilingStatus, MagiTier, Gender, TobaccoStatus, DateOfBirth, Concierge, ConciergeAmount, AlternateEmail, AlternateMobile, LifeExpectancy, AddressLine1, AddressLine2, Street, City, State, ZipCode, County, CountyCode, Latitude, Longitude | 1:1 with users, CASCADE delete |

### Provider
`Pomelo.EntityFrameworkCore.MySql` 9.0 with EF Core 9. Connection string configured in `appsettings.json` → `ConnectionStrings:DefaultConnection`. Uses `ServerVersion.AutoDetect`.

### Design-Time Factory
`AppDbContextFactory` enables migration generation without a running MySQL instance.

---

## MongoDB (Document Store)

### Purpose
Stores AI-generated results, session snapshots, chat history, and prescriptions — data that is deeply nested and evolves with AI prompt changes. MongoDB handles schema-flexible documents while MySQL retains relational integrity for user/profile data.

### Connection
Connection string configured in `appsettings.json` → `ConnectionStrings:MongoDb`. Database name configured via `MongoDb:DatabaseName` (default: `ai_medicare_assistant`).

### Collections & Indexes

| Collection | Document | Index | Purpose |
|------------|----------|-------|---------|
| `prescriptions` | `PrescriptionDocument` | `(userId ASC, createdAt DESC)` | Named prescriptions with embedded drug list |
| `chat_sessions` | `ChatSessionDocument` | `(userId ASC, createdAt DESC)` | Chat/AI conversation history with rolling 200-message window |
| `userAnalysisSelections` | `UserAnalysisSelectionsDocument` | `(userId ASC)` unique | Per-user current analysis selections — confirmed drugs, selected pharmacies + plans, activeSection |
| `recommendations` | `RecommendationDocument` | `(userId ASC, createdAt DESC)` | Full analysis snapshots — profile, drugs, pharmacies, plan selections, cost snapshots |
| `convStates` | `ConvStateDocument` | `(userId ASC)` unique + TTL on `expiresAt` | FSM chatbot conversation state (ConversationState enum, pendingChanges, collectedFields) |
| `ltcCurrentSelections` | `LtcCurrentSelectionsDocument` | `(userId ASC)` unique | Per-user LTC care-type inputs (health profile, care-year counts) + last projection result JSON |
| `logs` | _(Serilog-managed)_ | _(auto-created)_ | Structured application logs — written by `Serilog.Sinks.MongoDB` v6, 5-second batch period |

### Data Flow

```
User signs up / logs in            → MySQL (users, profiles)
User completes drug wizard      → MongoDB (userAnalysisSelections — drugs)
User selects pharmacies            → MongoDB (userAnalysisSelections — pharmacies)
User selects plans                 → MongoDB (userAnalysisSelections — plans + activeSection)
User saves analysis snapshot       → MongoDB (recommendations — full profile+drug+pharmacy+plan+cost doc)
Chat messages exchanged            → MongoDB (chat_sessions — rolling 200-message window)
AI chatbot FSM state               → MongoDB (convStates — TTL-based per-user FSM state)
User saves prescription            → MongoDB (prescriptions — named drug list)
LTC wizard selections              → MongoDB (ltcCurrentSelections — care-type inputs + last result)
Application logs                   → MongoDB (logs — Serilog structured BSON logs, 5-sec batch) + File fallback (Logs/log-*.txt)
```

### .NET Integration
- **Driver:** `MongoDB.Driver` 3.4.0 (Infrastructure project), `MongoDB.Bson` 3.4.0 (Domain project for BSON attributes)
- **DI:** `IMongoClient` (singleton), `IMongoDatabase` (singleton), `MongoDbContext` (singleton with index creation on startup)
- **Repositories (all scoped):** `IPrescriptionDocRepository`, `IChatSessionRepository`, `IUserAnalysisSelectionsRepository`, `IRecommendationRepository`, `IConvStateRepository`, `ILtcSelectionsRepository`

### Migrations

```bash
cd api-ai-medicare-assistant
dotnet ef migrations add <Name> --project AI.MedicareAssistant.Infrastructure --startup-project AI.MedicareAssistant.Api --context AppDbContext --output-dir Data/Migrations
dotnet ef database update --project AI.MedicareAssistant.Infrastructure --startup-project AI.MedicareAssistant.Api
```

---

## Authentication (JWT)

### Endpoints

| Method | Route | Body | Auth | Description |
|--------|-------|------|------|-------------|
| POST | `/api/auth/signup` | `{ email, phone, password, confirmPassword }` | Public | Create account, returns JWT |
| POST | `/api/auth/signin` | `{ email, password }` | Public | Login, returns JWT |
| POST | `/api/auth/forgot-password` | `{ email }` | Public | Generates reset token, sends email |
| POST | `/api/auth/reset-password` | `{ token, newPassword, confirmPassword }` | Public | Reset password with token |
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
