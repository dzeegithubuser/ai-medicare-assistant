---
name: api-standards
description: Coding standards for the .NET 10 Clean Architecture API at api-ai-medicare-assistant/. Trigger when editing or creating any .cs file under that folder, scaffolding controllers/services/repositories/hubs, modifying Program.cs or .csproj, or reviewing API PRs. Covers Clean Architecture rules, MongoDB, IChatClient, JWT, Serilog, exception handling, xUnit/Moq.
---

# API Standards — `api-ai-medicare-assistant/`

.NET 10 Web API, Clean Architecture, 5 projects. **Match these conventions exactly** — they reflect the existing solution.

## Solution layout & dependency direction

```
AI.MedicareAssistant.Domain          ← no project references
AI.MedicareAssistant.Application     ← references Domain
AI.MedicareAssistant.Infrastructure  ← references Domain + Application
AI.MedicareAssistant.Api             ← references Domain + Application + Infrastructure
AI.MedicareAssistant.Tests           ← xUnit + Moq
```

**Never invert this direction.** Domain depends on nothing. Don't add a NuGet package to Domain unless it's purely declarative (e.g. `MongoDB.Bson` for attributes).

## Project file conventions

Every `.csproj`:
- `<TargetFramework>net10.0</TargetFramework>`
- `<Nullable>enable</Nullable>`
- `<ImplicitUsings>enable</ImplicitUsings>`

No `.editorconfig`, no StyleCop/Roslynator, no `TreatWarningsAsErrors`. Don't add these without asking.

## Controllers

