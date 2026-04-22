# Chapter 8 — Feature Catalog

> The story of each implemented enhancement — what it does, how it works, and where it lives.

---

## ✅ Two-Step Drug Name Search Engine
- **What:** AI-powered drug name verification before full analysis. Users enter prescriptions with typos, abbreviations, or ambiguous names — the system identifies correct drug names and asks users to confirm before running the full analysis pipeline.
- **Flow:**
  1. **Step 1 — Name Suggestion:** User submits free-text input (e.g. "Eliquis 50mg 3daily, Ibuprofen 800mg") → `POST /api/drug/suggest-names` → AI extracts drug names (ignoring dosage/frequency), returns up to 3 candidates per input with confidence scores and Brand/Generic type.
  2. **Step 2 — User Confirmation:** Chat panel shows interactive selection panel with clickable candidate chips per drug. High-confidence (≥0.95) or single-candidate drugs are auto-selected. User clicks to select correct names, then "Confirm & Analyze".
  3. **Step 3 — Full Analysis:** Confirmed drug names sent to existing `POST /api/drug/analyze` pipeline (AI analysis → validation → CMS/RxNorm enrichment → interaction merging → pharmacy pricing).
- **Backend:** New `DrugNameRequest`/`DrugNameSuggestionResult` models. `IDrugAiService.SuggestDrugNames()` method. `PromptBuilder` assembles prompts from `tasks/drug-name-suggestion.txt`, `schemas/drug-name-suggestion-schema.txt`, `templates/drug-name-suggestion.txt`.
- **Frontend:** `DrugService.suggestNames()` HTTP method. `DrugStateService` extended with `drugSuggestions`, `isVerifyingNames`, `hasSuggestions` signals. `ChatComponent` rewritten with two-step flow — `send()` calls Step 1, `confirmAndAnalyze()` triggers Step 2. Input disabled during name verification.
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

## ✅ MongoDB (Single Database)
- **Driver:** `MongoDB.Driver` 3.4.0 with `MongoDB.Bson` 3.4.0.
- **User Document:** `UserDocument` merges user credentials and profile fields into a single document in the `users` collection. Unique indexes on `Email`, `Phone`, and `UserId`.
- **Collections:** `users`, `prescriptions`, `chat_sessions`, `userAnalysisSelections`, `recommendations`, `ltcCurrentSelections`, `logs`.
- **Repositories:** `MongoUserRepository`, `MongoProfileRepository` (both operate on `users` collection), plus `PrescriptionDocRepository`, `ChatSessionRepository`, `UserAnalysisSelectionsRepository`, `RecommendationRepository`, `LtcSelectionsRepository`.

---

## ✅ JWT Authentication (Sign Up / Sign In / Forgot Password / Reset Password / Change Password)
- **What:** Complete auth flow with JWT token-based authentication and full password lifecycle management.
- **Sign Up:** Validates uniqueness of email + phone, hashes password with BCrypt, creates user, returns JWT.
- **Sign In:** Validates email + BCrypt password verification, returns JWT on success.
- **Forgot Password:** Generates a 30-minute reset token. Sends an HTML reset-link email via SMTP (`EmailService`). Returns success regardless of email existence to prevent enumeration.
- **Reset Password:** Validates reset token (`purpose: password-reset` claim), updates password hash. Token expires in 30 minutes.
- **Change Password:** `[Authorize]` endpoint. Extracts `userId` from Bearer JWT `NameIdentifier` claim. BCrypt-verifies old password before writing new hash.
- **Security:** HMAC-SHA256 signed tokens, configurable expiry, ClockSkew=Zero.

---

## ✅ Email Service (SMTP)
- **What:** Transactional email delivery for password reset links.
- **Implementation:** `IEmailService` / `EmailService` in the Infrastructure layer. Registered as scoped in DI.
- **Transport:** SMTP via `smtp.1and1.com:587` with STARTTLS, sender `support@aivante.com`. Credentials in `appsettings.json` → `Email:SmtpHost`, `Email:SmtpPort`, `Email:Username`, `Email:Password`, `Email:FromAddress`, `Email:FromName`.
- **Usage:** `ForgotPasswordAsync` in `AuthService` calls `IEmailService.SendPasswordResetEmailAsync(to, resetLink)`. Sends an HTML email containing the `/reset-password?token=<jwt>` link.
- **Security:** Token is never returned in the HTTP response — it travels exclusively through the email channel.

---

## ✅ Frontend Auth Components & Routing
- **Components:** SigninComponent, SignupComponent, ForgotPasswordComponent, ResetPasswordComponent, ChangePasswordComponent.
- **ResetPasswordComponent:** Public route `/reset-password`. Reads `?token=` from URL; redirects to `/forgot-password` if missing. Two-field form (newPassword + confirmPassword with cross-field match validator). On success shows green banner then auto-navigates to `/signin` after 2 s.
- **ChangePasswordComponent:** Authenticated route `/change-password` (inside `authGuard` dashboard children). Three-field form (oldPassword + newPassword + confirmPassword). On success shows green banner then auto-navigates to `/` (dashboard) after 2 s. Cancel button returns immediately.
- **AuthService:** Signal-based state with `currentUser` and `isAuthenticated` signals, sessionStorage persistence (not localStorage — session ends on tab close). 1-hour token expiry with auto-refresh on activity.
- **Auth Interceptor:** `HttpInterceptorFn` that attaches `Authorization: Bearer <token>` header — no manual header management needed in components or services.
- **Auth Guard:** `CanActivateFn` protecting the Dashboard route.
- **Routing:** Lazy-loaded via `loadComponent`. App component simplified to `<router-outlet />`.
- **Styling:** Centered auth cards with cyan gradient background, pharmacy branding.

---

## ✅ First-Time User Profile Onboarding
- **What:** Consolidated single-form profile completion shown before medicare analysis access.
- **Detection:** Dashboard calls `GET /api/profile` and the post-login dashboard redirect always lands on `/profile` (profile complete or incomplete).
- **Landing behavior by state:**
  - **Profile complete:** profile opens in **view mode** (read-only) with a **Modify Profile** action.
  - **Profile incomplete:** profile opens in **create mode** and chat instructs user to complete profile before analysis.
- **Fields:** First name (required, alphabetic with separators), last name (required, same pattern), coverage year (radio), health profile (dropdown), tax filing status (radio), MAGI tier (dropdown, depends on tax filing + coverage year), gender (radio), tobacco status (radio), date of birth (datepicker, 18+ validator), concierge (radio), concierge amount (conditional input), alternate email (optional), alternate mobile (optional, US phone), life expectancy (65-120, default 95), plus all address fields with ZIP-based county/city cascading dropdowns.
- **Name Validation:** Pattern `^[A-Za-z]+([' -][A-Za-z]+)*$` — supports names like John, Mary-Jane, O'Connor, Anne Marie.
- **Save Flow:** Single `POST /api/profile` saves all fields. Auto-navigates to `/medicare-analysis`. Note: the explicit "Save Profile" button has been removed from the profile form — saving is triggered by the wizard’s Continue button when the user is embedded at `/medicare-analysis/profile`, keeping the form clean for the first-time onboarding flow.
- **Backend:** `ProfileController` extracts UserId from JWT. `ProfileService` creates or updates the consolidated `Profile` entity.

---

## ✅ Zipcode-Aware Drug Cost Estimation
- **What:** User's address zipcode is used for location-aware cost estimation.
- **Frontend:** ChatComponent reads profile zipcode.
- **Backend:** `DrugService` accepts `zipCode` parameter and stores it on `DrugAnalysisResult` for downstream consumers.

---

## ✅ Global Exception Handling & Structured Logging
- **Custom Exception Hierarchy:** `AppException` (abstract base), `NotFoundException` (404), `ValidationException` (400), `UnauthorizedException` (401), `ConflictException` (409).
- **GlobalExceptionMiddleware:** Maps exceptions to HTTP status codes. Logs 5xx as `Error`; 4xx as `Warning`. Returns `{ status, message, traceId, errors? }`.
- **Serilog (3-tier sink hierarchy):**
  1. **MongoDB (primary)** — `Serilog.Sinks.MongoDB` v6, structured BSON logs to `logs` collection, 5-second batch period.
  2. **Console** — development convenience.
  3. **File (fallback)** — daily rolling file (`Logs/log-.txt`), 30-day retention.
  - Bootstrap logger (console + file only) before DI host builds. `appsettings.json` → `Serilog:MinimumLevel` config (Default: Information, overrides: Microsoft.AspNetCore/EFCore/HttpClient → Warning).
  - `UseSerilogRequestLogging()` for automatic HTTP request logging.
- **Service-Level Logging:** All application + infrastructure services inject `ILogger<T>` with structured logging. 29 catch blocks pass the exception object (full stack trace preserved). Fixed silent catches: `AuthService` bare catch → `LogWarning`; `ChatSessionRepository` → added `ILogger`, `BsonSerializationException` logged with warning.

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

## ✅ Nearby Pharmacy Search & AI-Powered Per-Pharmacy Pricing (On-Demand)
- **What:** Finds nearby pharmacies and generates per-pharmacy, per-drug pricing via AI. Triggered on-demand when user clicks "Find Nearby Pharmacies" button below drug cards.
- **APIs:** IChatClient for pharmacy-specific AI pricing.
- **Backend:** `Pharmacy/` directory contains `FinancialPlannerPharmacyService`. Pharmacy data is fetched via the `GET /api/pharmacy/lookup` endpoint (Financial Planner getPharmacies API).
- **Graceful degradation chain:** AI pricing → fallback prices → `null` ("—").
- **Frontend:** Pharmacy selection is handled within the `PharmaciesStepComponent` in the Medicare analysis wizard.
- **Design Principles:** All changes additive. No database migrations. Optional fields. Graceful degradation. On-demand loading reduces initial analysis time.

---

## ✅ Lightweight Nearby Pharmacy Lookup
- **What:** Finds nearby pharmacies without pricing — only location data. Legacy endpoint kept for backward compatibility.
- **Endpoint:** `GET /api/pharmacy/nearby?zip=` — returns `PharmacyResult[]` (name, address, phone, fax, pharmacy type). Falls back to user's saved zip.
- **Purpose:** Separates pharmacy discovery from pricing. Superseded by Financial Planner Pharmacy Lookup (below) as the primary pharmacy source for step 2.

---

## ✅ Financial Planner Pharmacy Lookup (Primary)
- **What:** Fetches paginated pharmacies near the user via the Financial Planner `getPharmacies` API using lat/lng from the user profile. Primary pharmacy source for step 2 of the wizard — replaces the NPI-only lookup in the UI while keeping the NPI code intact.
- **Endpoint:** `GET /api/pharmacy/lookup?page=1&size=20&radius=25&name=CVS` — returns `PharmacyLookupResponse` with paginated pharmacy list, total count, total pages, and search radius.
- **Backend:**
  - **Interface:** `IPharmacyLookupService` in `Domain/Interfaces/IPharmacyLookupService.cs` — defines `GetPharmaciesAsync(PharmacyLookupRequest)`. Request/response models co-located: `PharmacyLookupRequest` (Lat, Lng, Radius, Name, Page, Size), `PharmacyLookupResponse` (pharmacies, page, size, totalPharmacies, totalPages, searchRadiusInMiles), `PharmacyLookupEntry` (pharmacyNumber, pharmacyName, latitude, longitude, address, distance, zipcode).
  - **Service:** `FinancialPlannerPharmacyService` in `Infrastructure/Pharmacy/` — HTTP GET to `{baseUrl}/getPharmacies` with JSON body, Basic auth from config. 15s timeout.
  - **Controller:** `PharmacyController.Lookup()` reads lat/lng from user profile via `ProfileService`, returns 400 if missing.
  - **DI:** `AddHttpClient<IPharmacyLookupService, FinancialPlannerPharmacyService>` with 15s timeout.
