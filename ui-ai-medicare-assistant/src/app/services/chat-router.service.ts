import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { DrugStateService, ChatDrugSelectionCommand, ChatPharmacySelectionCommand, ChatPlanSelectionCommand } from './drug-state.service';
import { ProfileService } from './profile.service';
import { AuthService } from './auth.service';
import { ChatIntentService, ChatIntentResponse } from './chat-intent.service';
import { PendingDrugChatCards } from './chat-drug-selection.service';
import { AnalysisSnapshotService } from './analysis-snapshot.service';
import { LtcAnalysisSnapshotService } from './ltc-analysis-snapshot.service';
import { RecommendationStateService } from './recommendation-state.service';
import { SavePrescriptionDialogComponent } from '../medicare-analysis/drug-step/save-prescription-dialog/save-prescription-dialog.component';
import { PrescriptionService } from './prescription.service';
import { buildCurrentPrescriptionDrugsFromState, buildSelectedPharmaciesSnapshotFromState } from '../medicare-analysis/current-prescription.mapper';
import { ChatAnalysisSelectionHydrationService } from './chat-analysis-selection-hydration.service';
import { ChatProfileEditFlowService } from './chat-profile-edit-flow.service';
import { ChatRouterSummaryService } from './chat-router-summary.service';
import { ChatDrugSelectionFlowService } from './chat-drug-selection-flow.service';
import { ChatPharmacySelectionFlowService } from './chat-pharmacy-selection-flow.service';
import { ChatPlanSelectionFlowService } from './chat-plan-selection-flow.service';
import { ChatNavigationFlowService } from './chat-navigation-flow.service';
import { ChatWizardService } from './chat-wizard.service';
import { ChatLtcCareTypeFlowService } from './chat-ltc-care-type-flow.service';
import {
  COST_PROJECTION_IMMUTABILITY_WARNING,
  COST_PROJECTION_PROCEED_PROMPT_SUFFIX,
} from '../medicare-analysis/cost-projection-messages';
import {
  ANALYSIS_MESSAGES,
  APP_MESSAGES,
  DRUG_MESSAGES,
  PHARMACY_MESSAGES,
  PLAN_MESSAGES,
  PROFILE_MESSAGES,
} from '../constants/chat-messages';
import { AppRoutes } from '../app-routes.const';
import {
  AFFIRMATIVE,
  BACK_PATTERN,
  TARGETED_STEP_PATTERN,
  RETURN_PATTERN,
  DRUG_KEYWORD_PATTERN,
  PHARMACY_KEYWORD_PATTERN,
  PLAN_KEYWORD_PATTERN,
} from './chat-router.constants';

/**
 * Handles message routing, confirmation flows, orchestrator responses,
 * and intent dispatch — extracted from ChatComponent.
 */
@Injectable({ providedIn: 'root' })
export class ChatRouterService {
  private state            = inject(DrugStateService);
  private profileService   = inject(ProfileService);
  private authService      = inject(AuthService);
  private chatIntentSvc    = inject(ChatIntentService);
  private recState         = inject(RecommendationStateService);
  private analysisSnapshot = inject(AnalysisSnapshotService);
  private ltcSnapshot      = inject(LtcAnalysisSnapshotService);
  private dialog           = inject(MatDialog);
  private router           = inject(Router);
  private prescriptionService = inject(PrescriptionService);
  private selectionHydrator = inject(ChatAnalysisSelectionHydrationService);
  private profileEditFlow = inject(ChatProfileEditFlowService);
  private summaryBuilder = inject(ChatRouterSummaryService);
  private drugSelectionFlow = inject(ChatDrugSelectionFlowService);
  private pharmacySelectionFlow = inject(ChatPharmacySelectionFlowService);
  private planSelectionFlow = inject(ChatPlanSelectionFlowService);
  private navigationFlow = inject(ChatNavigationFlowService);
  private wizard = inject(ChatWizardService);
  private ltcCareTypeFlow = inject(ChatLtcCareTypeFlowService);

  // ── Confirmation signals ──────────────────────────────────────────────────

  readonly pendingDrugAction       = signal<ChatDrugSelectionCommand | null>(null);
  readonly pendingProfileUpdate = this.profileEditFlow.pendingProfileUpdate;
  readonly pendingPharmacyAction   = signal<ChatPharmacySelectionCommand | null>(null);
  readonly pendingPlanAction       = signal<ChatPlanSelectionCommand | null>(null);
  readonly pendingRunAnalysisConfirm = signal(false);
  readonly pendingSaveAnalysisOverwrite = signal<string | null>(null);
  readonly pendingTaxFilingChoice = this.profileEditFlow.pendingTaxFilingChoice;
  readonly pendingMagiTierChoices = this.profileEditFlow.pendingMagiTierChoices;
  readonly hasUnsavedProfileChanges = this.profileEditFlow.hasUnsavedProfileChanges;
  /** Chip/button rows for drug type → form → strength → quantity on the drugs chat step. */
  readonly pendingDrugChatCards = signal<PendingDrugChatCards | null>(null);

