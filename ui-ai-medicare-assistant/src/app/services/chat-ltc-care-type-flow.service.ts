import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { catchError, finalize, of } from 'rxjs';
import { ChatStateService } from './chat-state.service';
import { ProfileService } from './profile.service';
import { LtcStateService } from '../long-term-care/ltc-state.service';
import { LtcService } from '../long-term-care/ltc.service';
import { LtcAnalysisSnapshotService } from './ltc-analysis-snapshot.service';
import { ReferenceDataService } from './reference-data.service';
import { ChatIntentParams } from './chat-intent.service';
import { LtcProjectionRequest } from '../models/ltc.model';
import { LTC_MESSAGES } from '../constants/chat-messages';
import { AppRoutes } from '../app-routes.const';

@Injectable({ providedIn: 'root' })
export class ChatLtcCareTypeFlowService {
  private state = inject(ChatStateService);
  private profileService = inject(ProfileService);
  private ltcState = inject(LtcStateService);
  private ltcService = inject(LtcService);
  private ltcSnapshot = inject(LtcAnalysisSnapshotService);
  private refData = inject(ReferenceDataService);
  private router = inject(Router);

  readonly pendingSaveLtcOverwrite = signal<string | null>(null);

  /**
   * Handle LTC_CARE_INPUT intent — update care-type form fields from chat.
   * If on care-type page, updates via pendingChatCareType signal.
   * If not on care-type page, updates state directly and navigates.
   */
  handleCareTypeInput(params: ChatIntentParams | undefined, confirmationMessage: string): void {
    if (!params) {
      this.state.addAssistantMessage('No care-type values detected. Try "set nursing to 5 years" or "quality best".');
      this.state.setLoading(false);
      return;
    }

    const updates: Record<string, number> = {};
    if (params.ltcHealthProfile != null) updates['healthProfile'] = params.ltcHealthProfile;
    if (params.ltcAdultDayYears != null) updates['adultDayYears'] = params.ltcAdultDayYears;
    if (params.ltcHomeCareYears != null) updates['homeCareYears'] = params.ltcHomeCareYears;
    if (params.ltcNursingCareYears != null) updates['nursingCareYears'] = params.ltcNursingCareYears;

    if (Object.keys(updates).length === 0) {
      this.state.addAssistantMessage('No care-type values detected. Try "set nursing to 5 years" or "quality best".');
      this.state.setLoading(false);
      return;
    }

    const onCareType = this.router.url.startsWith(AppRoutes.abs.LTC_CARE_TYPE);
    if (onCareType) {
      // On care-type page → update via signal (consumed by effect in component)
      this.ltcState.pendingChatCareType.set({
        healthProfile: params.ltcHealthProfile ?? undefined,
        adultDayYears: params.ltcAdultDayYears ?? undefined,
        homeCareYears: params.ltcHomeCareYears ?? undefined,
        nursingCareYears: params.ltcNursingCareYears ?? undefined,
      });
    } else {
      // Not on care-type page → update state directly
      if (params.ltcHealthProfile != null) this.ltcState.healthProfile.set(params.ltcHealthProfile);
      if (params.ltcAdultDayYears != null) this.ltcState.adultDayYears.set(params.ltcAdultDayYears);
      if (params.ltcHomeCareYears != null) this.ltcState.homeCareYears.set(params.ltcHomeCareYears);
      if (params.ltcNursingCareYears != null) this.ltcState.nursingCareYears.set(params.ltcNursingCareYears);
    }

    this.state.addAssistantMessage(confirmationMessage || 'Care type updated.');
    this.state.setLoading(false);

    // Navigate to care-type page if not already there
    if (!onCareType) {
      this.ltcState.currentStep.set(2);
      this.ltcState.careTypeVisited.set(true);
      this.router.navigate([AppRoutes.abs.LTC_CARE_TYPE]);
    }
  }

  /**
   * Handle ACTION_RUN_LTC_PROJECTION intent — validate prerequisites then run projection.
   */
  handleRunProjection(analysisName: string): void {
    if (!this.profileService.isProfileComplete()) {
      this.state.addAssistantMessage(LTC_MESSAGES.REQUIRE_PROFILE);
      this.state.setLoading(false);
      return;
    }
    if (!this.ltcState.careTypeVisited()) {
      this.state.addAssistantMessage(LTC_MESSAGES.REQUIRE_CARE_TYPE);
      this.state.setLoading(false);
      this.router.navigate([AppRoutes.abs.LTC_CARE_TYPE]);
      return;
    }

    const profile = this.profileService.profile()?.profile;
    if (!profile) {
      this.state.addAssistantMessage('Profile data is not available. Please try again.');
      this.state.setLoading(false);
      return;
    }

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
      healthProfile: this.ltcState.healthProfile(),
      location,
      zipcode: profile.zipCode,
      tobacco: profile.tobaccoStatus,
      currentLifeStyleExpenses: 1,
      numberOfAdultDayHealthCareLTCYears: this.ltcState.adultDayYears(),
      numberOfAssistedCareLTCYears: 0,
      numberOfHomeCareLTCYears: this.ltcState.homeCareYears(),
      numberOfNursingCareLTCYears: this.ltcState.nursingCareYears(),
      gender: profile.gender,
      alzheimersFlag: 0,
      heartStorkeFlag: 0,
    };

    this.state.addAssistantMessage(LTC_MESSAGES.PROJECTION_RUNNING);
    this.ltcState.isCallingApi.set(true);
    this.ltcService.getProjection(payload).pipe(
      finalize(() => this.ltcState.isCallingApi.set(false)),
      catchError(() => {
        this.state.addAssistantMessage(LTC_MESSAGES.PROJECTION_FAILED);
        this.state.setLoading(false);
        return of(null);
      }),
    ).subscribe(result => {
      if (!result) return;
      this.ltcState.ltcResult.set(result);

      const saveBody = {
        healthProfile: this.ltcState.healthProfile(),
        numberOfAdultDayHealthCareYears: this.ltcState.adultDayYears(),
        numberOfHomeCareYears: this.ltcState.homeCareYears(),
        numberOfNursingCareYears: this.ltcState.nursingCareYears(),
      };

      this.ltcService.saveCurrent(saveBody).pipe(
        catchError(() => of(void 0)),
      ).subscribe(() => {
        this.saveRecommendation(analysisName);
      });
    });
  }

  private saveRecommendation(name: string, force = false): void {
    this.ltcSnapshot.save(name, force).subscribe({
      next: () => {
        this.state.addAssistantMessage(LTC_MESSAGES.PROJECTION_COMPLETE);
        this.state.setLoading(false);
        this.router.navigate([AppRoutes.abs.LTC_PROJECTION]);
      },
      error: (err: HttpErrorResponse) => {
        if (err.status === 409 && !force) {
          this.state.addAssistantMessage(
            `A recommendation named "${name}" already exists. Would you like to overwrite it? (yes / no)`
          );
          this.pendingSaveLtcOverwrite.set(name);
          this.state.setLoading(false);
          this.router.navigate([AppRoutes.abs.LTC_PROJECTION]);
        } else {
          this.state.addAssistantMessage('Failed to save LTC analysis. Projection is still available.');
          this.state.setLoading(false);
          this.router.navigate([AppRoutes.abs.LTC_PROJECTION]);
        }
      },
    });
  }
}
