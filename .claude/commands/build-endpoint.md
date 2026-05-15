---
description: Build a Medicare Assistant endpoint following Clean Architecture — Domain doc (if new collection) → repository → Application service interface + impl → DTOs → controller → DI registration → prompt file (if AI). Delegates to the feature-builder agent.
argument-hint: <endpoint description, e.g. "GET /api/recommendation/{id}/summary on a new RecommendationSummaryService, reading from IRecommendationRepository">
allowed-tools: Agent, Read, Glob, Grep
---

# Build a Medicare Assistant Endpoint

Implements an ASP.NET Core 10 endpoint on the **AI Medicare Assistant** API (`api-ai-medicare-assistant/`) following the project's Clean Architecture + MongoDB.Driver conventions. Delegates to the `feature-builder` agent so the standards in `.claude/skills/api-standards/` are enforced.

## Argument

User intent: $ARGUMENTS

If no clear endpoint shape (verb, route, controller name, dependencies, request/response shape) is given, ask **one** clarifying question before generating.

## Layered architecture (always)

```
Controller (Api/Controllers/<Feature>Controller.cs)
    │   inherits ControllerBase, [ApiController], [Route("api/[controller]")]
    │   depends on I<Feature>Service interface
    ▼
Service (Application/Services/<Feature>Service.cs)
    │   implements I<Feature>Service (interface required — saved memory)
    │   throws AppException subtypes; never returns Result<T>
    │   depends on I<X>Repository interface(s)
    ▼
Repository (Infrastructure/Repositories/Mongo<Name>Repository.cs)
    │   implements I<Name>Repository
    │   accesses collections via singleton MongoDbContext
    ▼
MongoDB.Driver 3.4 (no EF, no SQL)
```

The controller never sees `IMongoCollection<>` / `MongoDbContext`. The Application layer never sees `OpenAI` / `Anthropic.*` / `Google.GenerativeAI` SDK types — only `IChatClient`.

## Files the agent must produce / modify

| Layer | Path | Purpose |
|---|---|---|
| Domain doc (only if new collection) | `AI.MedicareAssistant.Domain/Documents/<Name>Document.cs` | `[BsonId]` + `[BsonRepresentation(BsonType.ObjectId)]` + `[BsonIgnoreExtraElements]` |
| Repo interface | `AI.MedicareAssistant.Domain/Interfaces/I<Name>Repository.cs` (Domain-only types) **or** `AI.MedicareAssistant.Application/Interfaces/` (uses Application DTOs) | Contract |
| Repo impl | `AI.MedicareAssistant.Infrastructure/Repositories/Mongo<Name>Repository.cs` | MongoDB.Driver fluent API |
| Index registration (if new collection) | `AI.MedicareAssistant.Infrastructure/Data/MongoDbContext.cs` (add to `MongoDbContext` + `MongoIndexInitializer`) | `Unique = true, Sparse = true` for optional unique fields |
| DTOs | `AI.MedicareAssistant.Application/DTOs/<Feature>Dtos.cs` | Plain classes with data annotations |
| Service interface | `AI.MedicareAssistant.Application/Interfaces/I<Feature>Service.cs` | Contract |
| Service impl | `AI.MedicareAssistant.Application/Services/<Feature>Service.cs` | Logic, `ILogger<T>`, throws `AppException` |
| Controller | `AI.MedicareAssistant.Api/Controllers/<Feature>Controller.cs` | `ControllerBase`, `[Authorize]`, `ActionResult<T>` |
| DI | `AI.MedicareAssistant.Api/Extensions/ApplicationServicesExtensions.cs` | `AddScoped<IFooService, FooService>()` |
| AI prompt (if any) | `AI.MedicareAssistant.Api/Prompts/<feature>.txt` (or `Prompts/schemas/*.txt`) | Loaded via `PromptBuilder` |

If the endpoint goes on an **existing** controller, skip the new-controller file and just add an action method.

## Controller canonical shape

```csharp
using Application.DTOs;
using Application.Interfaces;
using Domain.Constants;
using Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FeatureController : ControllerBase
{
    private readonly IFeatureService _feature;
    private readonly ILogger<FeatureController> _logger;

    public FeatureController(IFeatureService feature, ILogger<FeatureController> logger)
    {
        _feature = feature;
        _logger = logger;
    }

    private Guid CallerUserId
    {
        get
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(claim, out var id)) return id;
            throw new UnauthorizedException("Missing or invalid user id claim.");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FeatureResponse>> GetById([FromRoute] Guid id) =>
        Ok(await _feature.GetByIdAsync(CallerUserId, id));
}
```

