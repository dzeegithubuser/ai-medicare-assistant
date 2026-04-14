import { Injectable, signal } from '@angular/core';
import { LtcProjectionResult } from '../models/ltc.model';

export interface PendingChatCareType {
  healthProfile?: number;
  adultDayYears?: number;
  homeCareYears?: number;
  nursingCareYears?: number;
}

@Injectable({ providedIn: 'root' })
export class LtcStateService {
  /** Step 1 = Profile, Step 2 = Care Type. Projection is a result page, not a step. */
  readonly currentStep = signal<1 | 2>(1);

  // Care-type selections (Step 2)
  /** Quality of Care: 1=Best, 2=Good, 3=Average, 4=Basic, 5=Minimum */
  readonly healthProfile = signal<number>(1);
  /** Adult Day Health Care years (0 = Not needed) */
  readonly adultDayYears = signal<number>(0);
  /** In-Home Care years (0 = Not needed) */
  readonly homeCareYears = signal<number>(0);
  /** Nursing Care years (0 = Not needed) */
  readonly nursingCareYears = signal<number>(0);

  /** True once the user has visited the Care Type step (defaults are valid after visit). */
  readonly careTypeVisited = signal(false);

  /** Saved URL for "go back to where I was" navigation. */
  readonly returnRoute = signal<string | null>(null);

  /** Chat-driven care-type field updates; consumed via effect() in care-type component. */
  readonly pendingChatCareType = signal<PendingChatCareType | null>(null);

  // API state
  readonly isCallingApi = signal(false);
  readonly ltcResult = signal<LtcProjectionResult | null>(null);

  resetAll(): void {
    this.currentStep.set(1);
    this.healthProfile.set(1);
    this.adultDayYears.set(0);
    this.homeCareYears.set(0);
    this.nursingCareYears.set(0);
    this.careTypeVisited.set(false);
    this.returnRoute.set(null);
    this.pendingChatCareType.set(null);
    this.isCallingApi.set(false);
    this.ltcResult.set(null);
  }
}
