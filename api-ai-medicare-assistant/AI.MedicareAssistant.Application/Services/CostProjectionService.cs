using Domain.Interfaces;
using Domain.Models;
using Application.DTOs;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class CostProjectionService
{
    private readonly ProfileService _profileService;
    private readonly IIndividualMedicareService _individualMedicareService;
    private readonly ICostEvaluationAiService _costEvaluationAi;
    private readonly IPresentValueService _presentValueService;
    private readonly ILogger<CostProjectionService> _logger;

    public CostProjectionService(
        ProfileService profileService,
        IIndividualMedicareService individualMedicareService,
        ICostEvaluationAiService costEvaluationAi,
        IPresentValueService presentValueService,
        ILogger<CostProjectionService> logger)
    {
        _profileService = profileService;
        _individualMedicareService = individualMedicareService;
        _costEvaluationAi = costEvaluationAi;
        _presentValueService = presentValueService;
        _logger = logger;
    }

    /// <summary>
    /// Calculate costs then run AI evaluation to produce chart-ready projections.
    /// </summary>
    public async Task<CostProjectionResult> EvaluateCostsAsync(
        Guid userId, string userEmail, CostCalculationInput input,
        CancellationToken cancellationToken = default)
    {
        var (request, profile, stateName) = await BuildMedicareRequestWithProfileAsync(
            userId, userEmail, input, cancellationToken);

        // Step 1: Financial Planner calculation
        var calcResult = await _individualMedicareService.CalculateAsync(request, cancellationToken);

        // Step 2: AI evaluation
        var evaluation = await _costEvaluationAi.EvaluateAsync(
            calcResult,
            input.PlanRecommendName,
            input.PlanBundleCode,
            profile.CoverageYear,
            profile.LifeExpectancy,
            profile.TaxFilingStatus,
            stateName,
            calcResult.SupplementPlanType ?? "",
            calcResult.SupplementPlanPremium,
            cancellationToken);

        var result = new CostProjectionResult
        {
            YearlyDetails = calcResult.IndividualMedicares,
            LifetimeTotals = new LifetimeTotals
            {
                LifeTimeABMedicareAdvantageExpenses = calcResult.LifeTimeABMedicareAdvantageExpenses,
                LifeTimeABMedicareAdvantagePremium = calcResult.LifeTimeABMedicareAdvantagePremium,
                LifeTimeABMedicareAdvantageOop = calcResult.LifeTimeABMedicareAdvantageOop,
                LifeTimeDSurcharge = calcResult.LifeTimeDSurcharge,
                LifeTimeBSurcharge = calcResult.LifeTimeBSurcharge,
                TotalIrmaa = calcResult.LifeTimeBSurcharge + calcResult.LifeTimeDSurcharge,
                LifeTimeConciergePremium = calcResult.LifeTimeConciergePremium,
                SupplementPlanType = calcResult.SupplementPlanType ?? "",
                SupplementPlanPremium = calcResult.SupplementPlanPremium,
                ConciergeIncluded = calcResult.ConciergeIncluded,
                LifeTimeABGDExpenses = calcResult.LifeTimeABGDExpenses,
                LifeTimeABGDPremium = calcResult.LifeTimeABGDPremium,
                LifeTimeABGDOop = calcResult.LifeTimeABGDOop,
                LifeTimeABFDExpenses = calcResult.LifeTimeABFDExpenses,
                LifeTimeABFDPremium = calcResult.LifeTimeABFDPremium,
                LifeTimeABFDOop = calcResult.LifeTimeABFDOop,
                LifeTimeABNDExpenses = calcResult.LifeTimeABNDExpenses,
                LifeTimeABNDPremium = calcResult.LifeTimeABNDPremium,
                LifeTimeABNDOop = calcResult.LifeTimeABNDOop,
                LifeTimeABCDExpenses = calcResult.LifeTimeABCDExpenses,
                LifeTimeABCDPremium = calcResult.LifeTimeABCDPremium,
                LifeTimeABCDOop = calcResult.LifeTimeABCDOop
            },
            Evaluation = evaluation
        };

        // Step 3: Present Value calculation
        try
        {
            var years = calcResult.IndividualMedicares;
            if (years.Count > 0)
            {
                var pvRequest = new PresentValueRequest
                {
                    FromYear = years[0].Year,
                    ToYear = years[^1].Year,
                    Expenses = years.Select(y => new YearExpense
                    {
                        Year = y.Year,
                        Expense = y.TotalABGD + y.TotalABFD + y.TotalABND + y.TotalABCD + y.TotalABMedicareAdvantage
                    }).ToList(),
                    PresentValueYears = new PresentValueYears { PvAsOnYear1 = profile.CoverageYear },
                    Discount = 6,
                    RateOfReturns = new RateOfReturns { RateOfReturn1 = 0 }
                };

                var pvResponse = await _presentValueService.CalculateAsync(pvRequest, cancellationToken);
                if (pvResponse.PvList.Count > 0)
                    result.PresentValue = pvResponse.PvList[0].PresentValue;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Present value calculation failed; continuing without PV");
        }

        return result;
    }

    private async Task<(IndividualMedicareRequest Request, ProfileDto Profile, string StateName)>
        BuildMedicareRequestWithProfileAsync(
            Guid userId, string userEmail, CostCalculationInput input,
            CancellationToken cancellationToken)
    {
        var profileResponse = await _profileService.GetProfileAsync(userId);
        var p = profileResponse.Profile
            ?? throw new InvalidOperationException("Please complete your profile before calculating costs.");

        if (string.IsNullOrWhiteSpace(p.State) || string.IsNullOrWhiteSpace(p.ZipCode))
            throw new InvalidOperationException("Profile address is required for cost calculation.");

        if (!StateCodeToName.TryGetValue(p.State, out var stateName))
            stateName = p.State;

        // Format DOB as MM-yyyy
        var birthDate = "";
        if (!string.IsNullOrWhiteSpace(p.DateOfBirth) &&
            DateTime.TryParse(p.DateOfBirth, out var dob))
        {
            birthDate = dob.ToString("MM-yyyy");
        }

        _ = int.TryParse(p.MagiTier, out var magiTierInt);

        // Remaining months in coverage year
        var remainingMonths = 12;
        if (p.CoverageYear == DateTime.UtcNow.Year)
        {
            remainingMonths = 12 - DateTime.UtcNow.Month + 1;
            if (remainingMonths < 1) remainingMonths = 1;
        }

        var request = new IndividualMedicareRequest
        {
            UserEmail = userEmail,
            BirthDate = birthDate,
            RetirementYear = p.CoverageYear.ToString(),
            LifeExpectancy = p.LifeExpectancy,
            HealthGrade = p.HealthCondition,
            StateName = stateName,
            Zipcode = p.ZipCode,
            RetirementState = stateName,
            RetirementZipcode = p.ZipCode,
            BoughtPlanA = input.BoughtPlanA,
            ReserveDaysUsed = input.ReserveDaysUsed,
            TaxFilingStatus = p.TaxFilingStatus,
            Tobacco = p.TobaccoStatus,
            MagiTier = magiTierInt,
            CoverageYear = p.CoverageYear.ToString(),
            ConciergeIncluded = p.Concierge == 1,
            ConciergePremium = p.ConciergeAmount ?? 0m,
            PlanBundleCode = input.PlanBundleCode,
            MedicareAdvantagePremium = input.MedicareAdvantagePremium,
            MaWithPrescriptionBenefit = input.MaWithPrescriptionBenefit,
            PartDOOP = input.PartDOOP,
            PartDOOPFullYear = input.PartDOOPFullYear,
            PartABenefitServiceCost = input.PartABenefitServiceCost,
            PartBBenefitServiceCost = input.PartBBenefitServiceCost,
            CalculateForAdjustedMonth = input.CalculateForAdjustedMonth > 0 ? input.CalculateForAdjustedMonth : remainingMonths,
            PlanRecommendName = input.PlanRecommendName,
            PlanRecommendEmail = userEmail,
            RecommendationListId = input.RecommendationListId,
            PartDDataProvided = input.PartDDataProvided,
            MedicareAdvantageDataProvided = input.MedicareAdvantageDataProvided,
            SupplementPlanDataProvided = input.SupplementDataProvided,
            SupplementPlanType = input.SupplementPlanType,
            Dental = input.Dental,
            DentalHealthGrade = input.DentalHealthGrade,
            PartDPremium = input.PartDPremium
        };

        return (request, p, stateName);
    }

    private static readonly Dictionary<string, string> StateCodeToName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AL"] = "Alabama", ["AK"] = "Alaska", ["AZ"] = "Arizona", ["AR"] = "Arkansas",
        ["CA"] = "California", ["CO"] = "Colorado", ["CT"] = "Connecticut", ["DE"] = "Delaware",
        ["FL"] = "Florida", ["GA"] = "Georgia", ["HI"] = "Hawaii", ["ID"] = "Idaho",
        ["IL"] = "Illinois", ["IN"] = "Indiana", ["IA"] = "Iowa", ["KS"] = "Kansas",
        ["KY"] = "Kentucky", ["LA"] = "Louisiana", ["ME"] = "Maine", ["MD"] = "Maryland",
        ["MA"] = "Massachusetts", ["MI"] = "Michigan", ["MN"] = "Minnesota", ["MS"] = "Mississippi",
        ["MO"] = "Missouri", ["MT"] = "Montana", ["NE"] = "Nebraska", ["NV"] = "Nevada",
        ["NH"] = "New Hampshire", ["NJ"] = "New Jersey", ["NM"] = "New Mexico", ["NY"] = "New York",
        ["NC"] = "North Carolina", ["ND"] = "North Dakota", ["OH"] = "Ohio", ["OK"] = "Oklahoma",
        ["OR"] = "Oregon", ["PA"] = "Pennsylvania", ["RI"] = "Rhode Island", ["SC"] = "South Carolina",
        ["SD"] = "South Dakota", ["TN"] = "Tennessee", ["TX"] = "Texas", ["UT"] = "Utah",
        ["VT"] = "Vermont", ["VA"] = "Virginia", ["WA"] = "Washington", ["WV"] = "West Virginia",
        ["WI"] = "Wisconsin", ["WY"] = "Wyoming", ["DC"] = "District of Columbia"
    };
}

/// <summary>
/// Input data from the controller DTO — no HTTP concerns, just values.
/// </summary>
public class CostCalculationInput
{
    public string PlanBundleCode { get; set; } = "";
    public decimal MedicareAdvantagePremium { get; set; }
    public bool MaWithPrescriptionBenefit { get; set; }
    public decimal PartDOOP { get; set; }
    public decimal PartDOOPFullYear { get; set; }
    public decimal PartABenefitServiceCost { get; set; }
    public decimal PartBBenefitServiceCost { get; set; }
    public string PlanRecommendName { get; set; } = "";
    public string RecommendationListId { get; set; } = "";
    public bool SupplementDataProvided { get; set; }
    public bool PartDDataProvided { get; set; }
    public int ReserveDaysUsed { get; set; }
    public bool Dental { get; set; }
    public int DentalHealthGrade { get; set; }
    public bool BoughtPlanA { get; set; }
    public bool MedicareAdvantageDataProvided { get; set; }
    public decimal PartDPremium { get; set; }
    public int CalculateForAdjustedMonth { get; set; }
    public string SupplementPlanType { get; set; } = "";
}
