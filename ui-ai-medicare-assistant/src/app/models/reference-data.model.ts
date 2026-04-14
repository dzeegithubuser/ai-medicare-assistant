export interface LabelValue {
  value: string;
  label: string;
}

export interface MagiTierOption {
  value: string;
  label: string;
  description: string;
}

export interface HouseholdSizeOption {
  value: number;
  label: string;
}

export interface MedigapDataSourceOption {
  value: string | null;
  label: string;
}

export interface ReferenceData {
  genders: LabelValue[];
  maritalStatuses: LabelValue[];
  taxFilingStatuses: LabelValue[];
  incomeFilingStatuses: LabelValue[];
  magiTiersByFiling: Record<string, MagiTierOption[]>;
  tobaccoStatuses: LabelValue[];
  disabilityStatuses: LabelValue[];
  chronicConditions: LabelValue[];
  usStates: LabelValue[];
  householdSizes: HouseholdSizeOption[];
  medigapDataSources: MedigapDataSourceOption[];
  medigapPlanTypes: LabelValue[];
}
