export function isNextStepCommand(lower: string): boolean {
  return (
    lower === 'continue' ||
    lower === 'done' ||
    lower === 'next' ||
    /^go to next(\s+step)?$/.test(lower) ||
    /^next\s+step$/.test(lower) ||
    /^move\s+(forward|to\s+next)/.test(lower) ||
    /^continue(\s+to)?\s+(next\s+step|drug(s)?)$/.test(lower) ||
    /^go to\s+drug(s)?$/.test(lower) ||
    /^want\s+to\s+go\s+to\s+next/.test(lower)
  );
}

/** "continue to drugs" / "go to drug(s)" — should run the same path as the shell Continue to Drugs (save + proceed). */
export function isExplicitDrugStepCommand(lower: string): boolean {
  return (
    /^continue(\s+to)?\s+drug(s)?$/.test(lower) ||
    /^go to\s+drug(s)?$/.test(lower)
  );
}

/**
 * Generic "advance" wording on profile review where we only nudge the footer (not drug-directed commands).
 */
export function isGenericProfileReviewHoldCommand(lower: string): boolean {
  if (isExplicitDrugStepCommand(lower)) return false;
  return isNextStepCommand(lower);
}

export function shouldTriggerProfileSaveOnNext(
  lower: string,
  onAnalysisProfile: boolean,
  isProfileEditMode: boolean,
): boolean {
  return onAnalysisProfile && isProfileEditMode && isNextStepCommand(lower);
}
