using Domain.Documents;
using Domain.Interfaces;
using Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Repositories;

public class PrescriptionDocRepository : IPrescriptionDocRepository
{
    private readonly MongoDbContext _context;

    public PrescriptionDocRepository(MongoDbContext context)
    {
        _context = context;
    }

    public const string CurrentPrescriptionName = "__current_prescriptions__";

    public async Task<PrescriptionDocument> SaveAsync(PrescriptionDocument document)
    {
        await _context.Prescriptions.InsertOneAsync(document);
        return document;
    }

    public async Task<PrescriptionDocument> ReplaceCurrentForUserAsync(PrescriptionDocument document)
    {
        document.Name = CurrentPrescriptionName;
        document.UpdatedAt = DateTime.UtcNow;
        if (document.CreatedAt == default)
            document.CreatedAt = DateTime.UtcNow;

        var filter = Builders<PrescriptionDocument>.Filter.And(
            Builders<PrescriptionDocument>.Filter.Eq(d => d.UserId, document.UserId),
            Builders<PrescriptionDocument>.Filter.Eq(d => d.Name, CurrentPrescriptionName));

        await _context.Prescriptions.ReplaceOneAsync(filter, document,
            new ReplaceOptions { IsUpsert = true });
        return document;
    }

    public async Task DeleteCurrentPrescriptionForUserAsync(Guid userId)
    {
        await _context.Prescriptions.DeleteManyAsync(d =>
            d.UserId == userId && d.Name == CurrentPrescriptionName);
    }

    public async Task<PrescriptionDocument?> GetByIdAsync(string id)
    {
        return await _context.Prescriptions
            .Find(d => d.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<List<PrescriptionDocument>> GetByUserIdAsync(Guid userId)
    {
        return await _context.Prescriptions
            .Find(d => d.UserId == userId)
            .SortByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task DeleteAsync(string id)
    {
        await _context.Prescriptions.DeleteOneAsync(d => d.Id == id);
    }
}

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

        if (string.IsNullOrEmpty(document.Id))
        {
            var existing = await _context.UserAnalysisSelections
                .Find(filter)
                .Project(d => d.Id)
                .FirstOrDefaultAsync();
            document.Id = existing ?? ObjectId.GenerateNewId().ToString();
        }

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

        if (string.IsNullOrEmpty(document.Id))
        {
            var existing = await _context.LtcCurrentSelections
                .Find(filter)
                .Project(d => d.Id)
                .FirstOrDefaultAsync();
            document.Id = existing ?? MongoDB.Bson.ObjectId.GenerateNewId().ToString();
        }

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