  // ── Main routing entry point ──────────────────────────────────────────────

  /**
   * Routes a user message through the appropriate handler.
   * @param onDrugFlow callback invoked when the message is a drug input or unknown intent.
   */
  route(text: string, onDrugFlow?: (text: string) => void): void {
    this.clearPendingDrugChatCards();

    const onLtc = this.router.url.startsWith(AppRoutes.abs.LTC);

    // Targeted step navigation: "go to profile", "go back to drugs", "switch to pharmacies", "go to care type", etc.
    const targetedMatch = text.match(TARGETED_STEP_PATTERN);
    if (targetedMatch) {
      if (onLtc) {
        const ltcStep = this.navigationFlow.resolveLtcStepKeyword(targetedMatch[1]);
        if (ltcStep) {
          this.navigationFlow.handleLtcStepNavigation(ltcStep);
          return;
        }
      }
      const targetStep = this.navigationFlow.resolveStepKeyword(targetedMatch[1]);
      if (targetStep) {
        this.navigationFlow.handleStepNavigation(targetStep);
        return;
      }
    }

    // Return to previous location: "go back to where I was", "return", etc.
    if (RETURN_PATTERN.test(text)) {
      this.navigationFlow.handleReturnNavigation();
      return;
    }

    // Generic back: "back", "go back", "previous", "previous step" → sequential backward.
    if (BACK_PATTERN.test(text)) {
      if (onLtc) {
        this.navigationFlow.handleLtcBackNavigation();
      } else {
        this.navigationFlow.handleBackNavigation();
      }
      return;
    }

    if (this.handlePendingConfirmations(text)) return;
    if (this.routeProfilePageMessage(text, onDrugFlow)) return;
    if (this.profileEditFlow.routeToProfileExtraction(text)) return;
    if (this.routeToPlanSelection(text, onDrugFlow)) return;
    if (this.routeToDrugSelection(text, onDrugFlow)) return;
    if (this.routePharmaciesStep(text)) return;
    this.routeToIntentClassifier(text, onDrugFlow);
  }

  /**
   * On profile pages, let AI intent classification run first so navigation-like
   * phrasing/typos (e.g. "go to drog") is not swallowed by profile extraction.
   * Falls back to profile extraction for actual profile edits or unknown text.
   */
  private routeProfilePageMessage(text: string, onDrugFlow?: (text: string) => void): boolean {
    const onProfilePage =
      this.router.url.startsWith('/profile') || this.router.url.startsWith(AppRoutes.abs.PROFILE);
    if (!onProfilePage) return false;

    this.chatIntentSvc.classify(text, this.profileService.isProfileComplete(), this.router.url).subscribe({
      next: (result) => {
        // NAVIGATE_PROFILE with actual profile field params (e.g. zipCode, gender, dateOfBirth)
        // must be handled as a profile extraction, not a navigation, because:
        //   - pendingPrefill is only consumed in UserProfileComponent.ngOnInit — it is a
        //     one-shot read that is silently ignored when the component is already mounted.
        //   - pendingChatProfileData (used by profile extraction) is watched via effect() and
        //     is therefore reactive even when the component is already on screen.
        const isProfileNavWithData =
          result.intent === 'NAVIGATE_PROFILE' &&
          !!result.params &&
          Object.values(result.params as object).some(v => v !== undefined && v !== null);

        // Drug name typed on the profile page — but the AI may misclassify profile-field
        // inputs (e.g. "magitier is 150") as DRUG_INPUT. Try profile extraction first:
        //   • If extraction finds fields → apply them (stay on profile, e.g. MAGI tier update).
        //   • If extraction returns empty AND the text contains an explicit drug keyword
        //     ("add drug metformin") → redirect to fp-drugs and auto-trigger search.
        //   • If extraction returns empty but no drug keyword (bare name like "metformin")
        //     → show guidance to navigate to the Drugs step instead.
        if (result.intent === 'DRUG_INPUT') {
          this.profileEditFlow.routeToProfileExtraction(text, () => {
            if (DRUG_KEYWORD_PATTERN.test(text)) {
              this.state.pendingCrossPageDrugSearch.set(text);
              this.handleIntent({ ...result, intent: 'NAVIGATE_ANALYSIS_DRUGS' }, text, onDrugFlow);
            } else {
              this.state.addAssistantMessage(DRUG_MESSAGES.NAVIGATE_TO_DRUGS_HINT);
              this.state.setLoading(false);
            }
          });
          return;
        }

        // UNKNOWN on the profile page — check for out-of-context keywords before
        // sending to profile extraction (which would find nothing and reply generically).
        if (result.intent === 'UNKNOWN') {
          if (PHARMACY_KEYWORD_PATTERN.test(text)) {
            this.navigationFlow.handleStepNavigation(3);
            return;
          }
          if (PLAN_KEYWORD_PATTERN.test(text)) {
            this.navigationFlow.handleStepNavigation(4);
            return;
          }
          this.profileEditFlow.routeToProfileExtraction(text);
          return;
        }
        if (isProfileNavWithData) {
          this.profileEditFlow.routeToProfileExtraction(text);
          return;
        }
        this.handleIntent(result, text, onDrugFlow);
      },
      error: () => {
        this.profileEditFlow.routeToProfileExtraction(text);
      },
    });
    return true;
  }

