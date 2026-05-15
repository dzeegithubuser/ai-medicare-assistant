---
description: Write xUnit + Moq tests for Medicare Assistant backend services or repositories (or Vitest for the Angular UI) and verify they pass. Delegates to the test-author agent.
argument-hint: <target — e.g. "FinancialPlannerService", "MongoUserRepository", or a UI service like "AdminService">
allowed-tools: Agent, Read, Glob, Grep, Bash
---

# Write Medicare Assistant Tests

Generate / extend tests for the named target and run the relevant suite. Delegates to the `test-author` agent.

## Argument

Target under test: $ARGUMENTS

If the target is ambiguous (multiple matches across API + UI, or the user gave a feature name instead of a class), ask **one** clarifying question.

## Backend (xUnit + Moq)

### Frameworks

- **xUnit** (`[Fact]`, `[Theory] / [InlineData]`)
- **Moq** (`Mock<T>`, `.Setup(...).ReturnsAsync(...)`, `.Verify(..., Times.X)`)
- **No MSTest** — do not use `[TestClass] / [TestMethod] / [TestInitialize]`.

### Skeleton (Application service)

```csharp
using Application.Services;
using Domain.Constants;
using Domain.Documents;
using Domain.Exceptions;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace AI.MedicareAssistant.Tests;

public class FeatureServiceTests
{
    private readonly Mock<IFeatureRepository> _repoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly FeatureService _sut;

    public FeatureServiceTests()
    {
        _sut = new FeatureService(
            _repoMock.Object,
            _userRepoMock.Object,
            Mock.Of<ILogger<FeatureService>>());
    }

    [Fact]
    public async Task GetByIdAsync_ExistingDoc_ReturnsMappedDto()
    {
        var userId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(new FeatureDocument
        {
            Id = "abc", UserId = userId, Name = "Test"
        });

        var result = await _sut.GetByIdAsync(userId, docId);

        Assert.Equal("Test", result.Name);
        _repoMock.Verify(r => r.GetByIdAsync(docId), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_MissingDoc_ThrowsNotFound()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((FeatureDocument?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task GetByIdAsync_WrongUser_ThrowsUnauthorized()
    {
        var callerId = Guid.NewGuid();
        var differentOwner = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new FeatureDocument
        {
            UserId = differentOwner
        });

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.GetByIdAsync(callerId, Guid.NewGuid()));
    }
}
```

### Cascade-delete patterns

For services that cascade across multiple repositories (e.g. `FinancialPlannerService.DeleteEndUserAsync`), test that **every** dependency was called exactly once:

```csharp
[Fact]
public async Task DeleteEndUser_OwnedTarget_CascadesAllPerUserCollections()
{
    var fpUserId = Guid.NewGuid();
    var endUserId = Guid.NewGuid();
    _userRepoMock.Setup(r => r.GetByIdAsync(endUserId)).ReturnsAsync(new UserDocument
    {
        UserId = endUserId, Role = UserRoles.User, FpId = fpUserId
    });

    await _sut.DeleteEndUserAsync(fpUserId, endUserId);

    _profileRepoMock.Verify(r => r.DeleteByUserIdAsync(endUserId), Times.Once);
    _chatRepoMock.Verify(r => r.DeleteByUserIdAsync(endUserId), Times.Once);
    _recRepoMock.Verify(r => r.DeleteByUserIdAsync(endUserId), Times.Once);
    _selectionsRepoMock.Verify(r => r.DeleteByUserIdAsync(endUserId), Times.Once);
    _ltcRepoMock.Verify(r => r.DeleteByUserIdAsync(endUserId), Times.Once);
    _userRepoMock.Verify(r => r.DeleteAsync(endUserId), Times.Once);
}
```

### Auth / token tests

`AuthService` exercises real BCrypt + real `JwtSecurityTokenHandler` — these are deterministic given the same inputs. The fixture wires an in-memory `IConfiguration` with test JWT settings:

```csharp
_config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Jwt:Secret"] = "TestSecretKeyAtLeast32CharactersLong!@#ForUnitTests",
        ["Jwt:Issuer"] = "TestIssuer",
        ["Jwt:Audience"] = "TestAudience",
        ["Jwt:ExpiryHours"] = "1"
    })
    .Build();
```

### Mocking AI providers

For Application services that call `IChatClient` (Microsoft.Extensions.AI):

```csharp
private readonly Mock<IChatClient> _chatClientMock = new();

_chatClientMock
    .Setup(c => c.GetResponseAsync(It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "stub")));
```

Never make real LLM calls in tests.

### Hard rules

- **NEVER** real HTTP / real Mongo / real AI.
- **NEVER** MSTest attributes.
- **NEVER** `async void` tests.
- **ALWAYS** `Assert.ThrowsAsync<TException>(() => sut.MethodAsync())` for error paths.
- **ALWAYS** name `MethodName_Scenario_Expected`.
- **ALWAYS** run the suite and report the actual pass count.

### Running

```powershell
# Full suite (96+ tests, ~1 s)
dotnet test api-ai-medicare-assistant

# One test class while iterating
dotnet test api-ai-medicare-assistant --filter "FullyQualifiedName~FeatureServiceTests"
```

## Frontend (Vitest)

### Service test skeleton

```typescript
import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { FeatureService } from './feature.service';
import { environment } from '../../environments/environment';

describe('FeatureService', () => {
  let service: FeatureService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), FeatureService],
    });
    service = TestBed.inject(FeatureService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('GET /api/feature/{id} returns the item', () => {
    const id = 'abc';
    service.getById(id).subscribe(result => expect(result.id).toBe(id));
    const req = http.expectOne(`${environment.apiUrl}/api/feature/${id}`);
    expect(req.request.method).toBe('GET');
    req.flush({ id });
  });

  it('surfaces 409 errors', () => {
    service.create({ name: 'dup' }).subscribe({
      next: () => { throw new Error('Expected error'); },
      error: err => expect(err.status).toBe(409),
    });
    const req = http.expectOne(r => r.url.endsWith('/api/feature'));
    req.flush({ message: 'Already exists' }, { status: 409, statusText: 'Conflict' });
  });
});
```

### Component testing

For standalone components with signals, prefer testing through the public surface — render the component, drive interactions via `TestBed`, assert against the DOM. Mock the injected service:

```typescript
const featureServiceMock = {
  list: vi.fn().mockReturnValue(of([{ id: '1', name: 'A' }])),
};

TestBed.configureTestingModule({
  imports: [FeatureListComponent],
  providers: [{ provide: FeatureService, useValue: featureServiceMock }],
});
```

### Hard rules

- **NEVER** real `HttpClient` — use `provideHttpClientTesting`.
- **NEVER** Karma / Jasmine — this project is **Vitest**.
- **ALWAYS** `http.verify()` in `afterEach`.

### Running

```powershell
# Full UI suite (no watch)
npm --prefix ui-ai-medicare-assistant test -- --run

# One file
npm --prefix ui-ai-medicare-assistant test -- --run src/app/services/feature.service.spec.ts
```

## What "done" looks like

- Test file(s) created / modified.
- Every public method on the target has at least one happy-path test.
- Every `throw new …` branch has at least one error-path test.
- Mocks are `Verify`'d for the calls that are part of the contract.
- Suite passes. The exact command + final pass-count line is in the report.
- If a test couldn't be written (private helper, integration-only), it's called out explicitly.

If tests still fail after **3 attempts**, STOP and report the failure clearly — don't suppress it.
