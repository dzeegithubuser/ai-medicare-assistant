# Chapter 3 — Prompt Architecture

> File-based prompt system powering all AI interactions.

---

## Directory Layout

```
Prompts/
 system/        → pharma-system.txt (system role instructions for drug analysis)
                → drug-name-suggestion-system.txt (system role for drug name identification)
                → pharmacy-pricing-system.txt (system role for pharmacy pricing AI)
                → plan-scoring-system.txt (system role for Medicare plan recommendation AI)
                → cost-evaluation-system.txt (system role for Medicare cost evaluation AI — financial advisor role, 8 rules)
                → gap-coverage-system.txt (system role for gap insurance advice AI — 8 rules for JSON output)
                → orchestrator-intent-system.txt (system role for 19-intent chatbot classifier — NLU for recommendation management)
                → delta-narrative-system.txt (system role for cost-impact narrative generation — 2-4 sentence plain-English summaries)
 tasks/         → drug-normalization.txt (task-specific instructions for full analysis)
                → drug-name-suggestion.txt (task instructions for drug name identification)
                → pharmacy-pricing.txt (task instructions for pharmacy pricing generation)
                → plan-scoring.txt (task instructions for Medicare plan scoring)
                → cost-evaluation.txt (task instructions for cost evaluation)
                → gap-coverage.txt (task instructions for gap insurance advice)
 schemas/       → drug-json-schema.txt (JSON output schema for full analysis)
                → drug-name-suggestion-schema.txt (JSON output schema for name suggestions)
                → pharmacy-pricing-schema.txt (JSON output schema for pharmacy pricing)
                → plan-scoring-schema.txt (JSON output schema for plan recommendations)
                → cost-evaluation-schema.txt (JSON output schema for cost evaluation with output rules)
                → gap-coverage-schema.txt (JSON output schema for gap insurance recommendations)
 templates/     → prescription-analysis.txt (user prompt template for drug analysis)
                → drug-name-suggestion.txt (user prompt template for name suggestions)
                → pharmacy-pricing.txt (user prompt template for AI pricing)
                → plan-scoring.txt (user prompt template for plan recommendations)
                → cost-evaluation.txt (user prompt template for cost evaluation)
                → gap-coverage.txt (user prompt template for gap insurance advice)
```

> **Note:** The `system/` directory also contains standalone prompt files for AI extraction services: `chat-intent-system.txt`, `profile-extract-system.txt`, `drug-selection-system.txt`, `pharmacy-selection-system.txt`, `plan-selection-system.txt`, `orchestrator-intent-system.txt`, and `delta-narrative-system.txt`.

## How It Works

Prompts are loaded from files and combined dynamically at runtime:

| AI Feature | System Prompt | Template | Placeholder Substitution |
|------------|--------------|----------|--------------------------|
| **Drug Name Suggestion** | `pharma-system.txt` | `drug-name-suggestion.txt` | Combined via `PromptBuilder.BuildDrugNameSuggestion()` (system + task + schema + template). `{{INPUT}}` replaced with user's raw text. |
| **Drug Analysis** | `pharma-system.txt` | `prescription-analysis.txt` | Combined via `PromptBuilder.Build()` (system + task + schema + template) |
| **Pharmacy Pricing** | `pharmacy-pricing-system.txt` | `pharmacy-pricing.txt` | `{{PHARMACY_LIST}}`, `{{PHARMACY_COUNT}}`, `{{DRUG_DESCRIPTIONS}}`, `{{ZIP_CODE}}` |
| **Plan Scoring** | `plan-scoring-system.txt` | `plan-scoring.txt` | `{{AGE}}`, `{{ZIP_CODE}}`, `{{COUNTY_NAME}}`, `{{MAGI_TIER}}`, `{{ANNUAL_INCOME}}`, `{{HOUSEHOLD_SIZE}}`, `{{FILING_STATUS}}`, `{{HAS_EMPLOYER_COVERAGE}}`, `{{DISABILITY_STATUS}}`, `{{HAS_CHRONIC_CONDITION}}`, `{{CHRONIC_DETAILS}}`, `{{LIS_TIER}}`, `{{DRUG_LIST}}`, `{{LIS_INSTRUCTIONS}}` |
| **Cost Evaluation** | `cost-evaluation-system.txt` | `cost-evaluation.txt` | Combined via `PromptBuilder.BuildCostEvaluation()` (system + task + schema + template). `{{PLAN_NAME}}`, `{{PLAN_BUNDLE_CODE}}`, `{{COVERAGE_YEAR}}`, `{{LIFE_EXPECTANCY}}`, `{{TAX_FILING_STATUS}}`, `{{STATE_NAME}}`, `{{LIFETIME_AB_MA_EXPENSES}}`, `{{LIFETIME_AB_MA_PREMIUM}}`, `{{LIFETIME_AB_MA_OOP}}`, `{{LIFETIME_D_SURCHARGE}}`, `{{LIFETIME_B_SURCHARGE}}`, `{{YEARLY_BREAKDOWN}}` |
| **Gap Coverage** | `gap-coverage-system.txt` | `gap-coverage.txt` | Combined via `PromptBuilder.BuildGapCoverage()` (system + task + schema + template). `{{PLAN_NAME}}`, `{{PLAN_TYPE}}`, `{{MISSING_COVERAGES}}` |
| **Orchestrator Intent Classification** | `orchestrator-intent-system.txt` | _(standalone system prompt + dynamic page context)_ | Loaded directly by `OrchestratorIntentService`. Defines 19 domain intents with parameter extraction rules + 55 few-shot examples. **Dynamic context injection:** at runtime, `PageContextBuilder.Build(currentPage)` appends a page-specific guidance block to the system prompt before the AI call. User message passed as-is in the user role. |
| **Delta Narrative** | `delta-narrative-system.txt` | _(standalone system prompt)_ | Loaded by `DeltaCalculationService`. Generates 2–4 sentence plain-English cost impact summary from before/after cost figures. User message contains the serialized delta data. |
| **Chat Intent Classification** | `chat-intent-system.txt` | _(standalone system prompt + dynamic page context)_ | Loaded by `ChatIntentService`. Classifies user messages into 20 intents (navigation, actions, plan switching, save analysis, run analysis, LTC navigation + input + projection) with parameter extraction for 11 profile fields + `prescriptionName` + `analysisName` + 4 LTC fields (`ltcHealthProfile`, `ltcAdultDayYears`, `ltcHomeCareYears`, `ltcNursingCareYears`). **Dynamic context injection:** `PageContextBuilder.Build(currentPage)` appends a page-specific guidance block to the system prompt (e.g., on `/medicare-analysis/drugs` an explicit profile-field phrase like "change my zip" maps to `NAVIGATE_PROFILE`, while a bare number/drug name does not). |
| **Profile Extraction** | `profile-extract-system.txt` | _(standalone system prompt)_ | Loaded by `ProfileExtractService`. Extracts profile fields (13 supported) from natural language. Receives user message + missing fields list. Returns extracted fields dict + conversational reply. |
| **Drug Selection Extraction** | `drug-selection-system.txt` | _(standalone system prompt)_ | Loaded by `DrugSelectionExtractService`. Extracts drug formulation selections (type, form, strength, qty) from chat. Supports actions: select, options, confirm_all, remove, edit. Receives user message + available drugs summary. Fuzzy matches drug names, forms, and strengths. |
| **Pharmacy Selection Extraction** | `pharmacy-selection-system.txt` | _(standalone system prompt)_ | Loaded by `PharmacySelectionExtractService`. Extracts pharmacy selection/removal/search commands from chat. Supports actions: select, remove, list, search. Receives user message + available pharmacies + selected pharmacies. Fuzzy matches pharmacy names. |
| **Plan Selection Extraction** | `plan-selection-system.txt` | _(standalone system prompt)_ | Loaded by `PlanSelectionExtractService`. Extracts plan selection/removal/section-switching commands from chat. Supports actions: select, remove, switch_section. Receives user message + available plans + selected plans. Matches plan names and types. |

## Key Prompt Rules