  // ── Pending confirmation handlers ─────────────────────────────────────────

  private handlePendingConfirmations(text: string): boolean {
    const lower = text.toLowerCase();

    // Handle follow-up values for drug selection prompts (e.g., "Please provide quantity").
    const pendingDrugFollowup = this.state.pendingDrugFollowupPrompt();
    if (pendingDrugFollowup && this.router.url === AppRoutes.abs.DRUGS) {
      const qtyInline = text.match(/\b(\d{1,4})\b/);
      const strengthInline = text.match(/\b(\d+(?:\.\d+)?)\s*(mg|mcg|g|ml)\b/i);
      const quantityNeeded = pendingDrugFollowup.missingFields.some(f => /quantity/i.test(f));
      const formNeeded = pendingDrugFollowup.missingFields.some(f => /dosage form/i.test(f));
      const strengthNeeded = pendingDrugFollowup.missingFields.some(f => /strength/i.test(f));

      const quantity = quantityNeeded && qtyInline ? parseInt(qtyInline[1], 10) : null;
      const strength = strengthNeeded && strengthInline
        ? `${strengthInline[1]} ${strengthInline[2].toUpperCase()}`
        : null;

      // Use full text as dosage-form hint for fuzzy matching in FP step.
      const dosageHint = formNeeded ? text.trim() : null;

      if ((quantity && quantity > 0) || strength || (dosageHint && dosageHint.length > 0)) {
        this.state.pendingDrugSelection.set({
          drugName: pendingDrugFollowup.drugName,
          type: null,
          dosageForm: dosageHint,
          strength,
          quantity: quantity && quantity > 0 ? quantity : null,
          action: 'select',
        });
        this.state.setLoading(false);
        return true;
      }
    }

    const drug = this.pendingDrugAction();
    if (drug) {
      this.pendingDrugAction.set(null);
      if (AFFIRMATIVE.includes(lower)) {
        this.state.pendingDrugSelection.set(drug);
        const verb = drug.action === 'remove' ? 'Removed' : 'Reopened';
        this.state.addAssistantMessage(DRUG_MESSAGES.ACTION_CONFIRM(verb, drug.drugName));
      } else {
        this.state.addAssistantMessage(APP_MESSAGES.GENERIC_CANCELLED);
      }
      this.state.setLoading(false);
      return true;
    }

    const profile = this.profileEditFlow.pendingProfileUpdate();
    if (profile) {
      this.profileEditFlow.resolvePendingProfileUpdate(AFFIRMATIVE.includes(lower));
      return true;
    }

    const pharm = this.pendingPharmacyAction();
    if (pharm) {
      this.pendingPharmacyAction.set(null);
      if (AFFIRMATIVE.includes(lower)) {
        this.state.pendingPharmacySelection.set(pharm);
        this.state.addAssistantMessage(PHARMACY_MESSAGES.REMOVED_FROM_SELECTION(pharm.pharmacyName));
      } else {
        this.state.addAssistantMessage(APP_MESSAGES.GENERIC_CANCELLED);
      }
      this.state.setLoading(false);
      return true;
    }

    const plan = this.pendingPlanAction();
    if (plan) {
      this.pendingPlanAction.set(null);
      if (AFFIRMATIVE.includes(lower)) {
        this.state.pendingPlanSelection.set(plan);
        const label = plan.planCategory === 'medigap' ? 'Medigap' : plan.planCategory === 'ma' ? 'MA' : 'Part D';
        this.state.addAssistantMessage(PLAN_MESSAGES.REMOVED_FROM_SELECTION(plan.planName, label));
      } else {
        this.state.addAssistantMessage(APP_MESSAGES.GENERIC_CANCELLED);
      }
      this.state.setLoading(false);
      return true;
    }

    if (this.pendingRunAnalysisConfirm()) {
      this.resolveRunAnalysisConfirmation(AFFIRMATIVE.includes(lower));
      return true;
    }

    if (this.pendingSaveAnalysisOverwrite()) {
      const name = this.pendingSaveAnalysisOverwrite()!;
      this.pendingSaveAnalysisOverwrite.set(null);
      if (AFFIRMATIVE.includes(lower)) {
        this.state.setLoading(true);
        this.analysisSnapshot.save(name, true).subscribe({
          next: () => {
            this.state.addAssistantMessage(APP_MESSAGES.SAVE_ANALYSIS_SUCCESS(name));
            this.state.setLoading(false);
            this.state.resetAll();
            this.router.navigate([AppRoutes.abs.PROFILE]);
          },
          error: () => {
            this.state.addAssistantMessage(APP_MESSAGES.SAVE_ANALYSIS_FAILED);
            this.state.setLoading(false);
          },
        });
      } else {
        this.state.addAssistantMessage(APP_MESSAGES.SAVE_CANCELLED);
      }
      this.state.setLoading(false);
      return true;
    }

    if (this.ltcCareTypeFlow.pendingSaveLtcOverwrite()) {
      const name = this.ltcCareTypeFlow.pendingSaveLtcOverwrite()!;
      this.ltcCareTypeFlow.pendingSaveLtcOverwrite.set(null);
      if (AFFIRMATIVE.includes(lower)) {
        this.state.setLoading(true);
        this.ltcSnapshot.save(name, true).subscribe({
          next: () => {
            this.state.addAssistantMessage(APP_MESSAGES.SAVE_ANALYSIS_SUCCESS(name));
            this.state.setLoading(false);
          },
          error: () => {
            this.state.addAssistantMessage(APP_MESSAGES.SAVE_ANALYSIS_FAILED);
            this.state.setLoading(false);
          },
        });
      } else {
        this.state.addAssistantMessage(APP_MESSAGES.SAVE_CANCELLED);
      }
      this.state.setLoading(false);
      return true;
    }

    return false;
  }

