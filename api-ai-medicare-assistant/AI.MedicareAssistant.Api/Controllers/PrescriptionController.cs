using System.Security.Claims;
using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.MedicareAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PrescriptionController : ControllerBase
{
    private readonly PrescriptionService _service;

    public PrescriptionController(PrescriptionService service)
    {
        _service = service;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SavePrescriptionRequest request)
    {
        var userId = GetUserId();
        var result = await _service.SaveAsync(userId, request);
        return Ok(result);
    }

    /// <summary>Upserts confirmed FP drugs as the user's current prescriptions (MongoDB + MySQL profile link).</summary>
    [HttpPost("current")]
    public async Task<IActionResult> SaveCurrent([FromBody] SaveCurrentPrescriptionsRequest request)
    {
        var userId = GetUserId();
        var result = await _service.SaveCurrentAsync(userId, request);
        return Ok(result);
    }

    /// <summary>Replaces only the drugs section. Pharmacies and plans are untouched.</summary>
    [HttpPut("current/drugs")]
    public async Task<IActionResult> SaveCurrentDrugs([FromBody] SaveCurrentDrugsRequest request)
    {
        await _service.SaveCurrentDrugsAsync(GetUserId(), request);
        return NoContent();
    }

    /// <summary>Replaces only the pharmacies section. Drugs and plans are untouched.</summary>
    [HttpPut("current/pharmacy")]
    public async Task<IActionResult> SaveCurrentPharmacy([FromBody] SaveCurrentPharmacyRequest request)
    {
        await _service.SaveCurrentPharmacyAsync(GetUserId(), request);
        return NoContent();
    }

    /// <summary>Replaces only the plans section and fpActiveSection. Drugs and pharmacies are untouched.</summary>
    [HttpPut("current/plans")]
    public async Task<IActionResult> SaveCurrentPlans([FromBody] SaveCurrentPlansRequest request)
    {
        await _service.SaveCurrentPlansAsync(GetUserId(), request);
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetUserId();
        var result = await _service.GetByUserIdAsync(userId);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }
}
