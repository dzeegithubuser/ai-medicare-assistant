using System.Security.Claims;
using Application.DTOs;
using Application.Services;
using Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly ProfileService _profileService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(ProfileService profileService, ILogger<ProfileController> logger)
    {
        _profileService = profileService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim, out var userId))
            throw new UnauthorizedException("User identity claim is missing or invalid.");
        return userId;
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var result = await _profileService.GetProfileAsync(GetUserId());
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> SaveProfile([FromBody] ProfileDto dto)
    {
        var userId = GetUserId();
        await _profileService.SaveAsync(userId, dto);
        var profile = await _profileService.GetProfileAsync(userId);
        return Ok(profile);
    }
}
