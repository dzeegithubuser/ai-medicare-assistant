/**
 * Centralized tooltip / description data for the entire application.
 * Edit this file to update labels, descriptions, colors, and icons
 * shown on plan cards, pharmacy cards, and formulary tier chips.
 */

export interface PlanTypeInfo {
  label: string;
  description: string;
  badgeClass: string;
  chipClass: string;
}

export interface CoverageItem {
  icon: string;
  label: string;
  tooltip: string;
  included: boolean;
}

export interface PharmacyTypeInfo {
  description: string;
  icon: string;
  bgClass: string;
  textClass: string;
}

export interface TierInfo {
  label: string;
  description: string;
  chipClass: string;
}

// ---------------------------------------------------------------------------
// Plan types  (keys: API codes + display codes)
// ---------------------------------------------------------------------------
export const PLAN_TYPES: Record<string, PlanTypeInfo> = {
  'MA-PD': {
    label: 'Medicare Advantage (MA-PD)',
    description:
      'All-in-one plan bundling Original Medicare (Parts A & B) with prescription drug coverage (Part D). Often includes dental, vision & hearing benefits.',
    badgeClass: 'bg-blue-100 text-blue-700',
    chipClass: 'bg-blue-100 text-blue-700',
  },
  MedicareAdvantage: {
    label: 'Medicare Advantage (MA-PD)',
    description:
      'All-in-one plan bundling Original Medicare (Parts A & B) with prescription drug coverage (Part D). Often includes dental, vision & hearing benefits.',
    badgeClass: 'bg-blue-100 text-blue-700',
    chipClass: 'bg-blue-100 text-blue-700',
  },
  PDP: {
    label: 'Part D + Medigap',
    description:
      'Standalone Part D drug plan paired with a Medigap (Medicare Supplement) policy that covers out-of-pocket costs like copays, coinsurance & deductibles from Original Medicare.',
    badgeClass: 'bg-purple-100 text-purple-700',
    chipClass: 'bg-purple-100 text-purple-700',
  },
  PartDPlusMedigap: {
    label: 'Part D + Medigap',
    description:
      'Standalone Part D drug plan paired with a Medigap (Medicare Supplement) policy that covers out-of-pocket costs like copays, coinsurance & deductibles from Original Medicare.',
    badgeClass: 'bg-purple-100 text-purple-700',
    chipClass: 'bg-purple-100 text-purple-700',
  },
  'D-SNP': {
    label: 'D-SNP (Dual Eligible)',
    description:
      'Dual-Eligible Special Needs Plan for people who qualify for both Medicare and Medicaid. Includes drug coverage with typically $0 or very low premiums and copays.',
    badgeClass: 'bg-orange-100 text-orange-700',
    chipClass: 'bg-orange-100 text-orange-700',
  },
  DSNPDualEligible: {
    label: 'D-SNP (Dual Eligible)',
    description:
      'Dual-Eligible Special Needs Plan for people who qualify for both Medicare and Medicaid. Includes drug coverage with typically $0 or very low premiums and copays.',
    badgeClass: 'bg-orange-100 text-orange-700',
    chipClass: 'bg-orange-100 text-orange-700',
  },
};

