namespace Infrastructure.AI.Extractors;

/// <summary>
/// Builds a page-context block that is appended to the static system prompt at runtime.
/// This tells the LLM which wizard step the user is on so it can disambiguate messages
/// that have different meanings depending on context
/// (e.g. "80113" alone on drugs page vs profile page).
/// </summary>
internal static class PageContextBuilder
{
    public static string Build(string? currentPage)
    {
        if (string.IsNullOrWhiteSpace(currentPage)) return string.Empty;

        var (pageName, guidance) = currentPage switch
        {
            var p when p.StartsWith("/analysis/profile") => (
                "Profile step",
                """
                The user is viewing or editing their Medicare profile.
                - Any field value explicitly mentioned (zip code, date of birth, name, gender, health status, tobacco, tax filing, address) → NAVIGATE_PROFILE with full parameter extraction.
                - Treat any numeric, date, or demographic value as a profile field update.
                """
            ),
            var p when p.StartsWith("/analysis/fp-drugs") => (
                "Drugs step",
                """
                The user is adding or confirming medications in their Medicare drug list.
                - Drug names (e.g. Metformin, Eliquis, Lisinopril, aspirin) → DRUG_INPUT (highest priority on this page).
                - EXPLICIT profile field change (user says "change my zip to X", "update my address", "I'm a non-smoker", "my DOB is ...", etc.) → NAVIGATE_PROFILE with parameter extraction. The trigger is a recognized profile field keyword (zip, address, gender, date of birth, tobacco, health, tax, filing status, etc.).
                - A BARE number or short string with NO profile field trigger word (e.g. just "80113" alone) → UNKNOWN; do not assume it is a zip code update.
                - Forward navigation ("next", "continue to pharmacies", "find pharmacy") → NAVIGATE_PHARMACIES.
                """
            ),
            var p when p.StartsWith("/analysis/pharmacies") => (
                "Pharmacy Finder step",
                """
                The user is searching for and selecting a pharmacy.
                - Pharmacy names (Walgreens, CVS, Rite Aid, etc.) are NOT drug names — do not classify as DRUG_INPUT.
                - "next", "continue", "go to plans" → NAVIGATE_PLANS.
                - Drug names mentioned here should be classified as DRUG_INPUT. The frontend will guide the user.
                """
            ),
            var p when p.StartsWith("/analysis/plans") => (
                "Plans step",
                """
                The user is reviewing Medicare plan recommendations (Part D, Medigap, MA).
                - Plan names or types (Part D, Medigap, Medicare Advantage, MA) → plan-related intents (SWITCH_TO_PDP, SWITCH_TO_MA, NAVIGATE_PLANS).
                - "run analysis", "calculate costs" → ACTION_RUN_ANALYSIS.
                """
            ),
            var p when p.StartsWith("/analysis/cost-projections") => (
                "Cost Projections page",
                """
                The user is viewing their lifetime Medicare cost analysis results.
                - Requests to save, name, or export → ACTION_SAVE_ANALYSIS.
                - To navigate to other steps, the user should use the stepper or type explicit navigation commands.
                """
            ),
            var p when p.StartsWith("/saved") => (
                "Saved Analyses page",
                """
                The user is browsing their previously saved Medicare analyses.
                - References to a saved analysis name → ACTION_LOAD_PRESCRIPTIONS or NAVIGATE_SAVED_ANALYSES.
                - "start new" → ACTION_RESET_ANALYSIS.
                """
            ),
            var p when p.StartsWith("/long-term-care/profile") => (
                "LTC Profile step",
                """
                The user is viewing or editing their profile for a Long-Term Care analysis.
                - Profile field changes → NAVIGATE_PROFILE with parameter extraction (same as Medicare profile).
                - "next", "continue", "go to care type" → NAVIGATE_LTC_CARE_TYPE.
                """
            ),
            var p when p.StartsWith("/long-term-care/care-type") => (
                "LTC Care Type step",
                """
                The user is configuring their Long-Term Care preferences (quality of care, years for adult day, home care, nursing care).
                - Care-type values ("set nursing to 5 years", "quality best", "home care 3", "adult day 2 years") → LTC_CARE_INPUT with parameter extraction: ltcHealthProfile (1-5), ltcAdultDayYears (0-20), ltcHomeCareYears (0-20), ltcNursingCareYears (0-20).
                - "run projection", "calculate ltc", "show projection", "project my costs" → ACTION_RUN_LTC_PROJECTION.
                - "go back", "profile" → NAVIGATE_PROFILE.
                """
            ),
            _ => (
                "Dashboard / Home",
                "The user is on the home or dashboard page. No specific wizard step is active."
            )
        };

        return $"""

            --- ACTIVE PAGE CONTEXT ---
            Current page: {pageName} ({currentPage})
            Classification guidance for this page:
            {guidance.Trim()}
            --- END PAGE CONTEXT ---
            """;
    }
}
