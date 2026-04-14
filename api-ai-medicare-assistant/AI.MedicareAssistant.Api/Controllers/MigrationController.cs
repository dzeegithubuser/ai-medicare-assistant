using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AI.MedicareAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class MigrationController : ControllerBase
{
    private readonly AppDbContext _db;

    public MigrationController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// GET api/migration/applied — list all migrations that have been applied to the database.
    /// </summary>
    [HttpGet("applied")]
    public async Task<IActionResult> GetAppliedMigrations()
    {
        var applied = await _db.Database.GetAppliedMigrationsAsync();
        return Ok(new { count = applied.Count(), migrations = applied });
    }

    /// <summary>
    /// GET api/migration/pending — list all migrations that are pending (not yet applied).
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingMigrations()
    {
        var pending = await _db.Database.GetPendingMigrationsAsync();
        return Ok(new { count = pending.Count(), migrations = pending });
    }

    /// <summary>
    /// POST api/migration/apply — apply all pending migrations to the database.
    /// </summary>
    [HttpPost("apply")]
    public async Task<IActionResult> ApplyPendingMigrations()
    {
        var pendingBefore = (await _db.Database.GetPendingMigrationsAsync()).ToList();

        if (pendingBefore.Count == 0)
        {
            return Ok(new { message = "No pending migrations to apply.", applied = Array.Empty<string>() });
        }

        await _db.Database.MigrateAsync();

        return Ok(new
        {
            message = $"Successfully applied {pendingBefore.Count} migration(s).",
            applied = pendingBefore
        });
    }
}