// ---------------------------------------------------------------------------
// Plan coverage info  (what each plan type includes)
// ---------------------------------------------------------------------------
export const PLAN_COVERAGE_INFO: Record<string, CoverageItem[]> = {
  'MA-PD': [
    { icon: 'local_hospital', label: 'Part A', tooltip: 'Hospital insurance — covers inpatient hospital stays, skilled nursing, hospice & home health care.', included: true },
    { icon: 'medical_services', label: 'Part B', tooltip: 'Medical insurance — covers doctor visits, outpatient care, preventive services & durable medical equipment.', included: true },
    { icon: 'medication', label: 'Part D', tooltip: 'Prescription drug coverage — covers formulary medications with tiered copays.', included: true },
    { icon: 'dentistry', label: 'Dental', tooltip: 'Many MA plans include routine dental coverage (cleanings, X-rays, extractions). Benefits vary by plan.', included: true },
    { icon: 'visibility', label: 'Vision', tooltip: 'Many MA plans include routine vision coverage (eye exams, eyeglasses or contacts). Benefits vary by plan.', included: true },
    { icon: 'hearing', label: 'Hearing', tooltip: 'Many MA plans include routine hearing exams and hearing aid coverage. Benefits vary by plan.', included: true },
    { icon: 'fitness_center', label: 'Fitness', tooltip: 'Some MA plans include gym memberships or fitness programs like SilverSneakers.', included: true },
  ],
  MedicareAdvantage: [
    { icon: 'local_hospital', label: 'Part A', tooltip: 'Hospital insurance — covers inpatient hospital stays, skilled nursing, hospice & home health care.', included: true },
    { icon: 'medical_services', label: 'Part B', tooltip: 'Medical insurance — covers doctor visits, outpatient care, preventive services & durable medical equipment.', included: true },
    { icon: 'medication', label: 'Part D', tooltip: 'Prescription drug coverage — covers formulary medications with tiered copays.', included: true },
    { icon: 'dentistry', label: 'Dental', tooltip: 'Many MA plans include routine dental coverage (cleanings, X-rays, extractions). Benefits vary by plan.', included: true },
    { icon: 'visibility', label: 'Vision', tooltip: 'Many MA plans include routine vision coverage (eye exams, eyeglasses or contacts). Benefits vary by plan.', included: true },
    { icon: 'hearing', label: 'Hearing', tooltip: 'Many MA plans include routine hearing exams and hearing aid coverage. Benefits vary by plan.', included: true },
    { icon: 'fitness_center', label: 'Fitness', tooltip: 'Some MA plans include gym memberships or fitness programs like SilverSneakers.', included: true },
  ],
  PDP: [
    { icon: 'local_hospital', label: 'Part A', tooltip: 'Hospital insurance — covered through Original Medicare, not this plan. You must already be enrolled in Part A.', included: false },
    { icon: 'medical_services', label: 'Part B', tooltip: 'Medical insurance — covered through Original Medicare, not this plan. You must already be enrolled in Part B.', included: false },
    { icon: 'medication', label: 'Part D', tooltip: 'Prescription drug coverage — this standalone PDP plan covers formulary medications with tiered copays.', included: true },
    { icon: 'shield', label: 'Medigap', tooltip: 'Medigap (Medicare Supplement) policy covers out-of-pocket costs from Original Medicare — copays, coinsurance & deductibles. Purchased separately.', included: true },
    { icon: 'dentistry', label: 'Dental', tooltip: 'Not included. Must be purchased as a separate standalone dental plan.', included: false },
    { icon: 'visibility', label: 'Vision', tooltip: 'Not included. Must be purchased as a separate standalone vision plan.', included: false },
    { icon: 'hearing', label: 'Hearing', tooltip: 'Not included. Must be purchased separately.', included: false },
  ],
  PartDPlusMedigap: [
    { icon: 'local_hospital', label: 'Part A', tooltip: 'Hospital insurance — covered through Original Medicare, not this plan. You must already be enrolled in Part A.', included: false },
    { icon: 'medical_services', label: 'Part B', tooltip: 'Medical insurance — covered through Original Medicare, not this plan. You must already be enrolled in Part B.', included: false },
    { icon: 'medication', label: 'Part D', tooltip: 'Prescription drug coverage — this standalone PDP plan covers formulary medications with tiered copays.', included: true },
    { icon: 'shield', label: 'Medigap', tooltip: 'Medigap (Medicare Supplement) policy covers out-of-pocket costs from Original Medicare — copays, coinsurance & deductibles. Purchased separately.', included: true },
    { icon: 'dentistry', label: 'Dental', tooltip: 'Not included. Must be purchased as a separate standalone dental plan.', included: false },
    { icon: 'visibility', label: 'Vision', tooltip: 'Not included. Must be purchased as a separate standalone vision plan.', included: false },
    { icon: 'hearing', label: 'Hearing', tooltip: 'Not included. Must be purchased separately.', included: false },
  ],
  'D-SNP': [
    { icon: 'local_hospital', label: 'Part A', tooltip: 'Hospital insurance — included in this D-SNP plan. Covers inpatient stays, skilled nursing & hospice.', included: true },
    { icon: 'medical_services', label: 'Part B', tooltip: 'Medical insurance — included in this D-SNP plan. Covers doctor visits, outpatient care & preventive services.', included: true },
    { icon: 'medication', label: 'Part D', tooltip: 'Prescription drug coverage — included with typically $0 or very low copays for dual-eligible beneficiaries.', included: true },
    { icon: 'account_balance', label: 'Medicaid', tooltip: 'Medicaid benefits coordinated through this plan — may include long-term care, transportation & personal care services.', included: true },
    { icon: 'dentistry', label: 'Dental', tooltip: 'Most D-SNP plans include comprehensive dental benefits at no additional cost.', included: true },
    { icon: 'visibility', label: 'Vision', tooltip: 'Most D-SNP plans include routine vision coverage at no additional cost.', included: true },
    { icon: 'hearing', label: 'Hearing', tooltip: 'Most D-SNP plans include hearing exams and hearing aid coverage at no additional cost.', included: true },
    { icon: 'local_taxi', label: 'Transport', tooltip: 'Many D-SNP plans include non-emergency medical transportation to appointments.', included: true },
  ],
  DSNPDualEligible: [
    { icon: 'local_hospital', label: 'Part A', tooltip: 'Hospital insurance — included in this D-SNP plan. Covers inpatient stays, skilled nursing & hospice.', included: true },
    { icon: 'medical_services', label: 'Part B', tooltip: 'Medical insurance — included in this D-SNP plan. Covers doctor visits, outpatient care & preventive services.', included: true },
    { icon: 'medication', label: 'Part D', tooltip: 'Prescription drug coverage — included with typically $0 or very low copays for dual-eligible beneficiaries.', included: true },
    { icon: 'account_balance', label: 'Medicaid', tooltip: 'Medicaid benefits coordinated through this plan — may include long-term care, transportation & personal care services.', included: true },
    { icon: 'dentistry', label: 'Dental', tooltip: 'Most D-SNP plans include comprehensive dental benefits at no additional cost.', included: true },
    { icon: 'visibility', label: 'Vision', tooltip: 'Most D-SNP plans include routine vision coverage at no additional cost.', included: true },
    { icon: 'hearing', label: 'Hearing', tooltip: 'Most D-SNP plans include hearing exams and hearing aid coverage at no additional cost.', included: true },
    { icon: 'local_taxi', label: 'Transport', tooltip: 'Many D-SNP plans include non-emergency medical transportation to appointments.', included: true },
  ],
};

