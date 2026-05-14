# Chapter 6.1 — Auth, Profile & Reference Data Endpoints

> Authentication flows, profile CRUD, reference data, and county lookup.

← [API Contract Index](../ch06-api-contract/ch06-api-contract.md)

---

## Authentication

> **Public sign-up was removed.** All accounts are created top-down: the admin is seeded on startup, FPG-admins are created by the admin, FPs by FPGs, and end-users by FPs (`POST /api/financial-planner/me/end-users` — see [ch06-07](ch06-07-roles-impersonation-endpoints.md)). See [ADMIN_SETUP.md](../ADMIN_SETUP.md) for the full bootstrap flow.

### `POST api/auth/signin`

**Auth:** Public.

**Request:**
```json
{ "email": "user@example.com", "password": "Secret123!" }
```

**Response:**
```json
{
  "success": true,
  "message": "Signed in successfully.",
  "token": "<jwt>",
  "expiresAt": "2026-05-09T08:00:00Z",
  "user": {
    "id": "…",
    "email": "user@example.com",
    "phone": "5551234567",
    "role": "user",
    "fpgId": null,
    "fpId": "…",
    "mustChangePassword": false
  }
}
```

The JWT carries `NameIdentifier`, `Email`, `Role`, `mustChangePassword`, `Jti`, plus `fpgId` (FPG/FP) / `fpId` (end-user) / `actingAs` (impersonation tokens) when applicable.

---

### `POST api/auth/forgot-password`

**Auth:** Public.

**Purpose:** Generates a 30-minute reset token and sends a password-reset email. Returns `success: true` regardless of whether the email exists (prevents enumeration).

**Request:**
```json
{ "email": "user@example.com" }
```

**Response:**
```json
{ "success": true, "message": "If that email exists you will receive a reset link shortly." }
```

---

### `POST api/auth/reset-password`

**Auth:** Public.

**Purpose:** Validates the JWT reset token (must have `purpose: password-reset` claim, expires in 30 min) and updates the password hash.

**Request:**
```json
{ "token": "<reset-jwt>", "newPassword": "NewSecret123!", "confirmPassword": "NewSecret123!" }
```

**Response (success):**
```json
{ "success": true, "message": "Password reset successfully." }
```

**Response (failure):**
```json
{ "success": false, "message": "Invalid or expired reset token." }
```

---

### `POST api/auth/change-password`

**Auth:** `[Authorize]` — Bearer JWT required.

**Purpose:** Changes the password for the currently authenticated user. Old password is verified with BCrypt before writing the new hash.

**Request:**
```json
{ "oldPassword": "CurrentSecret123!", "newPassword": "NewSecret456!", "confirmPassword": "NewSecret456!" }
```

**Response (success):**
```json
{
  "success": true,
  "message": "Password changed successfully.",
  "token": "<fresh-jwt>",
  "expiresAt": "2026-05-09T08:00:00Z",
  "user": { "id": "…", "email": "…", "role": "…", "mustChangePassword": false }
}
```

**Response (failure — wrong old password):**
```json
{ "success": false, "message": "Current password is incorrect." }
```

> **Notes:**
> - `newPassword` minimum length is 8 characters.
> - `confirmPassword` must match `newPassword` (validated server-side via `[Compare]`).
> - `userId` is extracted from the `NameIdentifier` claim in the Bearer JWT — no userId in the request body.
> - On success the API **clears `MustChangePassword`** on the user document and **reissues a fresh JWT** with the flag cleared. The UI swaps tokens via `auth.handleAuthSuccess(res)` so the user is not forced to re-sign-in.
> - While `mustChangePassword=true` is in the principal's claims, the global `MustChangePasswordFilter` rejects every endpoint other than this one with HTTP 401.

---

