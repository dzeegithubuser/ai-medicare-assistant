import { ProfileSnapshotDto, RecommendationCategory } from '../../models/recommendation.model';

/** Aliases used across all comparison views instead of raw recommendation names */
export const LABEL_A = 'Illustration A';
export const LABEL_B = 'Illustration B';

/** Accessible color palette — high-contrast shades for elderly readability */
export const CHART_COLOR_A = '#c2410c';          // orange-700
export const CHART_COLOR_A_BG = 'rgba(234, 88, 12, 0.08)';
export const CHART_COLOR_A_FILL = 'rgba(234, 88, 12, 0.7)';
export const CHART_COLOR_B = '#15803d';           // green-700
export const CHART_COLOR_B_BG = 'rgba(22, 163, 74, 0.08)';
export const CHART_COLOR_B_FILL = 'rgba(22, 163, 74, 0.7)';

export function deltaClass(left: number, right: number): string {
  if (left === right) return 'text-gray-600';
  return left < right ? 'text-green-700' : 'text-red-700';
}

export function deltaIcon(val: number): string {
  if (val === 0) return 'remove';
  return val < 0 ? 'arrow_downward' : 'arrow_upward';
}

export function deltaLabel(val: number): string {
  if (val === 0) return 'Even';
  return val < 0 ? 'Lower' : 'Higher';
}

export function getTrajectoryIcon(trajectory: string): string {
  switch (trajectory) {
    case 'Rising': return 'trending_up';
    case 'Declining': return 'trending_down';
    case 'Stable': return 'trending_flat';
    default: return 'swap_vert';
  }
}

export function getTrajectoryColor(trajectory: string): string {
  switch (trajectory) {
    case 'Rising': return 'text-red-600';
    case 'Declining': return 'text-green-600';
    case 'Stable': return 'text-blue-600';
    default: return 'text-amber-600';
  }
}

export function getPriorityColor(priority: string): string {
  switch (priority) {
    case 'High': return 'bg-red-100 text-red-700';
    case 'Medium': return 'bg-amber-100 text-amber-700';
    case 'Low': return 'bg-green-100 text-green-700';
    default: return 'bg-gray-100 text-gray-700';
  }
}

export function starArray(rating: number): boolean[] {
  return Array.from({ length: 5 }, (_, i) => i < Math.round(rating));
}

export function typeBadgeClass(type: RecommendationCategory): string {
  return type === 'longterm' ? 'bg-purple-100 text-purple-700' : 'bg-cyan-100 text-cyan-700';
}

export function typeLabel(type: RecommendationCategory): string {
  return type === 'longterm' ? 'Long Term Care' : 'Medicare';
}

export interface ProfileRow {
  label: string;
  left: string;
  right: string;
  icon: string;
  group: 'personal' | 'location' | 'health' | 'financial';
}

const HEALTH_LABELS: Record<number, string> = {
  1: 'Best Health', 2: 'Good Health', 3: 'Moderate Health', 4: 'Poor Health', 5: 'Sick',
};

const GENDER_LABELS: Record<string, string> = { M: 'Male', F: 'Female' };

const TAX_FILING_LABELS: Record<string, string> = {
  MARRIED_FILING_JOINTLY: 'Married Filing Jointly',
  FILING_INDIVIDUALLY: 'Filing Individually',
  SINGLE: 'Single',
  HEAD_OF_HOUSEHOLD: 'Head of Household',
};

function fmtHealth(v: number): string { return HEALTH_LABELS[v] ?? String(v); }
function fmtGender(v: string): string { return GENDER_LABELS[v] ?? v; }
function fmtTobacco(v: number): string { return v === 1 ? 'Yes' : 'No'; }
function fmtConcierge(v: number, amt: number | null): string {
  if (!v) return 'No';
  return amt ? `Yes — $${amt.toLocaleString()}/yr` : 'Yes';
}
function fmtTaxFiling(v: string): string { return TAX_FILING_LABELS[v] ?? v.replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase()); }

export function buildProfileRows(l: ProfileSnapshotDto, r: ProfileSnapshotDto): ProfileRow[] {
  return [
    { label: 'Name', left: `${l.firstName} ${l.lastName}`, right: `${r.firstName} ${r.lastName}`, icon: 'person', group: 'personal' },
    { label: 'Date of Birth', left: l.dateOfBirth, right: r.dateOfBirth, icon: 'cake', group: 'personal' },
    { label: 'Gender', left: fmtGender(l.gender), right: fmtGender(r.gender), icon: 'wc', group: 'personal' },
    { label: 'ZIP Code', left: l.zipCode, right: r.zipCode, icon: 'location_on', group: 'location' },
    { label: 'State', left: l.state, right: r.state, icon: 'map', group: 'location' },
    { label: 'County', left: l.county || l.countyCode, right: r.county || r.countyCode, icon: 'place', group: 'location' },
    { label: 'Health Grade', left: fmtHealth(l.healthCondition), right: fmtHealth(r.healthCondition), icon: 'favorite', group: 'health' },
    { label: 'Life Expectancy', left: `${l.lifeExpectancy} yrs`, right: `${r.lifeExpectancy} yrs`, icon: 'hourglass_top', group: 'health' },
    { label: 'Tobacco Use', left: fmtTobacco(l.tobaccoStatus), right: fmtTobacco(r.tobaccoStatus), icon: 'smoke_free', group: 'health' },
    { label: 'Concierge', left: fmtConcierge(l.concierge, l.conciergeAmount), right: fmtConcierge(r.concierge, r.conciergeAmount), icon: 'medical_services', group: 'health' },
    { label: 'Tax Filing', left: fmtTaxFiling(l.taxFilingStatus), right: fmtTaxFiling(r.taxFilingStatus), icon: 'receipt', group: 'financial' },
    { label: 'MAGI Tier', left: l.magiTier, right: r.magiTier, icon: 'account_balance', group: 'financial' },
    { label: 'Coverage Year', left: `${l.coverageYear}`, right: `${r.coverageYear}`, icon: 'calendar_today', group: 'financial' },
  ];
}