// ---------------------------------------------------------------------------
// Complementary plan suggestions (shown when a plan has coverage gaps)
// ---------------------------------------------------------------------------
export interface ComplementarySuggestion {
  icon: string;
  title: string;
  description: string;
  searchTerm: string;
}

export const COMPLEMENTARY_SUGGESTIONS: Record<string, ComplementarySuggestion[]> = {
  PDP: [
    { icon: 'local_hospital', title: 'Original Medicare (Part A & B)', description: 'You must already be enrolled in Original Medicare for hospital and medical coverage. Contact Social Security or visit medicare.gov to verify enrollment.', searchTerm: 'Original Medicare' },
    { icon: 'shield', title: 'Medigap (Medicare Supplement)', description: 'Covers out-of-pocket costs like copays, coinsurance & deductibles from Original Medicare. Plans are lettered A through N — Plan G and Plan N are most popular.', searchTerm: 'Medigap supplement' },
    { icon: 'dentistry', title: 'Standalone Dental Plan', description: 'PDP plans do not include dental. Consider a standalone dental plan for preventive care (cleanings, X-rays) and major services (crowns, dentures).', searchTerm: 'standalone dental plan' },
    { icon: 'visibility', title: 'Standalone Vision Plan', description: 'PDP plans do not include vision. Consider a standalone vision plan for annual eye exams, eyeglasses, and contact lenses.', searchTerm: 'standalone vision plan' },
    { icon: 'hearing', title: 'Hearing Coverage', description: 'PDP plans do not include hearing benefits. Look into standalone hearing plans or discount programs for hearing exams and hearing aids.', searchTerm: 'hearing coverage' },
  ],
  PartDPlusMedigap: [
    { icon: 'local_hospital', title: 'Original Medicare (Part A & B)', description: 'You must already be enrolled in Original Medicare for hospital and medical coverage. Contact Social Security or visit medicare.gov to verify enrollment.', searchTerm: 'Original Medicare' },
    { icon: 'shield', title: 'Medigap (Medicare Supplement)', description: 'Covers out-of-pocket costs like copays, coinsurance & deductibles from Original Medicare. Plans are lettered A through N — Plan G and Plan N are most popular.', searchTerm: 'Medigap supplement' },
    { icon: 'dentistry', title: 'Standalone Dental Plan', description: 'This plan type does not include dental. Consider a standalone dental plan for preventive care and major services.', searchTerm: 'standalone dental plan' },
    { icon: 'visibility', title: 'Standalone Vision Plan', description: 'This plan type does not include vision. Consider a standalone vision plan for annual eye exams and corrective lenses.', searchTerm: 'standalone vision plan' },
    { icon: 'hearing', title: 'Hearing Coverage', description: 'This plan type does not include hearing benefits. Look into standalone hearing plans or discount programs.', searchTerm: 'hearing coverage' },
  ],
};