- **Drug Name Suggestion:** Instructs the AI to extract drug names from free-text input (ignoring dosages, frequencies, quantities), suggest up to 3 correct candidate names per input, and assign confidence scores. Returns only valid JSON with no fabricated drug names.
- **Drug Analysis:** System prompt includes strict rules to return `{ "drugs": [] }` for unrecognizable input — preventing hallucinated drug data. Instructs the AI to perform clinical intelligence analysis: drug-drug interactions, dosage validation against FDA ranges, duplicate therapy detection, therapeutic alternatives with cost comparison, generic switch suggestions, contraindication listing, and confidence scoring.  - **Formulations:** Each drug returns a `formulations[]` array of validated `{dosageForm, strength, packaging, ndcCode}` tuples. Every formulation must use real, FDA-listed package sizes — strengths must match the dosage form (e.g., suspension 125 mg/5 mL, not tablet-only strengths) and packaging must be logically consistent (e.g., "Bottle of N mL" for suspensions, "Bottle of N tablets" for tablets). The AI sets `ndcCode` to `null` — NDC codes are resolved by the backend from the authoritative RxNorm API.
  - **Packaging Format:** Descriptive `"<container> of <count>"` format required (e.g., "Bottle of 60 tablets", "Blister pack of 100 tablets", "Tube of 30 g"). Plain formats like "30 tablets" are rejected. Package counts must correspond to actual FDA-listed NDC package sizes (e.g., "Blister pack of 60 tablets" for Eliquis is invalid — the real blister pack is 100 tablets).
  - **Dosage Form → Container Mapping:** tablet/capsule → Bottle/Blister pack; suspension/solution → Bottle (mL); cream/ointment → Tube (g); injection → Vial/Syringe; inhaler → Canister; patch → Box; suppository → Box.- **Pharmacy Pricing:** System prompt includes 16 numbered rules across 3 categories (Output, Pricing, Safety). Task file details per-pharmacy-type pricing variation (warehouse 15-25% below chain, mail-order 20-30% below, etc.). Schema enforces strict JSON array structure.
- **Plan Scoring:** System prompt includes 19 numbered rules across 4 categories (Output, Plan Generation, Clinical, Safety). Task file includes pharmacy-aware scoring instructions — plans whose preferred networks include selected pharmacies rank higher. Plan category rules assign each plan one of 4 categories: `MA_ONLY` (MA with PDP included), `PDP_ONLY` (standalone PDP), `PDP_MEDIGAP` (PDP + Medigap like Plan G/N), `MA_PDP` (MA + separate PDP). Extended benefit rules instruct the AI to generate `networkType` (HMO/PPO/PFFS/HMO-POS), benefit flags (`includesDental`, `includesVision`, `includesHearing`, `includesFitness`, `includesOtc`, `mailOrderSavings`, `emergencyCoverage`), `otcAllowancePerQuarter`, `gapCoverage` (None/Some/Full), `providerNetworkSize` (Large/Medium/Small), and `pros`/`cons` bullet lists. Schema enforces exactly 5 plans with per-drug coverage entries and all extended fields.
- **Cost Evaluation:** System prompt establishes a Medicare financial advisor role with 8 rules for JSON output. Task file describes the evaluation objective: analyze year-by-year Medicare cost data to produce chart-ready insights. Schema enforces structured output with `lifetimeSummary`, `costTrajectory` (Rising/Stable/Declining/Mixed), `yearlyHighlights` (flagged years), `categories` (cost breakdown with percentages and trends), `savingsTips` (actionable recommendations with estimated savings and priority), and `overallAssessment`. Template injects plan details, lifetime totals, and full yearly breakdown data.

## Orchestrator Prompt Rules

