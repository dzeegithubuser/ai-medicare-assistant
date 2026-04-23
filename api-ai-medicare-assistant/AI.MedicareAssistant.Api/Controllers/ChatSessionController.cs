using System.Security.Claims;
using Application.DTOs;
using Application.Services;
using Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/chat/session")]
[Authorize]
public class ChatSessionController : ControllerBase
{
    private readonly ChatSessionService _service;
    private readonly ILogger<ChatSessionController> _logger;

    public ChatSessionController(ChatSessionService service, ILogger<ChatSessionController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost("start-new")]
    public async Task<ActionResult<ChatSessionResponse>> StartNewSession()
    {
        var userId = GetUserId();
        _logger.LogInformation("Starting new chat session for user {UserId}", userId);
        var session = await _service.StartNewSessionAsync(userId);
        return Ok(session);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim, out var userId))
            throw new UnauthorizedException("User identity claim is missing or invalid.");
        return userId;
    }
}
