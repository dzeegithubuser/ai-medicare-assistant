using Application.DTOs;

namespace Application.Interfaces;

public interface IDrugSelectionExtractor
{
    Task<DrugSelectionExtractResponse> ExtractAsync(DrugSelectionExtractRequest request, CancellationToken cancellationToken = default);
}
