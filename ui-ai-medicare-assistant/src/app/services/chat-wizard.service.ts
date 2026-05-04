import { Injectable, signal, computed, inject } from '@angular/core';
import { MedicareStateService } from './drug-state.service';
import { ProfileService } from './profile.service';
import { LtcStateService } from '../long-term-care/ltc-state.service';

export type WizardMode = 'NONE' | 'MEDICARE_ANALYSIS' | 'LONG_TERM_ANALYSIS';

export type WizardStep =
  | 'AWAITING_MODE'      // startup — mode not yet selected
  | 'PROFILE'            // needs profile completion
  | 'PROFILE_REVIEW'     // profile complete — user must confirm before drugs (Medicare flow)
  | 'DRUGS'              // needs confirmed prescription drugs
  | 'PHARMACIES'         // needs pharmacy selection confirmed
  | 'PLANS'              // needs plan selection
  | 'ANALYSIS'           // all done, navigate to cost projections
  | 'COMPLETE'           // analysis page reached
  // LTC steps
  | 'LTC_PROFILE'        // LTC: needs profile completion
  | 'LTC_PROFILE_REVIEW' // LTC: profile complete — confirm before care type
  | 'LTC_CARE_TYPE';     // LTC: needs care type configuration

@Injectable({ providedIn: 'root' })
export class ChatWizardService {
  private state = inject(MedicareStateService);
  private profileService = inject(ProfileService);
  private ltcState = inject(LtcStateService);

  /** Which top-level mode is active. NONE = free-form only. */
  readonly mode = signal<WizardMode>('NONE');

  /** Whether the startup mode-selection buttons should be visible in the chat */
  readonly showModeButtons = signal(false);

  /**
   * After profile is complete, wizard stays on PROFILE_REVIEW until the user uses shell
   * **Continue to Drugs** (save + navigate). Set true in UserProfileComponent on that success.
   */
  readonly medicareProfileIntroComplete = signal(false);

  /**
   * After profile is complete in LTC, wizard stays on LTC_PROFILE_REVIEW until the user
   * uses shell **Continue to Care Type** (save + navigate). Set true on that success.
   */
  readonly ltcProfileIntroComplete = signal(false);

  /**
   * Incremented when an entry point outside chat (e.g. Saved page) should run the same
   * Medicare start as the "Medicare Analysis" chat card. ChatComponent reacts and runs the flow.
   */
  readonly medicareEntryRequest = signal(0);

  /** Last entry request id already consumed by ChatComponent. */
  private handledMedicareEntryRequest = signal(0);

  /** True when a new saved-page Medicare entry request is waiting to be consumed. */
  readonly hasPendingMedicareEntryRequest = computed(
    () => this.medicareEntryRequest() > this.handledMedicareEntryRequest()
  );

  requestMedicareAnalysisEntry(): void {
    this.medicareEntryRequest.update(n => n + 1);
  }

  /**
   * Marks the latest external Medicare entry request as consumed.
   * Returns true only once per request id.
   */
  consumeMedicareEntryRequest(): boolean {
    const requestId = this.medicareEntryRequest();
    const handledId = this.handledMedicareEntryRequest();
    if (requestId === 0 || requestId <= handledId) return false;
    this.handledMedicareEntryRequest.set(requestId);
    return true;
  }

  /**
   * Which step was last announced to the user via an assistant message.
   * Prevents duplicate messages when signals re-fire without actually changing step.
   */
  private lastAnnouncedStep = signal<WizardStep | null>(null);

  /** Computed current wizard step derived entirely from completion signals */
  readonly currentStep = computed<WizardStep>(() => {
    const mode = this.mode();
    if (mode === 'LONG_TERM_ANALYSIS') {
      const profileDone = this.profileService.isProfileComplete();
      if (!profileDone)                        return 'LTC_PROFILE';
      if (!this.ltcProfileIntroComplete())     return 'LTC_PROFILE_REVIEW';
      return 'LTC_CARE_TYPE';
    }
    if (mode !== 'MEDICARE_ANALYSIS') return 'AWAITING_MODE';

    const profileDone    = this.profileService.isProfileComplete();
    const drugsDone      = this.state.hasConfirmedDrugs();
    const pharmaciesDone = this.state.pharmacySelectionConfirmed();
    const plansDone      = !!(
      (this.state.selectedPartDPlan() && this.state.selectedMedigapPlan()) ||
      this.state.selectedMAPlan()
    );
    const analysisDone   = this.state.hasCostProjection();

    if (!profileDone)                    return 'PROFILE';
    if (!this.medicareProfileIntroComplete()) return 'PROFILE_REVIEW';
    if (!drugsDone)                     return 'DRUGS';
    if (!pharmaciesDone)                 return 'PHARMACIES';
    if (!plansDone)                      return 'PLANS';
    if (!analysisDone)                   return 'ANALYSIS';
    return 'COMPLETE';
  });

  /**
   * True when currentStep changed since the last time markStepAnnounced() was called.
   * ChatComponent watches this to know when to post the next assistant message.
   */
  readonly hasNewStep = computed(() =>
    (this.mode() === 'MEDICARE_ANALYSIS' || this.mode() === 'LONG_TERM_ANALYSIS') &&
    this.currentStep() !== this.lastAnnouncedStep()
  );

  /** Call this after posting the assistant message for the current step */
  markStepAnnounced() {
    this.lastAnnouncedStep.set(this.currentStep());
  }

  /** Start the Medicare Analysis wizard */
  startMedicareAnalysis() {
    this.mode.set('MEDICARE_ANALYSIS');
    this.medicareProfileIntroComplete.set(false);
    this.lastAnnouncedStep.set(null);
    this.showModeButtons.set(false);
  }

  /**
   * Resume Medicare wizard mode without re-announcing steps.
   * Useful on hard refresh while already on /medicare-analysis/* routes.
   */
  resumeMedicareAnalysis() {
    this.mode.set('MEDICARE_ANALYSIS');
    this.lastAnnouncedStep.set(this.currentStep());
    this.showModeButtons.set(false);
  }

  /** Reset wizard to startup state (called when resetAll() fires) */
  reset() {
    this.mode.set('NONE');
    this.medicareProfileIntroComplete.set(false);
    this.ltcProfileIntroComplete.set(false);
    this.medicareEntryRequest.set(0);
    this.handledMedicareEntryRequest.set(0);
    this.lastAnnouncedStep.set(null);
    // Only show mode buttons if profile is already complete
    this.showModeButtons.set(this.profileService.isProfileComplete());
  }

  // ── LTC Wizard ─────────────────────────────────────────────────────────

  /** Start the Long Term Care Analysis wizard */
  startLtcAnalysis() {
    this.mode.set('LONG_TERM_ANALYSIS');
    this.ltcProfileIntroComplete.set(false);
    this.lastAnnouncedStep.set(null);
    this.showModeButtons.set(false);
  }

  /**
   * Resume LTC wizard mode without re-announcing steps.
   * Useful on hard refresh while already on /long-term-care/* routes.
   */
  resumeLtcAnalysis() {
    this.mode.set('LONG_TERM_ANALYSIS');
    this.lastAnnouncedStep.set(this.currentStep());
    this.showModeButtons.set(false);
  }

  /** Whether all steps are complete */
  readonly isComplete = computed(() => this.currentStep() === 'COMPLETE');
}