// ---------------------------------------------------------------------------
// Pharmacy types  (matched by keyword in the NPI taxonomy description)
// ---------------------------------------------------------------------------
export const PHARMACY_TYPES: Record<string, PharmacyTypeInfo> = {
  retail: {
    description:
      'Community or retail pharmacy where prescriptions are filled in person at a physical location.',
    icon: 'storefront',
    bgClass: 'bg-blue-50',
    textClass: 'text-blue-600',
  },
  mail: {
    description:
      'Mail-order pharmacy that delivers prescriptions directly to your home, often with 90-day supply options at lower cost.',
    icon: 'mail',
    bgClass: 'bg-purple-50',
    textClass: 'text-purple-600',
  },
  specialty: {
    description:
      'Specialty pharmacy that dispenses high-cost, complex medications (e.g., biologics, injectables) requiring special handling or monitoring.',
    icon: 'science',
    bgClass: 'bg-amber-50',
    textClass: 'text-amber-600',
  },
  compounding: {
    description:
      'Compounding pharmacy that creates customized medications tailored to individual patient needs (e.g., dosage forms, allergen-free formulations).',
    icon: 'biotech',
    bgClass: 'bg-rose-50',
    textClass: 'text-rose-600',
  },
  nuclear: {
    description:
      'Nuclear pharmacy specializing in radioactive medications used for diagnostic imaging and certain cancer treatments.',
    icon: 'warning',
    bgClass: 'bg-red-50',
    textClass: 'text-red-600',
  },
  home_infusion: {
    description:
      'Home infusion or long-term care pharmacy providing medications and infusion therapy services to patients in their homes or care facilities.',
    icon: 'home_health',
    bgClass: 'bg-cyan-50',
    textClass: 'text-cyan-600',
  },
};

/** Default style when no pharmacy type keyword matches */
export const DEFAULT_PHARMACY_TYPE: PharmacyTypeInfo = {
  description:
    'Community or retail pharmacy where prescriptions are filled in person at a physical location.',
  icon: 'storefront',
  bgClass: 'bg-blue-50',
  textClass: 'text-blue-600',
};

// ---------------------------------------------------------------------------
// Formulary tiers  (Part D standard tier structure)
// ---------------------------------------------------------------------------
export const FORMULARY_TIERS: Record<number, TierInfo> = {
  1: {
    label: 'Tier 1 — Preferred Generic',
    description: 'Lowest-cost generic drugs. Typically $0–$10 copay.',
    chipClass: 'bg-green-100 text-green-700',
  },
  2: {
    label: 'Tier 2 — Generic',
    description: 'Other generic drugs with slightly higher copays than Tier 1.',
    chipClass: 'bg-cyan-100 text-cyan-700',
  },
  3: {
    label: 'Tier 3 — Preferred Brand',
    description: 'Preferred brand-name drugs. Moderate copay, often $30–$50.',
    chipClass: 'bg-amber-100 text-amber-700',
  },
  4: {
    label: 'Tier 4 — Non-Preferred Drug',
    description: 'Non-preferred brand or generic drugs. Higher copay, often $50–$100.',
    chipClass: 'bg-orange-100 text-orange-700',
  },
  5: {
    label: 'Tier 5 — Specialty',
    description: 'High-cost specialty drugs (biologics, injectables). May require 25–33% coinsurance.',
    chipClass: 'bg-red-100 text-red-700',
  },
};