## Service canonical shape

```csharp
using Application.DTOs;
using Application.Interfaces;
using Domain.Documents;
using Domain.Exceptions;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class FeatureService : IFeatureService
{
    private readonly IFeatureRepository _repo;
    private readonly ILogger<FeatureService> _logger;

    public FeatureService(IFeatureRepository repo, ILogger<FeatureService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<FeatureResponse> GetByIdAsync(Guid userId, Guid featureId)
    {
        var doc = await _repo.GetByIdAsync(featureId)
            ?? throw new NotFoundException("Feature", featureId);
        if (doc.UserId != userId)
            throw new UnauthorizedException("Feature does not belong to caller.");
        return Map(doc);
    }

    private static FeatureResponse Map(FeatureDocument d) => new() { Id = d.Id, Name = d.Name };
}
```

Register in `ApplicationServicesExtensions.AddApplicationServices(...)`:

```csharp
services.AddScoped<IFeatureService, FeatureService>();
services.AddScoped<IFeatureRepository, MongoFeatureRepository>();
```

## Forbidden patterns (always flag)

```csharp
// WRONG — synchronous blocking on async
var data = _svc.GetAsync().Result;

// WRONG — async void
public async void DoStuff() { ... }

// WRONG — Console in production code
Console.WriteLine("loaded");

// WRONG — new HttpClient
var c = new HttpClient();

// WRONG — record DTO (project uses plain classes)
public record FeatureResponse(Guid Id, string Name);

// WRONG — AutoMapper
public class FeatureProfile : Profile { ... }

// WRONG — returning Result<T> instead of throwing
public Task<Result<FeatureResponse>> GetAsync(Guid id) { ... }

// WRONG — concrete service injection
public FeatureController(FeatureService service) { ... }   // should be IFeatureService

// WRONG — service registration in Program.cs
builder.Services.AddScoped<IFeatureService, FeatureService>();   // belongs in ApplicationServicesExtensions

// WRONG — embedded prompt string
var prompt = "You are a Medicare expert. Given this drug list, return...";   // → Api/Prompts/<feature>.txt
```

Correct equivalents:

```csharp
var data = await _svc.GetAsync(ct);

public async Task DoStuffAsync() { ... }

_logger.LogInformation("Loaded {Count} items", count);

public FeatureService(IHttpClient client) { ... }   // typed client via AddInfrastructureHttpClients

public class FeatureResponse { public Guid Id { get; set; } public string Name { get; set; } = ""; }

// manual mapping
private static FeatureResponse Map(FeatureDocument d) => new() { Id = d.Id, Name = d.Name };

// throw AppException subtype
if (doc is null) throw new NotFoundException("Feature", id);

public FeatureController(IFeatureService feature) { ... }

// In ApplicationServicesExtensions.cs:
services.AddScoped<IFeatureService, FeatureService>();

// Prompt as a .txt file:
var promptText = await _promptBuilder.LoadAsync("feature.txt");
```

## Security — required

| Rule | Implementation |
|---|---|
| Authentication | Controller is `[Authorize]` unless explicitly public |
| User identity | `User.FindFirstValue(ClaimTypes.NameIdentifier)` — parse to Guid |
| Role gate (optional) | `[Authorize(Roles = UserRoles.X)]` from `Domain.Constants.UserRoles` |
| Secrets | `IConfiguration["..."]` for non-secrets, User Secrets / env vars for secrets — never hardcoded |
| HttpClient | `IHttpClientFactory` / typed clients (via `AddInfrastructureHttpClients()`) |
| AI calls | `IChatClient` (Microsoft.Extensions.AI) only |

## Output requirements

1. **Compiling code** — every file must compile against `api-ai-medicare-assistant`.
2. **DI registration** — every new service / repository registered in the matching `Api/Extensions/` method.
3. **Build pass** — `dotnet build api-ai-medicare-assistant` exits 0.
4. **Validation targets** — emit a `## Validation targets` block at the end of the report.

After generating, briefly summarize:

- Files created (with paths).
- New routes added.
- New `AppRoutes.*` UI constants if a UI surface was included.
- New AI prompt files (if any).
- Anything the developer should manually verify (Swagger at `http://localhost:5024/swagger`, smoke test against `http://localhost:4200`).
