import { Component, ChangeDetectionStrategy, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize } from 'rxjs/operators';
import { DrugStateService } from '../services/drug-state.service';
import { PrescriptionService } from '../services/prescription.service';
import { ProfileService } from '../services/profile.service';
import { RecommendationStateService } from '../services/recommendation-state.service';
import { buildCurrentPrescriptionDrugsFromState } from './current-prescription.mapper';
import { AppRoutes } from '../app-routes.const';

@Component({
  selector: 'app-analysis-shell',
  templateUrl: './analysis-shell.component.html',
  styleUrls: ['./analysis-shell.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [RouterOutlet, MatIconModule, MatButtonModule, MatProgressSpinnerModule],
})
export class AnalysisShellComponent {
  protected state = inject(DrugStateService);
  private router = inject(Router);
  private recState = inject(RecommendationStateService);

  /** Cost projections are terminal for editing prior inputs — hide shell Back/Continue; disable stepper navigation. */
  private readonly routerUrl = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(() => this.router.url),
      startWith(this.router.url),
    ),
    { initialValue: this.router.url },
  );
  readonly isCostProjectionsRoute = computed(() =>
    this.routerUrl().includes(AppRoutes.abs.COST_PROJECTIONS),
  );
  private prescriptionService = inject(PrescriptionService);
  protected profileService = inject(ProfileService);

  readonly steps = [
    { number: 1 as const, label: 'Profile',     icon: 'person',            route: 'profile' },
    { number: 2 as const, label: 'Drugs',       icon: 'medication',        route: 'fp-drugs' },
    { number: 3 as const, label: 'Pharmacies',  icon: 'local_pharmacy',    route: 'pharmacies' },
    { number: 4 as const, label: 'Plans',       icon: 'health_and_safety', route: 'plans' },
  ];

  get canGoBack(): boolean {
    return this.state.currentStep() > 1;
  }

  get canContinue(): boolean {
    const step = this.state.currentStep();
    if (step === 1) return true; // Profile — onboarding complete (guard); user may review before drugs
    if (step === 2) return this.state.hasDrugDetails() && this.state.hasConfirmedDrugs();
    if (step === 3) return this.state.hasSelectedLookupPharmacies();
    return false; // Plans is the last step with optional navigation to cost projections
  }

  get continueLabel(): string {
    const step = this.state.currentStep();
    if (step === 1) return 'Continue to Drugs';
    if (step === 2) return 'Continue to Pharmacies';
    if (step === 3) return 'Continue to Plans';
    return 'Continue';
  }

  /** Forward stepper clicks may not skip Drugs / Pharmacies when those prerequisites are unmet. */
  private canNavigateToStep(stepNumber: 1 | 2 | 3 | 4): boolean {
    const cur = this.state.currentStep();
    if (stepNumber <= cur) return true;
    if (stepNumber >= 3) {
      const drugsOk = this.state.hasDrugDetails() && this.state.hasConfirmedDrugs();
      if (!drugsOk) return false;
    }
    if (stepNumber >= 4) {
      const pharmOk = this.state.hasSelectedLookupPharmacies();
      if (!pharmOk) return false;
    }
    return true;
  }

  goBack() {
    if (this.isCostProjectionsRoute()) return;
    const step = this.state.currentStep();
    if (step <= 1) return;
    const prev = this.steps[step - 2];
    this.state.currentStep.set(prev.number);
    this.router.navigate([AppRoutes.abs.MEDICARE_ANALYSIS, prev.route]);
  }

  goNext() {
    if (this.isCostProjectionsRoute()) return;
    const step = this.state.currentStep();
    if (step >= 4) return;
    if (step === 1) {
      // On profile step, continue means save profile first.
      this.profileService.requestSaveFromChat();
      return;
    }
    // Confirm pharmacy selection when advancing from pharmacies to plans
    if (step === 3) this.state.pharmacySelectionConfirmed.set(true);
    const next = this.steps[step];
    this.state.addSystemMessage(`Navigated to ${next.label}`);
    this.state.currentStep.set(next.number);

    if (step === 2 && next.route === 'pharmacies') {
      const drugs = buildCurrentPrescriptionDrugsFromState(this.state);
      this.state.setSavingCurrentPrescription(true);
      // Save only drugs. Pharmacies and plans are untouched.
      this.recState.syncDrugsToRecommendation().subscribe({ error: () => {} });
      this.prescriptionService
        .saveCurrentDrugs(drugs)
        .pipe(finalize(() => this.state.setSavingCurrentPrescription(false)))
        .subscribe({
          next: () => {
            this.profileService.loadProfile().subscribe({ error: () => {} });
            this.router.navigate([AppRoutes.abs.MEDICARE_ANALYSIS, next.route]);
          },
          error: () => this.router.navigate([AppRoutes.abs.MEDICARE_ANALYSIS, next.route]),
        });
      return;
    }

    if (step === 3 && next.route === 'plans') {
      const pharmacies = this.state.selectedLookupPharmacies().map((p) => ({
        pharmacyNumber: String(p.pharmacyNumber ?? ''),
        pharmacyName: p.pharmacyName ?? '',
        address: p.address ?? '',
        distance: String(p.distance ?? ''),
        zipcode: String(p.zipcode ?? ''),
      }));
      this.state.setSavingCurrentPrescription(true);
      // Save only pharmacies. Drugs and plans are untouched.
      this.recState.savePharmacySelection().pipe(
        finalize(() => {
          this.prescriptionService
            .saveCurrentPharmacy(pharmacies)
            .pipe(finalize(() => this.state.setSavingCurrentPrescription(false)))
            .subscribe({
              next: () => {
                this.profileService.loadProfile().subscribe({ error: () => {} });
                this.router.navigate([AppRoutes.abs.MEDICARE_ANALYSIS, next.route]);
              },
              error: () => this.router.navigate([AppRoutes.abs.MEDICARE_ANALYSIS, next.route]),
            });
        })
      ).subscribe({ error: () => {} });
      return;
    }

    this.router.navigate([AppRoutes.abs.MEDICARE_ANALYSIS, next.route]);
  }

  goToStep(stepNumber: 1 | 2 | 3 | 4) {
    if (this.isCostProjectionsRoute()) return;

    // Profile → Drugs: same as Continue — trigger save and stay on /analysis/profile until HTTP completes.
    // (Stepper click on "Drugs" used to router.navigate immediately and skip the save.)
    if (this.state.currentStep() === 1 && stepNumber === 2) {
      if (this.profileService.chatSaveInProgress()) return;
      this.profileService.requestSaveFromChat();
      return;
    }

    if (stepNumber > this.state.currentStep() && !this.canNavigateToStep(stepNumber)) return;
    this.state.currentStep.set(stepNumber);
    const step = this.steps[stepNumber - 1];
    this.router.navigate([AppRoutes.abs.MEDICARE_ANALYSIS, step.route]);
  }

  startNewAnalysis() {
    this.state.addSystemMessage('Started a new analysis');
    this.state.resetAll();
    this.router.navigate([AppRoutes.abs.MEDICARE_ANALYSIS, 'profile']);
  }
}
