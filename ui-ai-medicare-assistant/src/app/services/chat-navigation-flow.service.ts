import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { catchError, of } from 'rxjs';
import { MedicareStateService } from './drug-state.service';
import { ProfileService } from './profile.service';
import { PrescriptionService } from './prescription.service';
import { LtcStateService } from '../long-term-care/ltc-state.service';
import { LtcService } from '../long-term-care/ltc.service';
import { ChatIntentResponse } from './chat-intent.service';
import { NAV_MESSAGES, LTC_MESSAGES, PHARMACY_MESSAGES, PLAN_MESSAGES } from '../constants/chat-messages';
import { AppRoutes } from '../app-routes.const';
import { buildCurrentPrescriptionDrugsFromState, buildSelectedPharmaciesSnapshotFromState } from '../medicare-analysis/current-prescription.mapper';

/** Step number ↔ label mapping for navigation messages. */
const STEP_LABELS: Record<1 | 2 | 3 | 4, string> = {
  1: 'Profile',
  2: 'Drugs',
  3: 'Pharmacies',
  4: 'Plans',
};

/** Map lowercase keyword from TARGETED_STEP_PATTERN capture group to step number. */
const KEYWORD_TO_STEP: Record<string, 1 | 2 | 3 | 4> = {
  profile: 1,
  drug: 2,
  drugs: 2,
  pharmacy: 3,
  pharmacies: 3,
  plan: 4,
  plans: 4,
};

/** Route for each step number. */
const STEP_ROUTES: Record<1 | 2 | 3 | 4, string> = {
  1: AppRoutes.abs.PROFILE,
  2: AppRoutes.abs.DRUGS,
  3: AppRoutes.abs.PHARMACIES,
  4: AppRoutes.abs.PLANS,
};

/** LTC step labels for navigation messages. */
const LTC_STEP_LABELS: Record<1 | 2, string> = {
  1: 'Profile',
  2: 'Care Type',
};

/** Map keyword to LTC step number. */
const LTC_KEYWORD_TO_STEP: Record<string, 1 | 2> = {
  profile: 1,
  'care type': 2,
  'care-type': 2,
  caretype: 2,
};

/** LTC route for each step. */
const LTC_STEP_ROUTES: Record<1 | 2, string> = {
  1: AppRoutes.abs.LTC_PROFILE,
  2: AppRoutes.abs.LTC_CARE_TYPE,
};

@Injectable({ providedIn: 'root' })
export class ChatNavigationFlowService {
  private state = inject(MedicareStateService);
  private profileService = inject(ProfileService);
  private prescriptionService = inject(PrescriptionService);
  private ltcState = inject(LtcStateService);
  private ltcService = inject(LtcService);
  private router = inject(Router);

  /** Confirmed FP drugs in working state/session. */
  private hasDrugsForPlanPrereqs(): boolean {
    this.state.hydrateConfirmedFromSessionStorage();
    return this.state.hasConfirmedDrugs();
  }

  /** Selected pharmacies in working state/session. */
  private hasPharmacyForPlanPrereqs(): boolean {
    return this.state.hasSelectedLookupPharmacies();
  }

  saveReturnRoute(): void {
    const url = this.router.url;
    if (url.startsWith(AppRoutes.abs.MEDICARE_ANALYSIS)) {
      this.state.returnRoute.set(url);
    }
  }

  /** Resolve a matched keyword from TARGETED_STEP_PATTERN to a step number. */
  resolveStepKeyword(keyword: string): 1 | 2 | 3 | 4 | null {
    return KEYWORD_TO_STEP[keyword.toLowerCase()] ?? null;
  }

  /** Resolve a matched keyword to an LTC step number. */
  resolveLtcStepKeyword(keyword: string): 1 | 2 | null {
    const lower = keyword.toLowerCase().replace(/-/g, ' ').trim();
    return LTC_KEYWORD_TO_STEP[lower] ?? null;
  }

  // ── Non-linear step navigation ────────────────────────────────────────────

  /**
   * Navigate to any step from any step. Auto-saves current step before leaving.
   * - Backward/lateral jumps: save current, navigate directly (no prerequisite checks).
   * - Forward jumps: save current, validate intermediate prerequisites, navigate if met.
   */
  handleStepNavigation(targetStep: 1 | 2 | 3 | 4): void {
    const currentStep = this.state.currentStep();

    // Same step — nothing to do
    if (targetStep === currentStep && this.router.url.startsWith(STEP_ROUTES[targetStep])) {
      this.state.addAssistantMessage(NAV_MESSAGES.ALREADY_ON_STEP(STEP_LABELS[targetStep]));
      this.state.setLoading(false);
      return;
    }

    // Forward jump: validate prerequisites for all intermediate steps
    if (targetStep > currentStep) {
      const missingStep = this.findMissingPrerequisite(currentStep, targetStep);
      if (missingStep) {
        this.state.addAssistantMessage(
          NAV_MESSAGES.CANNOT_NAVIGATE_MISSING_PREREQS(STEP_LABELS[missingStep])
        );
        this.state.setLoading(false);
        return;
      }
    }

    // Save return route for non-sequential jumps (skip > 1 step)
    if (Math.abs(targetStep - currentStep) > 1) {
      this.saveReturnRoute();
    }

    // Auto-save current step then navigate
    this.saveCurrentStepAndNavigate(currentStep, targetStep);
  }