- **Frontend:**
  - **Models:** `PharmacyLookupEntry`, `PharmacyLookupResponse` interfaces in `drug.model.ts`.
  - **Service:** `DrugService.lookupPharmacies({ page?, size?, radius?, name? })` — `GET /api/pharmacy/lookup` with query params.
  - **State:** `DrugStateService` extended with `pharmacyLookup` (signal), `isPharmacyLookupLoading`, `hasPharmacyLookup` (computed), `selectedLookupPharmacies` (signal, max 5), `hasSelectedLookupPharmacies` (computed). `toggleLookupPharmacy()` and `isLookupPharmacySelected()` methods. Both signals persisted/restored in state cycle.
  - **UI:** `PharmacyStepComponent` completely rewritten — filter bar (pharmacy name search, radius dropdown 10/25/50/100 mi, page size dropdown 10/20/50, search/clear buttons), pharmacy cards (name, number, distance badge, address, zipcode, checkbox toggle max 5), two Google Maps action buttons per card ("Spot on Map" + "Directions"), pagination controls (prev/next + page number window), selected pharmacies summary panel with remove buttons, loading spinner and empty state.
- **Config:** `FinancialPlanner:BaseUrl` and `FinancialPlanner:AuthToken` in `appsettings.json` (shared with other Financial Planner services).

---

## ✅ Medicare Plan Recommendation (AI-First + LIS Eligibility, On-Demand)
- **What:** Recommends ranked Medicare plans (MA-PD, PDP+Medigap, D-SNP) personalized to user's drugs, income, health, and location. Triggered on-demand when user clicks "Load Medicare Plan Recommendations" button below drug cards.
- **4-Step Flow:**
  1. **User Profile** — Complete address + income + health.
  2. **Drug Selection** — Analyze prescriptions, confirm drug selections (no costing).
  3. **Pharmacy Selection** — Click "Find Nearby Pharmacies" to get lightweight NPI list. Toggle-select up to 5 pharmacies.
  4. **Plan Recommendation** — Click "Load Medicare Plan Recommendations" (only shown after ≥1 pharmacy selected). Plans include per-pharmacy cost breakdowns.
- **Backend:** Plan recommendation orchestrates: county lookup → LIS tier → AI scoring (with pharmacy context) → CMS enrichment → pharmacy cost breakdowns. `PlanScoringAiService` generates 5 ranked plans with `{{PHARMACY_CONTEXT}}` placeholder for selected pharmacies. `CountyLookupService` fetches county data via Financial Planner API. LIS: 2025 FPL thresholds.
- **AI Extended Fields:** AI generates 12 additional fields per plan: `networkType` (HMO/PPO/PFFS/HMO-POS), benefit flags (`includesDental`, `includesVision`, `includesHearing`, `includesFitness`, `includesOtc`), `otcAllowancePerQuarter`, `gapCoverage` (None/Some/Full), `mailOrderSavings`, `providerNetworkSize` (Large/Medium/Small), `emergencyCoverage`, and `pros`/`cons` bullet lists. Additionally, each plan includes a `planCategory` field (`MA_ONLY`, `PDP_ONLY`, `PDP_MEDIGAP`, `MA_PDP`) indicating the coverage bundling strategy.
- **Frontend:** `PlanRecommendationComponent` orchestrates plan loading, compare state, LIS banner, and Part D gap fill via `ensurePartDGapLoadForMA()`. Decomposed into child components: `RecommendationCardComponent` (individual plan card), `MedigapCardComponent` (Medigap supplemental plan card), `MedigapGapSectionComponent`, `PartdGapSectionComponent` (Part D gap plan cards with checkboxes), `PlanDetailDialogComponent` (full plan detail dialog), and `SelectedPlansSummaryComponent`. All tooltip data centralized in `data/tooltips.ts`.
- **Early Summary Panel:** `hasAnyPlanSelected` computed signal in `PlanRecommendationComponent` shows `SelectedPlansSummaryComponent` as soon as _any_ plan is selected in the active section (MA or Part D), even before the selection is complete. The summary is rendered with `[canCalculate]="hasCompleteSelection()"` passed as input — when `false`, the Calculate button is disabled and an amber hint guides the user (e.g., "Select a Part D gap plan below to calculate your total cost."). `hasCompletePlanSelection` in `DrugStateService` remains the gate for enabling the actual cost evaluation.
- **Design:** AI-first approach — no fragile CMS Plan Finder REST API dependency.

---

## ✅ Plan-Aware Pharmacy Search
- **Status:** Feature structure exists but `PlanPharmacyService` has been removed. Plan-specific pharmacy pricing is not currently active.

---

## ✅ Drug Selection Confirm/Edit/Remove Workflow
- **What:** Single-panel accordion (MatExpansionPanel) where users configure, confirm, edit, and remove drug selections without leaving the card.
- **Components:**
  - `DrugSelectionPanelComponent` — 5-step accordion (name → dosage form → strength → packaging → confirm). Progressive reveal: steps 3–4 hidden until prior steps completed.
  - `SelectedDrugsSummaryComponent` — Shows confirmed drugs with edit/remove action buttons.
  - `ClinicalAlertsComponent` — Drug interactions + contraindications alert panel.
- **Flow:** Select all 4 options → "Select Drug" confirms → panel collapses → summary shows. "Edit" re-opens panel. "Remove" clears selection. Auto-advance to next unconfirmed drug.

---

## ✅ Prescription APIs (Legacy / Backend-Only)
- **What:** Prescription persistence endpoints remain in backend for compatibility, but are no longer part of current UI flows.
- **Current UX:** Main UI and chat flows do not expose save/load prescription actions. Saved Data page shows analyses only.
- **Backend:** `PrescriptionController` still exposes authorized `POST /api/prescription` and `GET /api/prescription` endpoints.

## ✅ Chat Session Lifecycle (Logout/New Login)
- **What:** Chat session persistence keeps historical conversation context while starting a fresh active session on each login.
- **Logout Behavior:** Frontend performs local sign-out cleanup only (no server-side session deletion).
- **Login Behavior:** After successful sign-in, frontend calls `POST /api/chat/session/start-new`. Backend archives previous active session content and resets active messages/UI state.
- **Next Dashboard Load:** `GET /api/chat/session` returns the new empty active session.

---

## ✅ Formulation-Based Drug Analysis (AI Prompt Architecture)
- **What:** AI returns pre-validated `{dosageForm, strength, packaging, ndcCode}` tuples instead of flat independent arrays, eliminating invalid combinations.
- **Prompt Rules:** Rule 7 in `pharma-system.txt` requires a `formulations` array with dosageForm→container mapping. AI sets `ndcCode` to `null` — NDC codes are resolved by the backend from the authoritative RxNorm API. Package counts must correspond to actual FDA-listed NDC package sizes (e.g., "Blister pack of 60 tablets" for Eliquis is invalid — the real blister pack is 100 tablets).
- **Backend Model:** `DrugFormulation` class with `DosageForm`, `Strength`, `Packaging`, `NdcCode` properties. `DrugResult.Formulations` list. `DrugValidationStep` auto-populates flat arrays from formulations for backward compatibility.
- **Frontend Model:** `Formulation` interface. `Drug.formulations` array. Cascading selection filters from formulations.

---

## ✅ Multi-Pharmacy Selection (Up to 5)
- **What:** Users can select up to 5 pharmacies to compare plan costs side-by-side. Replaces the previous single-pharmacy selection model.
- **Flow:** Pharmacy cards in step 2 show toggle checkboxes. Each click toggles selection (check/uncheck). Counter shows "X/5 selected". 6th selection attempt is silently rejected.
- **Backend:** `PlanRecommendationRequest.SelectedPharmacies` accepts `List<SelectedPharmacy>` (capped at 5 via `.Take(5)` in controller). `PlanScoringAiService.BuildPharmacyContext()` renders a numbered list for AI prompt context.
- **Frontend:** Two selection models coexist:
  - **Legacy (NPI):** `DrugStateService.selectedPharmacies` signal. `togglePharmacy(pharmacy)`. `isPharmacySelected(npi)`. `hasSelectedPharmacies` computed.
  - **Financial Planner:** `DrugStateService.selectedLookupPharmacies` signal. `toggleLookupPharmacy(pharmacy)` (max 5). `isLookupPharmacySelected(pharmacyNumber)`. `hasSelectedLookupPharmacies` computed. Both persisted in state cycle.
- **UI:** Emerald-themed pharmacy cards with custom checkboxes. Selected pharmacies summary panel with remove buttons. "X/5 selected" badge.

---

## ✅ Per-Pharmacy Cost Breakdown in Plan Recommendations
- **What:** Each recommended plan includes a cost breakdown for every selected pharmacy, showing annual premium, deductible, drug copay, and total — with per-drug copay details and preferred pharmacy discounts.
- **Backend:** Plan recommendation computes pharmacy cost breakdowns — iterates each selected pharmacy per plan. Preferred pharmacies get copay discounts. `RankedPlan.CostBreakdowns` is a `List<PlanCostBreakdown>` sorted cheapest-first. Plan totals re-calculated from best pharmacy.
- **Frontend:** `PlanCostBreakdownComponent` renders `plan.costBreakdowns` via a self-contained toggle button. Each pharmacy shows an indigo card with cost grid + per-drug copay table with tier chips and preferred discount icons. Component manages its own expanded/collapsed state.
- **Models:** `PlanCostBreakdown` (pharmacyName, pharmacyNpi, isPreferredPharmacy, annualPremium, annualDeductible, annualDrugCopay, annualTotal, drugCopays). `DrugCopayDetail` (drugName, rxCui, formularyTier, monthlyCopay, annualCopay, isCovered, preferredDiscount).

---

## ✅ Plan Card Toggle UI (Clean Design)
- **What:** Plan recommendation cards show a compact view by default (header + cost grid + action buttons). Two toggle buttons reveal detail sections on-demand.
- **Toggle Buttons:**
  - **"Plan Features"** (`PlanDrugCoverageComponent`) — expands drug coverage table (tier badges, copay/mo, PA/QL flags) and AI explanation text.
  - **"Cost Breakup (N)"** (`PlanCostBreakdownComponent`) — expands per-pharmacy cost breakdown cards (badge shows pharmacy count). Only visible when pharmacies were selected.
- **State:** Each child component manages its own `expanded` boolean — no parent Set tracking needed. Buttons toggle independently per plan card.
- **UX:** Keeps the initial plan list scannable. Users can quickly compare plan costs across the list, then drill into specific plans for drug coverage or pharmacy cost details.

