# Chapter 8.2 — Drug Analysis Features

> Drug search, formulation selection, clinical intelligence, and Financial Planner drug integration.

← [Feature Catalog Index](../ch08-feature-catalog/ch08-feature-catalog.md)

---

## ✅ Two-Step Drug Name Search Engine
- **What:** AI-powered drug name verification before full analysis. Users enter prescriptions with typos, abbreviations, or ambiguous names — the system identifies correct drug names and asks users to confirm before running the full analysis pipeline.
- **Flow:**
  1. **Step 1 — Name Suggestion:** User submits free-text input (e.g. "Eliquis 50mg 3daily, Ibuprofen 800mg") → `POST /api/drug/suggest-names` → AI extracts drug names (ignoring dosage/frequency), returns up to 3 candidates per input with confidence scores and Brand/Generic type.
  2. **Step 2 — User Confirmation:** Chat panel shows interactive selection panel with clickable candidate chips per drug. High-confidence (≥0.95) or single-candidate drugs are auto-selected. User clicks to select correct names, then "Confirm & Analyze".
  3. **Step 3 — Full Analysis:** Confirmed drug names proceed through the analysis pipeline (AI analysis → validation → CMS/RxNorm enrichment → interaction merging).
- **Backend:** New `DrugNameRequest`/`DrugNameSuggestionResult` models. `IDrugAiService.SuggestDrugNames()` method. `PromptBuilder` assembles prompts from `tasks/drug-name-suggestion.txt`, `schemas/drug-name-suggestion-schema.txt`, `templates/drug-name-suggestion.txt`.
- **Frontend:** `DrugService.suggestNames()` HTTP method. `MedicareStateService` extended with `drugSuggestions`, `isVerifyingNames`, `hasSuggestions` signals. `ChatComponent` rewritten with two-step flow — `send()` calls Step 1, `confirmAndAnalyze()` triggers Step 2. Input disabled during name verification.
- **UX:** Cancel button clears suggestions and re-enables input. Unrecognizable inputs show empty candidates with error message.

---

## ✅ Medicare Plan Cost Estimation (CMS API Integration)
- **What:** Real-time lookup of Medicare Part D drug spending data from the official CMS open data API (`data.cms.gov`).
- **Backend:** `CmsMedicareCostService` queries the SOCRATA API by brand/generic drug name, returns structured `MedicareCostEstimate` with total claims, beneficiary counts, average costs, and total spending.
- **Integration:** `DrugService` enriches AI results with CMS data in parallel (`Task.WhenAll`). Graceful fallback — if CMS is unavailable, AI estimates still display.
- **Frontend:** New blue "CMS Medicare Part D Data" card section with official badge, showing 7 cost metrics with currency formatting.
- **Configuration:** CMS endpoint URL configurable via `CMS:MedicarePartDSpendingUrl` in `appsettings.json`.

---

## ✅ Drug Selection with Mandatory Configuration & Formulation Cascading
- **What:** Users must configure each drug by selecting one option from each of 4 categories before confirming. Formulation-based architecture ensures only valid combinations are presented.
- **Selection Workflow:** Each drug card shows a numbered "Configure Drug Selection" section:
  1. **Brand or Generic** — pick one brand name (amber) or generic (cyan).
  2. **Dosage Form** — pick one (green buttons). Resets strength + packaging when changed.
  3. **Strength** — pick one (teal buttons). Only shows strengths matching selected dosage form (filtered from formulations). Hidden until dosage form selected.
  4. **Packaging** — pick one (slate buttons). Only shows packaging matching selected dosage form + strength (filtered from formulations). Hidden until strength selected.
- **Formulation Cascading:** `DrugsStepComponent.getFilteredStrengths(drug)` and `getFilteredPackaging(drug)` filter from `drug.formulations` array. `getSelectedNdc(drug)` resolves exact NDC from the matched formulation tuple.
- **Visual Feedback:** Progress dots in card header. Green check per completed step. "Select Drug" disabled until all 4 done.
- **Selection State:** Selection state per card tracked in a local `Map<string, CardSelection>`. Confirmed drugs tracked in `Set<string>`.
- **Post-Confirm:** Panel collapses. Drug appears in `SelectedDrugsSummaryComponent` with edit/remove actions.

---

