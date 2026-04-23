# Chapter 10.2 — Medicare Analysis: Drugs

> Route: `/medicare-analysis/fp-drugs`
> Covers drug name suggestion, drug analysis backend, interactions, dosage validation, duplicate therapy, formulation cascading, and drug confirm/edit/remove.

---

## 1. Drug Name Suggestion — Two-Step Search

| # | Scenario | Input | Expected Result |
|---|----------|-------|-----------------|
| 1.0a | Single drug suggestion | `"Eliquis 50mg"` | Suggestion panel: "Eliquis" input → candidates include "Eliquis" (Brand) and "apixaban" (Generic). |
| 1.0b | Multiple drug suggestions | `"Eliquis 5mg, Ibuprofen 800mg, Naproxen 500mg"` | 3 suggestion rows. Each shows input name + candidate chips. |
| 1.0c | Misspelled drug | `"Eliqis 5mg"` | Suggestion with corrected candidate "Eliquis" (confidence < 1.0). |
| 1.0d | Non-drug input | `"hello world"` | Chat message: "No recognizable drug names found." No suggestion panel. |
| 1.0e | Auto-select high confidence | `"Metformin 500mg"` | Single candidate auto-selected (pre-filled cyan chip). |
| 1.0f | Select candidate | Click "apixaban" for Eliquis input | Chip turns cyan. Other chips deselected for that row. |
| 1.0g | Confirm all selected | Select all → click "Confirm & Analyze" | Loading spinner → full analysis results appear. |
| 1.0h | Confirm disabled | Leave one drug unselected | "Confirm & Analyze" button disabled. |
| 1.0i | Cancel suggestions | Click "Cancel" | Suggestion panel removed. Input re-enabled. Assistant message shown. |
| 1.0j | Input disabled during verify | Suggestion panel visible | Input field and send button are disabled. |
| 1.0k | Confirmed names in chat | Confirm selection | User message: "Confirmed drugs: Eliquis, Ibuprofen, Naproxen". |

---

## 1b. Drug Analysis — Backend Pipeline (Backend-Only, Not Used in UI)

> **Note:** The `POST /api/drug/analyze` endpoint and pipeline have been removed from the backend. Drug analysis in the UI uses the Financial Planner bulk search (`POST /api/FinancialPlannerDrug/search-bulk`). These scenarios are retained for reference only.

| # | Scenario | Input | Expected Result |
|---|----------|-------|-----------------|
| 1.1 | Single valid drug | `"Eliquis 5mg"` | 1 drug card with normalized name "apixaban", brand "Eliquis", dosage forms, formulations with validated strength+packaging+NDC tuples. RxNormId populated. |
| 1.2 | Multiple valid drugs | `"Eliquis 5mg, Metformin 500mg, Lisinopril 10mg"` | 3 drug cards. Each has complete metadata with formulations. Interactions may be detected. |
| 1.3 | Invalid drug (typo) | `"eliq"` | No drug cards. Chat shows "No valid drugs could be identified" message. |
| 1.4 | Mixed valid + invalid | `"Eliquis 5mg, xyzfake"` | Only 1 drug card (Eliquis). Invalid drug filtered out. |
| 1.5 | Non-drug text | `"hello how are you"` | No drug cards. Message about no valid drugs. |
| 1.6 | Drug with dosage | `"Metformin 2000mg"` | 1 drug card. May trigger dosage alert (exceeds typical range of 500–2000mg). |
| 1.7 | Formulations populated | `"Naprosyn"` | Formulations include tablet (250mg, 375mg, 500mg) and suspension (125 mg/5 mL) with correct packaging per form. |
| 1.8 | NDC per formulation | `"Eliquis 5mg"` | Each formulation has a non-empty ndcCode (resolved by RxNorm enrichment, not AI) in XXXXX-XXXX-XX format. |
| 1.9 | Flat arrays from formulations | `"Metformin 500mg"` | `strengths`, `packaging`, `ndcCodes` flat arrays populated from formulations for backward compatibility. |

---

## 2. Drug Interaction Engine (via Bulk Search)

> **Note:** Drug interactions are now detected by the Financial Planner bulk search pipeline (`POST /api/FinancialPlannerDrug/search-bulk`), which calls AI for pairwise interaction evaluation when >1 drug is submitted. The old RxNorm-based interaction merging pipeline is backend-only.

| # | Scenario | Input | Expected Result |
|---|----------|-------|------------------|
| 2.1 | Known high-severity interaction | Confirm "Warfarin" and "Aspirin" in drugs step | Red interaction alert card in `InteractionAlertsComponent`: Warfarin ↔ Aspirin, severity "High", bleeding risk description. |
| 2.2 | Moderate interaction | Confirm "Lisinopril" and "Potassium Chloride" | Amber interaction alert card: hyperkalemia risk. |
| 2.3 | No interactions | Confirm "Metformin" and "Omeprazole" | No interaction alert cards shown. |
| 2.4 | Multiple interactions | Confirm "Warfarin", "Aspirin", "Ibuprofen" | Multiple interaction cards: Warfarin↔Aspirin, Warfarin↔Ibuprofen, Aspirin↔Ibuprofen. |
| 2.5 | Single drug — no interaction check | Confirm only 1 drug | No interaction cards shown (AI interaction evaluation skipped for single drug). |

---

## 3. Dosage Validation (Backend-Only, Not Displayed in UI)

> **Note:** Dosage validation was part of the removed `POST /api/drug/analyze` pipeline. The current UI flow does not display dosage alerts. These scenarios are retained for reference only.

