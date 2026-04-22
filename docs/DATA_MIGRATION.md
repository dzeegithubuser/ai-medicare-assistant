# Data Migration: MySQL → MongoDB

One-time migration to move `users` and `profiles` tables from MySQL into the MongoDB `users` collection.

## Prerequisites

- MySQL server running with existing data
- MongoDB server running (same instance the app connects to)
- `mongoimport` CLI tool (ships with MongoDB Database Tools)
- Node.js (for the transform step)

---

## Step 1 — Export MySQL Data as JSON

Run this query in any MySQL client. It joins `users` + `profiles` into a single JSON array:

```sql
SELECT JSON_ARRAYAGG(
  JSON_OBJECT(
    'userId',        BIN_TO_UUID(u.Id),
    'email',         u.Email,
    'phone',         u.Phone,
    'passwordHash',  u.PasswordHash,
    'isEmailVerified', IF(u.IsEmailVerified, true, false),
    'firstName',     COALESCE(p.FirstName, ''),
    'lastName',      COALESCE(p.LastName, ''),
    'coverageYear',  COALESCE(p.CoverageYear, 0),
    'healthCondition', COALESCE(p.HealthCondition, 1),
    'taxFilingStatus', COALESCE(p.TaxFilingStatus, 'MARRIED_FILING_JOINTLY'),
    'magiTier',      COALESCE(p.MagiTier, ''),
    'gender',        COALESCE(p.Gender, 'F'),
    'tobaccoStatus', COALESCE(p.TobaccoStatus, 0),
    'dateOfBirth',   COALESCE(DATE_FORMAT(p.DateOfBirth, '%Y-%m-%d'), null),
    'concierge',     COALESCE(p.Concierge, 0),
    'conciergeAmount', p.ConciergeAmount,
    'alternateEmail', p.AlternateEmail,
    'alternateMobile', p.AlternateMobile,
    'lifeExpectancy', COALESCE(p.LifeExpectancy, 95),
    'addressLine1',  COALESCE(p.AddressLine1, ''),
    'city',          COALESCE(p.City, ''),
    'state',         COALESCE(p.State, ''),
    'zipCode',       COALESCE(p.ZipCode, ''),
    'county',        COALESCE(p.County, ''),
    'countyCode',    COALESCE(p.CountyCode, ''),
    'latitude',      p.Latitude,
    'longitude',     p.Longitude,
    'currentPrescriptionDocumentId', p.CurrentPrescriptionDocumentId,
    'isProfileComplete', IF(p.Id IS NOT NULL, true, false),
    'createdAt',     DATE_FORMAT(u.CreatedDate, '%Y-%m-%dT%H:%i:%sZ'),
    'updatedAt',     DATE_FORMAT(u.ModifiedDate, '%Y-%m-%dT%H:%i:%sZ'),
    'createdBy',     u.CreatedBy,
    'modifiedBy',    u.ModifiedBy
  )
) AS result
FROM users u
LEFT JOIN profiles p ON p.UserId = u.Id;
```

> **Note:** If MySQL stores `Id` as `CHAR(36)` instead of `BINARY(16)`, replace `BIN_TO_UUID(u.Id)` with just `u.Id`. Check with `DESCRIBE users;`.

Save the output to `users_export.json`.

---

## Step 2 — Transform JSON for MongoDB

MongoDB needs `userId` as a UUID type and dates as `$date` objects. Save this as `transform.js`:

```js
const fs = require('fs');
const data = JSON.parse(fs.readFileSync('users_export.json', 'utf8'));

const transformed = data.map(u => ({
  ...u,
  userId: { $uuid: u.userId },
  coverageYear: u.coverageYear || 0,
  healthCondition: u.healthCondition || 1,
  tobaccoStatus: u.tobaccoStatus || 0,
  concierge: u.concierge || 0,
  lifeExpectancy: u.lifeExpectancy || 95,
  createdAt: { $date: u.createdAt },
  updatedAt: { $date: u.updatedAt }
}));

fs.writeFileSync('users_import.json', JSON.stringify(transformed, null, 2));
console.log(`Transformed ${transformed.length} user(s)`);
```

Run it:

```bash
node transform.js
```

---

## Step 3 — Import into MongoDB

```bash
mongoimport \
  --uri="mongodb://USER:PASSWORD@HOST:27017" \
  --db=ai_medicare_assistant \
  --collection=users \
  --file=users_import.json \
  --jsonArray
```

---

## Step 4 — Create Indexes

The app's `MongoIndexInitializer` creates indexes automatically on startup. To create them manually:

```js
// mongosh
use ai_medicare_assistant;

db.users.createIndex({ email: 1 }, { unique: true });
db.users.createIndex({ phone: 1 }, { unique: true });
db.users.createIndex({ userId: 1 }, { unique: true });
```

---

## Step 5 — Verify

```js
// mongosh
use ai_medicare_assistant;

// Count should match: SELECT COUNT(*) FROM users;
db.users.countDocuments();

// Spot-check a user
db.users.findOne({ email: "some-known-user@example.com" });

// Verify FK linkage with existing Mongo collections
const u = db.users.findOne({ email: "some-known-user@example.com" });
db.chatSessions.findOne({ userId: u.userId });
db.userAnalysisSelections.findOne({ userId: u.userId });
db.recommendations.findOne({ userId: u.userId });
db.ltcCurrentSelections.findOne({ userId: u.userId });
```

---

## Step 6 — Decommission MySQL

1. Deploy the updated app (MongoDB-only build)
2. Confirm all flows: sign-up → sign-in → profile → drugs → pharmacies → plans → cost → LTC → sign out/in
3. Take a final MySQL backup
4. Shut down MySQL server / remove from Docker config

---

## Alternative: Manual Insert (Small Datasets)

For a handful of users, insert directly in `mongosh`:

```js
db.users.insertOne({
  userId: UUID("paste-guid-from-mysql"),
  email: "user@example.com",
  phone: "5551234567",
  passwordHash: "$2a$11$...",   // copy verbatim from MySQL
  isEmailVerified: true,
  firstName: "John",
  lastName: "Doe",
  coverageYear: 2026,
  healthCondition: 3,
  taxFilingStatus: "Single",
  magiTier: "Tier 3",
  gender: "M",
  tobaccoStatus: 0,
  dateOfBirth: "1958-01-15",
  concierge: 0,
  conciergeAmount: null,
  alternateEmail: null,
  alternateMobile: null,
  lifeExpectancy: 95,
  addressLine1: "123 Main St",
  city: "Englewood",
  state: "CO",
  zipCode: "80113",
  county: "Arapahoe",
  countyCode: "08005",
  latitude: 39.6478,
  longitude: -104.9878,
  currentPrescriptionDocumentId: null,
  isProfileComplete: true,
  createdAt: new Date(),
  updatedAt: new Date(),
  createdBy: "migration",
  modifiedBy: "migration"
});
```

> **Critical:** The `userId` Guid must exactly match the value from MySQL. All existing MongoDB collections (`chatSessions`, `recommendations`, `userAnalysisSelections`, `ltcCurrentSelections`) reference it as a foreign key.