## ✅ Zipcode-Aware Drug Cost Estimation
- **What:** User's address zipcode is used for location-aware cost estimation.
- **Frontend:** ChatComponent reads profile zipcode.
- **Backend:** `DrugService` accepts `zipCode` parameter and stores it on `DrugAnalysisResult` for downstream consumers.

---

## ✅ Clinical Intelligence Layer
- **Drug Interaction Engine:** AI analyzes all pairwise interactions. RxNorm cross-validates via REST API. Results merged and deduplicated. Red alert panel with severity badges.
- **Dosage Validation:** AI validates against FDA-recommended ranges. Amber alert panel.
- **Duplicate Therapy Detection:** Identifies drugs in the same therapeutic class. Orange alert panel.
- **Chat Integration:** Assistant message summarizes clinical findings.

---

## ✅ RxNorm Normalization & Verification
- **Status:** `RxNorm/` directory exists but implementation is empty. NDC resolution is handled by `FdaNdcService` directly.
- **NDC Resolution:** FDA NDC Directory (`FdaNdcService`) fetches package descriptions for each NDC (e.g., "60 TABLET in 1 BOTTLE"), then matches each formulation's packaging string to the best-matching FDA package by scoring package size + container type.
- **Graceful degradation** on timeout or errors — AI-provided data remains available.

---

## ✅ Therapeutic Alternatives & Generic Substitution
- **Alternatives:** Up to 3 per drug with name, type, cost difference, and clinical notes. Indigo panel.
- **Generic Switch:** Brand → generic suggestion with estimated annual savings. Green panel.

---

## ✅ Medicare Part D Cost Breakdown by Phase
- **Phases:** Deductible → Initial Coverage → Coverage Gap (Donut Hole) → Catastrophic.
- **Status:** Backend model (`MedicareCostBreakdown`) retained. Per-drug UI panel removed from drug cards — cost breakdown data is available via API but not displayed inline.

---

## ✅ Confidence Scoring
- **Score range:** 0.0–1.0 indicating AI analysis reliability.
- **Frontend:** Color-coded badge in card header (green ≥90%, yellow ≥70%, red <70%) with tooltip.

---

## ✅ Contraindication Detection
- **What:** AI lists known contraindications for each drug.
- **Frontend:** Red panel with blocked-substance chips.

---

## ✅ Formulation-Based Drug Analysis (AI Prompt Architecture)
- **What:** AI returns pre-validated `{dosageForm, strength, packaging, ndcCode}` tuples instead of flat independent arrays, eliminating invalid combinations.
- **Prompt Rules:** Rule 7 in `pharma-system.txt` requires a `formulations` array with dosageForm→container mapping. AI sets `ndcCode` to `null` — NDC codes are resolved by the backend from the authoritative RxNorm API. Package counts must correspond to actual FDA-listed NDC package sizes (e.g., "Blister pack of 60 tablets" for Eliquis is invalid — the real blister pack is 100 tablets).
- **Backend Model:** `DrugFormulation` class with `DosageForm`, `Strength`, `Packaging`, `NdcCode` properties. `DrugResult.Formulations` list. `DrugValidationStep` auto-populates flat arrays from formulations for backward compatibility.
- **Frontend Model:** `Formulation` interface. `Drug.formulations` array. Cascading selection filters from formulations.

---

## ✅ Financial Planner Drug Details & AI Interaction Analysis
- **What:** After users confirm their drug selections, the system calls the Financial Planner API to retrieve extended formulation data (dosage forms, strengths, generic/brand variants) for each drug. When multiple drugs are selected, an AI evaluates all pairwise drug interactions and detects duplicate therapies with overlapping therapeutic classes.
  - **Flow:** User confirms all drugs in Step 1 → `DrugsStepComponent` auto-fetches via `DrugService.searchDrugsBulk(drugNames)` → `POST /api/FinancialPlannerDrug/search-bulk` → backend loops each drug through `drugSearch` + `getDrugDetailAdvance` APIs, computes `DrugType` (Generic/Branded) per formulation, then if >1 drug calls AI for interaction analysis → returns `BulkDrugSearchResponse` with per-drug results + interactions + duplicate therapies → frontend renders in `DrugsStepComponent` with per-drug formulation selection tables.
