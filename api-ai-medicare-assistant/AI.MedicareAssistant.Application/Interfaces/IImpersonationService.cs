using Application.DTOs;

namespace Application.Interfaces;

public interface IImpersonationService
{
    /// <summary>Issues a new 60-min impersonation token for an FP acting as one of their end-users.</summary>
    Task<ImpersonationResponse> ImpersonateAsync(Guid fpUserId, Guid targetUserId);
}