- Inherit `ControllerBase`, decorate with `[ApiController]` + `[Route("api/[controller]")]`. See [AuthController.cs:10-12](api-ai-medicare-assistant/AI.MedicareAssistant.Api/Controllers/AuthController.cs#L10-L12).
- Return type: prefer `ActionResult<T>` for typed payloads (e.g. [ChatSessionController.cs:25](api-ai-medicare-assistant/AI.MedicareAssistant.Api/Controllers/ChatSessionController.cs#L25)); use `IActionResult` only when the success/error shapes differ structurally.
- Validation: **data annotations** on DTOs (`[Required]`, `[EmailAddress]`, `[Compare]`, `[RegularExpression]`, `[MaxLength]`). No FluentValidation.
- Use `[Authorize]` to protect endpoints. Extract user id with `User.FindFirstValue(ClaimTypes.NameIdentifier)`.

## Services (Application layer)

- **Always use interfaces.** Every Application service must have an `IFooService` interface and a `FooService` implementation in `Application/Services/`. Inject the interface, never the concrete type.
  - **Where the interface lives:** if the interface depends on Application DTOs (e.g. `AuthResponse`), put it in `Application/Interfaces/` (Domain cannot reference Application). If the interface only uses Domain types, put it in `Domain/Interfaces/`.
- **DI lifetime: `AddScoped`** for services and repositories. `AddSingleton` only for stateless utilities like `PromptBuilder`.
- **DTOs**: plain classes (not records), grouped by feature in files like `AuthDtos.cs`, `ChatSessionDtos.cs`. Annotated with data annotations. No AutoMapper — map manually.
- Throw the domain exception types listed below; don't return error tuples or `Result<T>`.

## Domain layer

- **Documents**: MongoDB POCOs decorated with `[BsonId]`, `[BsonRepresentation(BsonType.ObjectId)]`, `[BsonElement]`. No base class. Use `Guid` for app-level keys, `ObjectId` for storage. See [UserDocument.cs:10-15](api-ai-medicare-assistant/AI.MedicareAssistant.Domain/Documents/UserDocument.cs#L10-L15).
- **Interfaces.** Repositories and infra abstractions whose contracts use only Domain types live in `Domain/Interfaces/` — `IUserRepository`, `IAiCompletionService`, `ICmsPlanDataService`, etc. Application service interfaces that reference Application DTOs live in `Application/Interfaces/` (e.g. `IAuthService`). **All services, repositories, and infra abstractions get interfaces.**
- **Exceptions**: inherit from abstract `AppException(string message, int statusCode)`. Concrete types: `NotFoundException`, `ValidationException`, `UnauthorizedException`, `ConflictException`. Throw these — the global middleware maps them to HTTP responses.

## Infrastructure layer

- **MongoDB**: per-entity repositories (e.g. `MongoUserRepository`, `ChatSessionRepository`). Access collections via the singleton `MongoDbContext`. Use the fluent API: `_collection.Find(...).FirstOrDefaultAsync()`. See [MongoUserRepository.cs:13-26](api-ai-medicare-assistant/AI.MedicareAssistant.Infrastructure/Repositories/MongoUserRepository.cs#L13-L26).
- **AI**: always go through `IChatClient` (Microsoft.Extensions.AI). Three providers are wired: `AnthropicMeaiChatClient`, `GeminiMeaiChatClient`, OpenAI. Register via `AddHttpClient<IChatClient, T>`. Never call provider SDKs directly from Application/Domain.
- **External HTTP clients**: registered through `AddInfrastructureHttpClients()` extension.

## SignalR

- Hub at `/hubs/chat` ([Program.cs:46](api-ai-medicare-assistant/AI.MedicareAssistant.Api/Program.cs#L46)).
- Class-level `[Authorize]` ([ChatHub.cs:10](api-ai-medicare-assistant/AI.MedicareAssistant.Api/Hubs/ChatHub.cs#L10)).
- JWT comes via **query param** (`access_token`), wired in `AuthExtensions.cs:38-40` through `OnMessageReceived`. Don't change to header-based auth without coordinating with the UI client.
- Push to caller with `Clients.Caller.SendAsync(...)`.

## Auth

- JWT setup in [`Extensions/AuthExtensions.cs:9-52`](api-ai-medicare-assistant/AI.MedicareAssistant.Api/Extensions/AuthExtensions.cs#L9-L52). Symmetric key, issuer/audience validation, `ClockSkew = TimeSpan.Zero`.
- Passwords: **BCrypt.Net-Next 4.0.3**. `BCrypt.Net.BCrypt.HashPassword(password)` — don't roll your own hashing or switch libraries.

## Logging

- **Serilog.** Bootstrap logger configured in [Program.cs:7-13](api-ai-medicare-assistant/AI.MedicareAssistant.Api/Program.cs#L7-L13). Sinks: Console + daily rolling file (`Logs/log-.txt`, 30-day retention) + MongoDB sink (`Serilog.Sinks.MongoDB`).
- Use `_logger.LogInformation/LogWarning/LogError` — don't `Console.WriteLine`.

## Program.cs / service registration

Use the existing extension-method pattern. New service registrations go in or beside an existing extension class in `Api/Extensions/`:

`AddSerilog()` · `AddCoreServices()` · `AddOpenApiDocumentation()` · `AddDatabaseServices()` · `AddJwtAuthentication()` · `AddEmailServices()` · `AddAiProvider()` · `AddApplicationServices()` · `AddInfrastructureHttpClients()`

Don't dump new `builder.Services.AddX()` calls inline in `Program.cs`.

## Prompts

- Stored as `.txt` files under `Api/Prompts/` (and `Api/Prompts/schemas/` for JSON-schema prompts).
- Copied to output via `<Content Include="Prompts/**/*">` in the `.csproj`.
- Loaded at runtime by the **`PromptBuilder` singleton**. Add new prompts as `.txt` files and reference them through `PromptBuilder` — don't embed prompt strings in C# code.

## Error handling

- `GlobalExceptionMiddleware` ([Middleware/GlobalExceptionMiddleware.cs:33-41](api-ai-medicare-assistant/AI.MedicareAssistant.Api/Middleware/GlobalExceptionMiddleware.cs#L33-L41)) catches all exceptions, switches on `AppException` subtypes for status codes, and returns an `ErrorResponse` (status, message, traceId, errors dict).
- **Throw, don't return errors.** Throw the domain exception types; the middleware does the HTTP mapping.

## Tests

- **xUnit** (`[Fact]`) + **Moq** (`Mock<T>`, `.Setup().ReturnsAsync(...)`).
- Naming: `MethodName_Scenario_Expected` — e.g. `SignUp_ValidRequest_ReturnsSuccess`.
- Coverage focus: services with mocked repositories. See [AuthServiceTests.cs:46-50](api-ai-medicare-assistant/AI.MedicareAssistant.Tests/AuthServiceTests.cs#L46-L50).

```powershell
dotnet test api-ai-medicare-assistant
```

## Configuration

- Secrets never go in `appsettings.json`. Use `appsettings.Development.json` (git-ignored) or **.NET User Secrets** for local dev. Production uses environment variables.

## Don't

- Don't inject Application services by concrete type — always go through their `IFooService` interface.
- Don't bypass `IChatClient` to call OpenAI/Anthropic/Gemini SDKs from Application or Domain.
- Don't add EF Core or another ORM — MongoDB.Driver only.
- Don't invert the Clean Architecture dependency direction.
- Don't return `Result<T>`/error tuples — throw `AppException` subtypes.
- Don't use records for DTOs — plain classes with data annotations.
- Don't use AutoMapper — map manually.
- Don't put service registrations directly in `Program.cs` — extend an existing `Extensions/` class.
- Don't embed prompt strings in code — add a `.txt` under `Api/Prompts/`.
- Don't `Console.WriteLine` — use the Serilog `ILogger<T>`.
