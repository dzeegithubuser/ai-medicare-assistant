using Domain.Models;

namespace Domain.Interfaces;

/// <summary>
/// Queries CMS open data (SOCRATA) for real Medicare plan details:
/// premiums, deductibles, star ratings, and formulary tier coverage.
/// Used to enrich AI-generated plan recommendations with verified CMS data.
/// </summary>
public interface ICmsPlanDataService
{
    /// <summary>
    /// Looks up real plan data from CMS for a given state/county area.
    /// Returns plans that can be matched against AI-generated plan recommendations.
    /// </summary>
    Task<List<CmsPlanInfo>> GetPlansForAreaAsync(
        string stateCode,
        string countyName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up formulary coverage for specific drugs under a given plan contract.
    /// </summary>
    Task<List<CmsFormularyEntry>> GetFormularyEntriesAsync(
        string contractId,
        IEnumerable<string> rxCuis,
        CancellationToken cancellationToken = default);
}
