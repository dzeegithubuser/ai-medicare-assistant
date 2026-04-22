using Domain.Documents;
using Domain.Interfaces;
using Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Repositories;

public class UserAnalysisSelectionsRepository : IUserAnalysisSelectionsRepository
{
    private readonly MongoDbContext _context;

    public UserAnalysisSelectionsRepository(MongoDbContext context)
    {
        _context = context;
    }

    public const string CurrentSelectionsName = "__current_analysis_selections__";

    public async Task<UserAnalysisSelectionsDocument> ReplaceCurrentForUserAsync(UserAnalysisSelectionsDocument document)
    {
        document.Name = CurrentSelectionsName;
        document.UpdatedAt = DateTime.UtcNow;
        if (document.CreatedAt == default)
            document.CreatedAt = DateTime.UtcNow;

        var filter = Builders<UserAnalysisSelectionsDocument>.Filter.And(
            Builders<UserAnalysisSelectionsDocument>.Filter.Eq(d => d.UserId, document.UserId),
            Builders<UserAnalysisSelectionsDocument>.Filter.Eq(d => d.Name, CurrentSelectionsName));

        var existingId = await _context.UserAnalysisSelections
            .Find(filter)
            .Project(d => d.Id)
            .FirstOrDefaultAsync();
        if (existingId != null)
            document.Id = existingId;

        await _context.UserAnalysisSelections.ReplaceOneAsync(filter, document,
            new ReplaceOptions { IsUpsert = true });

        return document;
    }

    public async Task<UserAnalysisSelectionsDocument?> GetCurrentForUserAsync(Guid userId)
    {
        return await _context.UserAnalysisSelections
            .Find(d => d.UserId == userId && d.Name == CurrentSelectionsName)
            .FirstOrDefaultAsync();
    }

    public async Task<UserAnalysisSelectionsDocument?> GetByIdAsync(string id)
    {
        return await _context.UserAnalysisSelections
            .Find(d => d.Id == id)
            .FirstOrDefaultAsync();
    }

    private FilterDefinition<UserAnalysisSelectionsDocument> CurrentFilter(Guid userId) =>
        Builders<UserAnalysisSelectionsDocument>.Filter.And(
            Builders<UserAnalysisSelectionsDocument>.Filter.Eq(d => d.UserId, userId),
            Builders<UserAnalysisSelectionsDocument>.Filter.Eq(d => d.Name, CurrentSelectionsName));

    public async Task UpdateDrugsAsync(Guid userId, List<PrescriptionDrugDoc> drugs)
    {
        var update = Builders<UserAnalysisSelectionsDocument>.Update
            .Set(d => d.Drugs, drugs)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);
        await _context.UserAnalysisSelections.UpdateOneAsync(
            CurrentFilter(userId), update, new UpdateOptions { IsUpsert = false });
    }

    public async Task UpdatePharmaciesAsync(Guid userId, List<UserAnalysisPharmacyDoc> pharmacies)
    {
        var update = Builders<UserAnalysisSelectionsDocument>.Update
            .Set(d => d.SelectedPharmacies, pharmacies)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);
        await _context.UserAnalysisSelections.UpdateOneAsync(
            CurrentFilter(userId), update, new UpdateOptions { IsUpsert = false });
    }

    public async Task UpdatePlansAsync(Guid userId, List<UserAnalysisPlanDoc> plans, string? fpActiveSection)
    {
        var update = Builders<UserAnalysisSelectionsDocument>.Update
            .Set(d => d.SelectedPlans, plans)
            .Set(d => d.FpActiveSection, fpActiveSection)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);
        await _context.UserAnalysisSelections.UpdateOneAsync(
            CurrentFilter(userId), update, new UpdateOptions { IsUpsert = false });
    }
}

public class LtcSelectionsRepository : ILtcSelectionsRepository
{
    private readonly MongoDbContext _context;
    public const string CurrentSelectionsName = "__current_ltc_selections__";

    public LtcSelectionsRepository(MongoDbContext context)
    {
        _context = context;
    }

    private FilterDefinition<LtcCurrentSelectionsDocument> CurrentFilter(Guid userId) =>
        Builders<LtcCurrentSelectionsDocument>.Filter.And(
            Builders<LtcCurrentSelectionsDocument>.Filter.Eq(d => d.UserId, userId),
            Builders<LtcCurrentSelectionsDocument>.Filter.Eq(d => d.Name, CurrentSelectionsName));

    public async Task UpsertCurrentAsync(LtcCurrentSelectionsDocument document)
    {
        document.Name = CurrentSelectionsName;
        document.UpdatedAt = DateTime.UtcNow;
        if (document.CreatedAt == default)
            document.CreatedAt = DateTime.UtcNow;

        var filter = CurrentFilter(document.UserId);

        var existingId = await _context.LtcCurrentSelections
            .Find(filter)
            .Project(d => d.Id)
            .FirstOrDefaultAsync();
        if (existingId != null)
            document.Id = existingId;

        await _context.LtcCurrentSelections.ReplaceOneAsync(filter, document,
            new ReplaceOptions { IsUpsert = true });
    }

    public async Task<LtcCurrentSelectionsDocument?> GetCurrentAsync(Guid userId)
    {
        return await _context.LtcCurrentSelections
            .Find(CurrentFilter(userId))
            .FirstOrDefaultAsync();
    }
}
