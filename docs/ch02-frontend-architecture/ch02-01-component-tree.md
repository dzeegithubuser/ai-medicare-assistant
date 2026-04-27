# Chapter 2.1 — Component Tree

> Visual overview of all routed components and their nesting hierarchy.

← [Chapter 2 — Frontend Architecture (Index)](../ch02-frontend-architecture/ch02-frontend-architecture.md)

---

## Component Tree

```
App (root) → <router-outlet />
 ├── SigninComponent (/signin)
 ├── SignupComponent (/signup)
 ├── ForgotPasswordComponent (/forgot-password)
 ├── ResetPasswordComponent (/reset-password)
 ├── VerifyEmailComponent (/verify-email)
 └── DashboardComponent (/ — guarded by authGuard)
      ├── Header (gradient toolbar with app branding, user menu dropdown, footer)
      └── Main Split Layout
           ├── Left Panel: <router-outlet> (child routes)
           │    ├── /profile → UserProfileComponent
           │    │    └── Consolidated single-form profile
           │    ├── /medicare-analysis → AnalysisShellComponent (4-step wizard, guarded by profileCompleteGuard)
           │    │    ├── Step Indicator (1·Profile → 2·Drugs → 3·Pharmacies → 4·Plans) + Back/Continue nav bar
           │    │    └── <router-outlet> (wizard step child routes)
           │    │         ├── /medicare-analysis/profile → UserProfileComponent (same component as `/profile`; analysis step 1)
           │    │         ├── /medicare-analysis/drugs → DrugsStepComponent (Financial Planner drug search, formulation selection, AI interactions, duplicate therapies)
           │    │         ├── /medicare-analysis/pharmacies → PharmacyStepComponent (Financial Planner pharmacy lookup with filters, pagination, multi-select)
           │    │         ├── /medicare-analysis/plans → PlansStepComponent
           │    │         │    └── PlanRecommendationComponent (MA / Part D / Medigap plan cards, section chooser, selected-plans summary)
           │    │         └── /medicare-analysis/cost-projections → CostProjectionsComponent (Chart.js dashboards + Save Analysis button)
           │    ├── /long-term-care → LtcShellComponent (2-step LTC wizard, guarded by profileCompleteGuard)
           │    │    ├── Step Indicator (1·Profile → 2·Care Type) + Back/Continue nav bar
           │    │    └── <router-outlet> (wizard step child routes)
           │    │         ├── /long-term-care/profile → UserProfileComponent (reused; LTC step 1)
           │    │         ├── /long-term-care/care-type → LtcCareTypeStepComponent (quality of care + LTC years + "Run Projection" button)
           │    │         └── /long-term-care/projection → LtcProjectionStepComponent (result page — chart.js cost breakdown + present-value summary; not a stepper step)
           │    ├── /saved → RecommendationComponent (saved analyses with filter/sort/pagination + compare basket)
           │    │    ├── /saved/compare → RecommendationCompareComponent (side-by-side comparison)
           │    │    │    ├── CompareMedicareComponent (Medicare-vs-Medicare, 4-tab shell + SCSS active-tab styling)
           │    │    │    │    ├── CompareMedicareMetricsComponent (unified KPI metrics grid)
           │    │    │    │    ├── TabOverviewComponent (KPIs, winner, diffs, Rx, pharmacy, plans, projections)
           │    │    │    │    ├── TabProfileComponent (shared — 4 grouped sections with match column)
           │    │    │    │    ├── TabRxPharmacyPlansComponent (side-by-side Rx cards, pharmacy comparison, plan cards with star ratings)
           │    │    │    │    └── TabCostAnalysisComponent (Chart.js line + bar, delta table, assessment)
           │    │    │    ├── CompareLtcComponent (LTC-vs-LTC, 4-tab + SCSS active-tab styling)
           │    │    │    │    ├── CompareLtcMetricsComponent (unified KPI metrics grid)
           │    │    │    │    └── TabProfileComponent (shared)
           │    │    │    └── CompareCrossComponent (Medicare-vs-LTC, 3-tab + SCSS active-tab styling)
           │    │    │         ├── CompareCrossMetricsComponent (cross-type unified KPI metrics grid)
           │    │    │         └── TabProfileComponent (shared)
           │    │    └── /saved/:id → RecommendationDetailComponent (full detail view with 5 tabs + Chart.js)
           │    └── /change-password → ChangePasswordComponent
           └── ChatComponent (right panel, fixed 420px — visible on /medicare-analysis/* and /long-term-care/* routes)

  Shared Component Library (shared/)
  ├── AuthFormShellComponent — reusable auth form card shell (gradient bg, icon, title, form projection, footer link)
  ├── LoadingSpinnerComponent — shared spinner with optional message
  ├── EmptyStateComponent — shared empty state card (icon, title, subtitle)
  ├── ErrorAlertComponent — shared error alert banner
  ├── KpiCardComponent — shared KPI metric card (label, value, icon, color)
  ├── SectionHeaderComponent — shared section header (icon, title, subtitle)
  └── ErrorDialogComponent — Material Dialog for global API error popups
```

---

← [Chapter 2 — Frontend Architecture (Index)](../ch02-frontend-architecture/ch02-frontend-architecture.md)
