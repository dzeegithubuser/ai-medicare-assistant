export type RecommendationCategory = 'medicare' | 'longterm';

export interface RecommendationSummaryResponse {
  id: string;
  name: string;
  status: string;
  type?: RecommendationCategory;
  drugCount: number;
  planCount: number;
  hasCostSnapshot: boolean;
  lifetimeTotal: number;
  createdAt: string;
  updatedAt: string;
}

export interface RecommendationResponse {
  id: string;
  name: string;
  status: string;
  type?: RecommendationCategory;
  profile: ProfileSnapshotDto;
  planSelections: SelectedPlanDto[];
  drugList: SelectedDrugDto[];
  pharmacy: SelectedPharmacyDto | null;
  mailOrderPharmacy: MailOrderPharmacyDto | null;
  lastCostSnapshot: CostSnapshotDto | null;
  ltcSnapshot: LtcSnapshotDto | null;
  createdAt: string;
  updatedAt: string;
}

export interface ProfileSnapshotDto {
  recommendationName: string;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  gender: string;
  zipCode: string;
  county: string;
  countyCode: string;
  state: string;
  city: string;
  addressLine1: string;
  healthCondition: number;
  lifeExpectancy: number;
  tobaccoStatus: number;
  taxFilingStatus: string;
  magiTier: string;
  coverageYear: number;
  concierge: number;
  conciergeAmount: number | null;
  alternateEmail: string | null;
  alternateMobile: string | null;
  latitude: number | null;
  longitude: number | null;
}

export interface SelectedDrugDto {
  drugName: string;
  dosage: string;
  quantity: number;
  refillFrequency: string;
  rxcui: string | null;
  ndcCode: string | null;
}

export interface SelectedPlanDto {
  planType: string;
  planId: string;
  planName: string;
  carrier: string;
  monthlyPremium: number;
  medigapPlanType: string | null;
  deductible: number;
  starRating: number;
  totalPrescriptionCost: number;
  totalPlanCost: number;
  prescriptionDrugCovered: boolean;
  unavailableDrugs: string[] | null;
  planExpenses: PlanExpenseDto[];
}

export interface PlanExpenseDto {
  month: number;
  oop: number;
  premium: number;
  drugRetailCost: number;
}

export interface SelectedPharmacyDto {
  npi: string;
  name: string;
  address: string;
  city: string;
  state: string;
  zipCode: string;
  phone: string;
  pharmacyType: string;
  distance: number | null;
}

export interface MailOrderPharmacyDto {
  npi: string;
  name: string;
  enabled: boolean;
}

export interface CostSnapshotDto {
  lifetimeTotal: number;
  lifetimePremiums: number;
  lifetimeOop: number;
  lifetimeIrmaa: number;
  presentValue: number;
  currentYearTotal: number;
  calculatedAt: string;
  ltcPresentValue: number | null;
  supplementPlanType: string;
  supplementPlanPremium: number;
  yearlyDetails: YearlyDetailDto[];
  evaluation: CostEvaluationDto | null;
}

export interface YearlyDetailDto {
  year: number;
  monthsUsedForExpenseCalc: number;
  partAPremium: number;
  partBPremium: number;
  partBPremiumSurcharge: number;
  medicareAdvantagePremium: number;
  partDPremium: number;
  partDPremiumSurcharge: number;
  conciergePremium: number;
  partAOOP: number;
  partBOOP: number;
  partDOOP: number;
  totalABMedicareAdvantage: number;
  reserveDaysLeft: number;
  dentalPremium: number;
  dentalOOP: number;
  planGPremium: number;
  planFPremium: number;
  planNPremium: number;
  totalABGD: number;
  totalABFD: number;
  totalABND: number;
  totalABCD: number;
}

export interface CostEvaluationDto {
  planName: string;
  planBundleCode: string;
  costTrajectory: string;
  trajectoryExplanation: string;
  overallAssessment: string;
  lifetimeSummary: LifetimeSummarySnapDto;
  yearlyHighlights: YearlyHighlightDto[];
  categories: CostCategorySnapDto[];
  savingsTips: SavingsTipSnapDto[];
}

export interface LifetimeSummarySnapDto {
  totalPremiums: number;
  totalOutOfPocket: number;
  totalCombined: number;
  projectionYears: number;
  averageAnnualCost: number;
}

export interface YearlyHighlightDto {
  year: number;
  totalCost: number;
  flag: string;
  explanation: string;
}

export interface CostCategorySnapDto {
  name: string;
  lifetimeTotal: number;
  percentOfTotal: number;
  trend: string;
  insight: string;
}

export interface SavingsTipSnapDto {
  title: string;
  description: string;
  estimatedSavings: string;
  priority: string;
}

export interface CreateRecommendationRequest {
  name: string;
  type?: RecommendationCategory;
  profile: ProfileSnapshotDto;
  drugs: SelectedDrugDto[];
  pharmacy: SelectedPharmacyDto | null;
  plans: SelectedPlanDto[];
  costSnapshot: CostSnapshotDto | null;
  ltcSnapshot?: LtcSnapshotDto;
}

export interface UpdateProfileRequest {
  profile: ProfileSnapshotDto;
}

export interface UpdateDrugsRequest {
  drugs: SelectedDrugDto[];
}

export interface UpdatePharmacyRequest {
  pharmacy: SelectedPharmacyDto | null;
  mailOrderPharmacy: MailOrderPharmacyDto | null;
}

export interface UpdatePlansRequest {
  plans: SelectedPlanDto[];
}

// ── LTC Snapshot DTOs ──

export interface LtcSnapshotDto {
  healthProfile: number;
  adultDayYears: number;
  homeCareYears: number;
  nursingCareYears: number;
  totalCost: number;
  totalPresentValue: number;
  evaluation: LtcEvaluationSnapDto | null;
}

export interface LtcEvaluationSnapDto {
  costTrajectory: string;
  trajectoryExplanation: string;
  overallAssessment: string;
  totalCost: number;
  totalPresentValue: number;
  projectionYears: number;
  averageAnnualCost: number;
  yearlyHighlights: YearlyHighlightDto[];
  categories: LtcCostCategorySnapDto[];
  savingsTips: SavingsTipSnapDto[];
}

export interface LtcCostCategorySnapDto {
  name: string;
  lifetimeTotal: number;
  presentValue: number;
  percentOfTotal: number;
  trend: string;
  insight: string;
}