---

## ✅ Plan Comparison (Side-by-Side)
- **What:** Users can compare up to 3 Medicare plans side-by-side in a detailed comparison table.
- **Flow:** Check compare checkbox on plan cards (max 3) → sticky indigo bar appears at ≥2 → click "Compare Plans" → `PlanComparePanelComponent` renders comparison table.
- **Comparison Table:** 15 rows: insurance, network, star rating, monthly premium, annual deductible, est. drug cost, est. total, max OOP, benefits (dental/vision/hearing/fitness/OTC/mail-order), gap coverage, per-drug coverage (tier + copay), preferred pharmacy.
- **Winner Indicators:** Green check_circle icon on the lowest-cost plan per cost row.
- **Frontend:** `PlanComparePanelComponent` — `@Input plans`, `@Output closed/cleared`. `getCompareWinner()` compares numeric fields across plans. Parent `PlanRecommendationComponent` manages compare state array and panel visibility.

---

## ✅ AI Gap Coverage Plans (with Sub-Component & Selection)
- **What:** PDP plans lack Part A/B/dental/vision/hearing coverage. An AI-powered panel displays actual complementary plans (with carrier, premium range, deductible, coverage highlights) to fill each gap. Users can select gap plans via checkboxes — selecting any gap plan auto-selects the parent PDP plan for comparison.
- **Flow:** Amber banner on PDP plan cards → click "Find Gap Coverage Plans" → `POST /api/plan-recommendation/gap-advice` → collapsible panel shows structured plan cards organized by coverage category. Each gap plan card has a checkbox for selection.
- **Backend:** `PlanRecommendationController.GapAdvice()` endpoint. `PromptBuilder` assembles system/task/schema/template prompts with `{{PLAN_NAME}}`, `{{PLAN_TYPE}}`, `{{MISSING_COVERAGES}}` placeholders. AI returns structured JSON (`GapCoverageResult` with `GapPlanDto[]`) — parsed and returned as-is. Schema enforces: category, planName, planType, carrier, monthlyPremiumRange, annualDeductible, coverageHighlights, whyNeeded, enrollmentTip, priority (Essential/Recommended/Optional).
- **Frontend:** Extracted to `PlanGapCoverageComponent` sub-component (standalone, OnPush). Calls `PlanRecommendationService.getGapAdvice()` directly. Response cached per plan — subsequent clicks toggle visibility without re-fetching. Each gap plan rendered as a card with category icon, priority badge, cost row, coverage highlight chips, enrollment tip, and **mat-checkbox**. Selected gap plans tracked in a local `Set` and emitted via `gapPlanSelected` output. `ChangeDetectorRef.markForCheck()` used to ensure OnPush detection works with async subscription callbacks.
- **Parent Integration:** `PlanCardComponent` handles `gapPlanSelected` via `onGapPlanSelected()` — auto-selects the parent plan for comparison (if not already selected and compare limit not reached) and emits `compareToggled`.
- **Prompts:** `gap-coverage-system.txt` (8 rules for JSON output), `gap-coverage.txt` (task), `gap-coverage-schema.txt` (JSON schema), `gap-coverage.txt` (template).
- **Models:** `GapCoverageResult` (gapPlans, comparisonTip). `GapPlan` (category, planName, planType, carrier, monthlyPremiumRange, annualDeductible, coverageHighlights, whyNeeded, enrollmentTip, priority).

---

---

## ✅ MongoDB Document Store
- **What:** MongoDB is used for document-oriented persistence — prescriptions, chat sessions, recommendations, analysis selections, FSM state, LTC selections, and structured application logs.
- **Driver:** `MongoDB.Driver` 3.4.0 (Infrastructure), `MongoDB.Bson` 3.4.0 (Domain for `[BsonId]` / `[BsonRepresentation]` attributes).
- **Collections:** `prescriptions`, `chatSessions`, `userAnalysisSelections`, `recommendations`, `ltcCurrentSelections`, `logs` (Serilog structured BSON logs).
- **Indexes:** Compound indexes on user/timestamp fields for efficient per-user retrieval. `MongoIndexInitializer` (hosted service) creates indexes at startup.
- **Architecture:** `MongoDbContext` includes typed collections for both prescriptions and chat sessions.
- **DI Registration:** `IMongoClient` + `IMongoDatabase` as singletons. `MongoDbContext` as singleton. Repository as scoped.

---

## ✅ Signal-Based Confirmed Drugs (Reactivity Fix)
- **What:** `DrugStateService.confirmedDrugs` changed from plain `Set<string>` to `signal(new Set<string>())` to fix Angular signal reactivity. Plain `Set.add/delete` mutations are invisible to `computed()` signals — the reference doesn't change, so dependents never recompute.
- **Fix:** `confirmDrug(name)` and `unconfirmDrug(name)` helper methods create a new `Set` reference on each call (copy + add/delete + set). `resetAll()` uses `confirmedDrugs.set(new Set())` instead of `.clear()`.
- **Impact:** Fixed the Continue button not enabling after drug selection (step 1 → step 2 gate uses `hasConfirmedDrugs()` computed signal). All callers updated — `DrugsStepComponent` reads via `confirmedDrugs()` (signal access), mutates via `state.confirmDrug()`/`state.unconfirmDrug()`.

---

## ✅ Lifetime Medicare Cost Projections (Financial Planner API)
- **What:** Integrates with the external Financial Planner `individualMedicareR5` API to compute lifetime Medicare cost projections for a user's selected plan — year-by-year breakdowns of Part A/B premiums, surcharges, OOP costs, Part D, concierge, and dental. Also calls `expensesPresentValue` API to compute present value of lifetime expenses at a 6% discount rate.
- **Flow:** User selects a plan → clicks "Calculate Lifetime Cost" button in the Selected Plans summary panel → `SavePrescriptionDialogComponent` opens pre-populated with a default name (`{FirstName} {Plan Type} – MM/DD/YYYY`, e.g. `John Medicare Advantage – 04/18/2026`) → user optionally edits name and clicks Save → `POST /api/plan-recommendation/evaluate-costs` → `CostProjectionService` loads user profile, resolves state code to full name, formats DOB as `MM-yyyy`, computes remaining months for adjusted month, maps all profile + plan fields → calls Financial Planner API → calls Present Value API (yearwise expenses, discount=6) → passes results to AI for evaluation → returns combined `CostProjectionResult` with financial data + present value + AI-generated insights → plans saved to `userAnalysisSelections` → recommendation saved inline to `recommendations` collection (with full profile + drugs + pharmacy + plans + cost snapshot) → user is navigated to `/medicare-analysis/cost-projections` page showing interactive Chart.js dashboards.
- **Backend:**
  - **Domain Models:** `IndividualMedicareRequest` (full API payload with 30+ fields including `supplementPlanType`), `IndividualMedicareResponse` (lifetime aggregates including `conciergeIncluded`, plan-specific lifetime fields: `lifeTimeABGD/ABFD/ABND/ABCDExpenses/Premium/Oop` + year-by-year list), `IndividualMedicareDetail` (per-year premiums/OOP/surcharges + `planGPremium`, `planFPremium`, `planNPremium`, `totalABGD/ABFD/ABND/ABCD`) — all in `Models/IndividualMedicare.cs`. `PresentValueRequest`, `PresentValueResponse`, `YearExpense`, `PvEntry` — in `Models/PresentValue.cs`. `CostProjectionResult` (with `PresentValue` field), `LifetimeTotals` (with `TotalIrmaa`, `SupplementPlanType`, `SupplementPlanPremium`, `ConciergeIncluded`, `LifeTimeConciergePremium`, 12 plan-specific lifetime fields), `CostEvaluation`, `LifetimeSummary`, `YearlyHighlight`, `CostCategory`, `SavingsTip` — all in `Models/CostProjection.cs`.
  - **Interface:** `IIndividualMedicareService` with `CalculateAsync(request, cancellationToken)` method. `IPresentValueService` with `CalculateAsync(request, cancellationToken)` for present value computation. `ICostEvaluationAiService` with `EvaluateAsync()` method for AI cost evaluation (accepts `supplementPlanType` and `supplementPlanPremium` parameters).
  - **Service:** `IndividualMedicareService` in `Infrastructure/FinancialPlanner/` — HTTP POST with Basic auth token from config, JSON serialization, structured logging. `PresentValueService` in `Infrastructure/FinancialPlanner/` — HTTP POST to `/expensesPresentValue` with same auth pattern. `CostEvaluationAiService` in `Infrastructure/AI/` — builds prompts via `PromptBuilder.BuildCostEvaluation()`, renders year-by-year breakdown text, calls `IChatClient`, parses AI JSON response into `CostEvaluation`.
  - **Application Service:** `CostProjectionService` orchestrates the full pipeline: profile resolution via `ProfileService`, state code→name resolution via static dictionary, DOB formatting, remaining months calculation, builds `IndividualMedicareRequest`, calls Financial Planner API, then AI evaluation, then Present Value API (non-fatal — wrapped in try/catch). Populates `LifetimeTotals` with `TotalIrmaa` (combined Part B + Part D surcharges), `SupplementPlanType`, `SupplementPlanPremium`, `ConciergeIncluded`, `LifeTimeConciergePremium`, and 12 plan-specific lifetime fields (ABGD/ABFD/ABND/ABCD × Expenses/Premium/Oop). Sets `CostProjectionResult.PresentValue` from PV API response. Method: `EvaluateCostsAsync()` (combined `CostProjectionResult`).
  - **Snapshot:** Cost snapshot uses plan-specific lifetime fields based on `SupplementPlanType` (G→ABGD, F→ABFD, N→ABND, C→ABCD, MA→ABMedicareAdvantage) and stores real FP present value instead of AI-derived `TotalCombined`.
  - **Controller:** `PlanRecommendationController.EvaluateCosts()` — thin delegation to `CostProjectionService` via `MapToInput()` helper. `CalculateCostsRequestDto` for plan-specific inputs (planBundleCode, premiums, OOP, benefit costs, supplementDataProvided, partDDataProvided, reserveDaysUsed, dental, dentalHealthGrade, boughtPlanA, medicareAdvantageDataProvided, partDPremium, calculateForAdjustedMonth, supplementPlanType).
  - **DI:** `AddHttpClient<IIndividualMedicareService, IndividualMedicareService>` with 30s timeout. `AddHttpClient<IPresentValueService, PresentValueService>` with 30s timeout. `AddScoped<ICostEvaluationAiService, CostEvaluationAiService>()`. `AddScoped<CostProjectionService>()`.
  - **AI Prompts:** 4 prompt files for cost evaluation: `cost-evaluation-system.txt` (Medicare financial advisor role, 8 rules), `cost-evaluation.txt` (task description), `cost-evaluation-schema.txt` (JSON schema with output rules), `cost-evaluation.txt` (template with 15 placeholders including plan details, lifetime totals, Total IRMAA, supplement plan type/premium, and yearly breakdown).
