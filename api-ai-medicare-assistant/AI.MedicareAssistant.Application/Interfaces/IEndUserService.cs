using Application.DTOs;

namespace Application.Interfaces;

public interface IEndUserService
{
    Task<EndUserSummaryDto> CreateAsync(Guid fpUserId, CreateEndUserRequest request);
}
