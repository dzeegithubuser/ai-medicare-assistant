using Application.DTOs;

namespace Application.Interfaces;

public interface IProfileExtractor
{
    Task<ProfileExtractResponse> ExtractAsync(ProfileExtractRequest request, CancellationToken cancellationToken = default);
}