  /**
   * Navigate to the immediate previous step (sequential backward).
   * Step 1: shows "already on first step". Steps 2-4: saves and goes back by one.
   */
  handleBackNavigation(): void {
    const currentStep = this.state.currentStep();
    if (currentStep <= 1) {
      this.state.addAssistantMessage(NAV_MESSAGES.ALREADY_ON_FIRST_STEP);
      this.state.setLoading(false);
      return;
    }
    const targetStep = (currentStep - 1) as 1 | 2 | 3 | 4;
    this.saveCurrentStepAndNavigate(currentStep, targetStep);
  }

  /**
   * Navigate to the immediate next step (sequential forward) with prerequisite validation.
   * Step 4: shows "already on last step". Steps 1-3: validates prerequisites, saves, and advances.
   * Step 1 (Profile): delegates to existing profile save mechanism.
   */
  handleForwardNavigation(): { handled: boolean; profileSave?: boolean } {
    const currentStep = this.state.currentStep();
    if (currentStep >= 4) {
      this.state.addAssistantMessage(NAV_MESSAGES.ALREADY_ON_LAST_STEP);
      this.state.setLoading(false);
      return { handled: true };
    }
    if (currentStep === 1) {
      // Profile → Drugs: delegate to existing profile save mechanism
      return { handled: false, profileSave: true };
    }
    const targetStep = (currentStep + 1) as 2 | 3 | 4;
    const missingStep = this.findMissingPrerequisite(currentStep, targetStep);
    if (missingStep) {
      this.state.addAssistantMessage(
        NAV_MESSAGES.CANNOT_NAVIGATE_MISSING_PREREQS(STEP_LABELS[missingStep])
      );
      this.state.setLoading(false);
      return { handled: true };
    }
    this.saveCurrentStepAndNavigate(currentStep, targetStep);
    return { handled: true };
  }

  /**
   * Navigate to the saved return route ("go back to where I was").
   */
  handleReturnNavigation(): void {
    const returnUrl = this.state.returnRoute() ?? this.ltcState.returnRoute();
    if (!returnUrl) {
      this.state.addAssistantMessage(NAV_MESSAGES.NO_RETURN_ROUTE);
      this.state.setLoading(false);
      return;
    }
    // Determine step label from the return URL
    const stepLabel = this.stepLabelFromUrl(returnUrl);
    this.state.addAssistantMessage(NAV_MESSAGES.RETURNING_TO(stepLabel));
    this.state.returnRoute.set(null);
    this.ltcState.returnRoute.set(null);
    this.state.setLoading(false);
    this.router.navigate([returnUrl]);
  }

  // ── LTC Navigation ─────────────────────────────────────────────────────────

  /**
   * Navigate to a specific LTC step. Auto-saves care-type data when leaving step 2.
   */
  handleLtcStepNavigation(targetStep: 1 | 2): void {
    const currentStep = this.ltcState.currentStep();

    if (targetStep === currentStep && this.router.url.startsWith(LTC_STEP_ROUTES[targetStep])) {
      this.state.addAssistantMessage(NAV_MESSAGES.ALREADY_ON_STEP(LTC_STEP_LABELS[targetStep]));
      this.state.setLoading(false);
      return;
    }

    // Forward: profile→care-type requires profile complete
    if (targetStep > currentStep && !this.profileService.isProfileComplete()) {
      this.state.addAssistantMessage(NAV_MESSAGES.CANNOT_NAVIGATE_MISSING_PREREQS('Profile'));
      this.state.setLoading(false);
      return;
    }

    this.saveLtcCurrentStepAndNavigate(currentStep, targetStep);
  }

  /**
   * Sequential backward navigation for LTC (step 2 → step 1).
   */
  handleLtcBackNavigation(): void {
    const currentStep = this.ltcState.currentStep();
    if (currentStep <= 1) {
      this.state.addAssistantMessage(NAV_MESSAGES.ALREADY_ON_FIRST_STEP);
      this.state.setLoading(false);
      return;
    }
    this.saveLtcCurrentStepAndNavigate(currentStep, 1);
  }

