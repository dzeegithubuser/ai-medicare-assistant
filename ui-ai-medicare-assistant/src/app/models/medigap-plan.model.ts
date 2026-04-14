// ───── REQUEST ─────

export interface MedigapPlanQuotesRequest {
  zip5: string;
  gender: string;
  tobacco: number;
  birthDate: string;       // MM-YYYY
  plan: string;
  county: string;
  taxFilingStatus: string;
  magiTier: number;
  healthProfile: number;
  coverageYear: string;
  versionId: 'AIVANTE' | 'MEDICARE_GOV' | null;
}

// ───── RESPONSE ─────

export interface MedigapPlanQuotesResponse {
  contractIdCarrierMap: Record<string, string>;
  deductible: number;
  planList: MedigapPlan[];
}

export interface MedigapPlan {
  key: string;
  age: number;
  archive: string | null;
  company_base: MedigapCompanyBase | null;
  contextual_data: MedigapContextualData | null;
  discount_category: string;
  discounts: MedigapDiscount[];
  e_app_link: string;
  effective_date: string;
  expires_date: string;
  fees: MedigapFee[];
  gender: string;
  has_brochure: boolean;
  has_pdf_app: boolean;
  is_open_rate: boolean;
  last_modified: string;
  legacy_id: string | null;
  plan: string;
  rate: MedigapRate | null;
  rate_increases: MedigapRateIncrease[];
  rate_type: string;
  rating_class: string;
  related_data: Record<string, unknown>;
  riders: unknown[];
  select: boolean;
  tobacco: boolean;
  view_type: string[];
  partBPremium: number;
  partBPremiumSurcharge: number;
  monthsUsedForExpenseCalc: number;
  yearForPartBData: number;
  medigapOOP: number;
  partAServiceOOP: number;
  partBServiceOOP: number;
  naic: string;
}

export interface MedigapCompanyBase {
  ambest_outlook: string;
  ambest_rating: string;
  business_type: string;
  company_image_url: string;
  customer_complaint_ratio: number | null;
  customer_satisfaction_ratio: number;
  established_year: number;
  last_modified: string;
  med_supp_national_market_data: MedigapMarketData | null;
  naic: string;
  name: string;
  name_full: string;
  parent_company_base: MedigapParentCompany | null;
  sp_rating: string;
  state_marketing_data: MedigapStateMarketingData[];
  type: string;
  underwriting_data: unknown[];
}

export interface MedigapMarketData {
  claims: number;
  lives: number;
  market_share: number;
  premiums: number;
  state: string | null;
}

export interface MedigapParentCompany {
  key: string;
  code: string;
  established_year: number;
  last_modified: string;
  name: string;
}

export interface MedigapStateMarketingData {
  marketing_name: string;
  state: string;
}

export interface MedigapContextualData {
  has_eapp: boolean;
}

export interface MedigapDiscount {
  name: string;
  rule: string | null;
  type: string;
  value: number;
}

export interface MedigapFee {
  name: string;
  type: string;
  value: number;
}

export interface MedigapRate {
  annual: number;
  month: number;
  quarter: number;
  semi_annual: number;
}

export interface MedigapRateIncrease {
  date: string;
  rate_increase: number;
}

// ===== Enrichment (computed display fields) =====

export interface EnrichedMedigapCard {
  premiumMonthly: number;
  premiumAnnual: number;
  insuranceCarrier: string;
  partBSurcharge: number;
  healthcareOOP: number;
  remainingMonths: number;
}
