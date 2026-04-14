using Domain.Models;

namespace Domain.Interfaces;

public interface IPresentValueService
{
    Task<PresentValueResponse> CalculateAsync(
        PresentValueRequest request,
        CancellationToken cancellationToken = default);
}
