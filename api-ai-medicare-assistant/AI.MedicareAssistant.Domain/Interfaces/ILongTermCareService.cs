using Domain.Models;

namespace Domain.Interfaces;

public interface ILongTermCareService
{
    Task<LongTermCareResponse> GetProjectionAsync(LongTermCareRequest request, string userEmail, CancellationToken cancellationToken = default);
}