  /**
   * Sequential forward navigation for LTC. Step 2 is the last step (projection is a result page).
   */
  handleLtcForwardNavigation(): { handled: boolean } {
    const currentStep = this.ltcState.currentStep();
    if (currentStep >= 2) {
      this.state.addAssistantMessage(LTC_MESSAGES.LAST_STEP);
      this.state.setLoading(false);
      return { handled: true };
    }
    // Step 1 → Step 2: requires profile
    if (!this.profileService.isProfileComplete()) {
      this.state.addAssistantMessage(NAV_MESSAGES.CANNOT_NAVIGATE_MISSING_PREREQS('Profile'));
      this.state.setLoading(false);
      return { handled: true };
    }
    // Profile save + navigate to care type is handled by the caller (same as Medicare profile save)
    return { handled: false };
  }

  saveLtcReturnRoute(): void {
    const url = this.router.url;
    if (url.startsWith(AppRoutes.abs.LTC)) {
      this.ltcState.returnRoute.set(url);
    }
  }

  /**
   * Auto-save LTC care-type data then navigate.
   */
  private saveLtcCurrentStepAndNavigate(currentStep: 1 | 2, targetStep: 1 | 2): void {
    const targetRoute = LTC_STEP_ROUTES[targetStep];
    const targetLabel = LTC_STEP_LABELS[targetStep];

    const doNavigate = () => {
      this.ltcState.currentStep.set(targetStep);
      this.state.addAssistantMessage(NAV_MESSAGES.NAVIGATING_TO(targetLabel));
      this.state.setLoading(false);
      this.router.navigate([targetRoute]);
    };

    // Leaving care-type (step 2) → save care-type selections to DB
    if (currentStep === 2) {
      const saveBody = {
        healthProfile: this.ltcState.healthProfile(),
        numberOfAdultDayHealthCareYears: this.ltcState.adultDayYears(),
        numberOfHomeCareYears: this.ltcState.homeCareYears(),
        numberOfNursingCareYears: this.ltcState.nursingCareYears(),
      };
      this.ltcService.saveCurrent(saveBody).pipe(
        catchError(() => of(void 0)),
      ).subscribe(() => {
        this.state.addAssistantMessage(NAV_MESSAGES.SAVING_AND_NAVIGATING_TO(targetLabel));
        this.ltcState.currentStep.set(targetStep);
        this.state.setLoading(false);
        this.router.navigate([targetRoute]);
      });
      return;
    }

    // Step 1 (Profile) → no special save needed from nav (profile saves via requestSaveFromChat)
    doNavigate();
  }

  // ── Existing prerequisite-based navigation ────────────────────────────────

  navigateWithPrerequisites(
    result: ChatIntentResponse,
    targetRoute: string,
    onSuccess?: () => void,
    requirePlan = false,
  ): void {
    this.state.hydrateConfirmedFromSessionStorage();
    if (!this.profileService.isProfileComplete()) {
      this.state.addAssistantMessage(NAV_MESSAGES.REQUIRE_PROFILE_FIRST);
      this.state.setLoading(false);
      this.router.navigate([AppRoutes.abs.PROFILE]);
      return;
    }
    if (!this.hasDrugsForPlanPrereqs()) {
      this.state.addAssistantMessage(NAV_MESSAGES.REQUIRE_DRUG_FIRST);
      this.state.setLoading(false);
      this.router.navigate([AppRoutes.abs.DRUGS]);
      return;
    }
    if (!this.hasPharmacyForPlanPrereqs()) {
      this.state.addAssistantMessage(PHARMACY_MESSAGES.REQUIRE_SELECTION);
      this.state.setLoading(false);
      this.router.navigate([AppRoutes.abs.PHARMACIES]);
      return;
    }
    if (requirePlan && !this.state.hasCompletePlanSelection()) {
      this.state.addAssistantMessage(PLAN_MESSAGES.REQUIRE_SELECTION);
      this.state.setLoading(false);
      this.router.navigate([AppRoutes.abs.PLANS]);
      return;
    }
    onSuccess?.();
    this.state.addAssistantMessage(result.confirmationMessage || `Navigating to ${targetRoute}.`);
    this.state.setLoading(false);
    this.router.navigate([targetRoute]);
  }

  checkDrugPharmacyPrereqs(): boolean {
    this.state.hydrateConfirmedFromSessionStorage();
    // Already on plan selection: allow Part D ↔ MA switches without re-checking FP drug/pharmacy signals
    // (they can be empty briefly while saved recommendation / session catches up).
    if (this.router.url.startsWith(AppRoutes.abs.PLANS) && this.profileService.isProfileComplete()) {
      return true;
    }
    if (!this.profileService.isProfileComplete()) {
      this.state.addAssistantMessage(PLAN_MESSAGES.REQUIRE_PROFILE);
      this.state.setLoading(false);
      this.router.navigate([AppRoutes.abs.PROFILE]);
      return false;
    }
    if (!this.hasDrugsForPlanPrereqs()) {
      this.state.addAssistantMessage(PLAN_MESSAGES.REQUIRE_DRUGS);
      this.state.setLoading(false);
      this.router.navigate([AppRoutes.abs.DRUGS]);
      return false;
    }
    if (!this.hasPharmacyForPlanPrereqs()) {
      this.state.addAssistantMessage(PHARMACY_MESSAGES.REQUIRE_SELECTION);
      this.state.setLoading(false);
      this.router.navigate([AppRoutes.abs.PHARMACIES]);
      return false;
    }
    return true;
  }

