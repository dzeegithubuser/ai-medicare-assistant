using Domain.Documents;
using Domain.Exceptions;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class RecommendationService
{
    private readonly IRecommendationRepository _repo;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(IRecommendationRepository repo, ILogger<RecommendationService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<RecommendationDocument?> GetActiveAsync(Guid userId)
    {
        return await _repo.GetByUserIdAsync(userId);
    }

    public async Task<RecommendationDocument?> GetByIdAsync(string id, Guid userId)
    {
        return await _repo.GetByIdAsync(id, userId);
    }

    public async Task<List<RecommendationDocument>> GetAllAsync(Guid userId)
    {
        return await _repo.GetAllByUserIdAsync(userId);
    }

    public async Task<bool> ExistsAsync(Guid userId)
    {
        return await _repo.ExistsByUserIdAsync(userId);
    }

    public async Task<RecommendationDocument> CreateAsync(Guid userId, RecommendationDocument document, bool force = false)
    {
        if (force)
        {
            _logger.LogInformation("Force-replacing existing recommendation for user {UserId}", userId);
            await _repo.DeleteByUserIdAsync(userId);
        }
        else if (await _repo.ExistsByNameAsync(userId, document.Name))
        {
            throw new ConflictException($"A recommendation named '{document.Name}' already exists.");
        }

        document.UserId = userId;
        document.Status = "active";
        document.CreatedAt = DateTime.UtcNow;
        document.UpdatedAt = DateTime.UtcNow;

        var created = await _repo.CreateAsync(document);
        _logger.LogInformation("Created recommendation '{Name}' for user {UserId}", document.Name, userId);
        return created;
    }

    public async Task<RecommendationDocument> UpdateProfileAsync(Guid userId, ProfileSnapshot profile)
    {
        var doc = await GetRequiredAsync(userId);
        doc.Profile = profile;
        await _repo.ReplaceAsync(doc);
        _logger.LogInformation("Updated profile for recommendation of user {UserId}", userId);
        return doc;
    }

    public async Task<RecommendationDocument> UpdateDrugsAsync(Guid userId, List<SelectedDrugDoc> drugs)
    {
        var doc = await GetRequiredAsync(userId);
        doc.DrugList = drugs;
        await _repo.ReplaceAsync(doc);
        _logger.LogInformation("Updated drugs ({Count}) for recommendation of user {UserId}", drugs.Count, userId);
        return doc;
    }

    public async Task<RecommendationDocument> UpdatePharmacyAsync(Guid userId, List<SelectedPharmacyDoc> pharmacies, MailOrderPharmacyDoc? mailOrder = null)
    {
        var doc = await GetRequiredAsync(userId);
        doc.Pharmacies = pharmacies;
        if (mailOrder is not null)
            doc.MailOrderPharmacy = mailOrder;
        await _repo.ReplaceAsync(doc);
        _logger.LogInformation("Updated pharmacies ({Count}) for recommendation of user {UserId}", pharmacies.Count, userId);
        return doc;
    }

    public async Task<RecommendationDocument> UpdatePlansAsync(Guid userId, List<SelectedPlanDoc> plans)
    {
        var doc = await GetRequiredAsync(userId);
        doc.PlanSelections = plans;
        await _repo.ReplaceAsync(doc);
        _logger.LogInformation("Updated plans ({Count}) for recommendation of user {UserId}", plans.Count, userId);
        return doc;
    }

    public async Task<RecommendationDocument> UpdateCostSnapshotAsync(Guid userId, CostSnapshotDoc snapshot)
    {
        var doc = await GetRequiredAsync(userId);
        doc.LastCostSnapshot = snapshot;
        await _repo.ReplaceAsync(doc);
        _logger.LogInformation("Updated cost snapshot for recommendation of user {UserId}", userId);
        return doc;
    }

    public async Task DeleteAsync(Guid userId)
    {
        var doc = await GetRequiredAsync(userId);
        await _repo.DeleteByUserIdAsync(userId);
        _logger.LogInformation("Deleted recommendation '{Name}' for user {UserId}", doc.Name, userId);
    }

    private async Task<RecommendationDocument> GetRequiredAsync(Guid userId)
    {
        var doc = await _repo.GetByUserIdAsync(userId);
        return doc ?? throw new NotFoundException("Recommendation", userId);
    }
}
