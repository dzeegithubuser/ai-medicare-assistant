using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ChatSessionService _sessionService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ChatSessionService sessionService, ILogger<ChatHub> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Replaces PATCH /api/chat/session/messages.
    /// Called by the Angular client whenever the message list changes.
    /// The bounded rolling-window logic is preserved inside ChatSessionService.
    /// </summary>
    public async Task SyncMessages(List<ChatSessionMessageDto> messages)
    {
        var userId = GetUserId();
        _logger.LogDebug("SyncMessages received {Count} messages from user {UserId}", messages.Count, userId);
        await _sessionService.UpdateMessagesAsync(userId, messages);
        // Lightweight ack so the client knows the write completed.
        await Clients.Caller.SendAsync("MessagesSynced");
    }

    /// <summary>
    /// Pushes the stored session to the client immediately on connect,
    /// replacing the GET /api/chat/session HTTP call.
    /// Always sends the event (with empty messages if no session exists)
    /// so the Angular forkJoin bootstrap does not hang.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        _logger.LogInformation("ChatHub connected for user {UserId}, ConnectionId={ConnectionId}", userId, Context.ConnectionId);
        var session = await _sessionService.GetOrCreateAsync(userId);
        await Clients.Caller.SendAsync("ReceiveSession", session.Messages, session.UiState);
        await base.OnConnectedAsync();
    }

    private Guid GetUserId()
    {
        if (Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return id;
        _logger.LogWarning("ChatHub unauthorized access attempt, ConnectionId={ConnectionId}", Context.ConnectionId);
        throw new HubException("Unauthorized");
    }
}
