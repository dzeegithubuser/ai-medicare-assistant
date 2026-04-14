export interface PartDPlanRecommendationRequest {
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
}

export interface CountyCodeModel {
  zipcode: string;
  state: string;
  stateCode: string;
  city: string;
  latitude: number;
  longitude: number;
  countyCode: string;
  countyName: string;
}

export interface PrescriptionInput {
  rxcui: string;
  refillDuration: string;
  prescriptionCount: number;
  ndc: string;
}

export interface PartDPharmacyInput {
  pharmacyNumber: string;
  pharmacyName: string;
  latitude: string;
  longitude: string;
  address: string;
  distance: string;
  zipcode: string;
}

// ===== Response =====

export interface PartDPlanRecommendationResponse {
  webServiceTransactionId: string;
  webServiceStatus: string;
  countycodeModel: CountyCodeModel | null;
  pharmacies: PartDPharmacyInput[];
  prescriptions: PrescriptionInput[];
  contractIdCarrierMap: Record<string, string>;
  carrierContractIdMap: Record<string, string[]>;
  beneficiaryCostDataRequired: boolean;
  contractIdFilter: string | null;
  planNameFilter: string | null;
  planIdFilter: string | null;
  segmentIdFilter: string | null;
  starRatingFilter: number | null;
  prescriptionCoverageFilter: unknown;
  recommendationPage: number;
  recommendationPageSize: number;
  totalRecommendationPages: number;
  totalRecommendations: number;
  sortRecommendations: string;
  retirementYear: number;
  dataYear: number;
  partAPremium: number;
  partBPremium: number;
  partBOOP: number;
  partBPremiumSurcharge: number;
  monthsUsedForExpenseCalc: number;
  partDPremiumSurcharge: number;
  recommendations: unknown[] | null;
  recommendationList: RecommendationListItem[];
}

export interface RecommendationListItem {
  contractId: string;
  planName: string;
  planId: string;
  segmentId: string;
  pharmacyWiseRecommendations: PharmacyWiseRecommendation[];
}

export interface PharmacyWiseRecommendation {
  contractId: string;
  planName: string;
  planType: string;
  planId: string;
  segmentId: string;
  pharmacyNumber: string;
  pharmacyName: string;
  pharmacyRetailType: string;
  dispenseFee: number;
  premium: number;
  deductible: number;
  icl: number;
  starRating: number;
  websiteLink: string;
  contactTitle: string;
  phone: string;
  ext: string;
  fax: string;
  email: string;
  drugPriceCosts: unknown[];
  totalPremiumToPay: number;
  totalPrescriptionCost: number;
  totalPrescriptionCostFullYear: number;
  totalPlanCost: number;
  prescriptionDrugCovered: boolean;
  partAandBBenefitServiceCost: number;
  partABenefitServiceCost: number;
  partBBenefitServiceCost: number;
  planExpenses: PlanExpenseMonth[];
  planExpensesFullYear: PlanExpenseOopMonth[];
  unavailableDrugs: string[] | null;
  pharmacyNetworks: PharmacyNetworkEntry[];
  lName: string;
  fName: string;
  mName: string;
}

export interface PlanExpenseMonth {
  month: number;
  oop: number;
  premium: number;
  drugRetailCost: number;
}

export interface PlanExpenseOopMonth {
  month: number;
  oop: number;
}

export interface PharmacyNetworkEntry {
  pharmacyNumber: string;
  pharmacyName: string;
  pharmacyNetworkType: string;
  distance: string;
}

// ===== Enrichment (computed display fields) =====

export interface EnrichedPartDCard {
  planIdDisplay: string;
  insuranceCarrier: string;
  partDSurcharge: number;
  prescriptionOOP: number;
  pharmaciesInNetwork: number;
  totalSelectedPharmacies: number;
  drugsCovered: number;
  totalDrugs: number;
}

export interface EnrichedMACard {
  planIdDisplay: string;
  insuranceCarrier: string;
  surcharges: number;
  prescriptionOOP: number;
  healthcareOOP: number;
  hasPrescriptionDrug: boolean;
  pharmaciesInNetwork: number;
  totalSelectedPharmacies: number;
  drugsCovered: number;
  totalDrugs: number;
}
