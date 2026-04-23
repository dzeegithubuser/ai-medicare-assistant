# Chapter 10 � Testing Scenarios

> Manual test matrix covering all features, split into files following the UI route flow.

---

## File Index

| # | File | Route / Area | Sections |
|---|------|-------------|----------|
| 1 | [Auth & Profile](ch10-01-auth-profile.md) | `/signin`, `/signup`, `/profile` | 9 |
| 2 | [Medicare: Drugs](ch10-02-medicare-drugs.md) | `/medicare-analysis/fp-drugs` | 1, 1b, 2, 3, 4, 4b, 6b, 6c |
| 3 | [Medicare: Pharmacies](ch10-03-medicare-pharmacies.md) | `/medicare-analysis/pharmacies` | 5, 5c, 5d |
| 4 | [Medicare: Plans](ch10-04-medicare-plans.md) | `/medicare-analysis/plans` | 5b, 12g, 17, 23 |
| 5 | [Medicare: Cost & Save](ch10-05-medicare-cost-save.md) | `/medicare-analysis/cost-projections` | 13, 14, 15, 24 |
| 6 | [Chat Intent & Wizard](ch10-06-chat-intent-wizard.md) | Cross-cutting chat/wizard | 12a�12f, 12h�12l, 19, 20 |
| 7 | [Saved Data, Detail & Compare](ch10-07-saved-compare-detail.md) | `/saved`, `/saved/:id`, `/saved/compare` | 16, 25, 26, 27, 28, 29, 30 |
| 8 | [LTC Analysis](ch10-08-ltc-analysis.md) | `/long-term-care/*` | 21 |
| 9 | [Error Handling & Infrastructure](ch10-09-error-handling-infra.md) | Backend-only / cross-cutting | 7, 8, 10, 18, 22 |

---

### UI Route Flow

```
/signin ? /signup ? /forgot-password ? /reset-password ? /verify-email     ? ch10-01
    ?
/ ? /saved (dashboard redirect)                                              ? ch10-07
    ?
/medicare-analysis                                                           ? ch10-06 (wizard)
    +- /profile                                                              ? ch10-01
    +- /fp-drugs                                                             ? ch10-02
    +- /pharmacies                                                           ? ch10-03
    +- /plans                                                                ? ch10-04
    +- /cost-projections                                                     ? ch10-05
/long-term-care                                                              ? ch10-08
    +- /profile
    +- /care-type
    +- /projection
/saved ? /saved/:id ? /saved/compare                                        ? ch10-07
```

---

? [Chapter 9 � Roadmap](../ch09-roadmap/ch09-roadmap.md) | [Table of Contents](../APPLICATION_BLUEPRINT.md)
/signin -> /signup -> /forgot-password -> /reset-password -> /verify-email     <- ch10-01
    |
/ -> /saved (dashboard redirect)                                               <- ch10-07
    |
/medicare-analysis                                                             <- ch10-06 (wizard)
    +-- /profile                                                               <- ch10-01
    +-- /fp-drugs                                                              <- ch10-02
    +-- /pharmacies                                                            <- ch10-03
    +-- /plans                                                                 <- ch10-04
    +-- /cost-projections                                                      <- ch10-05
/long-term-care                                                                <- ch10-08
    +-- /profile
    +-- /care-type
    +-- /projection
/saved -> /saved/:id -> /saved/compare                                         <- ch10-07
```

---

<- [Chapter 9 -- Roadmap](../ch09-roadmap/ch09-roadmap.md) | [Table of Contents](../APPLICATION_BLUEPRINT.md)