- **Frontend:**
  - **Plan Card Button:** "Calculate Lifetime Cost" button added to each `PlanCardComponent` via `@Input() isCostLoading` and `@Output() calculateCost`. Shows loading spinner during API call.
  - **State:** `DrugStateService` extended with `costProjection`, `hasCostProjection` signals and `setCostProjection()` method.
  - **Request Model:** `CalculateCostsRequest` includes all Financial Planner fields: `planBundleCode`, `medicareAdvantagePremium`, `maWithPrescriptionBenefit`, `partDOOP`, `partDOOPFullYear`, `partABenefitServiceCost`, `partBBenefitServiceCost`, `planRecommendName`, `recommendationListId`, `supplementDataProvided`, `partDDataProvided`, `reserveDaysUsed`, `dental`, `dentalHealthGrade`, `boughtPlanA`, `medicareAdvantageDataProvided`, `partDPremium`, `calculateForAdjustedMonth`, `supplementPlanType`.
  - **Response Model:** `EvaluateCostsResponse` includes `presentValue` (from FP Present Value API). `LifetimeTotals` includes `totalIrmaa`, `supplementPlanType`, `supplementPlanPremium`, `lifeTimeConciergePremium`, `conciergeIncluded`, and 12 plan-specific lifetime fields (`lifeTimeABGD/ABFD/ABND/ABCDExpenses/Premium/Oop`). `IndividualMedicareDetail` includes `planGPremium`, `planFPremium`, `planNPremium`, `totalABGD/ABFD/ABND/ABCD`. `ExpenseTableRow` interface for expense table data.
  - **Service:** `PlanRecommendationService.evaluateCosts()` calls `POST /api/plan-recommendation/evaluate-costs`.
  - **Cost Projections Page:** `CostProjectionsComponent` at route `/medicare-analysis/cost-projections`. Standalone component with Chart.js 4.x integration. Navigation guard: `ngOnInit` checks `hasCostProjection()` — if false, resets state and redirects to `/medicare-analysis/plans`. Five charts: (1) line chart — total annual cost trajectory over time, (2) stacked bar chart — premium vs OOP vs surcharges per year, (3) doughnut chart — lifetime cost category breakdown, (4) bar chart — Part B + Part D surcharges by year, (5) **medicare projection chart** — stacked bar with 3 layers: base Premium (rgb(132,201,54)), IRMAA Surcharge (rgb(106,162,42)), Out-of-Pocket (rgb(204,0,0)), with summary strip showing Present Value, bundle Total Expenses, and Total IRMAA Surcharge. Also renders: **Medicare Expense Table** (7-column table showing coverage year + lifetime totals by Medicare bundle using plan-specific lifetime fields), lifetime summary cards (total premiums, total OOP, combined total, projection years, average annual), cost trajectory banner with explanation, yearly highlights table (flagged years), cost category analysis with progress bars, savings tips with priority badges, and overall AI assessment.
  - **Recommendation Detail Parity:** `RecommendationDetailComponent` Cost tab mirrors the cost-projections page — same Medicare Expense Table, Medicare Projection Chart + summary strip, plus all 4 original charts, sourcing data from `rec.lastCostSnapshot` instead of live state.
  - **Chart.js Setup:** Manually registers Chart.js controllers (`LineController`, `BarController`, `DoughnutController`), elements, scales, and plugins. Charts built in `afterNextRender()` lifecycle. `OnDestroy` cleans up chart instances.
  - **Navigation:** After evaluation completes, `PlanRecommendationComponent` navigates to `/medicare-analysis/cost-projections`. Back button returns to `/medicare-analysis/plans`.
  - **Inline Recommendation Save:** `runLifetimeCostEvaluation(recommendationName?)` accepts the name directly. After `saveCurrentPlans` succeeds, if a name was provided it calls `AnalysisSnapshotService.save(name)` immediately (before navigation). On 409 Conflict it retries with `force=true`. The chat confirmation path (`fromChatConfirmation=true`) passes no name to `runLifetimeCostEvaluation` and continues to rely on `pendingCostRunRecommendationName` + `tryAutoSaveRecommendation` in `CostProjectionsComponent.ngOnInit`.
  - **Default Dialog Name:** `SavePrescriptionDialogComponent` accepts `defaultName?: string` in its `SavePrescriptionDialogData`. `PlanRecommendationComponent.buildDefaultRecommendationName()` reads `profileService.profile()?.profile?.firstName`, formats today's date as `MM/DD/YYYY`, and builds `{FirstName} Medicare Advantage – MM/DD/YYYY` (or `Part D + Medigap` for the partd section). Falls back to the plan type alone if profile is not yet loaded.
  - **DB Persistence Pipeline:** `YearlyDetailDoc/Dto` stores all per-year fields including `planG/F/NPremium` and `totalABGD/ABFD/ABND/ABCD`. `CostSnapshotDoc` stores `PresentValue` (real FP value), plan-specific `LifetimeTotal`, `SupplementPlanType`, `SupplementPlanPremium`. `RecommendationController` mappers (`MapToYearlyDetailDto/Doc`) include all extended fields. `AnalysisSnapshotService` maps all new yearly fields during save.
- **Config:** `FinancialPlanner:BaseUrl` and `FinancialPlanner:AuthToken` in `appsettings.json`.
- **Field Mapping:** Profile fields (DOB, state, zip, tax filing, MAGI tier, health, tobacco, concierge, life expectancy, coverage year) automatically mapped from saved profile. Plan-specific fields (bundle code, MA premium, Part D OOP, benefit costs) provided by the frontend via request DTO.

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
  - **State:** `DrugStateService` extended with `drugDetails` (`BulkDrugSearchResponse | null`), `isDrugDetailsLoading`, `hasDrugDetails` signals. Persisted to/restored from sessionStorage. Cleared on `resetAll()`.
  - **Service:** `DrugService.searchDrugsBulk(drugNames)` calls `POST /api/FinancialPlannerDrug/search-bulk`.
  - **Models:** `DrugListItem`, `DrugSearchResponse`, `DrugDetailAdvanceItem`, `DrugDetailResponse`, `DrugSearchResult`, `BulkDrugSearchResponse` — all in `drug.model.ts`.
  - **Trigger:** Auto-fetches on `ngOnInit` if confirmed drugs exist and drug details not yet loaded.
- **Config:** Reuses `FinancialPlanner:BaseUrl` and `FinancialPlanner:AuthToken` from `appsettings.json`.

---

## ✅ Chat Intent Routing & Guided Wizard
- **What:** The chat panel now supports two interaction modes: (A) a **Guided Chat Sequence** (wizard) that walks the user through the Medicare or LTC analysis steps, and (B) **Free-form Intent Routing** where AI classifies user messages into 20 intents and routes them to navigation, plan section switching, LTC care-type input, actions, or the drug analysis flow.
- **Feature A — Guided Wizard:**
  - **Startup:** Chat shows greeting message with two mode selection cards: "Medicare Analysis" (starts wizard) and "Long Term Analysis" (starts LTC wizard). Mode buttons gated behind `isProfileComplete()` — only appear after profile API resolves.
  - **Immediate Start:** Clicking "Medicare Analysis" starts wizard flow immediately without recommendation chooser checks.
  - **Fresh Flow Reset:** On Medicare mode click, chat clears prior carried flow state (confirmed drugs, selected pharmacies, pharmacy-confirmed flag, and plan selections) before starting.
  - **Wizard Steps:** AWAITING_MODE → PROFILE → DRUGS_PHARMACIES → PLANS → ANALYSIS → COMPLETE. Each step is announced via an assistant message with auto-navigation to the relevant route.
  - **Auto-Advance:** `ChatWizardService.hasNewStep` computed signal detects when completion signals fire (profile saved, drugs confirmed, pharmacy selection confirmed, plan loaded, cost projection done). `ChatComponent` watches via `effect()` and auto-announces the next step. Wizard uses `pharmacySelectionConfirmed` (not `hasSelectedLookupPharmacies`) to prevent auto-advance on first pharmacy checkbox — user must explicitly click "Continue to Plans" or use chat intent.
  - **Reset:** `DrugStateService.resetAll()` increments `wizardResetTrigger`, which `ChatComponent` watches to call `wizard.reset()` — returning to mode selection.
- **Feature B — Free-form Intent Routing:**
  - **AI Classification:** User types naturally → `POST /api/chat/intent` → `ChatIntentService` (backend) classifies into one of 20 intents using Anthropic Claude. System prompt loaded from `Prompts/system/chat-intent-system.txt` (file-based, not inline).
  - **Current intents:** Navigation (profile/drugs/pharmacies/plans/cost/saved/ltc-care-type), section switching, LTC care input, LTC projection, reset/save analysis/run analysis, sign out/help, and drug-input fallback intents.
  - **Intent Prerequisite Guards:** All navigation intents now enforce a profile-complete gate before proceeding. `NAVIGATE_ANALYSIS_DRUGS` and `NAVIGATE_PHARMACIES` each require profile complete (redirects to `/medicare-analysis/profile` with message if not). `NAVIGATE_PLANS`, `SWITCH_TO_PDP`, `SWITCH_TO_MA` enforce the full chain: (1) profile complete, (2) at least one drug confirmed, (3) at least one pharmacy selected. `NAVIGATE_COST_PROJECTIONS` adds a further gate: complete plan selection (`DrugStateService.hasCompletePlanSelection()`). Each unmet prerequisite shows a descriptive assistant message and redirects to the appropriate step.
  - **Plan Section Switching:** `SWITCH_TO_PDP` / `SWITCH_TO_MA` intents (after passing prerequisites) set `activeSection` via `state.setActiveSection()`, set `pharmacySelectionConfirmed`, and navigate to `/medicare-analysis/plans`. If already on the requested section, shows "already viewing" message.
  - **Cross-Page Drug Search:** When a drug name (e.g. "add metformin") is typed on the pharmacy page, the `DRUG_INPUT` intent is detected and reclassified as `NAVIGATE_ANALYSIS_DRUGS`. `DrugStateService.pendingCrossPageDrugSearch` is set to the original text before navigation. On `NavigationEnd` to `/medicare-analysis/drugs`, `ChatComponent` fires `runDrugFlow(text)` automatically — the user sees suggestion chips appear within 50 ms of page load. Pure navigation phrases ("go to drug") produce `NAVIGATE_ANALYSIS_DRUGS` directly — `pendingCrossPageDrugSearch` is NOT set, so the page opens blank with no search.
  - **Pharmacy-Save on Profile Redirect:** When `NAVIGATE_PROFILE` fires from the pharmacies page and the user has selected pharmacies, `recState.savePharmacySelection()` is called before navigation. The chat message prefixes a "Your N selected pharmacies have been saved." confirmation so the user knows their picks are preserved.
  - **Parameter Extraction:** AI extracts profile fields for chat-driven profile updates and analysis-save metadata.
  - **Confirmation Messages:** AI generates short, friendly confirmation text (max ~15 words) shown in chat.
  - **Fallback:** On classification error, falls back to drug name suggestion flow.