## Reference Data Endpoint

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/reference-data` | Public | All master data for profile form dropdowns |

**Response:**
```json
{
  "genders": [{ "value": "Male", "label": "Male" }],
  "maritalStatuses": [{ "value": "Single", "label": "Single" }],
  "taxFilingStatuses": [{ "value": "Single", "label": "Single" }],
  "incomeFilingStatuses": [{ "value": "Single", "label": "Single" }],
  "magiTiersByFiling": {
    "Single": [
      { "value": "Tier 1", "label": "Tier 1 — ≤ 138% FPL (≤ $21,597)", "description": "Medicaid eligible" },
      { "value": "Tier 2", "label": "Tier 2 — 139–200% FPL ($21,598–$31,200)", "description": "Enhanced subsidies" },
      { "value": "Tier 3", "label": "Tier 3 — 201–250% FPL ($31,201–$39,000)", "description": "Cost-sharing reductions" },
      { "value": "Tier 4", "label": "Tier 4 — 251–400% FPL ($39,001–$62,400)", "description": "Premium tax credits" },
      { "value": "Tier 5", "label": "Tier 5 — > 400% FPL (> $62,400)", "description": "No subsidies" }
    ],
    "Married Filing Jointly": [
      { "value": "Tier 1", "label": "Tier 1 — ≤ 138% FPL (≤ $29,274)", "description": "Medicaid eligible" }
    ]
  },
  "tobaccoStatuses": [{ "value": "Non-user", "label": "Non-user" }],
  "disabilityStatuses": [{ "value": "None", "label": "None" }],
  "chronicConditions": [
    { "value": "Hypertension", "label": "Hypertension (High Blood Pressure)" },
    { "value": "Type 2 Diabetes", "label": "Type 2 Diabetes" }
  ],
  "usStates": [{ "value": "AL", "label": "Alabama" }],
  "householdSizes": [{ "value": 1, "label": "1 (Individual)" }, { "value": 2, "label": "2 people" }]
}
```

---

## Profile Endpoints

| Method | Route | Body | Auth | Description |
|--------|-------|------|------|-------------|
| GET | `/api/profile` | — | Bearer JWT | `UserProfileResponse` with profile + `isProfileComplete` |
| POST | `/api/profile` | `ProfileDto` | Bearer JWT | Save/update consolidated profile |

**ProfileDto:**
```json
{
  "firstName": "Bill",
  "lastName": "Mrs",
  "coverageYear": 2026,
  "healthCondition": 1,
  "taxFilingStatus": "MARRIED_FILING_JOINTLY",
  "magiTier": "Tier 1",
  "gender": "F",
  "tobaccoStatus": 0,
  "dateOfBirth": "1960-05-15",
  "concierge": 0,
  "conciergeAmount": null,
  "alternateEmail": null,
  "alternateMobile": null,
  "lifeExpectancy": 95,
  "addressLine1": "123 Main St",
  "city": "New York",
  "state": "NY",
  "zipCode": "10001",
  "county": "New York",
  "countyCode": "36061",
  "latitude": 40.7128,
  "longitude": -74.006
}
```

**Response:**
```json
{
  "profile": {
    "firstName": "Bill",
    "lastName": "Mrs",
    "coverageYear": 2026,
    "healthCondition": 1,
    "taxFilingStatus": "MARRIED_FILING_JOINTLY",
    "magiTier": "Tier 1",
    "gender": "F",
    "tobaccoStatus": 0,
    "dateOfBirth": "1960-05-15",
    "concierge": 0,
    "conciergeAmount": null,
    "alternateEmail": null,
    "alternateMobile": null,
    "lifeExpectancy": 95,
    "addressLine1": "123 Main St",
    "city": "New York",
    "state": "NY",
    "zipCode": "10001",
    "county": "New York",
    "countyCode": "36061",
    "latitude": 40.7128,
    "longitude": -74.006
  },
  "isProfileComplete": true
}
```

---

## County Lookup Endpoints

### `POST api/county-lookup/getCountycodeList`

**Auth:** Public (no JWT required).

**Purpose:** Returns county code entries for a given ZIP code. Used by the profile form for county/city cascading dropdowns.

**Request:**
```json
{
  "zipCode": "80112"
}
```

**Response:**
```json
[
  {
    "countyCode": "08005",
    "countyName": "Arapahoe",
    "stateCode": "CO",
    "city": "Centennial"
  }
]
```

### `GET api/county-lookup/constants/magi-tiers`

**Auth:** Public (no JWT required).

**Purpose:** Returns MAGI tier options for a given tax filing status and coverage year. Used by the profile form to populate the MAGI tier dropdown dynamically based on the user's selected tax filing status.

**Query Parameters:** `filingStatus` (required), `coverageYear` (required).

**Response:**
```json
[
  { "value": "Tier 1", "label": "Tier 1 — ≤ 138% FPL (≤ $21,597)", "description": "Medicaid eligible" },
  { "value": "Tier 2", "label": "Tier 2 — 139–200% FPL ($21,598–$31,200)", "description": "Enhanced subsidies" },
  { "value": "Tier 3", "label": "Tier 3 — 201–250% FPL ($31,201–$39,000)", "description": "Cost-sharing reductions" },
  { "value": "Tier 4", "label": "Tier 4 — 251–400% FPL ($39,001–$62,400)", "description": "Premium tax credits" },
  { "value": "Tier 5", "label": "Tier 5 — > 400% FPL (> $62,400)", "description": "No subsidies" }
]
```

---

← [API Contract Index](../ch06-api-contract/ch06-api-contract.md) | [Next: Drug Endpoints →](ch06-02-drug-endpoints.md)
