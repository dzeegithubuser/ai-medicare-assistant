export const PROFILE_MESSAGES = {
  SWITCHED_TO_EDIT: 'Great — I have switched to edit mode. You can edit your profile on the left.',
  START_PROFILE: "Let's start with your profile. Please complete your profile information to proceed.",
  REVIEW_INTRO: 'Here is your profile for this analysis:',
  REVIEW_QUESTION:
    '**Review your information on the left.** When you are ready, click **Continue to Drugs** at the bottom of the page (edit the form first if you need to make changes).',
  /** Shown if the user types "next" in chat on the profile step instead of using the footer button. */
  USE_CONTINUE_TO_DRUGS_IN_FOOTER:
    'To continue, click **Continue to Drugs** at the bottom of the page — that saves your profile when needed and takes you to the drug step.',
  REVIEW_LOADING: '_Profile details are still loading. If this does not update, refresh the page._',
  MODIFY_PROMPT:
    '**What would you like to change?** Describe it in the chat (for example, your ZIP code or tax filing status), or edit the form on the left. When finished, click **Continue to Drugs** in the footer, or type **next**.',
  NEXT_PROMPT: 'No problem. Choose what you want to do next.',
  SAVE_FIRST_TO_DRUGS:
    'Please click **Continue to Drugs** in the footer first to save your profile changes and move to drugs.',
  UPDATED_FIELDS_PREFIX: 'I updated the following profile fields in the form:',
  UPDATED_FIELDS_SUFFIX:
    'Review on the left, then click **Continue to Drugs** in the footer to save and proceed.',
  INVALID_CONTINUE_PREFIX: 'Cannot continue yet. Please complete/fix these fields:',
  INVALID_CONTINUE_FALLBACK: 'Cannot continue yet. Please complete required profile fields.',
} as const;

export const DRUG_MESSAGES = {
  STEP_PROMPT:
    'Please add your prescription drugs. Type drug names in the chat or go to the Drug Analysis step.',
  NEED_DRUGS_AFTER_PHARMACY: 'Pharmacy selected! Now please add your prescription drugs.',
  NEED_PROFILE_FIRST: 'Before I can analyze your prescription, please complete your profile first.',
  NO_RECOGNIZABLE_DRUGS:
    'No recognizable drug names found in your input. Please check the spelling and try again.',
  IDENTIFY_ERROR: 'Sorry, something went wrong identifying drug names. Please try again.',
  DETAILS_NOT_FOUND: 'No drug details could be found. Please check the drug names and try again.',
  DETAILS_ERROR: 'Sorry, something went wrong fetching drug details. Please try again.',
  VERIFY_CANCELLED: 'Drug name verification cancelled. You can enter new drugs above.',
  ACTION_CONFIRM: (verb: 'Removed' | 'Reopened', drugName: string | null) => `${verb} **${drugName ?? 'item'}**.`,
  PICK_NEXT_OPTION: (drugName: string) => `**${drugName}** — pick the next option below.`,
  OUT_OF_LIST_FALLBACK: 'That drug is not in your current list. I will search and add it as a new drug.',
  NOT_MATCHED_FALLBACK: 'I could not match that to your current list. I will try searching it as a new drug.',
  FORMULATION_HELP:
    'Sorry, I couldn\'t process that. Try: "select Lisinopril generic tablet 10mg 30 per month" or "what options for Metformin?"',
  FINISH_WITH_BUTTONS: (drugName: string) =>
    `**${drugName}** — use the buttons below to finish this prescription.`,
  RESTORED_FROM_SAVED_ANALYSIS: (count: number) =>
    `I restored ${count} drug${count !== 1 ? 's' : ''} from your saved analysis and marked them as selected.`,
  RESTORE_FAILED:
    'I could not restore drugs from your saved analysis right now. You can still add drugs manually.',
  STORED_DRUGS_PROMPT:
    'I found previously stored drugs for this analysis. Do you want to use those as a starting point?',
  /** Shown when saved drugs are applied automatically on entering the Drugs step (no yes/no prompt). */
  STORED_DRUGS_AUTO_LOADED:
    'I loaded your saved drugs from this analysis automatically. You can edit these selections or add new drugs below.',
  STORED_DRUGS_USE:
    'Great — I loaded your stored drugs. You can still edit these selections and add new drugs.',
  STORED_DRUGS_SKIP:
    'No problem — we will start fresh. Please add your prescription drugs.',
  /** Shown on the profile page when a bare drug name (no explicit keyword) is detected. */
  NAVIGATE_TO_DRUGS_HINT:
    'It looks like you may be searching for a drug. To add drugs to your analysis, navigate to the **Drugs** step using the stepper above, or type **"go to drugs"**.',
} as const;