- **Return Route:** When navigating away from analysis (e.g., to profile), `ChatComponent.saveReturnRoute()` captures the current `/medicare-analysis/*` URL in `DrugStateService.returnRoute`. Header-initiated profile edit from analysis also stores the same return route. `UserProfileComponent` reads this on save/close and navigates back to the saved route instead of the default `/medicare-analysis`.
- **Impact-aware profile change handling:** If profile changes affect analysis assumptions (demographic/tax/location/coverage inputs), downstream state is invalidated after save (pharmacy/plans/cost), while confirmed drugs are retained.
- **Pharmacy Selection Gating:** `pharmacySelectionConfirmed` signal prevents the wizard from auto-advancing to plans when the first pharmacy is selected. User must explicitly click "Continue to Plans" in the analysis shell or use a chat intent to proceed.
- **Backend:**
  - **DTOs:** `ChatIntentRequest` (Message, IsProfileComplete, CurrentPage?), `ChatIntentResponse` (Intent, Params, ConfirmationMessage), `ChatIntentParams` (profile-related fields + analysis metadata + 4 LTC fields: LtcHealthProfile, LtcAdultDayYears, LtcHomeCareYears, LtcNursingCareYears) — in `Application/DTOs/ChatIntentDtos.cs`. `CurrentPage` carries the Angular `router.url` so the backend can apply page-specific disambiguation via `PageContextBuilder`.
  - **Service:** `ChatIntentService` in `Application/Services/` — injects `IChatClient` (M.E.AI), loads system prompt from `Prompts/system/chat-intent-system.txt` at construction time, calls `IChatClient.GetResponseAsync()`, parses JSON response (strips markdown fences), returns `ChatIntentResponse`. Falls back to `UNKNOWN` on error. Works with whichever AI provider is active.
  - **Controller:** `ChatIntentController` at `api/chat` route — `[Authorize]`, `POST intent` endpoint, thin delegation to `ChatIntentService`.
  - **DI:** `AddScoped<ChatIntentService>()` in `Program.cs`.
- **Frontend:**
  - **`ChatIntentService`** (`services/chat-intent.service.ts`) — HTTP service calling `POST /api/chat/intent`. Defines `ChatIntent` type (union of 20 strings, including `ACTION_HELP`, `NAVIGATE_LTC_CARE_TYPE`, `LTC_CARE_INPUT`, `ACTION_RUN_LTC_PROJECTION`), `ChatIntentResponse`, `ChatIntentParams` (11 profile fields + 4 LTC fields) interfaces.
  - **`ChatWizardService`** (`services/chat-wizard.service.ts`) — Reactive wizard state. `WizardMode` (`NONE`/`MEDICARE_ANALYSIS`/`LONG_TERM_ANALYSIS`). `WizardStep` (6 Medicare + 3 LTC values). Computed `currentStep` derived from mode-specific signals. For `LONG_TERM_ANALYSIS`: checks `isProfileComplete()` → `ltcProfileIntroComplete` → returns `LTC_PROFILE`/`LTC_PROFILE_REVIEW`/`LTC_CARE_TYPE`. For `MEDICARE_ANALYSIS`: checks profile, drugs, pharmacy, plans, cost. `startLtcAnalysis()` / `resumeLtcAnalysis()` for LTC mode. `hasNewStep` triggers auto-advance for both modes. `markStepAnnounced()` prevents duplicate messages.
  - **`ChatComponent` / `ChatRouterService`** — `ChatRouterService` handles all message routing. `send()` delegates to `route()` which dispatches through contextual branches: pending confirmations, orchestrator (guarded by route), profile extraction, plan/drug/pharmacy selection handlers, then intent classifier. **Action bypass:** app-level commands (save/run/reset analysis, sign out, help, show saved) bypass page-specific handlers and go directly to intent routing. **LTC routing:** detects `onLtc` via URL prefix, dispatches targeted steps to `resolveLtcStepKeyword()` + `handleLtcStepNavigation()`, back to `handleLtcBackNavigation()`, and 3 new intents to `ChatLtcCareTypeFlowService`.
  - **`ChatLtcCareTypeFlowService`** (`services/chat-ltc-care-type-flow.service.ts`) — Handles chat-driven care-type form population and projection. `handleCareTypeInput()` uses `pendingChatCareType` signal on care-type page or direct state update + navigate if off-page. `handleRunProjection()` validates profile + careTypeVisited, builds LTC payload, calls API, saves, navigates to projection.
  - **`DrugStateService`** — Maintains wizard/session signals including `wizardResetTrigger`, `pharmacySelectionConfirmed`, `returnRoute`, `pendingDrugSelection`, `pendingPharmacySelection`, and `pendingCrossPageDrugSearch` (drug text stored for cross-page auto-search after navigation to drugs).
  - **`AnalysisShellComponent`** — Four-step shell (Profile → Drugs → Pharmacies → Plans). `goNext()` sets `pharmacySelectionConfirmed` when advancing from Pharmacies to Plans. Default child route is `profile`. Emits system messages: "Navigated to {step}" on `goNext()`, "Started a new analysis" on `startNewAnalysis()` (resets state and navigates to `/medicare-analysis/profile`).
  - **`ProfileService`** — Added `pendingPrefill` signal (`Record<string, unknown> | null`) for chat-driven profile pre-fill, `pendingChatProfileData` signal for confirmed chat profile extraction, `missingRequiredFields` signal published by `UserProfileComponent`. Consumed by `UserProfileComponent` on init.
  - **`UserProfileComponent`** — Injects `DrugStateService`. `save()` emits "Profile saved" system message, navigates to `returnRoute`. `effect()` watches `pendingChatProfileData` → patches form + triggers cascading lookups (ZIP→county, DOB→age, taxFiling→MAGI). `updateMissingFields()` publishes to `missingRequiredFields` signal. Prefill consumer handles all profile fields via `Record<string, unknown>`.
  - **`DrugsStepComponent`** — Watches `pendingDrugSelection` and applies chat-driven selection commands with fuzzy matching for drug names/forms/strengths. Supports select, confirm_all, remove, and edit actions.
  - **`PlanRecommendationComponent`** — Plan page redesigned: no default section on landing. Shows two choice buttons (PDP / MA). Single full-width section after selection. "Switch to..." button with warning popup only when a plan is already selected in the current section. Emits system messages on: plan selection (Part D, Medigap, MA), section switching, cost calculation.
- **UI Action Tracking (System Messages):**
  - **What:** Key UI button actions are tracked as `system` role messages in the chat panel, providing the AI and the user visibility into actions performed via the UI (not chat).
  - **Rendering:** System messages appear as centered pill-shaped badges with a `touch_app` icon, grey background, muted text — visually distinct from user (cyan, right-aligned) and assistant (white, left-aligned) bubbles.
  - **Tracked actions:** Profile saved, drug confirmed/removed, pharmacy selected/deselected, Part D/Medigap/MA plan selected, section switched, cost calculation started, wizard navigation (step transitions), new analysis started.

## ✅ Chat-Based Recommendation Management

- **What:** A conversational AI assistant that helps manage Medicare recommendations through natural language chat. Uses intent classification to route user messages to appropriate handlers.
- **Backend:**
  - `ChatIntentService` — IChatClient-based classifier with `chat-intent-system.txt`. Classifies into 20 intents (navigation, actions, plan switching, save/run analysis, LTC).
  - `RecommendationService` — Full CRUD: GetActive, Exists, Create (with force), UpdateProfile/Drugs/Pharmacy/Plans/CostSnapshot, Delete.
  - `RecommendationController` — 8 REST endpoints for recommendation CRUD.
- **Frontend:**
  - `RecommendationService` — HTTP CRUD for `/api/recommendation`.
  - `RecommendationStateService` — Signal-based: `activeRecommendation`, `hasRecommendation` (computed), `refreshAfterUpdate()`, `clear()`.
  - `ChatComponent` — Routes messages through `ChatRouterService` with 6 contextual branches. Signals: `pendingDrugAction`, `pendingProfileUpdate`, `pendingPharmacyAction`, `pendingPlanAction`, `pendingSaveAnalysisOverwrite`.
  - `DashboardComponent` — Loads recommendation on init.

## ✅ Cost Projection & Snapshot

- **What:** Cost projection snapshots capture plan-specific lifetime cost data for comparison.
- **Snapshot:** Uses plan-specific lifetime fields based on `SupplementPlanType` (G→ABGD, F→ABFD, N→ABND, C→ABCD, MA→ABMedicareAdvantage) and stores real FP present value from `CostProjectionResult.PresentValue`.

## ✅ Chat-Based Profile Filling

- **What:** Users can fill their profile via natural language in the chat panel instead of manually filling form fields. Supports both one-shot ("I'm John Smith, male, born 01/15/1955, ZIP 80113") and conversational approaches.
- **Field Coverage:** Applies the same extract → confirm → apply flow across profile fields (name, DOB, gender, tobacco, health condition, tax filing, coverage year, MAGI tier, life expectancy, concierge + amount, alternate contact, ZIP/address).
- **Chat Actions:** After applying extracted fields, chat also provides a **Save Profile Now** button to trigger profile save directly from chat. (Note: the standalone Save button was removed from the profile form — the form saves via the Continue button in the analysis wizard, or automatically when the standalone `/profile` route saves on completion.)
- **Confirmation Flow:** AI extracts fields → shows formatted list of extracted fields → asks "Shall I apply these? (yes / no)" → user confirms or cancels. `pendingProfileUpdate` signal holds the pending data.
- **Cascading Lookups:** When applied, triggers automatic lookups: ZIP→county resolution, DOB→age check, taxFiling→MAGI tier options.
- **Backend:**
  - **Prompt:** `profile-extract-system.txt` — knows 13 profile fields, required vs optional, asks for remaining fields.
  - **DTOs:** `ProfileExtractRequest` (message, missingFields), `ProfileExtractResponse` (extractedFields dict, reply) — in `ProfileExtractDtos.cs`.
  - **Service:** `ProfileExtractService` — `IChatClient`-based, loads prompt from file, returns structured JSON. Fallback on error.
  - **Endpoint:** `POST /api/chat/extract-profile`.
- **Frontend:**
  - **`ChatProfileService`** — HTTP service for `POST /api/chat/extract-profile`.
  - **`ChatComponent`** — Routes to profile extraction when on `/profile` with incomplete profile. Intercepts response to show confirmation prompt.
  - **`ProfileService`** — `pendingChatProfileData` signal consumed by UserProfileComponent, `missingRequiredFields` signal sent to AI.
  - **`UserProfileComponent`** — `effect()` watches `pendingChatProfileData`, patches form, triggers cascading lookups.
  - **Startup Greeting:** New users see profile-fill guidance with example prompt.

## ✅ Chat-Based Drug Formulation Selection

- **What:** Users can select drug type, dosage form, strength, and quantity via chat instead of clicking through the 4-step formulation UI. Also supports removing and editing drugs via chat.
- **Confirmation Flow (remove/edit only):** Destructive actions (remove, edit) require yes/no confirmation. `pendingDrugAction` signal holds the pending command. Non-destructive actions (select, confirm_all, options) execute immediately.
- **Fuzzy Matching:** Drug names ("lipitor" → Atorvastatin), dosage forms ("tab" → Tablet), strengths ("10mg" → "10 MG"). Auto-confirms when all 4 selections are complete.
- **Backend:**
  - **Prompt:** `drug-selection-system.txt` — 6 actions (select, options, confirm_all, remove, edit), 8 few-shot examples.
  - **DTOs:** `DrugSelectionExtractRequest` (message, availableDrugs), `DrugSelectionExtractResponse` (drugName, type, dosageForm, strength, quantity, action, reply) — in `DrugSelectionDtos.cs`.
  - **Service:** `DrugSelectionExtractService` — `IChatClient`-based extraction.
  - **Endpoint:** `POST /api/chat/extract-drug-selection`.
