import {
  Component, ChangeDetectionStrategy, computed, inject, viewChild,
  ElementRef, effect, OnInit, signal, untracked,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, NavigationEnd } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { CommonModule } from '@angular/common';
import { DrugStateService } from '../services/drug-state.service';
import { ProfileService } from '../services/profile.service';
import { ReferenceDataService } from '../services/reference-data.service';
import { CountyLookupService } from '../services/county-lookup.service';
import { ChatWizardService } from '../services/chat-wizard.service';
import { ChatRouterService } from '../services/chat-router.service';
import { ChatNavigationFlowService } from '../services/chat-navigation-flow.service';
import { ChatDrugFlowService } from '../services/chat-drug-flow.service';
import { RecommendationStateService } from '../services/recommendation-state.service';
import { ChatAnalysisSelectionHydrationService } from '../services/chat-analysis-selection-hydration.service';
import { HttpLoaderService } from '../services/http-loader.service';
import { DrugNameSuggestion } from '../models/drug.model';
import { ProfileDto } from '../models/profile.model';
import { MarkdownPipe } from '../pipes/markdown.pipe';
import { ANALYSIS_MESSAGES, APP_MESSAGES, DRUG_MESSAGES, LTC_MESSAGES, PHARMACY_MESSAGES, PLAN_MESSAGES, PROFILE_MESSAGES } from '../constants/chat-messages';
import {
  isExplicitDrugStepCommand,
  isGenericProfileReviewHoldCommand,
  isNextStepCommand,
  shouldTriggerProfileSaveOnNext,
} from './chat-send-guards';

import { DeltaDisplayComponent } from './delta-display/delta-display.component';
import { HelpMenuComponent } from './help-menu/help-menu.component';
import { AppRoutes } from '../app-routes.const';

@Component({
  selector: 'app-chat',
  templateUrl: './chat.component.html',
  styleUrls: ['./chat.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, FormsModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatIconModule, MatProgressSpinnerModule, MatChipsModule,
    DeltaDisplayComponent, HelpMenuComponent, MarkdownPipe,
  ],
  standalone: true,
})
export class ChatComponent implements OnInit {
  private static readonly COST_PROJECTION_RESET_WARNINGS = [
    'Refreshing this page started a new analysis.',
    'Browser back started a new analysis.',
  ] as const;
  private static readonly ANALYSIS_REFRESH_NOISE_SNIPPETS = [
    "Everything is ready! Let's run your full Medicare cost analysis.",
    'Analysis reset. You can start a new prescription analysis',
  ] as const;

  protected state     = inject(DrugStateService);
  protected wizard    = inject(ChatWizardService);
  protected recState  = inject(RecommendationStateService);
  protected chatRouter = inject(ChatRouterService);
  protected navigationFlow = inject(ChatNavigationFlowService);
  protected drugFlow  = inject(ChatDrugFlowService);
  protected profileService = inject(ProfileService);
  protected httpLoader = inject(HttpLoaderService);
  private selectionHydrator = inject(ChatAnalysisSelectionHydrationService);
  private refData       = inject(ReferenceDataService);
  private countyLookup  = inject(CountyLookupService);
  private router      = inject(Router);

  /**
   * True while any known async work is in progress — chat input/send must stay disabled.
   * Includes orchestrator loading, FP/plan/pharmacy fetches, profile save, recommendation load,
   * and any HTTP request tracked by {@link HttpLoaderService} (except chat session message sync).
   */
  protected readonly chatSendBlocked = computed(
    () =>
      this.httpLoader.isLoading() ||
      this.state.isLoading() ||
      this.state.isVerifyingNames() ||
      this.state.isSavingCurrentPrescription() ||
      this.state.isDrugDetailsLoading() ||
      this.state.isPharmacyLookupLoading() ||
      this.state.isPartDLoading() ||
      this.state.isMedigapLoading() ||
      this.state.isMALoading() ||
      this.profileService.chatSaveInProgress() ||
      this.recState.isLoading(),
  );

  /**
   * Disables the text input + send button. Includes {@link chatSendBlocked} plus
   * any blocking UI state.
   */
  protected readonly chatInputDisabled = computed(
    () => this.chatSendBlocked(),
  );

  private messageContainer = viewChild<ElementRef<HTMLDivElement>>('messageContainer');

  input = '';

  private greetingShown = false;
  private startupGreetingPending = false;
  private lastHandledMedicareEntryRequest = 0;
  private readonly loginGreetingKey = 'chat-login-greeting-shown';
  private readonly switchedToEditMsg = PROFILE_MESSAGES.SWITCHED_TO_EDIT;
  /** User is editing profile after review message (footer Continue is the only way to advance). */
  protected pendingProfileModifyDetail = signal(false);
  private readonly storedDrugsChoiceKey = 'fp-drugs-stored-choice-asked';
  private readonly storedPharmacyChoiceKey = 'pharmacies-stored-choice-asked';
  private requestedActiveRecommendationForStoredDrugs = false;
  private storedDrugsAutoHydrateInFlight = false;
  private storedPharmacyAutoHydrateInFlight = false;
  private requestedActiveRecommendationForStoredPharmacy = false;