export const PHARMACY_MESSAGES = {
  STEP_PROMPT: "Drugs added! Now let's find your preferred pharmacy near your location.",
  NAVIGATE: 'Taking you to the pharmacy finder.',
  REQUIRE_PROFILE: 'Please complete your profile before accessing pharmacies.',
  REQUIRE_SELECTION: 'Please select at least one pharmacy first.',
  CLEARING_FILTERS: 'Clearing filters and reloading pharmacies.',
  FILTERING_BY: (term: string) => `Filtering by "${term}".`,
  SEARCH_NOT_UNDERSTOOD: 'No search keyword understood.',
  APPLY_HINT: 'I could not apply that. Try a pharmacy name, or say **select 3rd** for the third row on this list.',
  ASSISTANT_UNREACHABLE:
    'Sorry, I could not reach the assistant. Try again, or use: "select CVS" or "remove Walgreens".',
  REMOVED_FILTER: 'Removed the search filter. Reloading all nearby pharmacies.',
  REMOVE_SELECTED: 'Removed from selection.',
  SELECTED: 'Selected.',
  ORDINAL_ACTION: (action: 'remove' | 'select', name: string, ord: number) =>
    action === 'remove'
      ? `Removed **${name}** (${ord} on this page).`
      : `Selected **${name}** (${ord} on this page). You can select up to five pharmacies in total.`,
  LIST_SELECTED: (lines: string, count: number) => `You have ${count} pharmacy(ies) selected:\n${lines}`,
  LIST_EMPTY: 'You have not selected any pharmacies yet.',
  SAVED_RX_FAILED_OPENING: 'Could not save prescriptions to your profile; opening pharmacies anyway.',
  REMOVED_FROM_SELECTION: (pharmacyName: string | null) =>
    `Removed **${pharmacyName ?? 'pharmacy'}** from your selection.`,
  RESTORED_FROM_SAVED_ANALYSIS: (name: string) =>
    `I restored your saved pharmacy selection: **${name}**.`,
  RESTORE_FAILED:
    'I could not restore your saved pharmacy selection right now. You can select one manually.',
  STORED_PHARMACY_PROMPT:
    'I found a previously stored pharmacy for this analysis. Do you want to use it as a starting point?',
  /** Shown when saved pharmacy is applied automatically on entering the Pharmacies step (no yes/no prompt). */
  STORED_PHARMACY_AUTO_LOADED:
    'I loaded your saved pharmacy from this analysis automatically. You can change this selection or add more below.',
  STORED_PHARMACY_USE:
    'Great — I loaded your stored pharmacy. You can still change this selection.',
  STORED_PHARMACY_SKIP:
    'No problem — we will start fresh. Please select your preferred pharmacy.',
  /** Shown on non-pharmacy pages when a bare pharmacy-like input is detected. */
  NAVIGATE_TO_PHARMACY_HINT:
    'It looks like you may be looking for a pharmacy. Navigate to the **Pharmacies** step using the stepper above, or type **"go to pharmacies"**.',
} as const;

