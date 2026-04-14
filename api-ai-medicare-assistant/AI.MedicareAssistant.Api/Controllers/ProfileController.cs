using System.Security.Claims;
using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly ProfileService _profileService;

    public ProfileController(ProfileService profileService)
    {
        _profileService = profileService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