// ---------------------------------------------------------------------------
// Network types  (MA plan network structures)
// ---------------------------------------------------------------------------
export const NETWORK_TYPES: Record<string, { description: string; chipClass: string }> = {
  HMO: {
    description: 'Health Maintenance Organization — must use in-network providers and get referrals to see specialists. Typically lower premiums.',
    chipClass: 'bg-cyan-100 text-cyan-700',
  },
  PPO: {
    description: 'Preferred Provider Organization — can see any provider, but pay less with in-network doctors. No referrals needed for specialists.',
    chipClass: 'bg-sky-100 text-sky-700',
  },
  'HMO-POS': {
    description: 'HMO with Point-of-Service option — primarily HMO, but allows some out-of-network visits at higher cost.',
    chipClass: 'bg-cyan-100 text-cyan-700',
  },
  PFFS: {
    description: 'Private Fee-for-Service — can see any Medicare-approved provider who agrees to the plan payment terms. Most flexible network.',
    chipClass: 'bg-indigo-100 text-indigo-700',
  },
};

// ---------------------------------------------------------------------------
// Provider network size descriptions
// ---------------------------------------------------------------------------
export const NETWORK_SIZES: Record<string, string> = {
  Large: 'Broad national network with thousands of providers and hospitals across the country.',
  Medium: 'Regional network with good coverage in your state and surrounding area.',
  Small: 'Local or limited network — fewer provider choices, but often lower premiums.',
};

// ---------------------------------------------------------------------------
// Gap coverage descriptions
// ---------------------------------------------------------------------------
export const GAP_COVERAGE: Record<string, string> = {
  None: 'No coverage in the donut hole — you pay full price for drugs until you reach catastrophic coverage.',
  Some: 'Partial gap coverage — some generic or select brand drugs are covered during the donut hole at reduced cost.',
  Full: 'Full gap coverage — drugs remain covered during the donut hole with the same or similar copays.',
};

// ---------------------------------------------------------------------------
// Plan categories  (coverage bundling strategy)
// ---------------------------------------------------------------------------
export interface PlanCategoryInfo {
  label: string;
  description: string;
  chipClass: string;
  icon: string;
}

export const PLAN_CATEGORIES: Record<string, PlanCategoryInfo> = {
  MA_ONLY: {
    label: 'MA with PDP',
    description: 'Medicare Advantage plan that bundles Part A, B, and D (prescription drug coverage included). An all-in-one option — no separate drug plan needed.',
    chipClass: 'bg-blue-100 text-blue-700',
    icon: 'verified',
  },
  PDP_ONLY: {
    label: 'PDP Only',
    description: 'Standalone Part D prescription drug plan. Covers drug costs only — you stay on Original Medicare for Part A & B. No Medigap supplement included.',
    chipClass: 'bg-amber-100 text-amber-700',
    icon: 'medication',
  },
  PDP_MEDIGAP: {
    label: 'PDP + Medigap',
    description: 'Part D drug plan paired with a Medigap supplement (e.g., Plan G, Plan N) that covers out-of-pocket costs from Original Medicare — copays, coinsurance & deductibles.',
    chipClass: 'bg-purple-100 text-purple-700',
    icon: 'shield',
  },
  MA_PDP: {
    label: 'MA + Separate PDP',
    description: 'Medicare Advantage plan without built-in drug coverage, paired with a separate standalone Part D plan for prescriptions.',
    chipClass: 'bg-cyan-100 text-cyan-700',
    icon: 'add_circle',
  },
};
