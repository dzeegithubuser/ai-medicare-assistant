using Application.DTOs;
using Application.Services;
using Domain.Documents;
using Domain.Interfaces;
using Moq;

namespace AI.MedicareAssistant.Tests;

/// <summary>
/// Tests for ChatSessionService — session CRUD, message bounding, archiving.
/// </summary>
public class ChatSessionServiceTests
{
    private readonly Mock<IChatSessionRepository> _repoMock;
    private readonly ChatSessionService _sut;

    public ChatSessionServiceTests()
    {
        _repoMock = new Mock<IChatSessionRepository>();
        _sut = new ChatSessionService(_repoMock.Object);
    }

    // ═══════ GetOrCreateAsync ═══════

    [Fact]
    public async Task GetOrCreate_ExistingSession_ReturnsMappedSession()
    {
        var userId = Guid.NewGuid();
        var doc = new ChatSessionDocument
        {
            UserId = userId,
            Messages = [new ChatMessageDoc { Role = "user", Content = "hello", Timestamp = DateTime.UtcNow }],
            UiState = new ChatUiStateDoc { EditMode = true }
        };
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(doc);

        var result = await _sut.GetOrCreateAsync(userId);

        Assert.Single(result.Messages);
        Assert.Equal("user", result.Messages[0].Role);
        Assert.Equal("hello", result.Messages[0].Content);
        Assert.True(result.UiState.EditMode);
    }

    [Fact]
    public async Task GetOrCreate_NoSession_CreatesNewAndReturns()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((ChatSessionDocument?)null);

        var result = await _sut.GetOrCreateAsync(userId);

        Assert.Empty(result.Messages);
        Assert.False(result.UiState.EditMode);
        _repoMock.Verify(r => r.UpsertAsync(It.Is<ChatSessionDocument>(d =>
            d.UserId == userId)), Times.Once);
    }

    // ═══════ UpdateMessagesAsync ═══════

    [Fact]
    public async Task UpdateMessages_StoresMessages()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(new ChatSessionDocument { UserId = userId });

        var messages = new List<ChatSessionMessageDto>
        {
            new() { Role = "user", Content = "hello", Timestamp = DateTime.UtcNow },
            new() { Role = "assistant", Content = "hi there", Timestamp = DateTime.UtcNow }
        };

        var result = await _sut.UpdateMessagesAsync(userId, messages);

        Assert.Equal(2, result.Messages.Count);
        _repoMock.Verify(r => r.UpsertAsync(It.Is<ChatSessionDocument>(d =>
            d.Messages.Count == 2)), Times.Once);
    }

    [Fact]
    public async Task UpdateMessages_BoundsTo200Messages()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(new ChatSessionDocument { UserId = userId });

        var messages = Enumerable.Range(0, 300)
            .Select(i => new ChatSessionMessageDto
            {
                Role = "user",
                Content = $"message {i}",
                Timestamp = DateTime.UtcNow
            })
            .ToList();

        var result = await _sut.UpdateMessagesAsync(userId, messages);

        Assert.Equal(200, result.Messages.Count);
        // Should keep the LAST 200
        Assert.Equal("message 100", result.Messages[0].Content);
        Assert.Equal("message 299", result.Messages[199].Content);
    }

    [Fact]
    public async Task UpdateMessages_NoExistingSession_CreatesNew()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((ChatSessionDocument?)null);

        var messages = new List<ChatSessionMessageDto>
        {
            new() { Role = "user", Content = "first message" }
        };

        await _sut.UpdateMessagesAsync(userId, messages);

        _repoMock.Verify(r => r.UpsertAsync(It.Is<ChatSessionDocument>(d =>
            d.UserId == userId && d.Messages.Count == 1)), Times.Once);
    }

    // ═══════ UpdateUiStateAsync ═══════

    [Fact]
    public async Task UpdateUiState_SetsEditMode()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(new ChatSessionDocument { UserId = userId });

        var result = await _sut.UpdateUiStateAsync(userId, true);

        Assert.True(result.UiState.EditMode);
        _repoMock.Verify(r => r.UpsertAsync(It.Is<ChatSessionDocument>(d =>
            d.UiState.EditMode == true)), Times.Once);
    }

    // ═══════ StartNewSessionAsync ═══════

    [Fact]
    public async Task StartNewSession_ArchivesExistingMessages()
    {
        var userId = Guid.NewGuid();
        var doc = new ChatSessionDocument
        {
            UserId = userId,
            Messages = [new ChatMessageDoc { Role = "user", Content = "old message" }],
            Archives = []
        };
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(doc);

        var result = await _sut.StartNewSessionAsync(userId);

        Assert.Empty(result.Messages);
        _repoMock.Verify(r => r.UpsertAsync(It.Is<ChatSessionDocument>(d =>
            d.Messages.Count == 0 && d.Archives.Count == 1)), Times.Once);
    }

    [Fact]
    public async Task StartNewSession_EmptySession_NoArchive()
    {
        var userId = Guid.NewGuid();
        var doc = new ChatSessionDocument
        {
            UserId = userId,
            Messages = [],
            UiState = new ChatUiStateDoc { EditMode = false },
            Archives = []
        };
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(doc);

        await _sut.StartNewSessionAsync(userId);

        _repoMock.Verify(r => r.UpsertAsync(It.Is<ChatSessionDocument>(d =>
            d.Archives.Count == 0)), Times.Once);
    }

    [Fact]
    public async Task StartNewSession_BoundsArchivesTo10()
    {
        var userId = Guid.NewGuid();
        var doc = new ChatSessionDocument
        {
            UserId = userId,
            Messages = [new ChatMessageDoc { Role = "user", Content = "new" }],
            Archives = Enumerable.Range(0, 10)
                .Select(i => new ChatSessionArchiveDoc
                {
                    ArchivedAt = DateTime.UtcNow.AddDays(-i),
                    Messages = []
                })
                .ToList()
        };
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(doc);

        await _sut.StartNewSessionAsync(userId);

        _repoMock.Verify(r => r.UpsertAsync(It.Is<ChatSessionDocument>(d =>
            d.Archives.Count <= 10)), Times.Once);
    }

    // ═══════ ClearAsync ═══════

    [Fact]
    public async Task Clear_CallsDeleteByUserId()
    {
        var userId = Guid.NewGuid();
        await _sut.ClearAsync(userId);
        _repoMock.Verify(r => r.DeleteByUserIdAsync(userId), Times.Once);
    }
}