  constructor() {
    effect(() => {
      this.state.messages();
      this.state.isLoading();
      this.state.drugSuggestions();
      this.chatRouter.pendingRunAnalysisConfirm();
      setTimeout(() => this.scrollToBottom());
    });

    // Re-check auto-hydrate for stored drugs / pharmacy prompts when recommendation state changes.
    effect(() => {
      this.recState.activeRecommendation();
      this.recState.isLoading();
      this.autoHydrateStoredDrugsIfNeeded();
      this.autoHydrateStoredPharmacyIfNeeded();
    });

    effect(() => {
      // Wait until GET /api/profile completes so `isProfileComplete` is not stuck on its default `false`.
      this.profileService.profileLoadSettled();
      if (this.wizard.hasNewStep()) {
        this.announceNextWizardStep();
      }
    });

    effect(() => {
      const trigger = this.state.wizardResetTrigger();
      if (trigger > 0) {
        this.wizard.reset();
        // Clear session-storage gate keys so the next analysis run can re-hydrate
        // drugs, pharmacy and plans from the saved selection document.
        sessionStorage.removeItem(this.storedDrugsChoiceKey);
        sessionStorage.removeItem(this.storedPharmacyChoiceKey);
        // Reset in-flight / "already-asked" flags so the auto-hydrate helpers
        // are not blocked by a previous analysis run's state.
        this.requestedActiveRecommendationForStoredDrugs = false;
        this.storedDrugsAutoHydrateInFlight = false;
        this.storedPharmacyAutoHydrateInFlight = false;
        this.requestedActiveRecommendationForStoredPharmacy = false;
        if (this.router.url.startsWith(AppRoutes.abs.MEDICARE_ANALYSIS)) {
          this.removeAnalysisRefreshNoiseIfNeeded(this.router.url);
        }
      }
    });

    // If user starts editing profile on the review step, nudge toward footer Continue.
    effect(() => {
      const hasUnsaved = this.chatRouter.hasUnsavedProfileChanges();
      const onAnalysisProfile = this.router.url.startsWith(AppRoutes.abs.PROFILE);
      if (!hasUnsaved || !onAnalysisProfile) return;
      if (this.wizard.currentStep() !== 'PROFILE_REVIEW') return;
      if (this.pendingProfileModifyDetail()) return;
      this.pendingProfileModifyDetail.set(true);
      this.state.addAssistantMessage(
        'I can see you started updating your profile. Continue making changes, then click **Continue to Drugs** in the footer when finished.'
      );
    });

    // Wait for profile to be loaded before showing the real greeting + mode buttons.
    effect(() => {
      const profile = this.profileService.profile();
      if (profile !== null && !this.greetingShown) {
        this.greetingShown = true;
        if (!this.startupGreetingPending) return;
        sessionStorage.setItem(this.loginGreetingKey, '1');
        // Replace the placeholder with the real greeting only if Medicare has NOT started yet.
        // If Medicare already started (beginMedicareAnalysisFlow ran first), the greeting was
        // already resolved there to keep it before the user message and profile review.
        if (this.wizard.mode() !== 'MEDICARE_ANALYSIS') {
          this.state.replaceLastAssistantMessage(this.buildGreeting());
        }
      }
    });

    this.refData.load();

    // Saved page (and similar) calls ChatWizardService.requestMedicareAnalysisEntry().
    effect(() => {
      const n = this.wizard.medicareEntryRequest();
      if (n === 0 || n === this.lastHandledMedicareEntryRequest) return;
      this.lastHandledMedicareEntryRequest = n;
      // Run untracked so this effect depends only on medicareEntryRequest.
      untracked(() => this.beginMedicareAnalysisFlow(true));
    });
  }