- **Frontend:**
  - **`ChatDrugSelectionService`** — HTTP service.
  - **`ChatComponent`** — Routes to drug selection when on `/medicare-analysis/drugs` with loaded drug details. `buildAvailableDrugSummaries()` prepares data for AI. Remove/edit actions diverted to confirmation flow.
  - **`DrugStateService`** — `pendingDrugSelection` signal + `ChatDrugSelectionCommand` interface.
  - **`DrugsStepComponent`** — `effect()` watches signal, `applyChatDrugSelection()` with `findMatchingDrugName()` (partial match), `fuzzyMatchForm()`, `fuzzyMatchStrength()`, `confirmAllReadyDrugs()`.

## ✅ Chat-Based Pharmacy Selection

- **What:** Users can select, remove, search, and list pharmacies via chat instead of clicking checkboxes. Supports fuzzy name matching.
- **Confirmation Flow (remove only):** Remove actions require yes/no confirmation. `pendingPharmacyAction` signal holds the pending command. Select and search actions execute immediately.
- **Backend:**
  - **Prompt:** `pharmacy-selection-system.txt` — 4 actions (select, remove, list, search), 6 few-shot examples. Prefers closest pharmacy on ambiguous matches.
  - **DTOs:** `PharmacySelectionExtractRequest` (message, availablePharmacies, selectedPharmacies), `PharmacySelectionExtractResponse` (pharmacyName, action, searchTerm, reply) — in `PharmacySelectionDtos.cs`.
  - **Service:** `PharmacySelectionExtractService` — `IChatClient`-based extraction.
  - **Endpoint:** `POST /api/chat/extract-pharmacy-selection`.
- **Frontend:**
  - **`ChatPharmacySelectionService`** — HTTP service.
  - **`ChatComponent`** — Routes to pharmacy selection when on `/medicare-analysis/pharmacies` with loaded lookup. `buildPharmacySummaries()` prepares data for AI. Remove actions diverted to confirmation flow. Search actions set `pendingPharmacySelection` with searchTerm.
  - **`DrugStateService`** — `pendingPharmacySelection` signal + `ChatPharmacySelectionCommand` interface.
  - **`PharmacyStepComponent`** — `effect()` watches signal, `applyChatPharmacySelection()` with `findPharmacyByName()` (exact then partial match, prefers closest).

## ✅ Session State Leak Fix on Sign-Out

- **What:** Fixed critical bug where User A's drugs, pharmacies, chat messages, plans, and cost projections leaked to User B after sign-out/sign-in.
- **Root Cause:** `signOut()` was only clearing 3 auth keys from sessionStorage.
- **Fix:** `sessionStorage.clear()` + reset all in-memory signals: `DrugStateService` (25+ signals), `ProfileService` (6 signals), `RecommendationStateService.clear()`. Uses `Injector.get()` for lazy service resolution to avoid circular dependencies.

## ✅ Financial Disclaimers

- **What:** All cost projection and funding outputs include a disclaimer: "⚠️ These are estimates based on current CMS data and actuarial assumptions. Actual costs may vary. Consult a licensed financial advisor or Medicare counselor before making decisions."
- **Applied to:** `HandleViewProjections`, `HandleViewFunding`, and `FormatSummary` (cost snapshot section).

## ✅ Chat-Based Plan Selection

- **What:** Users can select, remove, and switch between plan sections (PDP/MA) via chat instead of clicking UI buttons. AI extracts plan selection commands from natural language.
- **Backend:**
  - **Prompt:** `plan-selection-system.txt` — 3 actions (select, remove, switch_section), matches plan names and types from available plans.
  - **DTOs:** `PlanSelectionExtractRequest` (message, availablePlans, selectedPlans), `PlanSelectionExtractResponse` (planName, planType, action, section, reply) — in `PlanSelectionDtos.cs`.
  - **Service:** `PlanSelectionExtractService` — `IChatClient`-based extraction.
  - **Endpoint:** `POST /api/chat/extract-plan-selection`.
- **Frontend:**
  - **`ChatPlanSelectionService`** — HTTP service for `POST /api/chat/extract-plan-selection`.
  - **`ChatComponent` / `ChatRouterService`** — Routes to plan selection extraction when on `/medicare-analysis/plans` with loaded plan data. Select/remove actions applied to state signals; switch_section sets `activeSection`.

## ✅ Chat-Based Run Analysis with Confirmation

- **What:** Users can trigger cost analysis calculation via chat using natural language (e.g., "run analysis", "calculate costs"). AI classifies the `ACTION_RUN_ANALYSIS` intent, which initiates the cost evaluation pipeline.
- **Flow:** User message → AI classifies as `ACTION_RUN_ANALYSIS` → prerequisite checks (profile, drugs, pharmacies, plan selected) → confirmation prompt → triggers `evaluateCosts()` → navigates to `/medicare-analysis/cost-projections`.

## ✅ Save Analysis (Chat + UI Button)

- **What:** Users can save their complete Medicare analysis (profile, drugs, pharmacy, plans, cost projections) as a named recommendation for future reference.
- **Three Entry Points:**
  1. **Plan page Calculate button (primary):** "Calculate Lifetime Cost" button in `SelectedPlansSummaryComponent` → name dialog pre-populated with `{FirstName} {Plan Type} – MM/DD/YYYY` → user saves → plans saved + recommendation saved inline before navigation to cost-projections. No separate save step required.
  2. **Chat:** Say "save analysis as My Medicare Plan" → AI extracts `ACTION_SAVE_ANALYSIS` intent with `analysisName` parameter → saves directly if prerequisites met.
  3. **UI Button:** "Save Analysis" button in `CostProjectionsComponent` header → opens `SavePrescriptionDialogComponent` (with custom title/subtitle/icon) → user enters name → saves.
- **Prerequisite Check:** `AnalysisSnapshotService.canSave()` verifies 5 prerequisites: profile complete, drugs confirmed, pharmacies selected, plan selected, cost projection available. Shows descriptive error message if any prerequisite is unmet. (Note: this check is used by the chat and cost-projections page paths; the plan-page inline save bypasses this check as all prerequisites are guaranteed at that point by UI guards.)
- **Overwrite Handling:** If a recommendation already exists (409 Conflict), the user is asked "Would you like to overwrite?" → `force: true` re-sends the request.
- **Reset After Save:** On successful save, calls `state.resetAll()` and navigates to `/medicare-analysis/profile` to start a fresh analysis.
- **Backend:** `POST /api/recommendation` with expanded request body including `CostSnapshotDto` (yearly details + full AI evaluation) and `SelectedPlanDto` (7 expanded fields: deductible, starRating, totalPrescriptionCost, totalPlanCost, prescriptionDrugCovered, unavailableDrugs, planExpenses).
- **Frontend:**
  - **`AnalysisSnapshotService`** — Assembles full snapshot from current state signals. `canSave()` checks prerequisites. `save(name, force?)` builds request and calls `RecommendationService.create()`.
  - **`ChatRouterService`** — `ACTION_SAVE_ANALYSIS` handler with dialog, prerequisite check, overwrite confirmation via `pendingSaveAnalysisOverwrite` signal.
  - **`CostProjectionsComponent`** — Save Analysis button, `saveAnalysis()` method with dialog + auto-overwrite on 409.

## ✅ Expanded Analysis Persistence for PDF Generation

- **What:** The recommendation document stores enough data to recreate a full PDF report without re-running any API calls or AI analysis.
- **Expanded `SelectedPlanDoc/Dto`:** +7 fields — `deductible`, `starRating`, `totalPrescriptionCost`, `totalPlanCost`, `prescriptionDrugCovered`, `unavailableDrugs[]`, `planExpenses[]`.
- **Expanded `CostSnapshotDoc/Dto`:** +4 fields — `supplementPlanType`, `supplementPlanPremium`, `yearlyDetails[]` (16 financial fields per year), `evaluation` (full AI analysis).
- **New Embedded Documents (Backend):** `YearlyDetailDoc`, `CostEvaluationDoc`, `LifetimeSummaryDoc`, `YearlyHighlightDoc`, `CostCategoryDoc`, `SavingsTipDoc`, `PlanExpenseDoc`.
- **New DTOs:** `RecommendationSummaryResponse`, `YearlyDetailDto`, `CostEvaluationDto`, `LifetimeSummaryDto`, `YearlyHighlightDto`, `CostCategoryDto`, `SavingsTipDto`, `PlanExpenseDto`.
- **New Frontend Interfaces:** `YearlyDetailDto`, `CostEvaluationDto`, `LifetimeSummarySnapDto`, `YearlyHighlightDto`, `CostCategorySnapDto`, `SavingsTipSnapDto`, `PlanExpenseDto`.
- **Controller Mapping:** 8+ new mapping helpers in `RecommendationController` for bidirectional document↔DTO conversion.

## ✅ Saved Data Page (Filter / Sort / Pagination / Compare)

- **What:** A full-featured page listing saved recommendation analyses with client-side search, filter, sort, pagination, and a compare basket. Accessible via header button, dropdown menu, and chat navigation.
- **Route:** `/saved` → `RecommendationComponent`; `/saved/compare` → `RecommendationCompareComponent`.
- **Filter / Sort / Pagination:**
  - **Search:** Text input filters cards by analysis name (case-insensitive, real-time).
  - **Type pills:** All / Medicare / Long Term Care — filters by `recommendation.type`.
  - **Sort:** 6 options — Newest First, Oldest First, Name A–Z, Name Z–A, Highest Cost, Lowest Cost.
  - **Pagination:** Configurable page size (10/25/50); Prev/Next and page number buttons.
- **Compare Basket:**
  - Each card has an **Add to Compare** / **Remove** toggle button.
  - Sticky ribbon at the bottom of the screen appears when ≥1 item is in the basket. At 2 items, a **Compare** button navigates to `/saved/compare`.
  - Compare is type-aware — Medicare and Long Term Care analyses are compared in their respective context.
  - `RecommendationCompareComponent` orchestrates comparison: reads `ids` from query params, `forkJoin` loads both records, determines mode (`medicare` / `longterm` / `cross`), renders hero header (left/right cards, VS badge, savings), and dispatches to:
    - `CompareMedicareComponent` — 5-tab Medicare comparison (Overview with 6 KPI deltas + 5 key-difference sections, Profile with 4 grouped card sections using human-readable labels, Prescriptions with count strip + shared/unique drug cards, Plans & Pharmacy with storefront cards + detailed plan cards, Cost Analysis with Chart.js + year-by-year delta table).
    - `CompareLtcComponent` — 4-tab LTC comparison (Overview, Profile, Care Config, Cost Analysis).
    - `CompareCrossComponent` — 3-tab cross-type comparison (Overview with disclaimer, Profile, Cost Summary).
  - Shared helpers in `compare-helpers.ts`: delta formatting, trajectory icons/colors, star arrays, profile row builder with grouped labels (personal/location/health/financial) and inline formatters (health condition, gender, tobacco, concierge, tax filing).
