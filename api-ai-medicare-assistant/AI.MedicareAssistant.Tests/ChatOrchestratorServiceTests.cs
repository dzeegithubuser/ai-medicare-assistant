using Application.DTOs;
using Application.Services;
using Domain.Documents;
using Domain.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Moq;

namespace AI.MedicareAssistant.Tests;

/// <summary>
/// Tests for ChatOrchestratorService — FSM routing, handler logic, and helper methods.
/// Uses Moq to isolate from database and AI dependencies.
/// </summary>
public class ChatOrchestratorServiceTests
{
    private readonly Mock<IConvStateRepository> _convStateRepoMock;
    private readonly Mock<IRecommendationRepository> _recRepoMock;
    private readonly Mock<IProfileRepository> _profileRepoMock;
    private readonly Mock<IChatClient> _chatClientMock;
    private readonly Mock<ICountyLookupService> _countyMock;
    private readonly Mock<IPharmacyLookupService> _pharmacyMock;
    private readonly Mock<IDrugAiService> _drugAiMock;
    private readonly Mock<IIndividualMedicareService> _medicareMock;
    private readonly Mock<ICostEvaluationAiService> _costEvalMock;
    private readonly ChatOrchestratorService _sut;

    public ChatOrchestratorServiceTests()
    {
        _convStateRepoMock = new Mock<IConvStateRepository>();
        _recRepoMock = new Mock<IRecommendationRepository>();
        _profileRepoMock = new Mock<IProfileRepository>();
        _chatClientMock = new Mock<IChatClient>();
        _countyMock = new Mock<ICountyLookupService>();
        _pharmacyMock = new Mock<IPharmacyLookupService>();
        _drugAiMock = new Mock<IDrugAiService>();
        _medicareMock = new Mock<IIndividualMedicareService>();
        _costEvalMock = new Mock<ICostEvaluationAiService>();

        // Build concrete service instances with mocked repos
        var convStateService = new ConvStateService(
            _convStateRepoMock.Object,
            Mock.Of<ILogger<ConvStateService>>());

        var recService = new RecommendationService(
            _recRepoMock.Object,
            Mock.Of<ILogger<RecommendationService>>());

        var intentService = new OrchestratorIntentService(
            _chatClientMock.Object,
            Mock.Of<ILogger<OrchestratorIntentService>>());

        var profileService = new ProfileService(
            _profileRepoMock.Object,
            Mock.Of<ILogger<ProfileService>>());

        var costProjectionService = new CostProjectionService(
            profileService,
            _medicareMock.Object,
            _costEvalMock.Object,
            Mock.Of<IPresentValueService>(),
            Mock.Of<ILogger<CostProjectionService>>());

        var deltaCalcService = new DeltaCalculationService(
            costProjectionService,
            recService,
            _chatClientMock.Object,
            Mock.Of<ILogger<DeltaCalculationService>>());

        _sut = new ChatOrchestratorService(
            convStateService,
            recService,
            intentService,
            profileService,
            deltaCalcService,
            costProjectionService,
            _countyMock.Object,
            _pharmacyMock.Object,
            _drugAiMock.Object,
            Mock.Of<ILogger<ChatOrchestratorService>>());
    }

    // ═══════════════════════════════════════════════════════════════
    //  Top-level error handling
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task ProcessMessage_WhenExceptionThrown_ReturnsErrorMessage()
    {
        // Arrange — force error by having GetOrCreate throw
        _convStateRepoMock
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("DB connection failed"));

        // Act
        var result = await _sut.ProcessMessageAsync(Guid.NewGuid(), "hello");