  ngOnInit(): void {
    this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) {
        this.removeCostProjectionResetWarningsIfIrrelevant(this.router.url);
        this.removeAnalysisRefreshNoiseIfNeeded(this.router.url);
        if (!this.router.url.startsWith(AppRoutes.abs.PROFILE)) {
          this.pendingProfileModifyDetail.set(false);
        }
        this.autoHydrateStoredDrugsIfNeeded();
        this.autoHydrateStoredPharmacyIfNeeded();
        if (this.router.url.startsWith(AppRoutes.abs.PLANS)) {
          this.selectionHydrator.hydratePlansFromActiveRecommendationSelection();
        }

        // Cross-page drug search: user typed a drug name on a non-drugs page (e.g. "add
        // metformin" on pharmacies). After navigation lands on fp-drugs, auto-fire the search.
        if (this.router.url.startsWith(AppRoutes.abs.DRUGS)) {
          const pending = this.state.pendingCrossPageDrugSearch();
          if (pending) {
            this.state.pendingCrossPageDrugSearch.set(null);
            setTimeout(() => this.drugFlow.runDrugFlow(pending), 50);
          }
        }
      }
    });
    this.removeCostProjectionResetWarningsIfIrrelevant(this.router.url);
    this.removeAnalysisRefreshNoiseIfNeeded(this.router.url);

    // Profile remains editable; stale "switched to edit" prompts are not needed.
    this.state.removeAssistantMessagesContaining(this.switchedToEditMsg);

    const shownInThisSession = sessionStorage.getItem(this.loginGreetingKey) === '1';
    const currentUrl = this.router.url;
    const isAnalysisPage = currentUrl.startsWith(AppRoutes.abs.MEDICARE_ANALYSIS);
    const isLtcPage = currentUrl.startsWith(AppRoutes.abs.LTC);
    this.startupGreetingPending =
      this.state.messages().length === 0 &&
      !shownInThisSession &&
      !isAnalysisPage &&
      !isLtcPage;

    // Show a placeholder greeting immediately while profile loads (only once per login session)
    if (this.startupGreetingPending) {
      this.state.addAssistantMessage(
        "Hello! I'm your AI Medicare Assistant. Analysing your profile, please wait..."
      );
    }

    // Past the profile step in analysis — treat intro gate as satisfied (e.g. refresh on drugs).
    // Must be set BEFORE resumeMedicareAnalysis() so lastAnnouncedStep is computed with the
    // correct step (not PROFILE_REVIEW), preventing a spurious hasNewStep on resume.
    const isPastProfileStep =
      isAnalysisPage &&
      (currentUrl.includes(AppRoutes.abs.DRUGS) ||
        currentUrl.includes(AppRoutes.abs.PHARMACIES) ||
        currentUrl.includes(AppRoutes.abs.PLANS) ||
        currentUrl.includes(AppRoutes.abs.COST_PROJECTIONS));
    if (isPastProfileStep) {
      this.wizard.medicareProfileIntroComplete.set(true);
    }

    // Skip resume when a fresh entry request is pending (the entry effect will handle init).
    const hasPendingMedicareEntry = this.wizard.medicareEntryRequest() > this.lastHandledMedicareEntryRequest;
    if (isAnalysisPage && !hasPendingMedicareEntry && this.state.messages().length === 0) {
      this.wizard.resumeMedicareAnalysis();
      this.state.addAssistantMessage(this.buildAnalysisResumeMessage(currentUrl));
    } else if (isAnalysisPage && !hasPendingMedicareEntry) {
      this.wizard.resumeMedicareAnalysis();
    }

    // LTC resume on hard refresh
    if (isLtcPage) {
      const isPastLtcProfile = currentUrl.startsWith(AppRoutes.abs.LTC_CARE_TYPE) ||
        currentUrl.startsWith(AppRoutes.abs.LTC_PROJECTION);
      if (isPastLtcProfile) {
        this.wizard.ltcProfileIntroComplete.set(true);
      }
      if (this.state.messages().length === 0) {
        this.wizard.resumeLtcAnalysis();
        this.state.addAssistantMessage(this.buildLtcResumeMessage(currentUrl));
      } else {
        this.wizard.resumeLtcAnalysis();
      }
    }

    if (!isAnalysisPage) {
      // Clear old greeting variant and always show new quick actions on home/saved screens.
      this.state.removeAssistantMessagesContaining('Would you like to modify anything in your profile?');
      this.pendingProfileModifyDetail.set(false);
    }

    this.ensureProfileReviewMessageIfNeeded(currentUrl);
  }

  /** After refresh on profile, re-post review summary if messages were cleared but wizard is still on PROFILE_REVIEW. */
  private ensureProfileReviewMessageIfNeeded(currentUrl: string): void {
    if (!currentUrl.startsWith(AppRoutes.abs.PROFILE)) return;
    if (this.wizard.mode() !== 'MEDICARE_ANALYSIS') return;
    if (this.wizard.currentStep() !== 'PROFILE_REVIEW') return;
    // Skip if a fresh Medicare entry request will be handled by the dedicated effect
    // (its async postProfileReviewMessage hasn't resolved yet so the message isn't in state).
    if (this.wizard.medicareEntryRequest() > this.lastHandledMedicareEntryRequest) return;
    const hasReviewMessage = this.state
      .messages()
      .some((m) => m.role === 'assistant' && m.content.includes(PROFILE_MESSAGES.REVIEW_INTRO));
    if (!hasReviewMessage) {
      this.postProfileReviewMessage();
    }
  }

  // ── Startup ───────────────────────────────────────────────────────────────

  private buildGreeting(): string {
    return "Hello! I'm your AI Medicare Assistant.\n\n" +
      "**What would you like to do today?** Choose one of the action cards below.";
  }

  /** Same option labels as `UserProfileComponent` health dropdown. */
  private static readonly HEALTH_CONDITION_OPTIONS: { value: number; label: string }[] = [
    { value: 1, label: 'Best Health' },
    { value: 2, label: 'Good Health' },
    { value: 3, label: 'Moderate Health' },
    { value: 4, label: 'Poor Health' },
    { value: 5, label: 'Sick' },
  ];

  private healthConditionLabel(p: ProfileDto): string {
    const row = ChatComponent.HEALTH_CONDITION_OPTIONS.find((h) => h.value === p.healthCondition);
    return row ? `${row.value} — ${row.label}` : String(p.healthCondition);
  }

  private taxFilingStatusLabel(status: string): string {
    const match = this.refData.taxFilingStatuses().find((s) => s.value === status);
    if (match) return match.label;
    const fallback: Record<string, string> = {
      MARRIED_FILING_JOINTLY: 'Jointly',
      FILING_INDIVIDUALLY: 'Individually',
    };
    return fallback[status] ?? status;
  }

  /**
   * @param magiTierLabel — resolved from the same MAGI API as the profile form; falls back to raw `magiTier` if omitted.
   */
  private buildMedicareProfileSummaryMarkdown(magiTierLabel?: string): string {
    const p = this.profileService.profile()?.profile;
    if (!p) {
      return '_Profile details are still loading. If this does not update, refresh the page._';
    }
    const tobacco = p.tobaccoStatus === 1 ? 'Yes' : 'No';
    const magiDisplay = magiTierLabel ?? p.magiTier;
    const lines = [
      `- **Name:** ${p.firstName} ${p.lastName}`,
      `- **Date of birth:** ${p.dateOfBirth}`,
      `- **Gender:** ${p.gender === 'M' ? 'Male' : p.gender === 'F' ? 'Female' : p.gender}`,
      `- **Coverage year:** ${p.coverageYear}`,
      `- **Address:** ${p.addressLine1}, ${p.city}, ${p.state} ${p.zipCode}`,
      `- **County:** ${p.county} (${p.countyCode})`,
      `- **Health condition:** ${this.healthConditionLabel(p)}`,
      `- **Life expectancy:** ${p.lifeExpectancy}`,
      `- **Tobacco:** ${tobacco}`,
      `- **Tax filing:** ${this.taxFilingStatusLabel(p.taxFilingStatus)}`,
      `- **MAGI tier:** ${magiDisplay}`,
    ];
    return lines.join('\n');
  }

  private postProfileReviewMessage(): void {
    const p = this.profileService.profile()?.profile;
    if (!p) {
      this.state.addAssistantMessage(
        `${PROFILE_MESSAGES.REVIEW_INTRO}\n\n${PROFILE_MESSAGES.REVIEW_LOADING}\n\n${PROFILE_MESSAGES.REVIEW_QUESTION}`
      );
      this.router.navigate([AppRoutes.abs.PROFILE]);
      return;
    }
    this.countyLookup.getMagiTiers(p.taxFilingStatus, p.coverageYear).subscribe({
      next: (tiers) => {
        const match = tiers.find((t) => String(t.value) === String(p.magiTier));
        const magiLabel = match?.label ?? p.magiTier;
        const summary = this.buildMedicareProfileSummaryMarkdown(magiLabel);
        this.state.addAssistantMessage(
          `${PROFILE_MESSAGES.REVIEW_INTRO}\n\n${summary}\n\n${PROFILE_MESSAGES.REVIEW_QUESTION}`
        );
        this.router.navigate([AppRoutes.abs.PROFILE]);
      },
      error: () => {
        const summary = this.buildMedicareProfileSummaryMarkdown();
        this.state.addAssistantMessage(
          `${PROFILE_MESSAGES.REVIEW_INTRO}\n\n${summary}\n\n${PROFILE_MESSAGES.REVIEW_QUESTION}`
        );
        this.router.navigate([AppRoutes.abs.PROFILE]);
      },
    });
  }

  private buildAnalysisResumeMessage(url: string): string {
    if (url.startsWith(AppRoutes.abs.PROFILE)) {
      return 'Resumed the Profile step. Review your details on the left, then use **Continue to Drugs** in the footer when ready.';
    }
    if (url.startsWith(AppRoutes.abs.DRUGS)) {
      this.autoHydrateStoredDrugsIfNeeded();
      return 'Resumed your progress on the Drugs step. Continue adding or updating your drugs.';
    }
    if (url.startsWith(AppRoutes.abs.PHARMACIES)) {
      this.autoHydrateStoredPharmacyIfNeeded();
      return 'Resumed your progress on the Pharmacies step. Continue selecting your preferred pharmacies.';
    }
    if (url.startsWith(AppRoutes.abs.PLANS)) {
      return 'Resumed your progress on the Plans step. Continue reviewing and selecting plans.';
    }
    if (url.startsWith(AppRoutes.abs.COST_PROJECTIONS)) {
      return 'Resumed your progress on Cost Projections. Continue reviewing your results.';
    }
    return 'Resumed your previous analysis progress.';
  }

  private buildLtcResumeMessage(url: string): string {
    if (url.startsWith(AppRoutes.abs.LTC_CARE_TYPE)) {
      return LTC_MESSAGES.RESUME_CARE_TYPE;
    }
    if (url.startsWith(AppRoutes.abs.LTC_PROJECTION)) {
      return LTC_MESSAGES.RESUME_PROJECTION;
    }
    return LTC_MESSAGES.RESUME_PROFILE;
  }

  onProfileUpdateDecision(action: 'save' | 'cancel'): void {
    this.state.addUserMessage(action === 'save' ? 'Save' : 'Cancel');
    this.chatRouter.resolvePendingProfileUpdate(action === 'save');
  }

  onTaxFilingChoice(value: 'MARRIED_FILING_JOINTLY' | 'FILING_INDIVIDUALLY'): void {
    this.state.addUserMessage(value === 'MARRIED_FILING_JOINTLY' ? 'Jointly' : 'Individually');
    this.chatRouter.applyTaxFilingChoice(value);
  }

  onMagiTierChoice(value: number): void {
    this.state.addUserMessage(`MAGI Tier ${value}`);
    this.chatRouter.applyMagiTierChoice(value);
  }

  onDirectProfileSave(): void {
    this.state.addUserMessage('Save Profile Now');
    this.chatRouter.triggerDirectProfileSave();
  }

  onRunAnalysisConfirm(accept: boolean): void {
    this.state.addUserMessage(accept ? 'Yes' : 'No');
    this.chatRouter.resolveRunAnalysisConfirmation(accept);
  }

  selectMode(mode: 'MEDICARE_ANALYSIS' | 'LONG_TERM_ANALYSIS') {
    if (mode === 'LONG_TERM_ANALYSIS') {
      this.state.addUserMessage('Long Term Analysis');
      this.beginLtcAnalysisFlow();
      return;
    }

    this.beginMedicareAnalysisFlow(true);
  }

  /** Shared entry for Medicare Analysis (chat action cards or Saved page via `requestMedicareAnalysisEntry()`). */
  private beginMedicareAnalysisFlow(addUserLine: boolean) {
    // If the greeting placeholder hasn't been resolved yet (profile still loading),
    // resolve it to the real greeting NOW so it always appears before the user message and profile review.
    if (this.startupGreetingPending && !this.greetingShown) {
      this.greetingShown = true;
      this.state.replaceLastAssistantMessage(this.buildGreeting());
      sessionStorage.setItem(this.loginGreetingKey, '1');
    }
    if (addUserLine) {
      this.state.addUserMessage('Medicare Analysis');
    }
    this.state.drugDetails.set(null);
    this.state.confirmedDrugNames.set(new Set());
    this.state.selectedLookupPharmacies.set([]);
    this.state.pharmacySelectionConfirmed.set(false);
    this.state.resetPlanSelections();
    this.requestedActiveRecommendationForStoredDrugs = false;
    this.storedDrugsAutoHydrateInFlight = false;
    this.storedPharmacyAutoHydrateInFlight = false;
    this.requestedActiveRecommendationForStoredPharmacy = false;
    sessionStorage.removeItem(this.storedDrugsChoiceKey);
    sessionStorage.removeItem(this.storedPharmacyChoiceKey);
    this.state.removeAssistantMessagesContaining(DRUG_MESSAGES.STORED_DRUGS_PROMPT);
    this.state.removeAssistantMessagesContaining(DRUG_MESSAGES.STORED_DRUGS_AUTO_LOADED);
    this.state.removeAssistantMessagesContaining(PHARMACY_MESSAGES.STORED_PHARMACY_PROMPT);
    this.state.removeAssistantMessagesContaining(PHARMACY_MESSAGES.STORED_PHARMACY_AUTO_LOADED);
    this.state.persistSelections();
    this.startFreshMedicareAnalysis();
  }

  openSavedRecommendations() {
    this.state.addUserMessage('Saved Recommendations');
    this.router.navigate(['/saved']);
  }

  compareMedicareAnalyses() {
    this.state.addUserMessage('Compare Medicare Analysis');
    this.state.addAssistantMessage(
      'Opening saved recommendations. Select 2 items and click "Compare selected" at the top.'
    );
    this.router.navigate(['/saved']);
  }

  compareLongTermAnalyses() {
    this.state.addUserMessage('Compare Long Term Analysis');
    this.state.addAssistantMessage('Opening saved recommendations. Filter by Long Term Care to compare two LTC analyses.');
    this.router.navigate(['/saved']);
  }

  private startFreshMedicareAnalysis() {
    this.wizard.startMedicareAnalysis();
    this.announceNextWizardStep();
  }

  // ── LTC Wizard ──────────────────────────────────────────────────────────

  private beginLtcAnalysisFlow() {
    // Resolve greeting if needed
    if (this.startupGreetingPending && !this.greetingShown) {
      this.greetingShown = true;
      this.state.replaceLastAssistantMessage(this.buildGreeting());
      sessionStorage.setItem(this.loginGreetingKey, '1');
    }
    this.wizard.startLtcAnalysis();
    this.announceNextWizardStep();
  }

  // ── Wizard Step Announcer ─────────────────────────────────────────────────

  private announceNextWizardStep() {
    if ((this.wizard.mode() === 'MEDICARE_ANALYSIS' || this.wizard.mode() === 'LONG_TERM_ANALYSIS') &&
        !this.profileService.profileLoadSettled()) {
      return;
    }
    this.wizard.markStepAnnounced();
    const step = this.wizard.currentStep();

    switch (step) {
      case 'PROFILE': {
        this.state.addAssistantMessage(PROFILE_MESSAGES.START_PROFILE);
        this.router.navigate([AppRoutes.abs.PROFILE]);
        break;
      }
      case 'PROFILE_REVIEW': {
        this.pendingProfileModifyDetail.set(false);
        this.postProfileReviewMessage();
        break;
      }
      case 'DRUGS': {
        if (!this.hasStoredDrugsInActiveRecommendation()) {
          this.state.addAssistantMessage(DRUG_MESSAGES.STEP_PROMPT);
        }
        this.router.navigate([AppRoutes.abs.DRUGS]);
        break;
      }
      case 'PHARMACIES': {
        if (!this.state.hasConfirmedDrugs()) {
          this.state.addAssistantMessage(DRUG_MESSAGES.NEED_DRUGS_AFTER_PHARMACY);
          this.router.navigate([AppRoutes.abs.DRUGS]);
        } else if (!this.router.url.startsWith(AppRoutes.abs.DRUGS)) {
          // Navigate to pharmacies only if we are not already on the drugs page.
          // Never auto-confirm pharmacySelectionConfirmed here — the user must explicitly
          // advance via "Continue to Plans" or the chat "go to plans" / "next" command.
          if (!this.hasStoredPharmacyInActiveRecommendation()) {
            this.state.addAssistantMessage(PHARMACY_MESSAGES.STEP_PROMPT);
          }
          this.router.navigate([AppRoutes.abs.PHARMACIES]);
        }
        break;
      }
      case 'PLANS': {
        // Only navigate to Plans if the user is already on Pharmacies.
        // Bootstrap hydration can satisfy pharmacy conditions while the user is still
        // on an earlier step — guard against that auto-skip.
        if (this.router.url.startsWith(AppRoutes.abs.PHARMACIES)) {
          this.state.addAssistantMessage(PLAN_MESSAGES.STEP_PROMPT);
          this.router.navigate([AppRoutes.abs.PLANS]);
        }
        break;
      }
      case 'ANALYSIS': {
        // Only announce readiness if the user is on the Plans page.
        // Do NOT navigate to Cost Projections — the cost calculation has not run yet.
        // PlanRecommendationComponent.calculateLifetimeCost() handles navigation after the API call.
        if (this.router.url.startsWith(AppRoutes.abs.PLANS)) {
          this.state.addAssistantMessage(ANALYSIS_MESSAGES.STEP_PROMPT);
        }
        break;
      }
      case 'COMPLETE': {
        this.state.addAssistantMessage(ANALYSIS_MESSAGES.COMPLETE_PROMPT);
        break;
      }

      // LTC wizard steps
      case 'LTC_PROFILE': {
        this.state.addAssistantMessage(LTC_MESSAGES.START_PROFILE);
        this.router.navigate([AppRoutes.abs.LTC_PROFILE]);
        break;
      }
      case 'LTC_PROFILE_REVIEW': {
        this.state.addAssistantMessage(LTC_MESSAGES.PROFILE_REVIEW);
        this.router.navigate([AppRoutes.abs.LTC_PROFILE]);
        break;
      }
      case 'LTC_CARE_TYPE': {
        this.state.addAssistantMessage(LTC_MESSAGES.CARE_TYPE_PROMPT);
        this.router.navigate([AppRoutes.abs.LTC_CARE_TYPE]);
        break;
      }
    }
  }

  // ── Free-form Send ────────────────────────────────────────────────────────

  send() {
    const text = this.input.trim();
    if (!text || this.chatInputDisabled()) return;

    const lower = text.toLowerCase();
    const wantsNextStep = isNextStepCommand(lower);
    const onAnalysisProfile = this.router.url.startsWith(AppRoutes.abs.PROFILE);
    // On PROFILE_REVIEW, generic "next/continue" always nudges the footer.
    // Explicit drug commands ("go to drugs") and AI-typed typos bypass this and reach save below.
    const isProfileReviewStep =
      onAnalysisProfile && this.wizard.currentStep() === 'PROFILE_REVIEW';
    const isGenericProfileHold = isProfileReviewStep && isGenericProfileReviewHoldCommand(lower);
    if (isGenericProfileHold) {
      this.input = '';
      this.state.addUserMessage(text);
      this.state.addAssistantMessage(PROFILE_MESSAGES.USE_CONTINUE_TO_DRUGS_IN_FOOTER);
      return;
    }

    // Profile page guard: in edit mode, explicit drug-step consent means "save and continue"
    // (valid => continue to drugs, invalid => stay and show missing/invalid fields).
    if (shouldTriggerProfileSaveOnNext(lower, onAnalysisProfile, true)) {
      this.input = '';
      this.state.addUserMessage(text);
      this.chatRouter.triggerDirectProfileSave();
      return;
    }
    if (this.pendingProfileModifyDetail() && onAnalysisProfile) {
      const looksLikeDiscardAttempt = /\b(canc|cancel|discard|abort)\b/.test(lower);
      if (looksLikeDiscardAttempt && this.chatRouter.hasUnsavedProfileChanges()) {
        this.input = '';
        this.state.addUserMessage(text);
        this.state.setLoading(true);
        this.chatRouter.resolveProfileEditIntentWithAi(text);
        return;
      }
      if (wantsNextStep) {
        if (this.chatRouter.hasUnsavedProfileChanges() && isExplicitDrugStepCommand(lower)) {
          this.input = '';
          this.state.addUserMessage(text);
          this.chatRouter.triggerDirectProfileSave();
          return;
        }
        if (isGenericProfileReviewHoldCommand(lower)) {
          this.input = '';
          this.state.addUserMessage(text);
          this.state.addAssistantMessage(PROFILE_MESSAGES.USE_CONTINUE_TO_DRUGS_IN_FOOTER);
          return;
        }
      }
    }

    // Non-profile steps: "next"/"continue"/"move forward" triggers sequential forward navigation
    // with prerequisite validation and auto-save.
    const onMedicareAnalysis = this.router.url.startsWith(AppRoutes.abs.MEDICARE_ANALYSIS);
    if (wantsNextStep && !onAnalysisProfile && onMedicareAnalysis) {
      this.state.addUserMessage(text);
      this.input = '';
      this.state.setLoading(true);
      const result = this.navigationFlow.handleForwardNavigation();
      if (result.handled) return;
      // profileSave should not happen here (we're not on profile), but handle defensively
      if (result.profileSave) {
        this.chatRouter.triggerDirectProfileSave();
        return;
      }
    }

    // LTC: "next"/"continue" on LTC routes
    const onLtcPage = this.router.url.startsWith(AppRoutes.abs.LTC);
    const onLtcProfile = this.router.url.startsWith(AppRoutes.abs.LTC_PROFILE) &&
      !this.router.url.startsWith(AppRoutes.abs.LTC_PROJECTION);
    if (wantsNextStep && onLtcPage) {
      if (onLtcProfile) {
        this.input = '';
        this.state.addUserMessage(text);
        this.profileService.requestSaveFromChat();
        return;
      }
      // On care-type: "next" → last step message
      this.state.addUserMessage(text);
      this.input = '';
      this.state.setLoading(true);
      const ltcResult = this.navigationFlow.handleLtcForwardNavigation();
      if (ltcResult.handled) return;
    }

    this.state.addUserMessage(text);
    this.input = '';
    this.state.setLoading(true);

    this.chatRouter.route(text, (t) => this.drugFlow.runDrugFlow(t));
  }

  /**
   * On /analysis/fp-drugs, load drugs from the active saved recommendation (Mongo-backed API)
   * without a yes/no prompt. Informs the user once; marks session so we do not re-run.
   */
  private autoHydrateStoredDrugsIfNeeded(): void {
    const alreadyHandled = sessionStorage.getItem(this.storedDrugsChoiceKey) === '1';
    if (alreadyHandled || this.storedDrugsAutoHydrateInFlight) return;
    if (!this.router.url.startsWith(AppRoutes.abs.DRUGS)) return;
    if (this.state.confirmedDrugNames().size > 0) return;
    if (!this.hasStoredSelectionsDocument()) return;

    this.storedDrugsAutoHydrateInFlight = true;
    this.selectionHydrator.hydrateDrugsFromActiveRecommendationSelection$({ silent: true }).subscribe({
      next: () => {
        this.storedDrugsAutoHydrateInFlight = false;
        if (this.state.confirmedDrugNames().size > 0) {
          sessionStorage.setItem(this.storedDrugsChoiceKey, '1');
          this.state.persistSelections();
          this.state.removeAssistantMessagesContaining(DRUG_MESSAGES.STORED_DRUGS_PROMPT);
          this.state.addAssistantMessage(DRUG_MESSAGES.STORED_DRUGS_AUTO_LOADED);
          if (!this.router.url.startsWith(AppRoutes.abs.DRUGS)) {
            this.router.navigate([AppRoutes.abs.DRUGS]);
          }
        } else {
          this.state.removeAssistantMessagesContaining(DRUG_MESSAGES.STORED_DRUGS_PROMPT);
        }
      },
      error: () => {
        this.storedDrugsAutoHydrateInFlight = false;
        this.state.addAssistantMessage(DRUG_MESSAGES.RESTORE_FAILED);
      },
    });
  }

  private hasStoredDrugsInActiveRecommendation(): boolean {
    return this.hasStoredSelectionsDocument();
  }

  /**
   * On /analysis/pharmacies, load pharmacy from the active saved recommendation without a prompt.
   * Informs the user once; marks session so we do not re-run. Does not navigate or advance the wizard.
   */
  private autoHydrateStoredPharmacyIfNeeded(): void {
    const alreadyHandled = sessionStorage.getItem(this.storedPharmacyChoiceKey) === '1';
    if (alreadyHandled || this.storedPharmacyAutoHydrateInFlight) return;
    if (!this.router.url.startsWith(AppRoutes.abs.PHARMACIES)) return;
    if (this.state.hasSelectedLookupPharmacies()) return;
    if (!this.hasStoredSelectionsDocument()) return;

    this.storedPharmacyAutoHydrateInFlight = true;
    this.selectionHydrator.hydratePharmacyFromActiveRecommendationSelection(true);
    this.storedPharmacyAutoHydrateInFlight = false;
    if (this.state.hasSelectedLookupPharmacies()) {
      sessionStorage.setItem(this.storedPharmacyChoiceKey, '1');
      this.state.persistSelections();
      this.state.removeAssistantMessagesContaining(PHARMACY_MESSAGES.STORED_PHARMACY_PROMPT);
      this.state.addAssistantMessage(PHARMACY_MESSAGES.STORED_PHARMACY_AUTO_LOADED);
    } else {
      this.state.removeAssistantMessagesContaining(PHARMACY_MESSAGES.STORED_PHARMACY_PROMPT);
    }
  }

  private hasStoredPharmacyInActiveRecommendation(): boolean {
    return this.hasStoredSelectionsDocument();
  }

  private removeCostProjectionResetWarningsIfIrrelevant(currentUrl: string): void {
    if (currentUrl.startsWith(AppRoutes.abs.COST_PROJECTIONS)) return;
    for (const warningSnippet of ChatComponent.COST_PROJECTION_RESET_WARNINGS) {
      this.state.removeAssistantMessagesContaining(warningSnippet);
    }
  }

  private removeAnalysisRefreshNoiseIfNeeded(currentUrl: string): void {
    if (!currentUrl.startsWith(AppRoutes.abs.MEDICARE_ANALYSIS)) return;
    if (currentUrl.startsWith(AppRoutes.abs.COST_PROJECTIONS)) return;
    for (const snippet of ChatComponent.ANALYSIS_REFRESH_NOISE_SNIPPETS) {
      this.state.removeAssistantMessagesContaining(snippet);
    }
  }

  private hasStoredSelectionsDocument(): boolean {
    return !!this.profileService.profile()?.currentPrescriptionDocumentId;
  }

  confirmOrCancel(answer: 'yes' | 'no'): void {
    this.chatRouter.confirmOrCancel(answer);
  }

  onHelpAction(action: string): void {
    this.chatRouter.activeDisplayData.set(null);
    this.input = action;
    this.send();
  }

  // ── Drug suggestion helpers (delegated) ───────────────────────────────────

  selectCandidate(suggestion: DrugNameSuggestion, candidateName: string) {
    this.drugFlow.selectCandidate(suggestion, candidateName);
  }

  isSelected(suggestion: DrugNameSuggestion, candidateName: string): boolean {
    return this.drugFlow.isSelected(suggestion, candidateName);
  }

  allSelected(): boolean {
    return this.drugFlow.allSelected();
  }

  confirmAndAnalyze() {
    this.drugFlow.confirmAndAnalyze();
  }

  cancelSuggestions() {
    this.drugFlow.cancelSuggestions();
  }

  // ── Scroll ────────────────────────────────────────────────────────────────

  private scrollToBottom() {
    const el = this.messageContainer()?.nativeElement;
    if (el) el.scrollTop = el.scrollHeight;
  }
}