export const PLAN_MESSAGES = {
  STEP_PROMPT: "Excellent! Both your drugs and pharmacy are set. Now let's find the best Medicare plan for you.",
  REQUIRE_SELECTION: 'Please select a plan before viewing cost projections.',
  REQUIRE_PROFILE: 'Please complete your profile before viewing plans.',
  REQUIRE_DRUGS: 'Please add at least one drug before viewing plans.',
  ALREADY_VIEWING: (target: 'partd' | 'ma') =>
    `You're already viewing ${target === 'partd' ? 'Part D + Medigap' : 'Medicare Advantage'} plans.`,
  SWITCHING: (target: 'partd' | 'ma') =>
    `Switching to ${target === 'partd' ? 'Part D + Medigap' : 'Medicare Advantage'} plans.`,
  REMOVE_CONFIRM: (label: string, planName: string | null) =>
    `Are you sure you want to **remove** your ${label} plan **${planName ?? 'plan'}**? (yes / no)`,
  SELECTION_HELP:
    'Sorry, I couldn\'t process that. Try: "select the Humana plan" or "remove Part D plan" or "what plans are available?"',
  REMOVED_FROM_SELECTION: (planName: string | null, label: string) =>
    `Removed **${planName ?? 'plan'}** (${label}) from your selection.`,
  /** Assistant bubble when the user selects a plan from the plan list (mirrors drug/pharmacy confirmations). */
  SELECTED_PART_D: (planName: string) =>
    `Selected **Part D** plan **${planName}**.`,
  SELECTED_MEDIGAP: (carrier: string, planLetter: string) =>
    `Selected **Medigap** — **${carrier}**, Plan ${planLetter}.`,
  SELECTED_MA: (planName: string) =>
    `Selected **Medicare Advantage** plan **${planName}**.`,
  SELECTED_MA_GAP_PART_D: (planName: string) =>
    `Selected **Part D** (gap) plan **${planName}**.`,
  RESTORED_FROM_SAVED_ANALYSIS: (count: number) =>
    `I restored ${count} saved plan selection${count !== 1 ? 's' : ''} for this analysis.`,
  RESTORE_PARTIAL: (count: number) =>
    `I restored ${count} saved plan selection${count !== 1 ? 's' : ''}. Some plans could not be matched yet and may need manual re-selection.`,
  /** All saved rows matched to current Part D / Medigap / MA API lists — `lines` is markdown bullet lines. */
  RESTORE_ALL_MATCHED: (lines: string) =>
    `I restored your saved plan selections for this analysis:\n\n${lines}`,
  /** Some rows matched, some not — each group names Part D vs Medigap vs MA explicitly. */
  RESTORE_PARTIAL_DETAIL: (restoredLines: string, pendingLines: string) =>
    `I restored:\n\n${restoredLines}\n\n**Not matched to the current lists yet** (wait for recommendations to finish loading, or pick manually):\n\n${pendingLines}`,
  /**
   * When saved plans are applied as placeholders before Part D / Medigap / MA API lists finish loading
   * (fallback hydration). Aligns with auto-load messaging for drugs/pharmacy.
   */
  SAVED_PLANS_PENDING:
    'I loaded your saved plan selections from this analysis. They will appear as selected in each list once recommendations finish loading.',
  MATCHED_SAVED_PART_D: (planName: string) =>
    `Matched your saved Part D plan **${planName}** to the current list — it should be highlighted below.`,
  MATCHED_SAVED_MEDIGAP: (carrier: string, planLetter: string) =>
    `Matched your saved Medigap plan **${carrier}**, Plan ${planLetter}, to the current quotes — it should be highlighted below.`,
  MATCHED_SAVED_MA: (planName: string) =>
    `Matched your saved Medicare Advantage plan **${planName}** to the current list — it should be highlighted below.`,
  MATCHED_SAVED_MA_GAP_PART_D: (planName: string) =>
    `Matched your saved Part D (gap) plan **${planName}** to the current list — it should be highlighted below.`,
  /** Shown when a stored/hydrated plan is dropped because it does not appear in the current API list. */
  CLEARED_PART_D_NO_LIST:
    'No Part D plans are available in the current results, so your Part D selection was cleared (and Medigap, if any).',
  CLEARED_PART_D_NOT_IN_LIST: (planName: string | null) =>
    `**${planName ?? 'Your Part D plan'}** is not in the current recommendation list, so that selection was cleared (and Medigap, if any). Please pick a Part D plan from the list.`,
  CLEARED_MEDIGAP_NO_LIST:
    'No Medigap quotes are available for your current filters, so your Medigap selection was cleared.',
  CLEARED_MEDIGAP_NOT_IN_LIST: (label: string) =>
    `**${label}** is not in the current Medigap quote list, so that Medigap selection was cleared. Choose a plan from the list if you still want Medigap.`,
  CLEARED_MA_NO_LIST:
    'No Medicare Advantage plans are available in the current results, so your MA selection was cleared.',
  CLEARED_MA_NOT_IN_LIST: (planName: string | null) =>
    `**${planName ?? 'Your Medicare Advantage plan'}** is not in the current recommendation list, so that selection was cleared. Please pick a plan from the list.`,
  CLEARED_MA_GAP_PART_D_NO_LIST:
    'No Part D plans are available for the gap step, so your Part D (gap) selection was cleared.',
  CLEARED_MA_GAP_PART_D_NOT_IN_LIST: (planName: string | null) =>
    `**${planName ?? 'Your Part D (gap) plan'}** is not in the current Part D list, so that gap selection was cleared.`,
} as const;

export const ANALYSIS_MESSAGES = {
  STEP_PROMPT: "Everything is ready! Let's run your full Medicare cost analysis.",
  COMPLETE_PROMPT:
    "Your Medicare analysis is complete! Review your results. Type 'reset analysis' to start a new one.",
  RUN_BLOCKED_NEED_PLANS: 'Please select your plans first before running cost analysis.',
  RUN_READY:
    'Ready to calculate your lifetime Medicare cost projection. This will analyze your selected plans, drugs, and pharmacy.',
  RUNNING_NOW: 'Running your cost analysis now.',
  SAVE_IN_PROGRESS: (name: string) => `Saving your analysis as "${name}"...`,
  OVERWRITE_PROMPT: (name: string) =>
    `An analysis named "${name}" already exists. Would you like to overwrite it? (yes / no)`,
  SAVE_REQUIRED_COMPLETE:
    'You need a complete analysis before saving — please ensure your profile, drugs, pharmacy, plans, and cost projections are all set.',
  /** Shown on non-plan pages when a bare plan-like input is detected. */
  NAVIGATE_TO_PLANS_HINT:
    'It looks like you may be looking for plans. Navigate to the **Plans** step using the stepper above, or type **"go to plans"**.',
} as const;