  // ── Internals ─────────────────────────────────────────────────────────────

  /**
   * Check forward prerequisites between `from` and `to` (exclusive of `from`).
   * Returns the first missing step number, or null if all prerequisites are met.
   */
  private findMissingPrerequisite(from: number, to: number): 1 | 2 | 3 | 4 | null {
    this.state.hydrateConfirmedFromSessionStorage();
    // Profile must be complete for anything beyond step 1
    if (to >= 2 && from < 2 && !this.profileService.isProfileComplete()) return 1;
    // Drugs must exist for anything beyond step 2
    if (to >= 3 && !this.hasDrugsForPlanPrereqs()) return 2;
    // Pharmacies must be selected for step 4
    if (to >= 4 && !this.hasPharmacyForPlanPrereqs()) return 3;
    return null;
  }

  /**
   * Auto-save the current step's data, then navigate to the target step.
   * On save failure, navigates anyway with a warning (matches AnalysisShellComponent behavior).
   */
  private saveCurrentStepAndNavigate(currentStep: 1 | 2 | 3 | 4, targetStep: 1 | 2 | 3 | 4): void {
    const targetRoute = STEP_ROUTES[targetStep];
    const targetLabel = STEP_LABELS[targetStep];

    const doNavigate = () => {
      this.state.currentStep.set(targetStep);
      this.state.addAssistantMessage(NAV_MESSAGES.SAVING_AND_NAVIGATING_TO(targetLabel));
      this.state.setLoading(false);
      this.router.navigate([targetRoute]);
    };

    // Step 2 (Drugs) → save current drugs before leaving
    if (currentStep === 2 && this.state.hasConfirmedDrugs()) {
      const drugs = buildCurrentPrescriptionDrugsFromState(this.state);
      this.state.setSavingCurrentPrescription(true);
      this.prescriptionService
        .saveCurrentDrugs(drugs)
        .pipe(finalize(() => this.state.setSavingCurrentPrescription(false)))
        .subscribe({
          next: () => {
            this.profileService.loadProfile().subscribe({ error: () => {} });
            doNavigate();
          },
          error: () => doNavigate(),
        });
      return;
    }

    // Step 3 (Pharmacies) → save pharmacy selection before leaving
    if (currentStep === 3 && this.state.hasSelectedLookupPharmacies()) {
      this.state.pharmacySelectionConfirmed.set(true);
      this.state.setSavingCurrentPrescription(true);
      const pharmacies = buildSelectedPharmaciesSnapshotFromState(this.state);
      this.prescriptionService
        .saveCurrentPharmacy(pharmacies)
        .pipe(finalize(() => this.state.setSavingCurrentPrescription(false)))
        .subscribe({
          next: () => {
            this.profileService.loadProfile().subscribe({ error: () => {} });
            doNavigate();
          },
          error: () => doNavigate(),
        });
      return;
    }

    // Steps 1 (Profile) and 4 (Plans) — no special save needed from chat nav
    // (Profile saves happen via requestSaveFromChat; Plans have no intermediate save)
    this.state.currentStep.set(targetStep);
    this.state.addAssistantMessage(NAV_MESSAGES.NAVIGATING_TO(targetLabel));
    this.state.setLoading(false);
    this.router.navigate([targetRoute]);
  }

  /** Derive a human-readable step label from a URL. */
  private stepLabelFromUrl(url: string): string {
    if (url.startsWith(AppRoutes.abs.LTC_PROJECTION)) return 'LTC Projection';
    if (url.startsWith(AppRoutes.abs.LTC_CARE_TYPE)) return 'Care Type';
    if (url.startsWith(AppRoutes.abs.LTC_PROFILE)) return 'Profile';
    if (url.startsWith(AppRoutes.abs.PLANS)) return 'Plans';
    if (url.startsWith(AppRoutes.abs.PHARMACIES)) return 'Pharmacies';
    if (url.startsWith(AppRoutes.abs.DRUGS)) return 'Drugs';
    if (url.startsWith(AppRoutes.abs.PROFILE)) return 'Profile';
    if (url.startsWith(AppRoutes.abs.COST_PROJECTIONS)) return 'Cost Projections';
    return 'your previous step';
  }
}
