import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AppRoutes } from '../app-routes.const';
import { LtcStateService } from './ltc-state.service';
import { ChatStateService } from '../services/chat-state.service';
import { MedicareStateService } from '../services/drug-state.service';
import { ProfileService } from '../services/profile.service';

@Component({
  selector: 'app-ltc-shell',
  standalone: true,
  templateUrl: './ltc-shell.component.html',
  styleUrls: ['./ltc-shell.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, MatIconModule, MatButtonModule, MatProgressSpinnerModule],
})
export class LtcShellComponent {
  readonly state = inject(LtcStateService);
  private chatState = inject(ChatStateService);
  private medicareState = inject(MedicareStateService);
  readonly profileService = inject(ProfileService);
  private router = inject(Router);

  private readonly routerUrl = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(() => this.router.url),
      startWith(this.router.url),
    ),
    { initialValue: this.router.url },
  );

  readonly isProjectionRoute = computed(() =>
    this.routerUrl().includes(AppRoutes.abs.LTC_PROJECTION),
  );

  readonly steps = [
    { number: 1 as const, label: 'Profile', icon: 'person', route: AppRoutes.PROFILE },
    { number: 2 as const, label: 'Care Type', icon: 'health_and_safety', route: AppRoutes.LTC_CARE_TYPE },
  ];

  get canGoBack(): boolean {
    return this.state.currentStep() > 1;
  }

  get canContinue(): boolean {
    return this.state.currentStep() === 1;
  }

  get continueLabel(): string {
    return 'Continue to Care Type';
  }

  goBack(): void {
    const step = this.state.currentStep();
    if (step <= 1) return;
    const prev = this.steps[step - 2];
    this.state.currentStep.set(prev.number);
    this.router.navigate([AppRoutes.abs.LTC, prev.route]);
  }

  goNext(): void {
    const step = this.state.currentStep();
    if (step >= 2) return;
    if (step === 1) {
      this.profileService.requestSaveFromChat();
      return;
    }
  }

  goToStep(stepNumber: 1 | 2): void {
    if (stepNumber > this.state.currentStep()) return;
    const target = this.steps.find(s => s.number === stepNumber);
    if (!target) return;
    this.state.currentStep.set(stepNumber);
    this.router.navigate([AppRoutes.abs.LTC, target.route]);
  }

  startNewAnalysis(): void {
    this.chatState.addSystemMessage('Started a new analysis');
    this.state.resetAll();
    this.medicareState.resetAll();
    this.router.navigateByUrl(AppRoutes.abs.LTC_PROFILE);
  }
}
