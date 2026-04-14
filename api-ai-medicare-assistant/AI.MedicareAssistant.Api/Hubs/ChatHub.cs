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

    public ChatHub(ChatSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    /// <summary>
    /// Replaces PATCH /api/chat/session/messages.
    /// Called by the Angular client whenever the message list changes.
    /// The bounded rolling-window logic is preserved inside ChatSessionService.
    /// </summary>
    public async Task SyncMessages(List<ChatSessionMessageDto> messages)
    {
        var userId = GetUserId();
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
        var session = await _sessionService.GetOrCreateAsync(userId);
        await Clients.Caller.SendAsync("ReceiveSession", session.Messages, session.UiState);
        await base.OnConnectedAsync();
    }

    private Guid GetUserId() =>
        Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new HubException("Unauthorized");
}
