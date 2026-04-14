import {
  CountyCodeModel,
  PartDPharmacyInput,
  PartDPlanRecommendationResponse,
  PrescriptionInput
} from './part-d-plan.model';

/**
 * Same shape as PartDPlanRecommendationRequest + medicareAdvantage = true.
 * Response reuses PartDPlanRecommendationResponse.
 */
export interface MedicareAdvantagePlanRequest {
  userId: string;
  sortRecommendations: string;
  countycodeModel: CountyCodeModel;
  prescriptions: PrescriptionInput[];
  beneficiaryCostDataRequired: boolean;
  pharmacyNetworkDataRequired: boolean;
  pharmacies: PartDPharmacyInput[];
  planRecommendName: string;
  planRecommendEmail: string;
  drugListingName: string;
  recommendationListId: string;
  taxFilingStatus: string;
  magiTier: number;
  healthGrade: number;
  birthDate: string;
  fullYearOOPCost: boolean;
  coverageYear: string;
  includePlanExpensesFullYear: boolean;
  planPage: number;
  planPageSize: number;
  recommendationPage: number;
  recommendationPageSize: number;
  starRatingFilter: number | null;
  prescriptionCoverageFilter: string | null;
  contractIdFilter: string | null;
  mailOrderPharmacy: boolean;
  medicareAdvantage: true;
}

export type MedicareAdvantagePlanResponse = PartDPlanRecommendationResponse;
