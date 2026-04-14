using System.Security.Claims;
using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/chat/session")]
[Authorize]
public class ChatSessionController : ControllerBase
{
    private readonly ChatSessionService _service;

    public ChatSessionController(ChatSessionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<ChatSessionResponse>> GetSession()
    {
        var userId = GetUserId();
        var session = await _service.GetOrCreateAsync(userId);
        return Ok(session);
    }

    [HttpPatch("messages")]
    public async Task<ActionResult<ChatSessionResponse>> UpdateMessages([FromBody] UpdateChatMessagesRequest request)
    {
        var userId = GetUserId();
        var session = await _service.UpdateMessagesAsync(userId, request.Messages);
        return Ok(session);
    }

    [HttpPatch("ui-state")]
    public async Task<ActionResult<ChatSessionResponse>> UpdateUiState([FromBody] UpdateChatUiStateRequest request)
    {
        var userId = GetUserId();
        var session = await _service.UpdateUiStateAsync(userId, request.EditMode);
        return Ok(session);
    }

    [HttpPost("start-new")]
    public async Task<ActionResult<ChatSessionResponse>> StartNewSession()
    {
        var userId = GetUserId();
        var session = await _service.StartNewSessionAsync(userId);
        return Ok(session);
    }

    [HttpDelete]
    public async Task<IActionResult> ClearSession()
    {
        var userId = GetUserId();
        await _service.ClearAsync(userId);
        return NoContent();
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