  resolvePendingProfileUpdate(accept: boolean): boolean {
    return this.profileEditFlow.resolvePendingProfileUpdate(accept);
  }

  triggerDirectProfileSave(): void {
    this.profileEditFlow.triggerDirectProfileSave();
  }

  /**
   * AI-assisted resolver for profile-edit command text (e.g. typos like "canc edit").
   * Uses intent classification first, with a conservative fallback for obvious cancel phrasing.
   */
  resolveProfileEditIntentWithAi(text: string): void {
    this.profileEditFlow.resolveProfileEditIntentWithAi(text);
  }

  discardPendingProfileChanges(): void {
    this.profileEditFlow.discardPendingProfileChanges();
  }

  /** Typed yes/no or chat Yes/No buttons after "Proceed? (yes / no)" for lifetime cost analysis. */
  resolveRunAnalysisConfirmation(accept: boolean): void {
    if (!this.pendingRunAnalysisConfirm()) return;
    this.pendingRunAnalysisConfirm.set(false);
    if (!accept) {
      this.state.addAssistantMessage(APP_MESSAGES.GENERIC_CANCELLED);
      this.state.setLoading(false);
      return;
    }
    const dialogRef = this.dialog.open(SavePrescriptionDialogComponent, {
      width: '420px',
      data: {
        title: 'Name this recommendation',
        subtitle: 'We will save your profile, drugs, pharmacies, and plans under this name after cost evaluation.',
        icon: 'bookmark',
        defaultName: `Medicare Analysis – ${new Date().toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}`,
      },
    });
    dialogRef.afterClosed().subscribe((name: string | null) => {
      const trimmed = name?.trim() ?? '';
      if (!trimmed) {
        this.state.addAssistantMessage(APP_MESSAGES.GENERIC_CANCELLED);
        this.state.setLoading(false);
        return;
      }
      this.state.addAssistantMessage(COST_PROJECTION_IMMUTABILITY_WARNING);
      this.state.setPendingCostRunRecommendationName(trimmed);
      this.state.pendingRunAnalysis.set({ trigger: true });
      this.state.addAssistantMessage(ANALYSIS_MESSAGES.RUNNING_NOW);
      this.router.navigate([AppRoutes.abs.PLANS]);
      this.state.setLoading(false);
    });
  }

  // ── Orchestrator ──────────────────────────────────────────────────────────

  applyTaxFilingChoice(choice: 'MARRIED_FILING_JOINTLY' | 'FILING_INDIVIDUALLY'): void {
    this.profileEditFlow.applyTaxFilingChoice(choice);
  }

  applyMagiTierChoice(value: number): void {
    this.profileEditFlow.applyMagiTierChoice(value);
  }

  clearPendingDrugChatCards(): void {
    this.pendingDrugChatCards.set(null);
  }

