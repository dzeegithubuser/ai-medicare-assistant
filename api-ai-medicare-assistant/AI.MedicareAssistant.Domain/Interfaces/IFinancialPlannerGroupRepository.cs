using Domain.Documents;

namespace Domain.Interfaces;

public interface IFinancialPlannerGroupRepository
{
    Task<FinancialPlannerGroupDocument?> GetByIdAsync(Guid groupId);
    Task<List<FinancialPlannerGroupDocument>> GetAllAsync();
    Task<FinancialPlannerGroupDocument> CreateAsync(FinancialPlannerGroupDocument doc);
    Task UpdateAsync(FinancialPlannerGroupDocument doc);
    Task DeleteAsync(Guid groupId);
    Task<bool> ExistsByNameAsync(string name);
}