- **Backend:**
  - **Domain Models:** `DrugSearchRequest`, `DrugSearchResponse`, `DrugListItem`, `DrugDetailRequest`, `DrugDetailResponse`, `DrugDetailAdvanceItem` (with computed `DrugType`), `DrugSearchResult`, `BulkDrugSearchResponse`, `DrugInteractionAnalysis` — all in `Models/FinancialPlannerDrug.cs`.
  - **Interface:** `IFinancialPlannerDrugService` with `SearchBulkAsync` method.
  - **Service:** `FinancialPlannerDrugService` in `Infrastructure/FinancialPlanner/` — HTTP POST with Basic auth token from config. `SearchBulkAsync` loops all drugs through search → match by `displayName` → detail, then calls `EvaluateInteractionsAsync` (private) which sends drug names to `IChatClient` for pairwise interaction + duplicate therapy analysis. AI returns structured JSON parsed into `DrugInteractionAnalysis`.
  - **Controller:** `FinancialPlannerDrugController` — thin controller with `search-bulk` endpoint.
  - **DI:** `AddHttpClient<IFinancialPlannerDrugService, FinancialPlannerDrugService>` with 15s timeout.
- **Frontend:**
  - **Component:** `DrugsStepComponent` (`medicare-analysis/drug-step/`) — Drugs step (analysis shell step 2) with direct drug input + verification UI on the page, plus formulation selection workflow.
  - **Dual Entry for Drug Search:** Users can add drugs either from chat or directly in the Drugs page. Both paths use the same `ChatDrugFlowService` capability (name suggestion → candidate confirmation → `searchDrugsBulk`).
    - **`InteractionAlertsComponent`** — Severity-coded drug interaction cards (High=red, Moderate=amber, Low=blue). Input: `interactions`.
    - **`DuplicateTherapyAlertsComponent`** — Amber duplicate therapy warning cards. Input: `duplicateTherapies`.
    - **`DrugSelectionPanelComponent`** — 4-step guided selection (type→form→strength→qty/month) + confirm/edit. Exports `DrugSelectionState`. Computed signals: `availableTypes`, `availableDosageForms`, `availableStrengths`, `isReadyToConfirm`.
    - **`SelectedDrugsSummaryComponent`** — Confirmed drugs summary with edit/remove actions. Input: `confirmedDrugs`. Outputs: `editDrug`, `removeDrug`.
  - **Persistence:** Formulation selections, step states, quantities, and confirmed drug names persisted to/restored from `sessionStorage` keys `fp-formulation-selections`, `fp-drug-selections`, `fp-drug-quantities`, `fp-confirmed-drugs`.
  - **State:** `MedicareStateService` extended with `drugDetails` (`BulkDrugSearchResponse | null`), `isDrugDetailsLoading`, `hasDrugDetails` signals. Persisted to/restored from sessionStorage. Cleared on `resetAll()`.
  - **Service:** `DrugService.searchDrugsBulk(drugNames)` calls `POST /api/FinancialPlannerDrug/search-bulk`.
  - **Models:** `DrugListItem`, `DrugSearchResponse`, `DrugDetailAdvanceItem`, `DrugDetailResponse`, `DrugSearchResult`, `BulkDrugSearchResponse` — all in `drug.model.ts`.
  - **Trigger:** Auto-fetches on `ngOnInit` if confirmed drugs exist and drug details not yet loaded.
- **Config:** Reuses `FinancialPlanner:BaseUrl` and `FinancialPlanner:AuthToken` from `appsettings.json`.

---

## ✅ Drug Selection Confirm/Edit/Remove Workflow
- **What:** Single-panel accordion (MatExpansionPanel) where users configure, confirm, edit, and remove drug selections without leaving the card.
- **Components:**
  - `DrugSelectionPanelComponent` — 5-step accordion (name → dosage form → strength → packaging → confirm). Progressive reveal: steps 3–4 hidden until prior steps completed.
  - `SelectedDrugsSummaryComponent` — Shows confirmed drugs with edit/remove action buttons.
  - `ClinicalAlertsComponent` — Drug interactions + contraindications alert panel.
- **Flow:** Select all 4 options → "Select Drug" confirms → panel collapses → summary shows. "Edit" re-opens panel. "Remove" clears selection. Auto-advance to next unconfirmed drug.

---

← [Feature Catalog Index](../ch08-feature-catalog/ch08-feature-catalog.md) | [← Auth & Profile](ch08-01-auth-profile.md) | [Next: Pharmacy & Plans →](ch08-03-pharmacy-plans.md)
