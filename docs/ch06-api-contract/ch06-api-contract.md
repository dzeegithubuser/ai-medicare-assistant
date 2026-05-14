# Chapter 6 — API Contract

> Endpoints, request/response schemas, and JSON examples.

---

## Sub-Files

| # | File | Topics | Endpoints |
|---|------|--------|-----------|
| 6.1 | [Auth, Profile & Reference Data](ch06-01-auth-profile-endpoints.md) | Sign-in, password flows, profile CRUD, reference data, county lookup. **Sign-up removed — accounts are created top-down (Admin → FPG → FP → end-user); see 6.7.** | 9 |
| 6.2 | [Drug Endpoints](ch06-02-drug-endpoints.md) | Drug name suggestion, full analysis, FP bulk search | 3 |
| 6.3 | [Pharmacy & Plan Endpoints](ch06-03-pharmacy-plan-endpoints.md) | Pharmacy lookup, plan recommendations, cost projections, FP plan APIs | 7 |
| 6.4 | [Chat Endpoints](ch06-04-chat-endpoints.md) | Session persistence, intent classification, profile/drug/pharmacy/plan extraction | 8 |
| 6.5 | [Recommendation & Prescription](ch06-05-recommendation-prescription-endpoints.md) | Recommendation CRUD, prescription save/load | 9 |
| 6.6 | [LTC Endpoints](ch06-06-ltc-endpoints.md) | Long Term Care projections, LTC selections | 3 |
| 6.7 | [Roles & Impersonation](ch06-07-roles-impersonation-endpoints.md) | Admin, FPG, FP CRUD + impersonation (start, refresh). Includes the bottom-up tenant tear-down chain: end-user cascade delete → FP delete → FPG-admin + group delete. | 20 |

**Total: 59 endpoints across 7 files**

---

## Endpoint Summary

```
Auth (Public)           POST signin / forgot-password / reset-password
                        POST verify-email / resend-verification
Auth (JWT)              POST change-password (clears MustChangePassword + reissues JWT)
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

Admin   (JWT, admin)    GET + POST + DELETE /api/admin/fpg-admin-users  ← preferred (auto-group, gated delete)
                        GET + POST /api/admin/financial-planner-groups,
                        POST /api/admin/financial-planner-groups/{id}/admin-user
                        (legacy, kept for back-compat)
FPG     (JWT, fpg)      GET /me, list/POST/PUT/DELETE /me/financial-planners,
                        GET /me/end-users, GET /me/recommendations
FP      (JWT, fp)       GET /me/end-users, POST /me/end-users, DELETE /me/end-users/{id} (cascade),
                        GET /me/recommendations, DELETE /me/recommendations/{id}
Impersonate             POST /api/impersonate (FP only),
                        POST /api/impersonate/refresh (any authed)
```

---

← [Chapter 5 — Data & Authentication](../ch05-data-and-auth/ch05-data-and-auth.md) | [Table of Contents](../APPLICATION_BLUEPRINT.md) | [Chapter 7 → Project Structure](../ch07-project-structure/ch07-project-structure.md)