| # | Scenario | Input | Expected Result |
|---|----------|-------|-----------------|
| 3.1 | Normal dosage | `"Metformin 500mg"` | No dosage alert. |
| 3.2 | High dosage | `"Metformin 5000mg"` | Dosage alert: "Exceeds maximum recommended range" (max 2550mg/day). Severity "High". |
| 3.3 | Low dosage | `"Eliquis 0.5mg"` | Dosage alert: below recommended range (2.5–5mg). |

---

## 4. Duplicate Therapy Detection (via Bulk Search)

| # | Scenario | Input | Expected Result |
|---|----------|-------|------------------|
| 4.1 | Duplicate NSAIDs | Confirm "Ibuprofen" and "Naproxen" in drugs step | Orange duplicate therapy card in `DuplicateTherapyAlertsComponent`: "Both are NSAIDs". |
| 4.2 | Duplicate SSRIs | Confirm "Fluoxetine" and "Sertraline" | Duplicate therapy card: both are SSRIs. |
| 4.3 | No duplicates | Confirm "Eliquis" and "Metformin" | No duplicate therapy cards shown. |

---

## 4b. Formulation Cascading & Validation (FP Drugs Step)

> **Note:** Formulation selection is now done via `DrugSelectionPanelComponent` inside `DrugsStepComponent`. The 4-step flow is: Type (Generic/Branded) → Dosage Form → Strength → Quantity/Month. Data comes from the Financial Planner `getDrugDetailAdvance` API.

| # | Scenario | Steps | Expected Result |
|---|----------|-------|------------------|
| 4b.1 | Cascading: type → dosage forms | Confirm "Naprosyn" in drugs step, select "Branded" type | Step 2 shows dosage forms returned by `getDrugDetailAdvance` (e.g., "tablet", "suspension"). |
| 4b.2 | Cascading: dosage form → strengths | Select "Branded" → dosage form "tablet" | Step 3 shows tablet-only strengths (250mg, 375mg, 500mg). Suspension strengths not shown. |
| 4b.3 | Cascading: dosage form → strengths (suspension) | Select "Branded" → dosage form "suspension" | Step 3 shows only "125 mg/5 mL". Tablet strengths not shown. |
| 4b.4 | Cascading: strength → quantity | Select "tablet" → "375 mg" | Step 4 shows quantity/month options for the selected strength. |
| 4b.5 | Reset on dosage form change | Select "tablet" → "375 mg" → change to "suspension" | Strength and quantity selections cleared. New filtered strengths shown. |
| 4b.6 | Reset on strength change | Select "tablet" → "375 mg" → "30/month" → change strength to "250 mg" | Quantity selection cleared. New filtered quantities shown. |
| 4b.7 | Progressive reveal: strength hidden | Only type selected (no dosage form) | Step 3 (Strength) section not visible. |
| 4b.8 | Progressive reveal: quantity hidden | Type + dosage form selected, no strength | Step 4 (Quantity) section not visible. |
| 4b.9 | Generic vs Branded types | Confirm a drug that has both generic and branded | Step 1 shows both "Generic" and "Branded" radio buttons. Selecting each shows different formulation tree. |
| 4b.10 | Single type only | Confirm a drug with only generic available | Step 1 auto-selects "Generic". Only one option shown. |
| 4b.11 | Confirm with full selection | Complete all 4 steps → click "Select Drug" | Confirmed drug summary shows type, form, strength, quantity. Panel collapses. |
| 4b.12 | NDC code resolved | Complete all 4 steps | Selected NDC code appears in confirmed drug summary, matching the exact formulation. |
| 4b.13 | Confirmed drug keeps NDC mapping | Confirm selected formulations | Confirmed drug selection includes the exact formulation NDC mapping used by flow. |
| 4b.14 | drugs page input mirrors chat capability | On `/medicare-analysis/drugs`, enter "Eliquis 5mg, Metformin 500mg" in drugs page search | Suggestion chips appear on page. After Confirm & Analyze, drug details load and formulation cards render. |

---

## 6b. Drug Selection Confirm/Edit/Remove Workflow

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 6b.1 | Confirm drug | Complete all 4 selection steps → click "Select Drug" | Panel collapses. Drug appears in confirmed summary with all selections shown. |
| 6b.2 | Edit confirmed drug | Click "Edit" on confirmed drug | Selection panel re-opens with prior selections preserved. |
| 6b.3 | Remove confirmed drug | Click "Remove" on confirmed drug | Drug removed from confirmed list. Selection panel resets. |
| 6b.4 | Auto-advance | Confirm drug 1 | Panel auto-advances to next unconfirmed drug's selection panel. |
| 6b.5 | Multiple drugs confirmed | Confirm 3 drugs | All 3 appear in SelectedDrugsSummaryComponent list with individual edit/remove. |
| 6b.6 | Confirmed summary actions only | Confirm at least 1 drug | Confirmed summary shows edit/remove controls only (no save-prescription action). |
| 6b.7 | Remove all confirmed drugs | Remove all confirmed drugs | Confirmed summary section empties correctly. |

---

## 6c. Legacy Prescription API (Backend-Only)

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 6c.1 | Save endpoint authorization | Call `POST /api/prescription` without JWT | 401 Unauthorized. |
| 6c.2 | List endpoint authorization | Call `GET /api/prescription` without JWT | 401 Unauthorized. |
| 6c.3 | List prescriptions (legacy) | Call `GET /api/prescription` with JWT | Returns prescription documents ordered by most recent. |
| 6c.4 | Save payload contract (legacy) | Call `POST /api/prescription` with valid payload | Persists prescription with embedded drug fields including `ndcCode`. |

---

← [Testing Index](../ch10-testing-scenarios/ch10-testing-scenarios.md)
