// ── Request ────────────────────────────────────────────────────

export interface CalculateCostsRequest {
  planBundleCode: string;
  medicareAdvantagePremium: number;
  maWithPrescriptionBenefit: boolean;
  partDOOP: number;
  partDOOPFullYear: number;
  partABenefitServiceCost: number;
  partBBenefitServiceCost: number;
  planRecommendName: string;
  recommendationListId: string;
  supplementDataProvided: boolean;
  partDDataProvided: boolean;
  reserveDaysUsed: number;
  dental: boolean;
  dentalHealthGrade: number;
  boughtPlanA: boolean;
  medicareAdvantageDataProvided: boolean;
  partDPremium: number;
  calculateForAdjustedMonth: number;
  supplementPlanType: string;
}

// ── Response from /evaluate-costs ─────────────────────────────

export interface EvaluateCostsResponse {
  yearlyDetails: IndividualMedicareDetail[];
  lifetimeTotals: LifetimeTotals;
  evaluation: CostEvaluation;
  presentValue: number;
}

export interface IndividualMedicareDetail {
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

export interface LifetimeTotals {
  lifeTimeABMedicareAdvantageExpenses: number;
  lifeTimeABMedicareAdvantagePremium: number;
  lifeTimeABMedicareAdvantageOop: number;
  lifeTimeDSurcharge: number;
  lifeTimeBSurcharge: number;
  totalIrmaa: number;
  lifeTimeConciergePremium: number;
  supplementPlanType: string;
  supplementPlanPremium: number;
  conciergeIncluded: boolean;
  lifeTimeABGDExpenses: number;
  lifeTimeABGDPremium: number;
  lifeTimeABGDOop: number;
  lifeTimeABFDExpenses: number;
  lifeTimeABFDPremium: number;
  lifeTimeABFDOop: number;
  lifeTimeABNDExpenses: number;
  lifeTimeABNDPremium: number;
  lifeTimeABNDOop: number;
  lifeTimeABCDExpenses: number;
  lifeTimeABCDPremium: number;
  lifeTimeABCDOop: number;
}

// ── AI Evaluation ─────────────────────────────────────────────

export interface CostEvaluation {
  planName: string;
  planBundleCode: string;
  lifetimeSummary: LifetimeSummary;
  costTrajectory: 'Rising' | 'Stable' | 'Declining' | 'Mixed';
  trajectoryExplanation: string;
  yearlyHighlights: YearlyHighlight[];
  categories: CostCategory[];
  savingsTips: SavingsTip[];
  overallAssessment: string;
}

export interface LifetimeSummary {
  totalPremiums: number;
  totalOutOfPocket: number;
  totalCombined: number;
  projectionYears: number;
  averageAnnualCost: number;
}

export interface YearlyHighlight {
  year: number;
  totalCost: number;
  flag: 'Highest' | 'Lowest' | 'Spike' | 'Normal';
  explanation: string;
}

export interface CostCategory {
  name: string;
  lifetimeTotal: number;
  percentOfTotal: number;
  trend: 'Rising' | 'Stable' | 'Declining';
  insight: string;
}

export interface SavingsTip {
  title: string;
  description: string;
  estimatedSavings: string;
  priority: 'High' | 'Medium' | 'Low';
}

// ── Expense Table ─────────────────────────────────────────────

export interface ExpenseTableRow {
  currentTotalExpense: number;
  currentTotalPremium: number;
  currentTotalOOP: number;
  lifetimeTotalExpense: number;
  lifetimeTotalPremium: number;
  lifetimeTotalOOP: number;
}
