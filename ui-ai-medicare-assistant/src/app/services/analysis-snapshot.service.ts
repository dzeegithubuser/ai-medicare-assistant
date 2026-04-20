import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { DrugStateService } from './drug-state.service';
import { ProfileService } from './profile.service';
import { RecommendationService } from './recommendation.service';
import {
  CreateRecommendationRequest,
  ProfileSnapshotDto,
  SelectedDrugDto,
  SelectedPharmacyDto,
  SelectedPlanDto,
  CostSnapshotDto,
  RecommendationResponse,
  PlanExpenseDto,
  CostEvaluationDto,
  YearlyDetailDto,
} from '../models/recommendation.model';
import { PharmacyWiseRecommendation } from '../models/part-d-plan.model';

/**
 * Assembles the current analysis state (profile, drugs, pharmacies, plans, cost)
 * into a CreateRecommendationRequest and saves it.
 */
@Injectable({ providedIn: 'root' })
export class AnalysisSnapshotService {
  private state = inject(DrugStateService);
  private profileService = inject(ProfileService);
  private recommendationService = inject(RecommendationService);

  /** Returns true if all 5 sections have data to save. */
  canSave(): boolean {
    return (
      this.profileService.isProfileComplete() &&
      this.state.hasConfirmedDrugs() &&
      this.state.hasSelectedLookupPharmacies() &&
      this.state.hasCompletePlanSelection() &&
      this.state.hasCostProjection()
    );
  }

  /** Assembles and saves the full analysis snapshot. */
  save(name: string, force = false): Observable<RecommendationResponse> {
    const request = this.buildRequest(name);
    return this.recommendationService.create(request, force);
  }

