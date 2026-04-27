using Application.DTOs;

namespace Application.Interfaces;

public interface IPlanSelectionExtractor
{
    Task<PlanSelectionExtractResponse> ExtractAsync(PlanSelectionExtractRequest request, CancellationToken cancellationToken = default);
}
