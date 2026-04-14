using System.Security.Claims;
using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.MedicareAssistant.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatOrchestratorController : ControllerBase
{
    private readonly ChatOrchestratorService _orchestrator;

    public ChatOrchestratorController(ChatOrchestratorService orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Main chatbot endpoint — accepts user message, routes through FSM + intent classification.
    /// </summary>
    [HttpPost("orchestrate")]
    public async Task<ActionResult<OrchestratorResponse>> Orchestrate(
        [FromBody] OrchestratorRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        var userId = GetUserId();
        var result = await _orchestrator.ProcessMessageAsync(userId, request.Message.Trim(), request.CurrentPage, ct);
        return Ok(result);
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
