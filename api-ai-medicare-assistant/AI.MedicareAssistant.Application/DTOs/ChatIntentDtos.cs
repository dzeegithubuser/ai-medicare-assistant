namespace Application.DTOs;

public class ChatIntentRequest
{
    /// <summary>The raw user message from the chat input.</summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Whether the user's profile is already complete.
    /// Used by AI to decide if NAVIGATE_PROFILE should warn about incomplete profile.
    /// </summary>
    public bool IsProfileComplete { get; set; }

    /// <summary>Relative URL of the Angular page where the user sent the message (e.g. /analysis/fp-drugs).</summary>
    public string? CurrentPage { get; set; }
}

public class ChatIntentResponse
{
    /// <summary>
    /// One of: NAVIGATE_PROFILE, NAVIGATE_ANALYSIS_DRUGS, NAVIGATE_PHARMACIES,
    /// NAVIGATE_PLANS, NAVIGATE_COST_PROJECTIONS, SWITCH_TO_PDP, SWITCH_TO_MA,
    /// ACTION_RESET_ANALYSIS, ACTION_SIGN_OUT,
    /// ACTION_LOAD_PRESCRIPTIONS, DRUG_INPUT,
    /// NAVIGATE_LTC_CARE_TYPE, LTC_CARE_INPUT, ACTION_RUN_LTC_PROJECTION,
    /// UNKNOWN
    /// </summary>
    public string Intent { get; set; } = "UNKNOWN";

    /// <summary>Optional parameters extracted from the message.</summary>
    public ChatIntentParams? Params { get; set; }

    /// <summary>Human-readable confirmation message to display in chat.</summary>
    public string ConfirmationMessage { get; set; } = "";
}

public class ChatIntentParams
{
    /// <summary>Extracted first name (for NAVIGATE_PROFILE with name change request).</summary>
    public string? FirstName { get; set; }

    /// <summary>Extracted last name (for NAVIGATE_PROFILE with name change request).</summary>
    public string? LastName { get; set; }

    /// <summary>Extracted prescription name (legacy field, may be null).</summary>
    public string? PrescriptionName { get; set; }

    // Extended profile field extraction (for NAVIGATE_PROFILE with field-specific updates)
    public string? Gender { get; set; }
    public string? DateOfBirth { get; set; }
    public int? TobaccoStatus { get; set; }
    public int? HealthCondition { get; set; }
    public string? TaxFilingStatus { get; set; }
    public int? CoverageYear { get; set; }
    public string? ZipCode { get; set; }
    public string? AddressLine1 { get; set; }
    public int? LifeExpectancy { get; set; }

    // LTC care-type field extraction (for LTC_CARE_INPUT)
    public int? LtcHealthProfile { get; set; }
    public int? LtcAdultDayYears { get; set; }
    public int? LtcHomeCareYears { get; set; }
    public int? LtcNursingCareYears { get; set; }
}
