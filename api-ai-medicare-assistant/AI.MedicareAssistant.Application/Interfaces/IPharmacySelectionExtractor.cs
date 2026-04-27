using Application.DTOs;

namespace Application.Interfaces;

public interface IPharmacySelectionExtractor
{
    Task<PharmacySelectionExtractResponse> ExtractAsync(PharmacySelectionExtractRequest request, CancellationToken cancellationToken = default);
}
