import { ChatIntent } from './chat-intent.service';

/** Affirmative responses accepted for yes/no confirmation prompts. */
export const AFFIRMATIVE: string[] = ['yes', 'y', 'confirm', 'ok', 'sure', 'do it'];

/**
 * Matches explicit drug-related keywords in user input. Used on the profile page
 * to distinguish bare drug names ("metformin") from explicit drug requests
 * ("add drug metformin"). Only texts matching this pattern trigger a cross-page
 * redirect to the Drugs step when profile extraction returns empty.
 */
export const DRUG_KEYWORD_PATTERN =
  /\b(drugs?|medications?|medicines?|prescriptions?|rx|meds|pills?|tablets?|capsules?)\b/i;

/**
 * Matches explicit pharmacy-related keywords. Used on non-pharmacy pages to
 * distinguish general text from explicit pharmacy requests.
 */
export const PHARMACY_KEYWORD_PATTERN =
  /\b(pharmac(y|ies)|cvs|walgreens|rite\s*aid|costco|walmart|kroger|store|location)\b/i;

/**
 * Matches explicit plan-related keywords. Used on non-plan pages to
 * distinguish general text from explicit plan requests.
 */
export const PLAN_KEYWORD_PATTERN =
  /\b(plans?|part\s*d|pdp|medigap|medicare\s*advantage|\bma\b|coverage|enroll(ment)?)\b/i;

/**
 * Catches generic backward navigation: "back", "go back", "previous", "previous step".
 * These trigger sequential backward navigation (current step − 1).
 */
export const BACK_PATTERN =
  /^\s*(go\s+)?back\s*$|^\s*previous(\s+step)?\s*$/i;

/**
 * Catches targeted step navigation: "go to profile", "go back to drugs",
 * "switch to pharmacies", "take me to plans", "navigate to drugs", "back to profile",
 * "go to care type" (LTC).
 * Capture group 1 = step keyword (profile|drug(s)|pharmac(y|ies)|plan(s)|care[\s-]?type).
 */
export const TARGETED_STEP_PATTERN =
  /(?:go\s+(?:back\s+)?to|switch\s+to|take\s+me\s+to|navigate\s+to|back\s+to)\s+(profile|drugs?|pharmac(?:y|ies)|plans?|care[\s-]?type)\s*$/i;

/**
 * Catches "return to where I was" / "go to my last step" / "go to step where I came from".
 */
export const RETURN_PATTERN =
  /\b(?:return|go\s+back\s+to\s+where\s+I\s+was|go\s+to\s+(?:my\s+last\s+step|step\s+where\s+I\s+came\s+from)|where\s+I\s+was|where\s+I\s+came\s+from)\b/i;

/** Patterns that indicate an app-level action rather than a page-specific selection. */
export const ACTION_PATTERNS = [
  /\bsave\b.*\bprescription/i,
  /\bload\b.*\bprescription/i,
  /\bsave\b.*\banalysis/i,
  /\brun\b.*\banalysis/i,
  /\bcalculate\b.*\bcost/i,
  /\breset\b.*\banalysis/i,
  /\bsign\s*out\b/i,
  /\blog\s*out\b/i,
  /\bshow\b.*\bsaved/i,
  /\bhelp\b/i,
];

/**
 * Intents handled by `handleIntent` when the user is on the Drugs step — run AI classification
 * before drug formulation extraction so phrases like "get me pharmacy" map to NAVIGATE_PHARMACIES.
 */
export const INTENTS_BEFORE_DRUG_FORMULATION_ON_FP_DRUGS = new Set<ChatIntent>([
  'NAVIGATE_PROFILE',
  'NAVIGATE_ANALYSIS_DRUGS',
  'NAVIGATE_PHARMACIES',
  'NAVIGATE_PLANS',
  'NAVIGATE_COST_PROJECTIONS',
  'NAVIGATE_SAVED_ANALYSES',
  'SWITCH_TO_PDP',
  'SWITCH_TO_MA',
  'ACTION_RESET_ANALYSIS',
  'ACTION_SIGN_OUT',
  'ACTION_LOAD_PRESCRIPTIONS',
  'ACTION_HELP',
  'ACTION_RUN_ANALYSIS',
  'ACTION_SAVE_ANALYSIS',
]);
