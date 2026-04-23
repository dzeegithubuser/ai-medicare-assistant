# Chapter 6 — API Contract

> Endpoints, request/response schemas, and JSON examples.

---

## Sub-Files

| # | File | Topics | Endpoints |
|---|------|--------|-----------|
| 6.1 | [Auth, Profile & Reference Data](ch06-01-auth-profile-endpoints.md) | Sign up/in, password flows, profile CRUD, reference data, county lookup | 10 |
| 6.2 | [Drug Endpoints](ch06-02-drug-endpoints.md) | Drug name suggestion, full analysis, FP bulk search | 3 |
| 6.3 | [Pharmacy & Plan Endpoints](ch06-03-pharmacy-plan-endpoints.md) | Pharmacy lookup, plan recommendations, cost projections, FP plan APIs | 7 |
| 6.4 | [Chat Endpoints](ch06-04-chat-endpoints.md) | Session persistence, intent classification, profile/drug/pharmacy/plan extraction | 8 |
| 6.5 | [Recommendation & Prescription](ch06-05-recommendation-prescription-endpoints.md) | Recommendation CRUD, prescription save/load | 9 |
| 6.6 | [LTC Endpoints](ch06-06-ltc-endpoints.md) | Long Term Care projections, LTC selections | 3 |

**Total: 40 endpoints across 6 files**

---

## Endpoint Summary

```
Auth (Public)           POST signup / signin / forgot-password / reset-password
Auth (JWT)              POST change-password
Profile (JWT)           GET + POST /api/profile
Reference (Public)      GET /api/reference-data
County (Public)         POST getCountycodeList, GET magi-tiers

Drug (JWT)              POST suggest-names, POST analyze
FP Drug (JWT)           POST search-bulk

Pharmacy (JWT)          GET /api/pharmacy/lookup
Plans (JWT)             POST plan-recommendation, GET lis-check, POST evaluate-costs
FP Plans (JWT)          POST PartDPlan/recommend, POST MedicareAdvantagePlan/recommend, POST MedigapPlan/quotes

Chat (JWT)              GET session, PATCH messages, PATCH ui-state
                        POST intent, POST extract-profile
                        POST extract-drug-selection, POST extract-pharmacy-selection
                        POST extract-plan-selection

Recommendation (JWT)    GET (active), GET {id}, GET all, POST, PUT ×4, DELETE
Prescription (JWT)      POST, GET, GET {id}

LTC (JWT)               POST /api/long-term-care
LTC Selections (JWT)    PUT + GET /api/ltc/current
```

---

← [Chapter 5 — Data & Authentication](../ch05-data-and-auth/ch05-data-and-auth.md) | [Table of Contents](../APPLICATION_BLUEPRINT.md) | [Chapter 7 → Project Structure](../ch07-project-structure/ch07-project-structure.md)
