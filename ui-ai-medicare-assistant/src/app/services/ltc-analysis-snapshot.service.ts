import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ProfileService } from './profile.service';
import { RecommendationService } from './recommendation.service';
import { LtcStateService } from '../long-term-care/ltc-state.service';
import { LtcProjectionResponse } from '../models/ltc.model';
import {
  CreateRecommendationRequest,
  ProfileSnapshotDto,
  LtcSnapshotDto,
  LtcEvaluationSnapDto,
  LtcProjectionSnapDto,
  RecommendationResponse,
} from '../models/recommendation.model';

@Injectable({ providedIn: 'root' })
export class LtcAnalysisSnapshotService {
  private profileService = inject(ProfileService);
  private ltcState = inject(LtcStateService);
  private recommendationService = inject(RecommendationService);

  canSave(): boolean {
    return this.profileService.isProfileComplete() && this.ltcState.ltcResult() !== null;
  }

  save(name: string, force = false): Observable<RecommendationResponse> {
    const request = this.buildRequest(name);
    return this.recommendationService.create(request, force);
  }

  private buildRequest(name: string): CreateRecommendationRequest {
    return {
      name,
      type: 'longterm',
      profile: this.buildProfile(name),
      drugs: [],
      pharmacies: [],
      plans: [],
      costSnapshot: null,
      ltcSnapshot: this.buildLtcSnapshot(),
    };
  }

  private buildProfile(analysisName: string): ProfileSnapshotDto {
    const p = this.profileService.profile()?.profile;
    if (!p) throw new Error('Profile not loaded');
    return {
      recommendationName: analysisName,
      firstName: p.firstName,
      lastName: p.lastName,
      dateOfBirth: p.dateOfBirth,
      gender: p.gender,
      zipCode: p.zipCode,
      county: p.county,
      countyCode: p.countyCode,
      state: p.state,
      city: p.city,
      addressLine1: p.addressLine1,
      healthCondition: p.healthCondition,
      lifeExpectancy: p.lifeExpectancy,
      tobaccoStatus: p.tobaccoStatus,
      taxFilingStatus: p.taxFilingStatus,
      magiTier: p.magiTier,
      coverageYear: p.coverageYear,
      concierge: p.concierge,
      conciergeAmount: p.conciergeAmount,
      alternateEmail: p.alternateEmail,
      alternateMobile: p.alternateMobile,
      latitude: p.latitude,
      longitude: p.longitude,
    };
  }

  private buildLtcSnapshot(): LtcSnapshotDto {
    const result = this.ltcState.ltcResult();
    if (!result) throw new Error('No LTC projection result');

    const { projection, evaluation } = result;

    const totalCost =
      projection.expectedAdultDayHealthCare +
      projection.expectedHomeCare +
      projection.expectedAssistedCare +
      projection.expectedNursingCare;

    const totalPV =
      projection.presentValueExpectedAdultDayHealthCare +
      projection.presentValueExpectedHomeCare +
      projection.presentValueExpectedAssistedCare +
      projection.presentValueExpectedNursingCare;

    let evalSnap: LtcEvaluationSnapDto | null = null;
    if (evaluation) {
      evalSnap = {
        costTrajectory: evaluation.costTrajectory,
        trajectoryExplanation: evaluation.trajectoryExplanation,
        overallAssessment: evaluation.overallAssessment,
        totalCost: evaluation.lifetimeSummary.totalCost,
        totalPresentValue: evaluation.lifetimeSummary.totalPresentValue,
        projectionYears: evaluation.lifetimeSummary.projectionYears,
        averageAnnualCost: evaluation.lifetimeSummary.averageAnnualCost,
        yearlyHighlights: evaluation.yearlyHighlights.map(h => ({
          year: h.year, totalCost: h.totalCost, flag: h.flag, explanation: h.explanation,
        })),
        categories: evaluation.categories.map(c => ({
          name: c.name, lifetimeTotal: c.lifetimeTotal, presentValue: c.presentValue,
          percentOfTotal: c.percentOfTotal, trend: c.trend, insight: c.insight,
        })),
        savingsTips: evaluation.savingsTips.map(s => ({
          title: s.title, description: s.description,
          estimatedSavings: s.estimatedSavings, priority: s.priority,
        })),
      };
    }

    return {
      healthProfile: this.ltcState.healthProfile(),
      adultDayYears: this.ltcState.adultDayYears(),
      homeCareYears: this.ltcState.homeCareYears(),
      nursingCareYears: this.ltcState.nursingCareYears(),
      totalCost,
      totalPresentValue: totalPV,
      projection: this.buildProjectionSnap(projection),
      evaluation: evalSnap,
    };
  }

  private buildProjectionSnap(p: LtcProjectionResponse): LtcProjectionSnapDto {
    return {
      pvHomeCare: p.presentValueExpectedHomeCare,
      pvNursingCare: p.presentValueExpectedNursingCare,
      adultDayExpenses: p.futureAdultDayHealthCareExpenseList.map(e => ({ year: e.year, expense: e.expense })),
      homeCareExpenses: p.futureHomeCareExpenseList.map(e => ({ year: e.year, expense: e.expense })),
      assistedCareExpenses: p.futureAssistedCareExpensesList.map(e => ({ year: e.year, expense: e.expense })),
      nursingCareExpenses: p.futureNursingCareExpensesList.map(e => ({ year: e.year, expense: e.expense })),
    };
  }
}