- **Card Layout (4-row grid):** Analysis name (displayed in **uppercase**), creation date, type badge, drug count, plan count, lifetime total (when available), status pill. Compare basket slots also show the name in uppercase.
- **Empty State:** “No saved analyses” when list is empty; “No results” when active filters match nothing.
- **Backend:** `GET /api/recommendation/all` returns `RecommendationSummaryResponse[]` (id, name, status, drugCount, planCount, hasCostSnapshot, lifetimeTotal, dates). `IRecommendationRepository.GetAllByUserIdAsync()` sorted by CreatedAt desc.
- **Frontend Navigation (3 entry points):**
  1. **Header icon button:** `folder_open` icon always visible in the toolbar.
  2. **Dropdown menu item:** “Saved Data” in user menu (when profile complete).
  3. **Chat:** `NAVIGATE_SAVED_ANALYSES` intent routes to `/saved`. `ACTION_LOAD_PRESCRIPTIONS` also navigates to `/saved`.
  3. **Chat:** `NAVIGATE_SAVED_ANALYSES` intent routes to `/saved`. `ACTION_LOAD_PRESCRIPTIONS` also navigates to `/saved` (page now shows analyses only).

## ✅ Medicare Analysis Starts Fresh

- **What:** Clicking "Medicare Analysis" now starts wizard flow directly without checking saved analyses/prescriptions first.
- **Flow:** `selectMode('MEDICARE_ANALYSIS')` clears prior flow carry-over (`drugDetails`, `confirmedDrugNames`, `selectedLookupPharmacies`, `pharmacySelectionConfirmed`, plan selections) and starts wizard.
- **Result:** No path chooser, no saved-analysis/prescription copy-in behavior, and no pharmacy auto-restore from saved data.

## ✅ Action Intent Bypass for Page-Specific Handlers

- **What:** App-level chat commands (save analysis, run analysis, help, sign out, etc.) now correctly reach the intent classifier even when the user is on a wizard step page where a page-specific selection handler would normally intercept all messages.
- **Problem:** On wizard pages, app-level commands could be captured by page-specific handlers and never reach intent routing.
- **Fix:** `ACTION_PATTERNS` — a regex array in `ChatRouterService` that matches app-level action phrases. All three page-specific selection handlers (`routeToDrugSelection`, `routeToPharmacySelection`, `routeToPlanSelection`) check this pattern first and return `false` to let matching messages fall through to `routeToIntentClassifier()`.
- **Patterns matched:** save analysis, run analysis, calculate cost, reset analysis, sign out, log out, show saved, help.

## ~~Orchestrator URL Guard~~ (Removed)

> **Note:** The chatbot orchestrator (`ChatOrchestratorService`, `ChatOrchestratorController`, `ConvStateService`, `DeltaCalculationService`) has been fully removed. Chat coordination is now handled by `ChatRouterService` with `ChatIntentService` (20 intents), page-specific extraction services, and `ChatNavigationFlowService`. This feature section is retained for historical reference only.

- **What:** The orchestrator handler now skips routing when the user is on wizard step pages, allowing page-specific drug/pharmacy/plan selection handlers to process messages correctly.
- **Problem:** `routeToOrchestrator()` had no URL guard and ran before all page-specific handlers. When a saved recommendation existed, _all_ messages were captured by the orchestrator, preventing chat-based pharmacy selection, drug selection, and plan selection from working.
- **Fix:** `routeToOrchestrator()` returns `false` when `router.url` starts with `/medicare-analysis/profile`, `/medicare-analysis/drugs`, `/medicare-analysis/pharmacies`, or `/medicare-analysis/plans`.

---

## ✅ Long Term Care (LTC) Cost Projection Wizard

- **What:** A 2-step wizard with chat-driven navigation that projects lifetime Long Term Care costs using the Financial Planner LTC API. Users configure care type years and quality of care, then run a projection from the Care Type step. The projection result is displayed on a dedicated result page (not a stepper step).
- **Flow:**
  1. **Step 1 — Profile:** Reuses `UserProfileComponent` at `/long-term-care/profile`. Reads DOB, gender, state, ZIP, countyCode, lifeExpectancy, tobaccoStatus.
  2. **Step 2 — Care Type:** `LtcCareTypeStepComponent` — user selects quality of care level (1=Best … 5=Minimum) and number of years for each care type: Adult Day Health Care, In-Home Care, Nursing Care. Contains a **"Run Projection"** button that triggers the API call and navigates to the result page.
  3. **Projection (result page):** `LtcProjectionStepComponent` at `/long-term-care/projection` — reads `LtcProjectionResponse` from `LtcStateService.ltcResult`, shows totals and present values for each care category, plus year-by-year expense lists. Not a stepper step — the stepper only shows Profile and Care Type.
- **Projection Gating:** The "Run Projection" button is enabled only when the profile is complete AND the Care Type step has been visited (`careTypeVisited` signal). This ensures defaults are valid once the user has entered the step.
- **Auto-Save:** Step transitions auto-save the current step's data. Navigating away from Care Type saves care-type selections to the database via `LtcService.saveCurrent()`. Profile saves via `requestSaveFromChat()` on Continue.
- **Backend:**
  - **Domain:** `LongTermCareRequest`, `LongTermCareResponse`, `LtcExpenseEntry` in `Models/LongTermCare.cs`.
  - **Interface:** `ILongTermCareService.CalculateAsync(request, cancellationToken)`.
  - **Service:** `LongTermCareService` in `Infrastructure/FinancialPlanner/` — HTTP POST with Basic auth token to LTC endpoint.
  - **Controller:** `LongTermCareController` — `[Authorize]` `POST /api/long-term-care`. Reads user profile from DB, builds `LongTermCareRequest`, delegates to service.
- **Frontend:**
  - **Shell:** `LtcShellComponent` — 2-step stepper (Profile → Care Type), `<router-outlet>`, Back/Continue nav bar. Continue button triggers `profileService.requestSaveFromChat()` on step 1. No "Calculate" button in the footer — projection is triggered from Care Type step.
  - **Care Type Step:** `LtcCareTypeStepComponent` — health profile quality selector (1–5), care-type year inputs. `runProjection()` method builds `LtcProjectionRequest` from profile + state signals, calls `LtcService.calculate()`, saves result via `LtcService.saveCurrent()`, navigates to `/long-term-care/projection`. `canRunProjection` getter requires `profileComplete && careTypeVisited && !isCallingApi`. Consumes `pendingChatCareType` signal via `effect()` to apply chat-driven form patches.
  - **State:** `LtcStateService` — signals: `currentStep` (`1|2`), `healthProfile`, `adultDayYears`, `homeCareYears`, `nursingCareYears`, `careTypeVisited` (set true on step 2 entry), `returnRoute` (saved URL for return navigation), `pendingChatCareType` (chat-driven field updates consumed by Care Type component), `isCallingApi`, `ltcResult`. `resetAll()` resets all signals to defaults. `PendingChatCareType` interface defines optional fields: `healthProfile?`, `adultDayYears?`, `homeCareYears?`, `nursingCareYears?`.
  - **HTTP Service:** `LtcService.calculate(request)` → `POST /api/long-term-care`. `LtcService.saveCurrent()` → `PUT /api/ltc/current`.
  - **Models:** `LtcProjectionRequest`, `LtcProjectionResponse`, `LtcExpenseEntry` in `models/ltc.model.ts`.
- **Route:** `/long-term-care` — guarded by `profileCompleteGuard`. Visible in dashboard via route navigation.

---

## ✅ LTC Chat Integration (Wizard, Navigation & AI Intents)

- **What:** Full chat-driven interaction for the LTC wizard — matching the Medicare analysis chat experience. Users can navigate between LTC steps, populate care-type fields, and trigger projections via natural language.
- **Chat Wizard (LONG_TERM_ANALYSIS mode):**
  - `ChatWizardService` extended with `LONG_TERM_ANALYSIS` mode and 3 LTC steps: `LTC_PROFILE`, `LTC_PROFILE_REVIEW`, `LTC_CARE_TYPE`.
  - `ltcProfileIntroComplete` signal — set when user clicks Continue from profile step on LTC route.
  - Computed `currentStep`: if profile incomplete → `LTC_PROFILE`; if profile complete but intro not done → `LTC_PROFILE_REVIEW`; else → `LTC_CARE_TYPE`.
  - `startLtcAnalysis()` / `resumeLtcAnalysis()` methods for starting and resuming the LTC wizard.
  - `ChatComponent` handles `selectMode('LONG_TERM_ANALYSIS')` → calls `beginLtcAnalysisFlow()` (resolves greeting + starts wizard). Auto-announces steps with appropriate `LTC_MESSAGES` constants.
  - Resume on hard refresh: detects LTC route, checks `ltcProfileIntroComplete` gate, posts resume-aware messages (`RESUME_PROFILE`, `RESUME_CARE_TYPE`, `RESUME_PROJECTION`).
  - `UserProfileComponent` — both save paths (no-changes + successful-save) check for LTC route → set `ltcProfileIntroComplete`, navigate to `LTC_CARE_TYPE` step instead of drugs.
- **Chat Step Navigation:**
  - `ChatNavigationFlowService` — `LTC_STEP_LABELS` (1→Profile, 2→Care Type), `LTC_KEYWORD_TO_STEP` (profile→1, care type/care-type/caretype→2), `LTC_STEP_ROUTES` (1→LTC_PROFILE, 2→LTC_CARE_TYPE).
  - `handleLtcStepNavigation(step)` — validate prerequisites (profile complete for forward nav), auto-save care-type on leave, navigate.
  - `handleLtcBackNavigation()` — step 2→1 with care-type save.
  - `handleLtcForwardNavigation()` — on step 2 shows "last step" message; on step 1 saves and advances.
  - `saveLtcReturnRoute()` — captures current LTC URL in `LtcStateService.returnRoute` for later return.
  - `saveLtcCurrentStepAndNavigate()` — saves care-type to DB when leaving step 2.
  - `handleReturnNavigation()` — checks both `DrugStateService.returnRoute()` and `LtcStateService.returnRoute()`.
  - `TARGETED_STEP_PATTERN` regex updated to include `care[\s-]?type`.
- **Chat Intent Routing:**
  - `ChatRouterService.route()` — detects `onLtc = router.url.startsWith(AppRoutes.abs.LTC)`. Targeted step matches dispatch to `resolveLtcStepKeyword()` + `handleLtcStepNavigation()`. Back pattern dispatches to `handleLtcBackNavigation()`.
  - LTC "next" handling in `ChatComponent.send()`: on LTC profile → `requestSaveFromChat()`, on LTC care type → `handleLtcForwardNavigation()`.
- **3 New AI Intents:**
  - `NAVIGATE_LTC_CARE_TYPE` — navigate to care type step (with profile check).
  - `LTC_CARE_INPUT` — populate care-type fields from chat. Params: `ltcHealthProfile` (1–5), `ltcAdultDayYears` (0–20), `ltcHomeCareYears` (0–20), `ltcNursingCareYears` (0–20).
  - `ACTION_RUN_LTC_PROJECTION` — trigger projection from chat. Validates profile + careTypeVisited.