- **Intent Classification (`orchestrator-intent-system.txt`):** Classifies user messages into exactly one of 19 domain intents (e.g., `create_recommendation`, `modify_drugs`, `update_demographic`, `view_projections`). Extracts structured parameters (field names, values, drug names, plan names). Returns strict JSON `{ "intent": "...", "params": { ... } }` with no preamble. 55+ few-shot examples cover disambiguation (health vs. demographic), compound phrases, slang, educational questions, and edge cases. Markdown code fences are stripped if present.
- **Delta Narrative (`delta-narrative-system.txt`):** Generates a 2–4 sentence plain-English summary (under 80 words) of a cost change. Highlights the direction of change (increase/decrease), the magnitude, and which cost categories are most affected. Uses markdown bold for key figures. Called by `DeltaCalculationService` when proposing profile changes with cost impact.
- **Chat Intent Classification (`chat-intent-system.txt`):** Classifies user messages into exactly one of 20 intents. Extracts structured parameters for 11 profile fields (firstName, lastName, gender, dateOfBirth, tobaccoStatus, healthCondition, taxFilingStatus, coverageYear, zipCode, addressLine1, lifeExpectancy) plus prescriptionName, analysisName, and 4 LTC fields (ltcHealthProfile 1–5, ltcAdultDayYears 0–20, ltcHomeCareYears 0–20, ltcNursingCareYears 0–20). Includes intents for save analysis (`ACTION_SAVE_ANALYSIS`), run analysis (`ACTION_RUN_ANALYSIS`), navigate to saved data (`NAVIGATE_SAVED_ANALYSES`), and 3 LTC intents (`NAVIGATE_LTC_CARE_TYPE`, `LTC_CARE_INPUT`, `ACTION_RUN_LTC_PROJECTION`). 60+ few-shot examples including 7 LTC examples. **Page-context injection:** `PageContextBuilder.Build(currentPage)` appends a trailing guidance block to the system prompt at call time, giving the AI page-specific disambiguation rules without modifying the prompt file itself.
- **Profile Extraction (`profile-extract-system.txt`):** Extracts 13 profile fields from conversational text. Knows required vs optional fields, asks for remaining fields in the reply. Returns `{ extractedFields: { ... }, reply: "..." }` JSON. Used for one-shot profile filling ("I'm John Smith, male, born 01/15/1955, ZIP 80113").
- **Drug Selection Extraction (`drug-selection-system.txt`):** Parses drug formulation commands from chat. 6 actions: select (apply formulation), options (show available), confirm_all, remove, edit. Fuzzy matches drug names ("lipitor" → Atorvastatin), dosage forms ("tab" → Tablet), strengths ("10mg" → "10 MG"). 8 few-shot examples including remove/edit.
- **Pharmacy Selection Extraction (`pharmacy-selection-system.txt`):** Parses pharmacy selection commands from chat. 4 actions: select, remove, list, search. Fuzzy matches pharmacy names ("cvs" → "CVS PHARMACY"). Prefers closest pharmacy on ambiguous partial matches. 6 few-shot examples.
- **Plan Selection Extraction (`plan-selection-system.txt`):** Parses plan selection/removal/section-switching commands from chat. 3 actions: select (choose a plan), remove (deselect a plan), switch_section (change between PDP and MA views). Matches plan names and types from available plans. Returns structured JSON with planName, planType, action, section, and reply.

## PageContextBuilder

`Application/Services/PageContextBuilder.cs` — internal static helper. Called by `ChatIntentService` and `OrchestratorIntentService` immediately before each AI call.

**Signature:** `string Build(string? currentPage)`

**Behaviour:** Returns an empty string when `currentPage` is null or unrecognized. Otherwise returns a short guidance block appended to the system prompt that instructs the AI how to interpret user input differently depending on the active Angular route.

| Route prefix | Injected guidance |
|---|---|
| `/medicare-analysis/drugs` | Explicit profile-field phrases (e.g., "change my zip", "update coverage year") → `NAVIGATE_PROFILE`. Bare numbers, drug names, or drug-action phrases → do not map to `NAVIGATE_PROFILE`. |
| `/medicare-analysis/profile` or `/profile` | Any field-like value (ZIP, year, name) → treat as a profile edit and classify `NAVIGATE_PROFILE`. |
| `/medicare-analysis/pharmacies` | Non-pharmacy inputs (profile fields, drug names) should be routed to their natural intent, not forced into pharmacy selection. |
| `/medicare-analysis/plans` | Plan selection phrases → plan selection intent. Profile-field phrases → `NAVIGATE_PROFILE`. |
| `/medicare-analysis/cost-projections` | Save/run phrases → `ACTION_SAVE_ANALYSIS` / `ACTION_RUN_ANALYSIS`. |
| `/long-term-care/profile` | LTC Profile step — profile-field changes → `NAVIGATE_PROFILE`. "next", "continue", "go to care type" → `NAVIGATE_LTC_CARE_TYPE`. |
| `/long-term-care/care-type` | LTC Care Type step — care-type values ("set nursing to 5 years", "quality best") → `LTC_CARE_INPUT` with parameter extraction. "run projection", "calculate ltc" → `ACTION_RUN_LTC_PROJECTION`. "go back", "profile" → `NAVIGATE_PROFILE`. |
| `/saved` | Navigate saved phrases → `NAVIGATE_SAVED_ANALYSES`. |
| Any other / null | No extra guidance appended. Base system prompt used as-is. |

**Key property:** The `.txt` prompt files are never modified — all page-specific logic lives in `PageContextBuilder`, keeping prompts clean and independently testable.

---

← [Chapter 2 — Frontend Architecture](ch02-frontend-architecture.md) | [Table of Contents](APPLICATION_BLUEPRINT.md) | [Chapter 4 → Backend Architecture](ch04-backend-architecture.md)
