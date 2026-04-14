using Domain.Models;

namespace Domain.Interfaces;

public interface IMedicareCostService
{
    Task<MedicareCostEstimate?> GetCostEstimate(string drugName);
}
