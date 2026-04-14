using Application.DTOs;
using Domain.Documents;
using Domain.Interfaces;

namespace Application.Services;

public class ChatSessionService
{
    private readonly IChatSessionRepository _repo;

    public ChatSessionService(IChatSessionRepository repo)
    {
        _repo = repo;
    }

    public async Task<ChatSessionResponse> GetOrCreateAsync(Guid userId)
    {
        var doc = await _repo.GetByUserIdAsync(userId);
        if (doc is null)
        {
            doc = new ChatSessionDocument
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _repo.UpsertAsync(doc);
        }

        return Map(doc);
    }

    public async Task<ChatSessionResponse> UpdateMessagesAsync(Guid userId, List<ChatSessionMessageDto> messages)
    {
        var doc = await _repo.GetByUserIdAsync(userId) ?? new ChatSessionDocument { UserId = userId };
        // Keep a bounded rolling window
        var bounded = messages.TakeLast(200).ToList();
        doc.Messages = bounded.Select(m => new ChatMessageDoc
        {
            Role = m.Role,
            Content = m.Content,
            Timestamp = m.Timestamp,
            Context = m.Context
        }).ToList();
        doc.UpdatedAt = DateTime.UtcNow;
        await _repo.UpsertAsync(doc);
        return Map(doc);
    }

    public async Task<ChatSessionResponse> UpdateUiStateAsync(Guid userId, bool editMode)
    {
        var doc = await _repo.GetByUserIdAsync(userId) ?? new ChatSessionDocument { UserId = userId };
        doc.UiState.EditMode = editMode;
        doc.UpdatedAt = DateTime.UtcNow;
        await _repo.UpsertAsync(doc);
        return Map(doc);
    }

    public async Task<ChatSessionResponse> StartNewSessionAsync(Guid userId)
    {
        var doc = await _repo.GetByUserIdAsync(userId) ?? new ChatSessionDocument { UserId = userId };

        if (doc.Messages.Count > 0 || doc.UiState.EditMode)
        {
            doc.Archives.Add(new ChatSessionArchiveDoc
            {
                ArchivedAt = DateTime.UtcNow,
                Messages = doc.Messages,
                UiState = doc.UiState
            });

            // Keep archive history bounded.
            if (doc.Archives.Count > 10)
            {
                doc.Archives = doc.Archives
                    .OrderByDescending(a => a.ArchivedAt)
                    .Take(10)
                    .ToList();
            }
        }

        doc.Messages = [];
        doc.UiState = new ChatUiStateDoc();
        doc.UpdatedAt = DateTime.UtcNow;
        await _repo.UpsertAsync(doc);
        return Map(doc);
    }

    public async Task ClearAsync(Guid userId)
    {
        await _repo.DeleteByUserIdAsync(userId);
    }

    private static ChatSessionResponse Map(ChatSessionDocument doc) => new()
    {
        Messages = doc.Messages.Select(m => new ChatSessionMessageDto
        {
            Role = m.Role,
            Content = m.Content,
            Timestamp = m.Timestamp,
            Context = m.Context
        }).ToList(),
        UiState = new ChatUiStateDto
        {
            EditMode = doc.UiState.EditMode
        }
    };
}
