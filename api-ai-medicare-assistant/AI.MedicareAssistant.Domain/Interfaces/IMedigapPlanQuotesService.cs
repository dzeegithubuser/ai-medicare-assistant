using Domain.Models;

namespace Domain.Interfaces;

public interface IMedigapPlanQuotesService
{
    Task<MedigapPlanQuotesResponse> GetQuotesAsync(
        MedigapPlanQuotesRequest request,
        CancellationToken cancellationToken = default);
}
