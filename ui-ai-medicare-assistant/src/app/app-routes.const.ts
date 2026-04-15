/**
 * Central route path registry.
 * All navigation and URL checks must reference these constants —
 * never use hard-coded path strings in components or services.
 */
export const AppRoutes = {
  // Auth
  SIGNIN: 'signin',
  SIGNUP: 'signup',
  FORGOT_PASSWORD: 'forgot-password',
  RESET_PASSWORD: 'reset-password',
  CHANGE_PASSWORD: 'change-password',

  // Top-level dashboard children
  SAVED: 'saved',
  SAVED_COMPARE: 'saved/compare',
  SAVED_DETAIL: 'saved/:id',

  // Medicare analysis wizard (parent)
  MEDICARE_ANALYSIS: 'medicare-analysis',

  // Medicare analysis wizard steps (child paths)
  PROFILE: 'profile',
  DRUGS: 'fp-drugs',
  PHARMACIES: 'pharmacies',
  PLANS: 'plans',
  COST_PROJECTIONS: 'cost-projections',

  // Long Term Care wizard (parent)
  LTC: 'long-term-care',

  // Long Term Care child paths
  LTC_CARE_TYPE: 'care-type',
  LTC_PROJECTION: 'projection',

  // Absolute paths (for router.navigate / router.url checks)
  abs: {
    MEDICARE_ANALYSIS: '/medicare-analysis',
    PROFILE: '/medicare-analysis/profile',
    DRUGS: '/medicare-analysis/fp-drugs',
    PHARMACIES: '/medicare-analysis/pharmacies',
    PLANS: '/medicare-analysis/plans',
    COST_PROJECTIONS: '/medicare-analysis/cost-projections',
    SAVED: '/saved',
    CHANGE_PASSWORD: '/change-password',
    LTC: '/long-term-care',
    LTC_PROFILE: '/long-term-care/profile',
    LTC_CARE_TYPE: '/long-term-care/care-type',
    LTC_PROJECTION: '/long-term-care/projection',
  },
} as const;
