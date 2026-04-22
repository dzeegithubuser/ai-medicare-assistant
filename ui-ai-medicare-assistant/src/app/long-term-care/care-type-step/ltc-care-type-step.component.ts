import { ChangeDetectionStrategy, Component, DestroyRef, inject, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, finalize, of } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { LtcStateService } from '../ltc-state.service';
import { ProfileService } from '../../services/profile.service';
import { LtcService } from '../ltc.service';
import { LtcAnalysisSnapshotService } from '../../services/ltc-analysis-snapshot.service';
import { ReferenceDataService } from '../../services/reference-data.service';
import { LtcProjectionRequest } from '../../models/ltc.model';
import { AppRoutes } from '../../app-routes.const';
import { SavePrescriptionDialogComponent } from '../../medicare-analysis/drug-step/save-prescription-dialog/save-prescription-dialog.component';

@Component({
  selector: 'app-ltc-care-type-step',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './ltc-care-type-step.component.html',
  styleUrls: ['./ltc-care-type-step.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LtcCareTypeStepComponent {
  private fb = inject(FormBuilder);
  readonly state = inject(LtcStateService);
  private profileService = inject(ProfileService);
  private ltcService = inject(LtcService);
  private ltcSnapshot = inject(LtcAnalysisSnapshotService);
  private refData = inject(ReferenceDataService);
  private router = inject(Router);
  private dialog = inject(MatDialog);
  private destroyRef = inject(DestroyRef);

  readonly healthProfileOptions = [
    { value: 1, label: 'Best' },
    { value: 2, label: 'Good' },
    { value: 3, label: 'Average' },
    { value: 4, label: 'Basic' },
    { value: 5, label: 'Minimum' },
  ];

  readonly yearOptions = Array.from({ length: 21 }, (_, i) => i);

  readonly form = this.fb.nonNullable.group({
    healthProfile: [this.state.healthProfile(), [Validators.required, Validators.min(1), Validators.max(5)]],
    adultDayYears: [this.state.adultDayYears(), [Validators.required, Validators.min(0), Validators.max(20)]],
    homeCareYears: [this.state.homeCareYears(), [Validators.required, Validators.min(0), Validators.max(20)]],
    nursingCareYears: [this.state.nursingCareYears(), [Validators.required, Validators.min(0), Validators.max(20)]],
  });

  get canRunProjection(): boolean {
    return this.profileService.isProfileComplete() &&
      this.state.careTypeVisited() &&
      !this.state.isCallingApi();
  }

  constructor() {
    this.state.currentStep.set(2);
    this.state.careTypeVisited.set(true);

    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(v => {
        this.state.healthProfile.set(v.healthProfile ?? 1);
        this.state.adultDayYears.set(v.adultDayYears ?? 0);
        this.state.homeCareYears.set(v.homeCareYears ?? 0);
        this.state.nursingCareYears.set(v.nursingCareYears ?? 0);
      });

    // Consume chat-driven care-type updates
    effect(() => {
      const pending = this.state.pendingChatCareType();
      if (!pending) return;
      this.state.pendingChatCareType.set(null);
      this.form.patchValue({
        ...(pending.healthProfile != null ? { healthProfile: pending.healthProfile } : {}),
        ...(pending.adultDayYears != null ? { adultDayYears: pending.adultDayYears } : {}),
        ...(pending.homeCareYears != null ? { homeCareYears: pending.homeCareYears } : {}),
        ...(pending.nursingCareYears != null ? { nursingCareYears: pending.nursingCareYears } : {}),
      });
    });

    // Hydrate form from persisted care-type selections (mirrors Medicare drug/pharmacy step hydration)
    this.ltcService.getCurrent().pipe(
      catchError(() => of(null)),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(current => {
      if (!current) return;
      this.state.healthProfile.set(current.healthProfile);
      this.state.adultDayYears.set(current.numberOfAdultDayHealthCareYears);
      this.state.homeCareYears.set(current.numberOfHomeCareYears);
      this.state.nursingCareYears.set(current.numberOfNursingCareYears);
      this.form.patchValue({
        healthProfile: current.healthProfile,
        adultDayYears: current.numberOfAdultDayHealthCareYears,
        homeCareYears: current.numberOfHomeCareYears,
        nursingCareYears: current.numberOfNursingCareYears,
      }, { emitEvent: false });
    });
  }

  runProjection(): void {
    const dialogRef = this.dialog.open(SavePrescriptionDialogComponent, {
      width: '420px',
      data: {
        title: 'Name this analysis',
        subtitle: 'Enter a name to save your long-term care projection.',
        icon: 'elderly',
        defaultName: `LTC Analysis – ${new Date().toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}`,
      },
    });

    dialogRef.afterClosed().subscribe((name: string | null) => {
      const trimmedName = name?.trim() ?? '';
      if (!trimmedName) return;

      const profile = this.profileService.profile()?.profile;
      if (!profile) return;

      const today = new Date();
      const dob = new Date(profile.dateOfBirth);
      const age = today.getFullYear() - dob.getFullYear() -
        (today.getMonth() < dob.getMonth() || (today.getMonth() === dob.getMonth() && today.getDate() < dob.getDate()) ? 1 : 0);

      const location = this.refData.usStates().find(s => s.value === profile.state)?.label ?? profile.state;

      const payload: LtcProjectionRequest = {
        age: Math.max(0, age),
        pvAsOfYear: today.getFullYear(),
        lifeExpectancy: profile.lifeExpectancy,
        transactionTypeFlag: 'false',
        healthProfile: this.state.healthProfile(),
        location,
        zipcode: profile.zipCode,
        tobacco: profile.tobaccoStatus,
        currentLifeStyleExpenses: 1,
        numberOfAdultDayHealthCareLTCYears: this.state.adultDayYears(),
        numberOfAssistedCareLTCYears: 0,
        numberOfHomeCareLTCYears: this.state.homeCareYears(),
        numberOfNursingCareLTCYears: this.state.nursingCareYears(),
        gender: profile.gender,
        alzheimersFlag: 0,
        heartStorkeFlag: 0,
      };

      this.state.isCallingApi.set(true);
      this.ltcService.getProjection(payload).pipe(
        finalize(() => this.state.isCallingApi.set(false)),
        catchError(() => of(null)),
      ).subscribe(result => {
        if (!result) return;
        this.state.ltcResult.set(result);

        const saveBody = {
          healthProfile: this.state.healthProfile(),
          numberOfAdultDayHealthCareYears: this.state.adultDayYears(),
          numberOfHomeCareYears: this.state.homeCareYears(),
          numberOfNursingCareYears: this.state.nursingCareYears(),
        };

        this.ltcService.saveCurrent(saveBody).pipe(
          catchError(() => of(void 0)),
        ).subscribe(() => {
          this.saveRecommendation(trimmedName);
        });
      });
    });
  }

  private saveRecommendation(name: string, force = false): void {
    this.ltcSnapshot.save(name, force).subscribe({
      next: () => this.router.navigate([AppRoutes.abs.LTC_PROJECTION]),
      error: (err: HttpErrorResponse) => {
        if (err.status === 409 && !force) {
          const overwrite = confirm(`A recommendation named "${name}" already exists. Overwrite it?`);
          if (overwrite) {
            this.saveRecommendation(name, true);
          } else {
            this.router.navigate([AppRoutes.abs.LTC_PROJECTION]);
          }
        } else {
          this.router.navigate([AppRoutes.abs.LTC_PROJECTION]);
        }
      },
    });
  }
}