- **Care Type Flow Service (`ChatLtcCareTypeFlowService`):**
  - `handleCareTypeInput(params, confirmationMessage)` — on care-type page, uses `pendingChatCareType` signal (consumed by component `effect()`); off-page, updates state directly + navigates.
  - `handleRunProjection()` — validates prerequisites, builds LTC payload, calls API, saves result, navigates to projection page. Uses `LTC_MESSAGES.PROJECTION_RUNNING/COMPLETE/FAILED`.
- **LTC Messages (`LTC_MESSAGES` constant):** 12 entries covering the full wizard lifecycle: `START_PROFILE`, `PROFILE_REVIEW`, `CARE_TYPE_PROMPT`, `CARE_TYPE_UPDATED`, `REQUIRE_PROFILE`, `REQUIRE_CARE_TYPE`, `LAST_STEP`, `PROJECTION_RUNNING`, `PROJECTION_COMPLETE`, `PROJECTION_FAILED`, `RESUME_PROFILE`, `RESUME_CARE_TYPE`, `RESUME_PROJECTION`.
- **Backend (AI Prompt Updates):**
  - `chat-intent-system.txt` — 3 LTC intent definitions, LTC parameter extraction rules, 4 LTC params in JSON format, 7 LTC few-shot examples.
  - `PageContextBuilder.cs` — 2 new page context entries: `/long-term-care/profile` (profile field changes + next/continue guidance) and `/long-term-care/care-type` (care-type values → `LTC_CARE_INPUT`, run projection → `ACTION_RUN_LTC_PROJECTION`, go back → `NAVIGATE_PROFILE`).
  - `ChatIntentDtos.cs` — 4 nullable int properties: `LtcHealthProfile`, `LtcAdultDayYears`, `LtcHomeCareYears`, `LtcNursingCareYears`.

---

## ✅ Financial Planner Plan Recommendations (Part D, Medicare Advantage, Medigap)

- **What:** Three standalone FP-integrated plan recommendation endpoints that allow the frontend to fetch real Financial Planner plan data (Part D, MA, Medigap quotes) and display them in the Medicare analysis step 4 plan cards.
- **Part D Plan Recommendation:**
  - **Backend:** `PartDPlanController` — `[Authorize]` `POST /api/PartDPlan/recommend`. `IPartDPlanRecommendationService` / `PartDPlanRecommendationService` in Infrastructure.
  - **Domain:** `PartDPlanRecommendationRequest`, `CountyCodeModel`, `PrescriptionInput`, `PharmacyInput` in `Models/PartDPlanRecommendation.cs`.
  - **Frontend:** `part-d-plan.service.ts` calls `POST /api/PartDPlan/recommend`. Model types: `PartDPlanRecommendationRequest`, `CountyCodeModel`, `PrescriptionInput`, `PartDPharmacyInput` in `models/part-d-plan.model.ts`.
- **Medicare Advantage Plan Recommendation:**
  - **Backend:** `MedicareAdvantagePlanController` — `[Authorize]` `POST /api/MedicareAdvantagePlan/recommend`. `IMedicareAdvantagePlanService` / `MedicareAdvantagePlanService` in Infrastructure. Request extends Part D fields with `medicareAdvantage: true`.
  - **Frontend:** `medicare-advantage-plan.service.ts`. Model: `MedicareAdvantagePlanRequest` in `models/medicare-advantage-plan.model.ts` (extends PartD request shape with `medicareAdvantage: true`).
- **Medigap Plan Quotes:**
  - **Backend:** `MedigapPlanController` — `[Authorize]` `POST /api/MedigapPlan/quotes`. `IMedigapPlanQuotesService` / `MedigapPlanQuotesService` in Infrastructure.
  - **Domain:** `MedigapPlanQuotesRequest`, `MedigapPlanQuotesResponse`, `MedigapPlanQuote` + nested carrier/rate structs in `Models/MedigapPlanQuotes.cs`.
  - **Frontend:** `medigap-plan.service.ts`. Models: `MedigapPlanQuotesRequest`, `MedigapPlanQuotesResponse`, `MedigapPlan`, `MedigapRate`, `MedigapCompanyBase` in `models/medigap-plan.model.ts`.
- **Plan Recommendation Component (`PlanRecommendationComponent`):**
  - Section chooser when no `activeSection` — two cards: "Part D + Medigap" / "Medicare Advantage" separated by a vertical "OR" divider (horizontal on mobile).
  - After selection: Shows full-width plan list for the active section.
  - Sub-components: `RecommendationCard`, `MedigapCard`, `MedigapGapSection`, `PartdGapSection`, `PlanDetailDialog`, `SelectedPlansSummary`.
  - Reconciles saved plan stubs from `ChatAnalysisSelectionHydrationService` with live API rows (match by id/name, clear unmatched).
  - Plan selections posted as system messages; chat-driven picks use `pendingPlanSelection` to avoid duplicate bubbles.
  - **Plan Card Enrichment:** `PlanCardEnrichmentService` (pure computation, no HTTP) computes derived display fields from raw API responses. `PlanRecommendationComponent` creates `computed()` enrichment maps (`partDEnrichmentMap`, `maEnrichmentMap`, `medigapEnrichmentMap`) keyed by plan ID/key, passed to card components as `[enriched]` input.
  - **Part D card enrichment:** Formatted plan ID (`contractId-planId-segmentId`), insurance carrier (from `contractIdCarrierMap[contractId]`), Part D surcharge (response-level `partDPremiumSurcharge`), prescription OOP, drugs covered X/Y, pharmacies in network X/Y.
  - **Medicare Advantage card enrichment:** Same as Part D plus combined surcharges (Part B + Part D), healthcare OOP (`partAandBBenefitServiceCost`), `hasPrescriptionDrug` flag for conditional Rx OOP display.
  - **Medigap card enrichment:** Insurance carrier (from `contractIdCarrierMap[naic]`), premium cents→dollars conversion (`rate.month/100`, `rate.annual/100`), Part B surcharge, healthcare OOP (`partBServiceOOP`), remaining months count. Gap section component (`MedigapGapSectionComponent`) also injects `PlanCardEnrichmentService` directly.
  - **Part D gap section enrichment:** `PartDGapSectionComponent` injects `PlanCardEnrichmentService` and passes enriched data to recommendation cards displayed in the MA gap section.
- **State:** `DrugStateService` signals: `partDPlans`, `medigapQuotes`, `maPlans`, `selectedPartDPlan`, `selectedMedigapPlan`, `selectedMAPlan`, `selectedMAGapPartDPlan`, `activeSection`.

---

## ✅ Recommendation Snapshot Persistence (Full Analysis CRUD)

- **What:** The system persists a complete recommendation snapshot (profile + drugs + pharmacy + plans + cost projections) in MongoDB, enabling the orchestrator to manage the active recommendation lifecycle via natural language and allowing users to view, compare, and reload past analyses.
- **MongoDB Collection:** `recommendations` — one document per active recommendation per user (no multi-version history; overwrite with `force: true`).
- **Document (`RecommendationDocument`):** Stores all fields needed to reconstruct a full analysis display without re-running any APIs: `profileSnapshot`, `drugList`, `pharmacySelection`, `mailOrderPharmacy`, `planSelections`, `costSnapshot` (with `yearlyDetails[]`, `evaluation` EmbeddedDoc); all with embedded document types.
- **Backend CRUD:**
  - `GET /api/recommendation` — get active recommendation
  - `GET /api/recommendation/{id}` — get by ID (full detail)
  - `GET /api/recommendation/all` — get all summaries for current user
  - `POST /api/recommendation` — create (with `?force=true` for overwrite)
  - `PUT /api/recommendation/profile` — update profile snapshot
  - `PUT /api/recommendation/drugs` — update drug list
  - `PUT /api/recommendation/pharmacy` — update pharmacy selection
  - `PUT /api/recommendation/plans` — update plan selections
  - `PUT /api/recommendation/cost-snapshot` — update cost snapshot
  - `DELETE /api/recommendation` — delete active recommendation
- **Application Layer:** `RecommendationService` (CRUD methods), `IRecommendationRepository` (MongoDB).
- **Frontend:** `RecommendationService` (HTTP CRUD), `RecommendationStateService` (signal-based: `activeRecommendation`, `hasRecommendation`, `refreshAfterUpdate`, `clear`). Loaded on `DashboardComponent.ngOnInit` via `loadActiveRecommendation$()`.

---

## ✅ User Analysis Selections Persistence

- **What:** Users' Medicare analysis wizard selections (drugs, pharmacies, plans, active section) are persisted across logins in a dedicated MongoDB document, separate from the full recommendation snapshot.
- **MongoDB Collection:** `userAnalysisSelections` — one document per user.
- **Document (`UserAnalysisSelectionsDocument`):** Stores: `drugs[]` (with formulation + detail), `pharmacies: UserAnalysisPharmacyDoc[]`, `plans: UserAnalysisPlanDoc[]`, `activeSection` (partd/ma).
- **Backend:** `IUserAnalysisSelectionsRepository`, updates via `PUT` calls on underlying recommendation sub-fields.
- **LTC Selections Persistence:** A separate `ltcCurrentSelections` MongoDB collection stores the most recent LTC care-type selections per user (`LtcCurrentSelectionsDocument`). `LtcSelectionsController` — `[Authorize]` `PUT /api/ltc/current` (save), `GET /api/ltc/current` (load current).

---

## ✅ Saved Recommendation Detail View

- **What:** A dedicated full-detail view for any saved recommendation, accessible from the Saved Data page. Professional redesign matching the compare page design language.
- **Route:** `/saved/:id` → `RecommendationDetailComponent`.
- **Design:**
  - **Hero Header:** Dark gradient bar with type badge (Medicare/LTC), back button, save date.
  - **Medicare KPI Strip:** 6 cards above tabs (Lifetime, Premiums, OOP, IRMAA, Present Value, Current Year).
  - **Medicare Tabs (5):**
    1. **Profile** — 3 grouped section cards (Personal, Location, Health & Financial) with colored icons and human-readable labels.
    2. **Prescriptions** — Drug count pill + clean HTML table.
    3. **Pharmacy** — Storefront-style cards with type badge, phone/distance/NPI icons, mail-order card.
    4. **Plans** — Card-per-plan with colored type headers, 6-metric grid, visual star ratings, unavailable drug chips.
    5. **Cost & Charts** — Trajectory banner, Chart.js charts (line, stacked bar, doughnut, projection), Medicare Expense Table, summary strip.
  - **LTC Tabs (3):** Profile, Care Config, Cost Analysis (trajectory, categories, tips, assessment).
- **Helper Methods:** `fmtGender()`, `fmtHealth()`, `fmtTaxFiling()`, `starArray()`.
- **Backend:** `GET /api/recommendation/{id}` returns full `RecommendationResponse` including embedded `CostSnapshotDto` with `yearlyDetails[]` and `evaluation`.
- **Frontend:** `RecommendationDetailComponent` — standalone, OnPush, Chart.js manually registered. `RecommendationService.getById(id)` called on init from route param.

← [Chapter 7 — Project Structure](ch07-project-structure.md) | [Table of Contents](APPLICATION_BLUEPRINT.md) | [Chapter 9 → Roadmap](ch09-roadmap.md)
