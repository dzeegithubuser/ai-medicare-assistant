import { Injectable } from '@angular/core';
import {
  PartDPlanRecommendationResponse,
  RecommendationListItem,
  EnrichedPartDCard,
  EnrichedMACard,
} from '../models/part-d-plan.model';
import {
  MedigapPlanQuotesResponse,
  MedigapPlan,
  EnrichedMedigapCard,
} from '../models/medigap-plan.model';

/**
 * Pure-computation service that derives display fields for plan cards
 * from raw API responses. No HTTP calls.
 */
@Injectable({ providedIn: 'root' })
export class PlanCardEnrichmentService {

  enrichPartD(
    plan: RecommendationListItem,
    response: PartDPlanRecommendationResponse,
    selectedPharmacyNumbers: string[],
    totalDrugs: number,
  ): EnrichedPartDCard {
    const rec = plan.pharmacyWiseRecommendations?.[0];
    const networkCount = rec?.pharmacyNetworks
      ?.filter(pn => selectedPharmacyNumbers.includes(pn.pharmacyNumber)).length ?? 0;
    const unavailable = rec?.unavailableDrugs?.length ?? 0;

    return {
      planIdDisplay: `${plan.contractId}-${plan.planId}-${plan.segmentId}`,
      insuranceCarrier: response.contractIdCarrierMap?.[plan.contractId] ?? '',
      partDSurcharge: response.partDPremiumSurcharge ?? 0,
      prescriptionOOP: rec?.totalPrescriptionCost ?? 0,
      pharmaciesInNetwork: networkCount,
      totalSelectedPharmacies: selectedPharmacyNumbers.length,
      drugsCovered: totalDrugs - unavailable,
      totalDrugs,
    };
  }

  enrichMedigap(
    quote: MedigapPlan,
    response: MedigapPlanQuotesResponse,
  ): EnrichedMedigapCard {
    const carrierName = (quote.naic && response.contractIdCarrierMap)
      ? (response.contractIdCarrierMap[quote.naic] ?? '')
      : '';

    return {
      premiumMonthly: (quote.rate?.month ?? 0) / 100,
      premiumAnnual: (quote.rate?.annual ?? 0) / 100,
      insuranceCarrier: carrierName,
      partBSurcharge: quote.partBPremiumSurcharge ?? 0,
      healthcareOOP: quote.partBServiceOOP ?? 0,
      remainingMonths: quote.monthsUsedForExpenseCalc ?? 0,
    };
  }

  enrichMA(
    plan: RecommendationListItem,
    response: PartDPlanRecommendationResponse,
    selectedPharmacyNumbers: string[],
    totalDrugs: number,
  ): EnrichedMACard {
    const rec = plan.pharmacyWiseRecommendations?.[0];
    const surcharges = (response.partBPremiumSurcharge ?? 0) + (response.partDPremiumSurcharge ?? 0);
    const networkCount = rec?.pharmacyNetworks
      ?.filter(pn => selectedPharmacyNumbers.includes(pn.pharmacyNumber)).length ?? 0;
    const unavailable = rec?.unavailableDrugs?.length ?? 0;

    return {
      planIdDisplay: `${plan.contractId}-${plan.planId}-${plan.segmentId}`,
      insuranceCarrier: response.contractIdCarrierMap?.[plan.contractId] ?? '',
      surcharges,
      prescriptionOOP: rec?.totalPrescriptionCost ?? 0,
      healthcareOOP: rec?.partAandBBenefitServiceCost ?? 0,
      hasPrescriptionDrug: rec?.prescriptionDrugCovered ?? false,
      pharmaciesInNetwork: networkCount,
      totalSelectedPharmacies: selectedPharmacyNumbers.length,
      drugsCovered: totalDrugs - unavailable,
      totalDrugs,
    };
  }
}