        // Assert
        Assert.Contains("something went wrong", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.RequiresConfirmation);
    }

    // ═══════════════════════════════════════════════════════════════
    //  FSM: Idle → Intent Classification
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task ProcessMessage_IdleState_ClassifiesIntent()
    {
        // Arrange — idle state (no prior conversation)
        var userId = Guid.NewGuid();
        SetupIdleConvState(userId);

        // Mock intent classification to return "help"
        SetupChatClientResponse("{\"intent\":\"help\",\"params\":{}}");

        // Act
        var result = await _sut.ProcessMessageAsync(userId, "what can you do?");

        // Assert — should get help response with displayData
        Assert.Contains("help you with", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.DisplayData);
        Assert.Equal("help_menu", result.DisplayData.Type);
    }

    // ═══════════════════════════════════════════════════════════════
    //  FSM: Create Recommendation
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateRecommendation_WhenExists_StillStartsCollection()
    {
        var userId = Guid.NewGuid();
        SetupIdleConvState(userId);
        SetupChatClientResponse("{\"intent\":\"create_recommendation\",\"params\":{}}");
        _recRepoMock.Setup(r => r.ExistsByUserIdAsync(userId)).ReturnsAsync(true);

        var result = await _sut.ProcessMessageAsync(userId, "create recommendation");

        Assert.Contains("name", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRecommendation_NoProfile_StartsCollection()
    {
        var userId = Guid.NewGuid();
        SetupIdleConvState(userId);
        SetupChatClientResponse("{\"intent\":\"create_recommendation\",\"params\":{}}");
        _recRepoMock.Setup(r => r.ExistsByUserIdAsync(userId)).ReturnsAsync(false);

        // ProfileService.GetProfileAsync will use IChatClient — mock it to return no profile
        // (The mock IChatClient returns empty, which causes profile check to fail gracefully)

        var result = await _sut.ProcessMessageAsync(userId, "create recommendation");

        // Should start multi-turn collection asking for name
        Assert.Contains("name", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════
    //  FSM: Confirmation flow
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Confirmation_Affirmative_DeleteRoute_MovesToDeletePhrase()
    {
        var userId = Guid.NewGuid();
        SetupConvState(userId, ConversationState.AwaitingConfirmation, "delete_recommendation");

        var result = await _sut.ProcessMessageAsync(userId, "yes");

        Assert.Contains("DELETE MY RECOMMENDATION", result.Message);
    }

    [Fact]
    public async Task Confirmation_Negative_CancelsAndReturnsMessage()
    {
        var userId = Guid.NewGuid();
        SetupConvState(userId, ConversationState.AwaitingConfirmation, "update_profile");

        var result = await _sut.ProcessMessageAsync(userId, "no");

        Assert.Contains("No changes", result.Message);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public async Task Confirmation_ExpiredState_ResetsGracefully()
    {
        var userId = Guid.NewGuid();
        // Simulate TTL expiry: state is AwaitingConfirmation but PendingChanges is empty
        SetupConvState(userId, ConversationState.AwaitingConfirmation, activeIntent: null, pendingChanges: new BsonDocument());

        var result = await _sut.ProcessMessageAsync(userId, "yes");

        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════
    //  FSM: Delete phrase flow
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeletePhrase_ExactMatch_DeletesRecommendation()
    {
        var userId = Guid.NewGuid();
        SetupConvState(userId, ConversationState.AwaitingDeletePhrase, "delete_recommendation");
        _recRepoMock.Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(new RecommendationDocument { UserId = userId, Name = "Test Rec" });

        var result = await _sut.ProcessMessageAsync(userId, "DELETE MY RECOMMENDATION");

        Assert.Contains("permanently deleted", result.Message);
        _recRepoMock.Verify(r => r.DeleteByUserIdAsync(userId), Times.Once);
    }

    [Fact]
    public async Task DeletePhrase_WrongPhrase_CancelsDeletion()
    {
        var userId = Guid.NewGuid();
        SetupConvState(userId, ConversationState.AwaitingDeletePhrase, "delete_recommendation");

        var result = await _sut.ProcessMessageAsync(userId, "some random text");

        Assert.Contains("cancelled", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════
    //  FSM: Profile collection validation
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task ProfileCollection_InvalidDob_RePrompts()
    {
        var userId = Guid.NewGuid();
        SetupCollectingProfileState(userId, "_step", "dob");

        var result = await _sut.ProcessMessageAsync(userId, "not a date");

        Assert.Contains("valid date", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProfileCollection_InvalidGender_RePrompts()
    {
        var userId = Guid.NewGuid();
        SetupCollectingProfileState(userId, "_step", "gender");

        var result = await _sut.ProcessMessageAsync(userId, "X");

        Assert.Contains("Male", result.Message);
    }

    [Fact]
    public async Task ProfileCollection_InvalidZip_RePrompts()
    {
        var userId = Guid.NewGuid();
        SetupCollectingProfileState(userId, "_step", "zip");

        var result = await _sut.ProcessMessageAsync(userId, "abc");

        Assert.Contains("valid 5-digit", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProfileCollection_LifeExpectancy_OutOfRange_RePrompts()
    {
        var userId = Guid.NewGuid();
        SetupCollectingProfileState(userId, "_step", "lifeExpectancy");

        var result = await _sut.ProcessMessageAsync(userId, "45");

        Assert.Contains("between", result.Message);
    }

    [Fact]
    public async Task ProfileCollection_NameTooShort_RePrompts()
    {
        var userId = Guid.NewGuid();
        SetupCollectingProfileState(userId, "_step", "name");

        var result = await _sut.ProcessMessageAsync(userId, "John");

        Assert.Contains("first and last name", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Handler: No recommendation guard
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task ViewSummary_NoRecommendation_ReturnsCreatePrompt()
    {
        var userId = Guid.NewGuid();
        SetupIdleConvState(userId);
        SetupChatClientResponse("{\"intent\":\"view_summary\",\"params\":{}}");
        _recRepoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((RecommendationDocument?)null);

        var result = await _sut.ProcessMessageAsync(userId, "show my summary");

        // Should mention creating a recommendation
        Assert.Contains("don't have", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteRecommendation_NoRecommendation_ReturnsMessage()
    {
        var userId = Guid.NewGuid();
        SetupIdleConvState(userId);
        SetupChatClientResponse("{\"intent\":\"delete_recommendation\",\"params\":{}}");
        _recRepoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((RecommendationDocument?)null);

        var result = await _sut.ProcessMessageAsync(userId, "delete my recommendation");

        Assert.Contains("don't have", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Handler: View summary with data
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task ViewSummary_WithRecommendation_ReturnsSummary()
    {
        var userId = Guid.NewGuid();
        SetupIdleConvState(userId);
        SetupChatClientResponse("{\"intent\":\"view_summary\",\"params\":{}}");

        var rec = CreateTestRecommendation(userId);
        _recRepoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(rec);

        var result = await _sut.ProcessMessageAsync(userId, "show my summary");

        Assert.Contains("Summary", result.Message);
        Assert.Contains("John Doe", result.Message);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Handler: Help returns displayData
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Help_ReturnsHelpMenuDisplayData()
    {
        var userId = Guid.NewGuid();
        SetupIdleConvState(userId);
        SetupChatClientResponse("{\"intent\":\"help\",\"params\":{}}");

        var result = await _sut.ProcessMessageAsync(userId, "help");

        Assert.Equal("help_menu", result.DisplayData?.Type);
        Assert.Contains("RECOMMENDATION", result.Message);
        Assert.Contains("PROFILE", result.Message);
        Assert.Contains("DRUGS", result.Message);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Setup Helpers
    // ═══════════════════════════════════════════════════════════════

    private void SetupIdleConvState(Guid userId)
    {
        _convStateRepoMock
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(new ConvStateDocument
            {
                UserId = userId,
                State = ConversationState.Idle
            });
    }

    private void SetupConvState(Guid userId, ConversationState state, string? activeIntent,
        BsonDocument? pendingChanges = null)
    {
        _convStateRepoMock
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(new ConvStateDocument
            {
                UserId = userId,
                State = state,
                ActiveIntent = activeIntent,
                PendingChanges = pendingChanges ?? new BsonDocument { ["changeType"] = "update_profile" }
            });
    }

    private void SetupCollectingProfileState(Guid userId, string fieldName, string fieldValue)
    {
        var fields = new BsonDocument { [fieldName] = fieldValue };
        _convStateRepoMock
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(new ConvStateDocument
            {
                UserId = userId,
                State = ConversationState.CollectingProfile,
                ActiveIntent = "create_recommendation",
                CollectedFields = fields
            });
    }

    private void SetupChatClientResponse(string responseJson)
    {
        var chatResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, responseJson));
        _chatClientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);
    }

    private static RecommendationDocument CreateTestRecommendation(Guid userId) => new()
    {
        UserId = userId,
        Name = "Medicare Plan — Apr 2026",
        Profile = new ProfileSnapshot
        {
            RecommendationName = "Medicare Plan — Apr 2026",
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateOnly(1960, 5, 15),
            Gender = "M",
            ZipCode = "33140",
            County = "Miami-Dade",
            CountyCode = "12086",
            State = "FL",
            City = "Miami Beach",
            HealthCondition = 2,
            LifeExpectancy = 95,
            TobaccoStatus = 0,
            TaxFilingStatus = "SINGLE",
            MagiTier = "1",
            CoverageYear = 2026,
            Concierge = 0
        }
    };
}