  private buildRequest(name: string): CreateRecommendationRequest {
    return {
      name,
      profile: this.buildProfile(name),
      drugs: this.buildDrugs(),
      pharmacy: this.buildPharmacy(),
      plans: this.buildPlans(),
      costSnapshot: this.buildCostSnapshot(),
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

  private buildDrugs(): SelectedDrugDto[] {
    const details = this.state.drugDetails();
    const confirmed = this.state.confirmedDrugNames();
    if (!details?.results || confirmed.size === 0) return [];

    const selRaw = sessionStorage.getItem('formulation-selections');
    const selMap: Map<string, Record<string, string>> = selRaw
      ? new Map(JSON.parse(selRaw))
      : new Map();

    return details.results
      .filter(r => confirmed.has(r.drugName))
      .map(r => {
        const sel = selMap.get(r.drugName);
        return {
          drugName: r.drugName,
          fullName: sel?.['drugName'] ?? null,
          drugType: sel?.['drugType'] ?? null,
          dosage: sel?.['strength'] ?? '',
          quantity: parseInt(sel?.['quantity'] ?? '30', 10),
          refillFrequency: '30',
          rxcui: sel?.['rxcui'] ?? r.matchedDrug?.rxcui ?? null,
          ndcCode: null,
        };
      });
  }

  private buildPharmacy(): SelectedPharmacyDto | null {
    const pharmacies = this.state.selectedLookupPharmacies();
    if (pharmacies.length === 0) return null;
    const p = pharmacies[0];
    return {
      npi: String(p.pharmacyNumber),
      name: p.pharmacyName,
      address: p.address ?? '',
      city: '',
      state: '',
      zipCode: String(p.zipcode ?? ''),
      phone: '',
      pharmacyType: 'RETAIL',
      distance: p.distance ? parseFloat(String(p.distance)) : null,
    };
  }

  private buildPlans(): SelectedPlanDto[] {
    const plans: SelectedPlanDto[] = [];
    const section = this.state.activeSection();

    if (section === 'partd') {
      const partD = this.state.selectedPartDPlan();
      if (partD) {
        const rec = partD.pharmacyWiseRecommendations?.[0];
        plans.push(this.mapPartDPlan('PDP', partD.planId, partD.planName, partD.contractId, rec));
      }
      const medigap = this.state.selectedMedigapPlan();
      if (medigap) {
        plans.push({
          planType: 'MEDIGAP',
          planId: medigap.key,
          planName: medigap.company_base?.name ?? 'Medigap',
          carrier: medigap.company_base?.name ?? '',
          monthlyPremium: medigap.rate?.month ?? 0,
          medigapPlanType: medigap.plan,
          deductible: 0,
          starRating: 0,
          totalPrescriptionCost: 0,
          totalPlanCost: 0,
          prescriptionDrugCovered: true,
          unavailableDrugs: [],
          planExpenses: [],
        });
      }
    } else if (section === 'ma') {
      const ma = this.state.selectedMAPlan();
      if (ma) {
        const rec = ma.pharmacyWiseRecommendations?.[0];
        plans.push(this.mapPartDPlan('MA', ma.planId, ma.planName, ma.contractId, rec));
      }
      const gapPartD = this.state.selectedMAGapPartDPlan();
      if (gapPartD) {
        const rec = gapPartD.pharmacyWiseRecommendations?.[0];
        plans.push(this.mapPartDPlan('PDP', gapPartD.planId, gapPartD.planName, gapPartD.contractId, rec));
      }
    }

    return plans;
  }

  private mapPartDPlan(
    planType: string, planId: string, planName: string, contractId: string,
    rec: PharmacyWiseRecommendation | undefined
  ): SelectedPlanDto {
    const expenses: PlanExpenseDto[] = (rec?.planExpenses ?? []).map(e => ({
      month: e.month, oop: e.oop, premium: e.premium, drugRetailCost: e.drugRetailCost,
    }));
    return {
      planType,
      planId,
      planName,
      carrier: contractId,
      monthlyPremium: rec?.totalPremiumToPay ?? rec?.premium ?? 0,
      medigapPlanType: null,
      deductible: rec?.deductible ?? 0,
      starRating: rec?.starRating ?? 0,
      totalPrescriptionCost: rec?.totalPrescriptionCost ?? 0,
      totalPlanCost: rec?.totalPlanCost ?? 0,
      prescriptionDrugCovered: rec?.prescriptionDrugCovered ?? true,
      unavailableDrugs: rec?.unavailableDrugs ?? [],
      planExpenses: expenses,
    };
  }

  private buildCostSnapshot(): CostSnapshotDto | null {
    const cost = this.state.costProjection();
    if (!cost) return null;
    const totals = cost.lifetimeTotals;
    const firstYear = cost.yearlyDetails?.[0];
    const firstYearTotal = firstYear
      ? firstYear.partAPremium + firstYear.partBPremium + firstYear.partDPremium
        + firstYear.partDOOP + firstYear.partBOOP + firstYear.partAOOP
        + firstYear.medicareAdvantagePremium + firstYear.partBPremiumSurcharge
        + firstYear.partDPremiumSurcharge + firstYear.dentalPremium + firstYear.dentalOOP
      : 0;

    const yearlyDetails: YearlyDetailDto[] = (cost.yearlyDetails ?? []).map(y => ({
      year: y.year,
      monthsUsedForExpenseCalc: y.monthsUsedForExpenseCalc,
      partAPremium: y.partAPremium,
      partBPremium: y.partBPremium,
      partBPremiumSurcharge: y.partBPremiumSurcharge,
      medicareAdvantagePremium: y.medicareAdvantagePremium,
      partDPremium: y.partDPremium,
      partDPremiumSurcharge: y.partDPremiumSurcharge,
      conciergePremium: y.conciergePremium,
      partAOOP: y.partAOOP,
      partBOOP: y.partBOOP,
      partDOOP: y.partDOOP,
      totalABMedicareAdvantage: y.totalABMedicareAdvantage,
      reserveDaysLeft: y.reserveDaysLeft,
      dentalPremium: y.dentalPremium,
      dentalOOP: y.dentalOOP,
      planGPremium: y.planGPremium,
      planFPremium: y.planFPremium,
      planNPremium: y.planNPremium,
      totalABGD: y.totalABGD,
      totalABFD: y.totalABFD,
      totalABND: y.totalABND,
      totalABCD: y.totalABCD,
    }));

    let evaluation: CostEvaluationDto | null = null;
    if (cost.evaluation) {
      const e = cost.evaluation;
      evaluation = {
        planName: e.planName,
        planBundleCode: e.planBundleCode,
        costTrajectory: e.costTrajectory,
        trajectoryExplanation: e.trajectoryExplanation,
        overallAssessment: e.overallAssessment,
        lifetimeSummary: {
          totalPremiums: e.lifetimeSummary.totalPremiums,
          totalOutOfPocket: e.lifetimeSummary.totalOutOfPocket,
          totalCombined: e.lifetimeSummary.totalCombined,
          projectionYears: e.lifetimeSummary.projectionYears,
          averageAnnualCost: e.lifetimeSummary.averageAnnualCost,
        },
        yearlyHighlights: e.yearlyHighlights.map(h => ({
          year: h.year, totalCost: h.totalCost, flag: h.flag, explanation: h.explanation,
        })),
        categories: e.categories.map(c => ({
          name: c.name, lifetimeTotal: c.lifetimeTotal, percentOfTotal: c.percentOfTotal,
          trend: c.trend, insight: c.insight,
        })),
        savingsTips: e.savingsTips.map(s => ({
          title: s.title, description: s.description,
          estimatedSavings: s.estimatedSavings, priority: s.priority,
        })),
      };
    }

    return {
      lifetimeTotal: totals.lifeTimeABMedicareAdvantageExpenses ?? 0,
      lifetimePremiums: totals.lifeTimeABMedicareAdvantagePremium ?? 0,
      lifetimeOop: totals.lifeTimeABMedicareAdvantageOop ?? 0,
      lifetimeIrmaa: totals.totalIrmaa ?? 0,
      presentValue: 0,
      currentYearTotal: firstYearTotal,
      calculatedAt: new Date().toISOString(),
      ltcPresentValue: null,
      supplementPlanType: totals.supplementPlanType ?? '',
      supplementPlanPremium: totals.supplementPlanPremium ?? 0,
      yearlyDetails,
      evaluation,
    };
  }
}
