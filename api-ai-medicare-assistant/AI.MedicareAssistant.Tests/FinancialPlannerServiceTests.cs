using Application.Services;
using Domain.Constants;
using Domain.Documents;
using Domain.Exceptions;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace AI.MedicareAssistant.Tests;

/// <summary>
/// Tests for FinancialPlannerService — focused on the cascade delete for end-users.
/// </summary>
public class FinancialPlannerServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IRecommendationRepository> _recRepoMock = new();
    private readonly Mock<IProfileRepository> _profileRepoMock = new();
    private readonly Mock<IChatSessionRepository> _chatRepoMock = new();
    private readonly Mock<IUserAnalysisSelectionsRepository> _selectionsRepoMock = new();
    private readonly Mock<ILtcSelectionsRepository> _ltcRepoMock = new();
    private readonly FinancialPlannerService _sut;

    public FinancialPlannerServiceTests()
    {
        _sut = new FinancialPlannerService(
            _userRepoMock.Object,
            _recRepoMock.Object,
            _profileRepoMock.Object,
            _chatRepoMock.Object,
            _selectionsRepoMock.Object,
            _ltcRepoMock.Object,
            Mock.Of<ILogger<FinancialPlannerService>>());
    }

    [Fact]
    public async Task DeleteEndUser_OwnedTarget_CascadesAllPerUserCollections()
    {
        var fpUserId = Guid.NewGuid();
        var endUserId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(endUserId)).ReturnsAsync(new UserDocument
        {
            UserId = endUserId, Role = UserRoles.User, FpId = fpUserId, Email = "end@x.com"
        });

        await _sut.DeleteEndUserAsync(fpUserId, endUserId);

        _profileRepoMock.Verify(r => r.DeleteByUserIdAsync(endUserId), Times.Once);
        _chatRepoMock.Verify(r => r.DeleteByUserIdAsync(endUserId), Times.Once);
        _recRepoMock.Verify(r => r.DeleteByUserIdAsync(endUserId), Times.Once);
        _selectionsRepoMock.Verify(r => r.DeleteByUserIdAsync(endUserId), Times.Once);
        _ltcRepoMock.Verify(r => r.DeleteByUserIdAsync(endUserId), Times.Once);
        _userRepoMock.Verify(r => r.DeleteAsync(endUserId), Times.Once);
    }

    [Fact]
    public async Task DeleteEndUser_TargetBelongsToDifferentFp_ThrowsUnauthorized()
    {
        var fpUserId = Guid.NewGuid();
        var endUserId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(endUserId)).ReturnsAsync(new UserDocument
        {
            UserId = endUserId, Role = UserRoles.User, FpId = Guid.NewGuid(), Email = "end@x.com"
        });

        await Assert.ThrowsAsync<UnauthorizedException>(() => _sut.DeleteEndUserAsync(fpUserId, endUserId));

        _userRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        _profileRepoMock.Verify(r => r.DeleteByUserIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeleteEndUser_TargetMissing_ThrowsNotFound()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((UserDocument?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.DeleteEndUserAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteEndUser_TargetWrongRole_ThrowsUnauthorized()
    {
        var fpUserId = Guid.NewGuid();
        var endUserId = Guid.NewGuid();
        // FpId matches caller but role is "financial_planner" instead of "user"
        _userRepoMock.Setup(r => r.GetByIdAsync(endUserId)).ReturnsAsync(new UserDocument
        {
            UserId = endUserId, Role = UserRoles.FinancialPlanner, FpId = fpUserId
        });

        await Assert.ThrowsAsync<UnauthorizedException>(() => _sut.DeleteEndUserAsync(fpUserId, endUserId));
    }
}
