using Domain.Models;

namespace Domain.Interfaces;

public interface IFinancialPlannerDrugService
{
    Task<BulkDrugSearchResponse> SearchBulkAsync(List<string> drugNames, CancellationToken cancellationToken = default);
}
