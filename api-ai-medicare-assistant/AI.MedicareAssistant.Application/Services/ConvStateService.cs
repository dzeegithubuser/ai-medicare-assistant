using Domain.Documents;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace Application.Services;

public class ConvStateService
{
    private readonly IConvStateRepository _repo;
    private readonly ILogger<ConvStateService> _logger;

    public ConvStateService(IConvStateRepository repo, ILogger<ConvStateService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<ConvStateDocument> GetOrCreateAsync(Guid userId)
    {
        var state = await _repo.GetByUserIdAsync(userId);
        if (state is not null)
        {
            RefreshExpiry(state);
            return state;
        }

        state = new ConvStateDocument
        {
            UserId = userId,
            State = ConversationState.Idle
        };
        await _repo.UpsertAsync(state);
        _logger.LogInformation("Created new conversation state for user {UserId}", userId);
        return state;
    }

    public async Task UpdateStateAsync(Guid userId, ConversationState newState, string? activeIntent = null)
    {
        var state = await GetOrCreateAsync(userId);
        state.State = newState;
        state.ActiveIntent = activeIntent;
        RefreshExpiry(state);
        await _repo.UpsertAsync(state);
    }

    public async Task SetPendingChangeAsync(Guid userId, string description, BsonDocument changes)
    {
        var state = await GetOrCreateAsync(userId);
        state.State = ConversationState.AwaitingConfirmation;
        state.AwaitingConfirmationFor = description;
        state.PendingChanges = changes;
        RefreshExpiry(state);
        await _repo.UpsertAsync(state);
    }

    public async Task SetCollectedFieldAsync(Guid userId, string fieldName, BsonValue value)
    {
        var state = await GetOrCreateAsync(userId);
        state.CollectedFields[fieldName] = value;
        RefreshExpiry(state);
        await _repo.UpsertAsync(state);
    }

    public async Task ClearPendingAsync(Guid userId)
    {
        var state = await GetOrCreateAsync(userId);
        state.State = ConversationState.Idle;
        state.ActiveIntent = null;
        state.AwaitingConfirmationFor = null;
        state.PendingChanges = new BsonDocument();
        RefreshExpiry(state);
        await _repo.UpsertAsync(state);
    }

    public async Task ResetAsync(Guid userId)
    {
        var state = await GetOrCreateAsync(userId);
        state.State = ConversationState.Idle;
        state.ActiveIntent = null;
        state.AwaitingConfirmationFor = null;
        state.PendingChanges = new BsonDocument();
        state.CollectedFields = new BsonDocument();
        RefreshExpiry(state);
        await _repo.UpsertAsync(state);
        _logger.LogInformation("Reset conversation state for user {UserId}", userId);
    }

    public async Task DeleteAsync(Guid userId)
    {
        await _repo.DeleteByUserIdAsync(userId);
    }

    private static void RefreshExpiry(ConvStateDocument state)
    {
        state.LastActivity = DateTime.UtcNow;
        state.ExpiresAt = DateTime.UtcNow.AddMinutes(30);
    }
}
