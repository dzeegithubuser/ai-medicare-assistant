# Chapter 10.5 — Medicare Analysis: Cost Projections & Save

> Route: `/medicare-analysis/cost-projections`
> Covers cost projection fields, cost projections navigation guard, save analysis (plan page, UI button, chat), and expanded analysis persistence.

---

## 13. Cost Projection — Extended Fields

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 13.1 | Supplement plan type sent | Select PDP+Medigap plan (e.g., Plan G) → Calculate Lifetime Cost | Request includes `supplementPlanType: "G"`. |
| 13.2 | Part D OOP full year | Calculate cost from any plan | `partDOOPFullYear` sourced from `PharmacyWiseRecommendation.totalPrescriptionCostFullYear` (not same as `partDOOP`). |
| 13.3 | Total IRMAA in response | Evaluate costs | Response `lifetimeTotals.totalIrmaa` = `lifeTimeBSurcharge + lifeTimeDSurcharge`. |
| 13.4 | Supplement data in response | Evaluate costs for PDP+Medigap plan | Response `lifetimeTotals.supplementPlanType` and `supplementPlanPremium` populated. |
| 13.5 | AI prompt includes IRMAA | Evaluate costs | AI evaluation prompt includes `{{TOTAL_IRMAA}}`, `{{SUPPLEMENT_PLAN_TYPE}}`, `{{SUPPLEMENT_PLAN_PREMIUM}}` data. |
| 13.6 | All request fields sent | Calculate cost | Request includes: `supplementDataProvided`, `partDDataProvided`, `reserveDaysUsed`, `dental`, `dentalHealthGrade`, `boughtPlanA`, `medicareAdvantageDataProvided`, `partDPremium`, `calculateForAdjustedMonth`, `supplementPlanType`. |

---

## 24. Cost Projections Navigation Guard

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 24.1 | Direct URL with no projection | Navigate directly to `/medicare-analysis/cost-projections` with no prior cost evaluation | Redirected to `/medicare-analysis/plans`. State reset. |
| 24.2 | Valid projection | Run cost evaluation → navigate to cost-projections page | Page renders with all 5 charts + expense table + summary strip. |
| 24.3 | Browser refresh after evaluation | Run evaluation → F5 on cost-projections page | Redirected to `/medicare-analysis/plans` (signal state lost). |
| 24.4 | Back navigation | Use browser back from another page to cost-projections without state | Redirected to `/medicare-analysis/plans`. |

---

## 14. Save Analysis (Chat + UI Button + Plan Page)

### Save Analysis via Plan Page (Primary Path — Calculate Lifetime Cost)

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| SA.0 | Summary panel shows early | Select one MA plan (no gap Part D) | `SelectedPlansSummaryComponent` appears. Calculate button disabled. Amber hint visible. |
| SA.0a | Button enables | Select MA plan with Part D coverage OR add gap Part D | Calculate button enabled. Amber hint disappears. |
| SA.0b | Pre-populated name | Click Calculate (enabled) with profile first name "Jane" on MA section | Dialog opens with `"Jane Medicare Advantage – [today MM/DD/YYYY]"` pre-filled. |
| SA.0c | Full save flow | Complete name dialog → click Save | Eval → plans saved → recommendation saved → chat confirms → navigate to cost-projections. No `resetAll()` called. |
| SA.0d | State preserved after plan-page save | After navigation to cost-projections | Analysis state still intact (drugs, pharmacies, plans, cost projection). No wizard reset. |

### Save Analysis via UI Button (cost-projections page)

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| SA.1 | Save button visible | Navigate to `/medicare-analysis/cost-projections` with cost data loaded | "Save Analysis" button visible in header (mat-flat-button with assessment icon). |
| SA.2 | Save button opens dialog | Click "Save Analysis" button | `SavePrescriptionDialogComponent` opens with title "Save Analysis", subtitle "Enter a name for this analysis", icon "assessment". |
| SA.3 | Save with name | Enter "Heart Plan 2026" → click Save | `AnalysisSnapshotService.save("Heart Plan 2026")` called. Success: snackbar + chat confirmation. State reset. Navigated to `/medicare-analysis/profile`. |
| SA.4 | Cancel save dialog | Open dialog → click Cancel | Dialog closes. No API call. No state change. |
| SA.5 | Empty name disabled | Open dialog → leave name empty | Save button disabled. |
| SA.6 | Overwrite existing | Save → API returns 409 | Auto-retries with `force: true`. Saves successfully. State reset. |
| SA.7 | API error | Save → API returns 500 | Snackbar shows error. State NOT reset. User can retry. |

### Save Analysis via Chat

