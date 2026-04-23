# Chapter 6.6 — LTC Endpoints

> Long Term Care projections and LTC selections persistence.

← [API Contract Index](../ch06-api-contract/ch06-api-contract.md)

---

## Long Term Care Endpoint

### `POST /api/long-term-care`

**Auth:** Bearer JWT required.

**Purpose:** Calculates Long Term Care cost projections using the Financial Planner LTC API. Backend reads user profile (DOB, gender, state, ZIP, countyCode, lifeExpectancy, tobaccoStatus) from DB and merges with care-type selections from the request body.

**Request:**
```json
{
  "healthProfile": 2,
  "numberOfAdultDayHealthCareLTCYears": 0,
  "numberOfHomeCareLTCYears": 3,
  "numberOfNursingCareLTCYears": 2
}
```

**Response:**
```json
{
  "age": 65,
  "healthProfile": 2,
  "gender": "M",
  "state": "FL",
  "zipcode": 33140,
  "countyCode": 11,
  "lifeExpenctancy": 95,
  "tobaccoUsage": false,
  "currentLifeStyleExpenses": 0,
  "numberOfHomeCareLTCYears": 3,
  "numberOfNursingCareLTCYears": 2,
  "homeCare": 95000,
  "presentValueHomeCare": 72000,
  "nursingCare": 180000,
  "presentValueNursingCare": 130000,
  "futureHomeCareExpenseList": [
    { "year": 2030, "expense": 28000 },
    { "year": 2031, "expense": 29400 },
    { "year": 2032, "expense": 30870 }
  ],
  "futureNursingCareExpensesList": [
    { "year": 2033, "expense": 89000 },
    { "year": 2034, "expense": 93450 }
  ],
  "expectedHomeCare": 95000,
  "presentValueExpectedHomeCare": 72000,
  "expectedNursingCare": 180000,
  "presentValueExpectedNursingCare": 130000
}
```

---

## LTC Selections Endpoints

### `PUT /api/ltc/current`

**Auth:** Bearer JWT required.

**Purpose:** Saves the user's current LTC care-type selections to MongoDB (`ltcCurrentSelections` collection). One document per user — upserted on each call.

**Request:**
```json
{
  "healthProfile": 2,
  "adultDayYears": 0,
  "homeCareYears": 3,
  "nursingCareYears": 2,
  "ltcResultJson": "{ ... serialized LtcProjectionResponse ... }"
}
```

**Response (200):** `{ "success": true }`

### `GET /api/ltc/current`

**Auth:** Bearer JWT required.

**Purpose:** Returns the most recently saved LTC selections for the current user.

**Response (200):** Same shape as PUT request body.
**Response (404):** No LTC selections saved yet.

---

← [API Contract Index](../ch06-api-contract/ch06-api-contract.md) | [← Recommendation & Prescription](ch06-05-recommendation-prescription-endpoints.md)
