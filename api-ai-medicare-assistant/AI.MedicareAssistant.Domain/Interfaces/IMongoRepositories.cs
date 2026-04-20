using Domain.Documents;

namespace Domain.Interfaces;

/// <summary>Per-user current FP pharmacy + plan selections (collection <c>userAnalysisSelections</c>).</summary>
public interface IUserAnalysisSelectionsRepository
{
    Task<UserAnalysisSelectionsDocument> ReplaceCurrentForUserAsync(UserAnalysisSelectionsDocument document);
    Task<UserAnalysisSelectionsDocument?> GetCurrentForUserAsync(Guid userId);
    Task<UserAnalysisSelectionsDocument?> GetByIdAsync(string id);

    /// <summary>Replaces only the drugs array. Pharmacies and plans are untouched.</summary>
    Task UpdateDrugsAsync(Guid userId, List<PrescriptionDrugDoc> drugs);

    /// <summary>Replaces only the pharmacies array. Drugs and plans are untouched.</summary>
    Task UpdatePharmaciesAsync(Guid userId, List<UserAnalysisPharmacyDoc> pharmacies);

    /// <summary>Replaces only the plans array and fpActiveSection. Drugs and pharmacies are untouched.</summary>
    Task UpdatePlansAsync(Guid userId, List<UserAnalysisPlanDoc> plans, string? fpActiveSection);
}

public interface IRecommendationRepository
{
    Task<RecommendationDocument?> GetByUserIdAsync(Guid userId);
    Task<RecommendationDocument?> GetByIdAsync(string id, Guid userId);
    Task<List<RecommendationDocument>> GetAllByUserIdAsync(Guid userId);
    Task<RecommendationDocument> CreateAsync(RecommendationDocument document);
    Task ReplaceAsync(RecommendationDocument document);
    Task<bool> ExistsByUserIdAsync(Guid userId);
    Task<bool> ExistsByNameAsync(Guid userId, string name);
    Task DeleteByUserIdAsync(Guid userId);
}

public interface IChatSessionRepository
{
    Task<ChatSessionDocument?> GetByUserIdAsync(Guid userId);
    Task UpsertAsync(ChatSessionDocument document);
    Task DeleteByUserIdAsync(Guid userId);
}

public interface ILtcSelectionsRepository
{
    Task UpsertCurrentAsync(LtcCurrentSelectionsDocument document);
    Task<LtcCurrentSelectionsDocument?> GetCurrentAsync(Guid userId);
}
