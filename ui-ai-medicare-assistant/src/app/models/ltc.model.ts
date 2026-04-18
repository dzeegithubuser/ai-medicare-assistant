export interface LtcExpenseEntry {
  year: number;
  expense: number;
}

export interface LtcProjectionResponse {
  webServiceTransactionId?: string;
  webServiceStatus?: string;
  transactionTypeFlag: boolean;
  age: number;
  healthProfile: number;
  gender?: string;
  state?: string;
  region?: string;
  zipcode: number;
  countyCode: number;
  lifeExpenctancy: number;
  tobaccoUsage: boolean;
  alzheimersFlag: boolean;
  heartStorkeFlag: boolean;
  currentLifeStyleExpenses: number;
  numberOfAdultDayHealthCareLTCYears: number;
  numberOfHomeCareLTCYears: number;
  numberOfAssistedCareLTCYears: number;
  numberOfNursingCareLTCYears: number;
  startingYearOfAdultDayHealthCare: number;
  startingYearOfHomeCare: number;
  startingYearOfAssistedCare: number;
  startingYearOfNursingCare: number;
  adultDayHealthCare: number;
  presentValueAdultDayHealthCare: number;
  homeCare: number;
  presentValueHomeCare: number;
  assistedCare: number;
  presentValueAssistedCare: number;
  nursingCare: number;
  presentValueNursingCare: number;
  futureAdultDayHealthCareExpenseList: LtcExpenseEntry[];
  expectedAdultDayHealthCare: number;
  presentValueExpectedAdultDayHealthCare: number;
  futureHomeCareExpenseList: LtcExpenseEntry[];
  expectedHomeCare: number;
  presentValueExpectedHomeCare: number;
  futureAssistedCareExpensesList: LtcExpenseEntry[];
  expectedAssistedCare: number;
  presentValueExpectedAssistedCare: number;
  futureNursingCareExpensesList: LtcExpenseEntry[];
  expectedNursingCare: number;
  presentValueExpectedNursingCare: number;
  presentValueYear: number;
}

// ── AI evaluation models (matches backend LtcCostEvaluation) ──

export interface LtcLifetimeSummary {
  totalCost: number;
  totalPresentValue: number;
  projectionYears: number;
  averageAnnualCost: number;
}

export interface LtcYearlyHighlight {
  year: number;
  totalCost: number;
  flag: 'Highest' | 'Lowest' | 'Spike' | 'Normal';
  explanation: string;
}

export interface LtcCostCategory {
  name: string;
  lifetimeTotal: number;
  presentValue: number;
  percentOfTotal: number;
  trend: 'Rising' | 'Stable' | 'Declining';
  insight: string;
}

export interface LtcSavingsTip {
  title: string;
  description: string;
  estimatedSavings: string;
  priority: 'High' | 'Medium' | 'Low';
}

export interface LtcCostEvaluation {
  lifetimeSummary: LtcLifetimeSummary;
  costTrajectory: 'Rising' | 'Stable' | 'Declining' | 'Mixed';
  trajectoryExplanation: string;
  yearlyHighlights: LtcYearlyHighlight[];
  categories: LtcCostCategory[];
  savingsTips: LtcSavingsTip[];
  overallAssessment: string;
}

export interface LtcProjectionResult {
  projection: LtcProjectionResponse;
  evaluation: LtcCostEvaluation;
}

export interface LtcProjectionRequest {
  age: number;
  pvAsOfYear: number;
  lifeExpectancy: number;
  transactionTypeFlag: string;
  healthProfile: number;
  location: string;
  zipcode: string;
  tobacco: number;
  currentLifeStyleExpenses: number;
  numberOfAdultDayHealthCareLTCYears: number;
  numberOfAssistedCareLTCYears: number;
  numberOfHomeCareLTCYears: number;
  numberOfNursingCareLTCYears: number;
  gender: string;
  alzheimersFlag: number;
  heartStorkeFlag: number;
}

export interface SaveLtcCurrentRequest {
  healthProfile: number;
  numberOfAdultDayHealthCareYears: number;
  numberOfHomeCareYears: number;
  numberOfNursingCareYears: number;
}

export interface LtcCurrentResponse {
  healthProfile: number;
  numberOfAdultDayHealthCareYears: number;
  numberOfHomeCareYears: number;
  numberOfNursingCareYears: number;
  updatedAt: string;
}
