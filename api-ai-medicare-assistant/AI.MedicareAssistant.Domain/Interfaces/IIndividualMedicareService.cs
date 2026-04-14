using Domain.Models;

namespace Domain.Interfaces;

public interface IIndividualMedicareService
{
    /// <summary>
    /// Calls the Financial Planner individualMedicareR5 API to compute
    /// lifetime Medicare cost projections for the user's selected plan.
    /// </summary>
    Task<IndividualMedicareResponse> CalculateAsync(
        IndividualMedicareRequest request,
        CancellationToken cancellationToken = default);
}