export const APP_MESSAGES = {
  LONG_TERM_COMING: 'Long Term Analysis is coming soon! Stay tuned for updates.',
  COMPARE_LONG_TERM_COMING: 'Compare long term analysis is coming soon.',
  GENERIC_CANCELLED: 'Cancelled — no changes made.',
  GENERIC_ERROR: 'Sorry, something went wrong. Please try again.',
  SAVE_CANCELLED: 'Save cancelled.',
  SAVE_ANALYSIS_FAILED: 'Failed to save analysis. Please try again.',
  SAVE_ANALYSIS_SUCCESS: (name: string) => `Analysis "${name}" saved successfully! Starting a fresh analysis.`,
  PROFILE_UPDATED_IN_FORM: 'Profile updated in the form. Review on the left and click Save to persist.',
  PROFILE_DISCARDED: 'Discarded unsaved profile changes.',
  PROFILE_INTENT_HELP:
    'I can do one of two things here: **Continue to Drugs** (save and continue) or **Discard changes**. Say one of those directly.',
  PROFILE_INTENT_UNCLEAR:
    'I could not confirm that request. Please say **Continue to Drugs** or **Discard changes**.',
  REQUEST_TIMEOUT: 'Request timed out. Please check your connection and try again.',
  SIGNING_OUT: 'Signing you out now. Goodbye!',
  OPENING_SAVED_PRESCRIPTIONS: 'Opening your saved prescriptions.',
  OPENING_SAVED_ANALYSES: 'Opening your saved analyses.',
  OPENING_PROFILE_FOR_EDIT: 'Opening your profile for editing.',
} as const;

export const NAV_MESSAGES = {
  REQUIRE_PROFILE_FIRST: 'Please complete your profile first.',
  REQUIRE_DRUG_FIRST: 'Please add at least one drug first.',
  ALREADY_ON_STEP: (step: string) => `You're already on the **${step}** step.`,
  ALREADY_ON_FIRST_STEP: "You're already on the first step (Profile).",
  ALREADY_ON_LAST_STEP:
    "You're on the last step. Select a plan and run cost analysis, or type **run analysis**.",
  NO_RETURN_ROUTE: 'No previous step to return to.',
  NAVIGATING_TO: (step: string) => `Navigating to the **${step}** step.`,
  SAVING_AND_NAVIGATING_TO: (step: string) => `Saving your progress and navigating to **${step}**.`,
  CANNOT_NAVIGATE_MISSING_PREREQS: (missing: string) =>
    `Cannot navigate forward yet — please complete the **${missing}** step first.`,
  RETURNING_TO: (step: string) => `Returning to the **${step}** step where you came from.`,
} as const;

export const LTC_MESSAGES = {
  START_PROFILE:
    "Let's start your Long-Term Care analysis. Please complete your profile information to proceed.",
  PROFILE_REVIEW:
    'Your profile looks good! Click **Continue to Care Type** in the footer to proceed, or edit the form if needed.',
  CARE_TYPE_PROMPT:
    'Now configure your long-term care preferences. Set the quality of care and years for each care type, then click **Run Projection** when ready.',
  CARE_TYPE_UPDATED: 'Care type updated in the form.',
  REQUIRE_PROFILE: 'Please complete your profile first before running a projection.',
  REQUIRE_CARE_TYPE: 'Please configure your care type first before running a projection.',
  LAST_STEP:
    "You're on the last step. Configure your care preferences and click **Run Projection**, or say **run projection** in the chat.",
  PROJECTION_RUNNING: 'Running your long-term care projection…',
  PROJECTION_COMPLETE: 'Projection complete! Navigating to your results.',
  PROJECTION_FAILED: 'Failed to run projection. Please try again.',
  RESUME_PROFILE: 'Resumed the Long-Term Care analysis on the Profile step. Review your details, then click **Continue to Care Type** in the footer.',
  RESUME_CARE_TYPE: 'Resumed your Long-Term Care analysis on the Care Type step. Configure your care preferences and click **Run Projection** when ready.',
  RESUME_PROJECTION: 'Resumed your Long-Term Care projection results.',
} as const;
