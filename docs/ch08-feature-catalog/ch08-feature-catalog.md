# Chapter 8 — Feature Catalog

> The story of each implemented enhancement — what it does, how it works, and where it lives.

---

## Sub-Files

| # | File | Topics | Features |
|---|------|--------|----------|
| 8.1 | [Auth & Profile](ch08-01-auth-profile.md) | Authentication, email, profile onboarding, session security | 5 |
| 8.2 | [Drug Analysis](ch08-02-drug-analysis.md) | Drug search, formulation, CMS, clinical intelligence, FP drug details | 14 |
| 8.3 | [Pharmacy & Plans](ch08-03-pharmacy-plans.md) | Pharmacy lookup, plan recommendations, comparison, gap coverage, FP plans | 11 |
| 8.4 | [Cost Projections & Persistence](ch08-04-cost-persistence.md) | Lifetime cost projections, snapshots, save/load, saved data page | 8 |
| 8.5 | [Chat Features](ch08-05-chat-features.md) | Intent routing, wizard, chat-based profile/drug/pharmacy/plan selection | 9 |
| 8.6 | [LTC](ch08-06-ltc.md) | Long Term Care wizard, LTC chat integration | 2 |
| 8.7 | [Infrastructure](ch08-07-infrastructure.md) | MongoDB, logging, session lifecycle, reactivity fixes, HTTP subscription guards, disclaimers | 8 |

**Total: 57 features across 7 files**

---

## Feature Flow (Analysis Wizard Order)

```
Sign Up / Sign In ──→ Profile Onboarding ──→ Medicare Analysis Wizard
                                                │
                          ┌─────────────────────┼─────────────────────┐
                          ▼                     ▼                     ▼
                    Step 1: Drugs         Step 2: Pharmacy      Step 3: Plans
                    (ch08-02)             (ch08-03)             (ch08-03)
                          │                     │                     │
                          ▼                     ▼                     ▼
                    Drug Search &         FP Pharmacy           Plan Recommendations
                    Formulation           Lookup & Select       (AI + FP APIs)
                    Selection             (up to 5)             Gap Coverage
                          │                     │               Plan Comparison
                          └─────────────────────┼─────────────────────┘
                                                ▼
                                    Step 4: Cost Projections (ch08-04)
                                    Lifetime Cost → Charts → Save Analysis
                                                │
                                                ▼
                                    Saved Data Page (ch08-04)
                                    Detail View / Compare
                                    Illustration A/B Aliasing
                                    Orange/Green Color Coding

Chat Assistant (ch08-05) ──→ All steps accessible via natural language
LTC Wizard (ch08-06) ──→ Separate 2-step flow (Profile → Care Type → Projection)
```

---

← [Chapter 7 — Project Structure](../ch07-project-structure/ch07-project-structure.md) | [Table of Contents](../APPLICATION_BLUEPRINT.md) | [Chapter 9 → Roadmap](../ch09-roadmap/ch09-roadmap.md)