  /**
   * Applies one chip choice and refreshes `pendingDrugChatCards` + `pendingDrugSelection`.
   * Order: type → dosage form → strength → quantity.
   */
  applyDrugChatChip(
    kind: 'type' | 'form' | 'strength' | 'quantity',
    value: string | number
  ): void {
    const current = this.pendingDrugChatCards();
    if (!current) return;

    const drugName = current.drugName;
    const partialIn = { ...current.partial };
    if (kind === 'type') partialIn.type = value as string;
    else if (kind === 'form') partialIn.dosageForm = value as string;
    else if (kind === 'strength') partialIn.strength = value as string;
    else partialIn.quantity = typeof value === 'number' ? value : parseInt(String(value), 10);

    const label =
      kind === 'quantity' ? `${partialIn.quantity} per month` : String(value);
    this.state.addUserMessage(label);

    const summaries = this.summaryBuilder.buildAvailableDrugSummaries();
    const summary = summaries.find(d => d.name === drugName);
    if (!summary) {
      this.clearPendingDrugChatCards();
      return;
    }

    const nextCards = this.summaryBuilder.computeDrugSelectionCards(summary, partialIn);
    if (nextCards) {
      this.pendingDrugChatCards.set(nextCards);
    } else {
      this.clearPendingDrugChatCards();
    }

    const finalPartial = nextCards?.partial ?? partialIn;
    this.state.pendingDrugSelection.set({
      drugName,
      type: finalPartial.type,
      dosageForm: finalPartial.dosageForm,
      strength: finalPartial.strength,
      quantity: finalPartial.quantity,
      action: 'select',
    });

    if (nextCards) {
      this.state.addAssistantMessage(DRUG_MESSAGES.PICK_NEXT_OPTION(drugName));
    }
  }

  // ── Drug selection extraction ─────────────────────────────────────────────

  /**
   * On the Drugs step, classify intent with AI first so navigation ("find pharmacy", typos, informal
   * phrasing) is not misrouted to drug-formulation extraction. DRUG_INPUT and UNKNOWN fall through.
   */
  private routeToDrugSelection(text: string, onDrugFlow?: (text: string) => void): boolean {
    return this.drugSelectionFlow.routeToDrugSelection(
      text,
      onDrugFlow,
      (nextText, nextDrugFlow) =>
        this.routeToIntentClassifier(nextText, nextDrugFlow),
      (cmd) => this.pendingDrugAction.set(cmd),
      (cards) => this.pendingDrugChatCards.set(cards)
    );
  }

  // ── Pharmacy selection extraction ─────────────────────────────────────────

  /**
   * On the Pharmacies step: classify intent first so navigation ("show plans", "go to cost projections")
   * reaches `handleIntent`. Only `DRUG_INPUT` / `UNKNOWN` fall through to pharmacy extract + local heuristics.
   * The already-classified result is forwarded directly — no second API round-trip.
   */
  private routePharmaciesStep(text: string): boolean {
    return this.pharmacySelectionFlow.routePharmaciesStep(
      text,
      (result, originalText) => this.handleIntent(result, originalText, undefined)
    );
  }

  // ── Plan selection extraction ───────────────────────────────────────────

  /**
   * On the Plans step: classify intent first so "cost evaluation" / "show cost projections"
   * reach `handleIntent` → `NAVIGATE_COST_PROJECTIONS`. Otherwise every message went to
   * extract-plan-selection, which only returned chat text and never navigated.
   */
  private routeToPlanSelection(text: string, onDrugFlow?: (text: string) => void): boolean {
    return this.planSelectionFlow.routeToPlanSelection(
      text,
      onDrugFlow,
      (result, originalText, nextDrugFlow) => this.handleIntent(result, originalText, nextDrugFlow),
      (cmd) => this.pendingPlanAction.set(cmd),
    );
  }

  // ── Intent classifier ─────────────────────────────────────────────────────

  /**
   * Falls back to intent classification.
   * Calls `onDrugFlow` callback when intent is DRUG_INPUT/UNKNOWN.
   */
  routeToIntentClassifier(text: string, onDrugFlow?: (text: string) => void): void {
    this.chatIntentSvc
      .classify(text, this.profileService.isProfileComplete(), this.router.url)
      .subscribe({
        next: (result) => this.handleIntent(result, text, onDrugFlow),
        error: () => {
          this.state.setLoading(false);
          onDrugFlow?.(text);
        },
      });
  }