| # | Scenario | Input | Expected Result |
|---|----------|-------|-----------------|
| SA.8 | Save with name from chat | `"save analysis as Heart Plan"` | Intent: `ACTION_SAVE_ANALYSIS`. `analysisName: "Heart Plan"`. Saves directly (no dialog). State reset. |
| SA.9 | Save without name from chat | `"save my analysis"` | Intent: `ACTION_SAVE_ANALYSIS`. No `analysisName`. Dialog opens for name input. |
| SA.10 | Prerequisites not met | `"save analysis"` (no cost projection) | Assistant: "Please complete your analysis before saving." Lists unmet prerequisites. |
| SA.11 | Overwrite via chat | Save → 409 conflict | Assistant: "Analysis already exists. Overwrite? (yes/no)". `pendingSaveAnalysisOverwrite` set. |
| SA.12 | Confirm overwrite | `"yes"` after overwrite prompt | Re-saves with `force: true`. Success. State reset. `pendingSaveAnalysisOverwrite` cleared. |
| SA.13 | Cancel overwrite | `"no"` after overwrite prompt | Assistant: "Save cancelled." `pendingSaveAnalysisOverwrite` cleared. No state change. |

### Reset After Save

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| SA.14 | State reset on save success | Successfully save analysis (UI or chat) | `state.resetAll()` called. All signals cleared (drugs, pharmacies, plans, cost, data). SessionStorage cleared. |
| SA.15 | Navigate after reset | Save success | Router navigates to `/medicare-analysis/profile` (Profile step — step 1 of the analysis shell). |
| SA.16 | Wizard resets | Save success with wizard active | Wizard mode resets via `wizardResetTrigger`. Mode selection buttons re-appear. |
| SA.17 | Chat message after reset | Save success | Assistant message confirms save. New greeting may appear for fresh wizard. |

### AnalysisSnapshotService

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| SA.18 | canSave() — all met | Profile complete + drugs confirmed + pharmacies selected + plan selected + cost projection | `canSave()` returns `true`. |
| SA.19 | canSave() — missing profile | Profile incomplete | `canSave()` returns `false`. |
| SA.20 | canSave() — missing drugs | No confirmed drugs | `canSave()` returns `false`. |
| SA.21 | canSave() — missing pharmacy | No pharmacies selected | `canSave()` returns `false`. |
| SA.22 | canSave() — missing plan | No plan selected | `canSave()` returns `false`. |
| SA.23 | canSave() — missing cost | No cost projection | `canSave()` returns `false`. |
| SA.24 | Snapshot includes all data | Save analysis | Request body includes: profile snapshot, drugs array, pharmacies array, plans (with expanded fields), costSnapshot (with yearlyDetails + evaluation). |

---

## 15. Expanded Analysis Persistence for PDF

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| EP.1 | SelectedPlanDto expanded fields | Save analysis with plan selected | Saved plan includes: `deductible`, `starRating`, `totalPrescriptionCost`, `totalPlanCost`, `prescriptionDrugCovered`, `unavailableDrugs[]`, `planExpenses[]`. |
| EP.2 | Plan expenses populated | Save analysis | Each plan's `planExpenses` array contains expense items with `name` and `amount`. |
| EP.3 | Unavailable drugs list | Save analysis with uncovered drugs | `unavailableDrugs` array lists drug names not on the plan's formulary. |
| EP.4 | CostSnapshot yearly details | Save analysis | `costSnapshot.yearlyDetails[]` contains one entry per projection year with 16 financial fields (partAPremium, partBPremium, partBSurcharge, maPremium, partDPremium, partDSurcharge, conciergePremium, partAOOP, partBOOP, partDOOP, totalABMA, dentalPremium, dentalOOP, reserveDaysLeft, monthsUsed, year). |
| EP.5 | CostSnapshot evaluation | Save analysis | `costSnapshot.evaluation` includes full AI analysis: planName, planBundleCode, lifetimeSummary (5 fields), costTrajectory, trajectoryExplanation, yearlyHighlights[], categories[], savingsTips[], overallAssessment. |
| EP.6 | Supplement plan data | Save PDP+Medigap analysis | `costSnapshot.supplementPlanType` (e.g., "G") and `supplementPlanPremium` populated. |
| EP.7 | Lifetime totals preserved | Save analysis | `costSnapshot` includes lifetimeTotal, currentYearTotal, averageAnnual, projectionYears, lifetimePremiums, lifetimeOOP, lifetimeIrmaa, costTrajectory. |
| EP.8 | Backend document mapping | `POST /api/recommendation` with expanded data | Controller maps to MongoDB documents: `SelectedPlanDoc` (+7 fields), `CostSnapshotDoc` (+4 fields), `YearlyDetailDoc`, `CostEvaluationDoc`, `LifetimeSummaryDoc`, `YearlyHighlightDoc`, `CostCategoryDoc`, `SavingsTipDoc`, `PlanExpenseDoc`. |
| EP.9 | Round-trip fidelity | Save → `GET /api/recommendation/all` | Summary response includes correct `drugCount`, `planCount`, `hasCostSnapshot`, `lifetimeTotal` derived from saved documents. |

---

← [Testing Index](../ch10-testing-scenarios/ch10-testing-scenarios.md)
