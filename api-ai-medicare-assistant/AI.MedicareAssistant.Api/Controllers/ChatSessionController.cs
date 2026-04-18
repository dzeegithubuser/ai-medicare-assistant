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

    [HttpPost("start-new")]
    public async Task<ActionResult<ChatSessionResponse>> StartNewSession()
    {
        var userId = GetUserId();
        var session = await _service.StartNewSessionAsync(userId);
        return Ok(session);
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