  private handleIntent(
    result: ChatIntentResponse,
    originalText: string,
    onDrugFlow?: (text: string) => void
  ): void {
    switch (result.intent) {

      case 'NAVIGATE_PROFILE': {
        const params = result.params;
        if (params) {
          const prefill: Record<string, unknown> = {};
          if (params.firstName) prefill['firstName'] = params.firstName;
          if (params.lastName) prefill['lastName'] = params.lastName;
          if (params.gender) prefill['gender'] = params.gender;
          if (params.dateOfBirth) prefill['dateOfBirth'] = params.dateOfBirth;
          if (params.tobaccoStatus != null) prefill['tobaccoStatus'] = params.tobaccoStatus;
          if (params.healthCondition != null) prefill['healthCondition'] = params.healthCondition;
          if (params.taxFilingStatus) prefill['taxFilingStatus'] = params.taxFilingStatus;
          // Coverage year is system-managed and not user-editable.
          if (params.zipCode) prefill['zipCode'] = params.zipCode;
          if (params.addressLine1) prefill['addressLine1'] = params.addressLine1;
          if (params.lifeExpectancy != null) prefill['lifeExpectancy'] = params.lifeExpectancy;
          if (Object.keys(prefill).length > 0) {
            this.profileService.pendingPrefill.set(prefill);
          }
        }

        // When navigating to profile from the pharmacies step, persist the current
        // pharmacy selection so the user does not lose their picks on return.
        const fromPharmacies = this.router.url.startsWith(AppRoutes.abs.PHARMACIES);
        const selectedPharmacies = this.state.selectedLookupPharmacies();
        if (fromPharmacies && selectedPharmacies.length > 0) {
          this.recState.savePharmacySelection().subscribe({ error: () => {} });
        }

        this.navigationFlow.saveReturnRoute();

        const pharmacySavedPrefix = fromPharmacies && selectedPharmacies.length > 0
          ? `Your ${selectedPharmacies.length} selected pharmacies have been saved. `
          : '';
        this.state.addAssistantMessage(
          pharmacySavedPrefix + (result.confirmationMessage || APP_MESSAGES.OPENING_PROFILE_FOR_EDIT)
        );
        this.state.setLoading(false);
        this.router.navigate([AppRoutes.abs.PROFILE]);
        break;
      }

      case 'NAVIGATE_ANALYSIS_DRUGS': {
        if (!this.profileService.isProfileComplete()) {
          this.state.addAssistantMessage(PHARMACY_MESSAGES.REQUIRE_PROFILE);
          this.state.setLoading(false);
          this.router.navigate([AppRoutes.abs.PROFILE]);
          break;
        }
        // AI-classified drug navigation on profile review should follow the same
        // save-and-continue path as the footer "Continue to Drugs" button.
        if (
          this.router.url.startsWith(AppRoutes.abs.PROFILE) &&
          this.wizard.currentStep() === 'PROFILE_REVIEW'
        ) {
          // Do not setLoading(false) here — save is in progress; chatSaveInProgress blocks input.
          // The save handler posts its own assistant message on success/no-change.
          this.triggerDirectProfileSave();
          break;
        }
        if (this.hasUnsavedProfileChanges() && this.router.url.startsWith(AppRoutes.abs.PROFILE)) {
          this.state.addAssistantMessage(PROFILE_MESSAGES.SAVE_FIRST_TO_DRUGS);
          this.state.setLoading(false);
          this.router.navigate([AppRoutes.abs.PROFILE]);
          break;
        }
        // Already on the drugs page — fire the drug search directly instead of
        // navigating (same-URL navigation is a no-op; NavigationEnd never fires).
        if (this.router.url.startsWith(AppRoutes.abs.DRUGS) && onDrugFlow) {
          const searchText = result.params?.prescriptionName || originalText;
          this.state.addAssistantMessage(result.confirmationMessage || DRUG_MESSAGES.STEP_PROMPT);
          this.state.setLoading(false);
          onDrugFlow(searchText);
          break;
        }
        this.state.addAssistantMessage(result.confirmationMessage || DRUG_MESSAGES.STEP_PROMPT);
        this.state.setLoading(false);
        this.state.pendingCrossPageDrugSearch.set(result.params?.prescriptionName || originalText);
        this.router.navigate([AppRoutes.abs.DRUGS]);
        break;
      }

      case 'NAVIGATE_PHARMACIES': {
        if (!this.profileService.isProfileComplete()) {
          this.state.addAssistantMessage(PHARMACY_MESSAGES.REQUIRE_PROFILE);
          this.state.setLoading(false);
          this.router.navigate([AppRoutes.abs.PROFILE]);
        } else {
          this.state.addAssistantMessage(result.confirmationMessage || PHARMACY_MESSAGES.NAVIGATE);
          // Hydrate pharmacy state silently now so it is ready the moment the page mounts.
          // The "restored pharmacy" confirmation is deferred until after navigation so the
          // chat stays in sync with what is actually visible on screen.
          this.selectionHydrator.hydratePharmacyFromActiveRecommendationSelection(true);
          this.navigateToPharmaciesAfterSavingCurrentRx(() => {
            const pharmacyName = this.state.selectedLookupPharmacies()[0]?.pharmacyName;
            if (pharmacyName) {
              this.state.addAssistantMessage(PHARMACY_MESSAGES.RESTORED_FROM_SAVED_ANALYSIS(pharmacyName));
            }
          });
        }
        break;
      }

      case 'NAVIGATE_PLANS': {
        this.navigationFlow.navigateWithPrerequisites(result, AppRoutes.abs.PLANS, () => {
          this.state.pharmacySelectionConfirmed.set(true);
          this.recState.savePharmacySelection().subscribe({ error: () => {} });
          // Save only pharmacies — drugs are already saved when user advanced past the drugs step.
          const pharmacies = buildSelectedPharmaciesSnapshotFromState(this.state);
          this.prescriptionService.saveCurrentPharmacy(pharmacies).subscribe({ error: () => {} });
          // Plan hydration runs from ChatComponent NavigationEnd when `/analysis/plans` loads
          // so footer navigation and chat navigation both get saved-plan restore messages.
        });
        break;
      }

      case 'NAVIGATE_COST_PROJECTIONS': {
        this.navigationFlow.navigateWithPrerequisites(result, AppRoutes.abs.COST_PROJECTIONS, undefined, true);
        break;
      }

      case 'SWITCH_TO_PDP':
      case 'SWITCH_TO_MA': {
        if (!this.navigationFlow.checkDrugPharmacyPrereqs()) break;

        const target = result.intent === 'SWITCH_TO_PDP' ? 'partd' : 'ma';
        const current = this.state.activeSection();
        if (current === target) {
          this.state.addAssistantMessage(PLAN_MESSAGES.ALREADY_VIEWING(target));
          this.state.setLoading(false);
        } else {
          this.state.pharmacySelectionConfirmed.set(true);
          this.recState.savePharmacySelection().subscribe({ error: () => {} });
          // Save only pharmacies — drugs are already saved when user advanced past the drugs step.
          const pharmacies = buildSelectedPharmaciesSnapshotFromState(this.state);
          this.prescriptionService.saveCurrentPharmacy(pharmacies).subscribe({ error: () => {} });
          this.state.setActiveSection(target);
          this.state.addAssistantMessage(
            result.confirmationMessage || PLAN_MESSAGES.SWITCHING(target)
          );
          this.state.setLoading(false);
          this.router.navigate([AppRoutes.abs.PLANS]);
        }
        break;
      }

      case 'ACTION_RESET_ANALYSIS': {
        this.state.setLoading(false);
        this.state.resetAll();
        break;
      }

      case 'ACTION_SIGN_OUT': {
        this.state.addAssistantMessage(result.confirmationMessage || APP_MESSAGES.SIGNING_OUT);
        this.state.setLoading(false);
        setTimeout(() => this.authService.signOut(), 800);
        break;
      }

      case 'ACTION_LOAD_PRESCRIPTIONS': {
        this.navigationFlow.saveReturnRoute();
        this.state.addAssistantMessage(result.confirmationMessage || APP_MESSAGES.OPENING_SAVED_PRESCRIPTIONS);
        this.state.setLoading(false);
        this.router.navigate(['/saved']);
        break;
      }

      case 'NAVIGATE_SAVED_ANALYSES': {
        this.navigationFlow.saveReturnRoute();
        this.state.addAssistantMessage(result.confirmationMessage || APP_MESSAGES.OPENING_SAVED_ANALYSES);
        this.state.setLoading(false);
        this.router.navigate(['/saved']);
        break;
      }

      case 'ACTION_HELP': {
        this.state.addAssistantMessage(
          `Here's what I can help you with:\n\n` +
          `**Navigate**\n` +
          `• "Go to my profile" — edit your profile\n` +
          `• "Show drugs" — open drug analysis\n` +
          `• "Find pharmacies" — browse nearby pharmacies\n` +
          `• "Show plans" — view Medicare plan recommendations\n` +
          `• "Show cost projections" — view financial forecasts\n` +
          `• "Switch to Part D" / "Switch to MA" — change plan type\n\n` +
          `**Actions**\n` +
          `• "Load prescriptions" — view saved prescriptions\n` +
          `• "Show saved analyses" — view saved analyses\n` +
          `• "Run analysis" / "Calculate costs" — trigger cost projections\n` +
          `• "Save analysis as [name]" — save your full analysis\n` +
          `• "Reset analysis" — start over\n\n` +
          `**Plans (on the Plans page)**\n` +
          `• "Select the Humana plan" — pick a plan\n` +
          `• "Remove Part D plan" — deselect a plan\n` +
          `• "What plans are available?" — list options\n\n` +
          `**Drugs**\n` +
          `• Type drug names directly (e.g. "Eliquis 5mg, Metformin 500mg")`
        );
        this.state.setLoading(false);
        break;
      }

      case 'ACTION_RUN_ANALYSIS': {
        if (!this.navigationFlow.checkDrugPharmacyPrereqs()) break;
        if (!this.state.hasCompletePlanSelection()) {
          this.state.addAssistantMessage(ANALYSIS_MESSAGES.RUN_BLOCKED_NEED_PLANS);
          this.state.setLoading(false);
          this.router.navigate([AppRoutes.abs.PLANS]);
          break;
        }
        this.pendingRunAnalysisConfirm.set(true);
        this.state.addAssistantMessage(
          ANALYSIS_MESSAGES.RUN_READY +
            COST_PROJECTION_PROCEED_PROMPT_SUFFIX +
            ' Proceed? (yes / no)'
        );
        this.state.setLoading(false);
        break;
      }

      case 'ACTION_SAVE_ANALYSIS': {
        if (!this.analysisSnapshot.canSave()) {
          this.state.addAssistantMessage(
            ANALYSIS_MESSAGES.SAVE_REQUIRED_COMPLETE
          );
          this.state.setLoading(false);
          break;
        }
        const analysisName = result.params?.analysisName;
        if (analysisName) {
          this.performSaveAnalysis(analysisName);
        } else {
          this.openSaveAnalysisDialog();
        }
        break;
      }

      case 'NAVIGATE_LTC_CARE_TYPE': {
        if (!this.profileService.isProfileComplete()) {
          this.state.addAssistantMessage('Please complete your profile first.');
          this.state.setLoading(false);
          this.router.navigate([AppRoutes.abs.LTC_PROFILE]);
        } else {
          this.state.addAssistantMessage(result.confirmationMessage || 'Taking you to the care type step.');
          this.state.setLoading(false);
          this.router.navigate([AppRoutes.abs.LTC_CARE_TYPE]);
        }
        break;
      }

      case 'LTC_CARE_INPUT': {
        this.ltcCareTypeFlow.handleCareTypeInput(result.params, result.confirmationMessage);
        break;
      }

      case 'ACTION_RUN_LTC_PROJECTION': {
        const nameDialogRef = this.dialog.open(SavePrescriptionDialogComponent, {
          width: '420px',
          data: {
            title: 'Name this analysis',
            subtitle: 'Enter a name to save your long-term care projection.',
            icon: 'elderly',
            defaultName: `LTC Analysis – ${new Date().toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}`,
          },
        });
        nameDialogRef.afterClosed().subscribe((name: string | null) => {
          const trimmed = name?.trim() ?? '';
          if (!trimmed) {
            this.state.setLoading(false);
            return;
          }
          this.ltcCareTypeFlow.handleRunProjection(trimmed);
        });
        break;
      }

      case 'DRUG_INPUT':
      case 'UNKNOWN':
      default: {
        this.state.setLoading(false);
        onDrugFlow?.(originalText);
        break;
      }
    }
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  /**
   * Persists confirmed FP drug list to MongoDB (`prescriptions` collection, name `__current_prescriptions__`)
   * and stores the document id on the MySQL profile when navigating from the Drugs step to Pharmacies.
   * The optional `afterNavigate` callback runs immediately after the router navigation so callers
   * can emit chat messages that should appear on the pharmacies page rather than the drugs page.
   */
  private navigateToPharmaciesAfterSavingCurrentRx(afterNavigate?: () => void): void {
    const drugs = buildCurrentPrescriptionDrugsFromState(this.state);
    const finish = () => {
      this.state.setLoading(false);
      this.state.setSavingCurrentPrescription(false);
      this.router.navigate([AppRoutes.abs.PHARMACIES]);
      afterNavigate?.();
    };
    if (this.router.url === AppRoutes.abs.DRUGS) {
      this.state.setSavingCurrentPrescription(true);
      this.prescriptionService.saveCurrentDrugs(drugs).subscribe({
        next: () => {
          this.profileService.loadProfile().subscribe({ error: () => {} });
          finish();
        },
        error: () => {
          this.state.addAssistantMessage(PHARMACY_MESSAGES.SAVED_RX_FAILED_OPENING);
          finish();
        },
      });
    } else {
      finish();
    }
  }

  // ── Save Analysis ─────────────────────────────────────────────────────────

  private openSaveAnalysisDialog(): void {
    this.state.setLoading(false);
    const dialogRef = this.dialog.open(SavePrescriptionDialogComponent, {
      width: '420px',
      data: {
        title: 'Save Analysis',
        subtitle: 'Enter a name for this analysis snapshot',
        icon: 'assessment',
        defaultName: `Analysis – ${new Date().toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}`,
      },
    });

    dialogRef.afterClosed().subscribe((name: string | undefined) => {
      if (name?.trim()) {
        this.performSaveAnalysis(name.trim());
      } else {
        this.state.addAssistantMessage(APP_MESSAGES.SAVE_CANCELLED);
      }
    });
  }

  private performSaveAnalysis(name: string): void {
    this.state.setLoading(true);
    this.state.addAssistantMessage(ANALYSIS_MESSAGES.SAVE_IN_PROGRESS(name));

    this.analysisSnapshot.save(name).subscribe({
      next: () => {
        this.state.addAssistantMessage(APP_MESSAGES.SAVE_ANALYSIS_SUCCESS(name));
        this.state.setLoading(false);
        this.state.resetAll();
        this.router.navigate([AppRoutes.abs.PROFILE]);
      },
      error: (err) => {
        if (err.status === 409) {
          this.state.addAssistantMessage(
            ANALYSIS_MESSAGES.OVERWRITE_PROMPT(name)
          );
          this.pendingSaveAnalysisOverwrite.set(name);
          this.state.setLoading(false);
        } else {
          this.state.addAssistantMessage(APP_MESSAGES.SAVE_ANALYSIS_FAILED);
          this.state.setLoading(false);
        }
      },
    });
  }

}
