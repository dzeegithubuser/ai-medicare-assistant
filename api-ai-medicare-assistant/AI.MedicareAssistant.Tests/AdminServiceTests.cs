using Application.DTOs;
using Application.Services;
using Domain.Constants;
using Domain.Documents;
using Domain.Exceptions;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace AI.MedicareAssistant.Tests;

/// <summary>
/// Tests for AdminService — focused on the direct FPG-admin endpoints that hide the
/// group concept from the UI (CreateFpgAdminUserAsync + ListFpgAdminUsersAsync).
/// </summary>
public class AdminServiceTests
{
    private readonly Mock<IFinancialPlannerGroupRepository> _fpgRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly AdminService _sut;

    public AdminServiceTests()
    {
        _sut = new AdminService(
            _fpgRepoMock.Object,
            _userRepoMock.Object,
            Mock.Of<ILogger<AdminService>>());
    }

    [Fact]
    public async Task CreateFpgAdminUser_NewEmail_AutoCreatesGroupAndUser()
    {
        _userRepoMock.Setup(r => r.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userRepoMock.Setup(r => r.PhoneExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _fpgRepoMock.Setup(r => r.ExistsByNameAsync(It.IsAny<string>())).ReturnsAsync(false);
        _fpgRepoMock.Setup(r => r.CreateAsync(It.IsAny<FinancialPlannerGroupDocument>()))
            .ReturnsAsync((FinancialPlannerGroupDocument d) => d);
        _userRepoMock.Setup(r => r.CreateAsync(It.IsAny<UserDocument>()))
            .ReturnsAsync((UserDocument u) => u);

        var result = await _sut.CreateFpgAdminUserAsync(new CreateFpgAdminUserRequest
        {
            Email = "Jane.Doe@example.com",
            FirstName = "Jane",
            LastName = "Doe",
            Password = "Password123!"
        });

        Assert.Equal("jane.doe@example.com", result.Email);
        Assert.Equal(UserRoles.FinancialPlannerGroup, result.Role);
        Assert.NotNull(result.FpgId);
        Assert.True(result.MustChangePassword);

        _fpgRepoMock.Verify(r => r.CreateAsync(It.Is<FinancialPlannerGroupDocument>(d => d.Name == "Jane Doe")), Times.Once);
        _userRepoMock.Verify(r => r.CreateAsync(It.Is<UserDocument>(u =>
            u.Email == "jane.doe@example.com"
            && u.Role == UserRoles.FinancialPlannerGroup
            && u.FpgId.HasValue
            && u.MustChangePassword)), Times.Once);
    }

    [Fact]
    public async Task CreateFpgAdminUser_DuplicateEmail_Throws()
    {
        _userRepoMock.Setup(r => r.EmailExistsAsync("dup@example.com")).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.CreateFpgAdminUserAsync(new CreateFpgAdminUserRequest
        {
            Email = "dup@example.com",
            FirstName = "Jane",
            LastName = "Doe",
            Password = "Password123!"
        }));

        _fpgRepoMock.Verify(r => r.CreateAsync(It.IsAny<FinancialPlannerGroupDocument>()), Times.Never);
    }

    [Fact]
    public async Task CreateFpgAdminUser_GroupNameCollision_AppendsSuffix()
    {
        _userRepoMock.Setup(r => r.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userRepoMock.Setup(r => r.PhoneExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _fpgRepoMock.Setup(r => r.ExistsByNameAsync("Jane Doe")).ReturnsAsync(true);
        _fpgRepoMock.Setup(r => r.ExistsByNameAsync("Jane Doe 2")).ReturnsAsync(false);
        _fpgRepoMock.Setup(r => r.CreateAsync(It.IsAny<FinancialPlannerGroupDocument>()))
            .ReturnsAsync((FinancialPlannerGroupDocument d) => d);
        _userRepoMock.Setup(r => r.CreateAsync(It.IsAny<UserDocument>()))
            .ReturnsAsync((UserDocument u) => u);

        await _sut.CreateFpgAdminUserAsync(new CreateFpgAdminUserRequest
        {
            Email = "jane2@example.com",
            FirstName = "Jane",
            LastName = "Doe",
            Password = "Password123!"
        });

        _fpgRepoMock.Verify(r => r.CreateAsync(It.Is<FinancialPlannerGroupDocument>(d => d.Name == "Jane Doe 2")), Times.Once);
    }

    [Fact]
    public async Task DeleteFpgAdminUser_GroupEmpty_DeletesUserAndGroup()
    {
        var userId = Guid.NewGuid();
        var fpgId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new UserDocument
        {
            UserId = userId, FpgId = fpgId, Role = UserRoles.FinancialPlannerGroup, Email = "fpg@x.com"
        });
        _userRepoMock.Setup(r => r.GetByFpgIdAndRoleAsync(fpgId, UserRoles.FinancialPlanner))
            .ReturnsAsync(new List<UserDocument>());

        await _sut.DeleteFpgAdminUserAsync(userId);

        _fpgRepoMock.Verify(r => r.DeleteAsync(fpgId), Times.Once);
        _userRepoMock.Verify(r => r.DeleteAsync(userId), Times.Once);
    }

    [Fact]
    public async Task DeleteFpgAdminUser_GroupHasFps_ThrowsConflict()
    {
        var userId = Guid.NewGuid();
        var fpgId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new UserDocument
        {
            UserId = userId, FpgId = fpgId, Role = UserRoles.FinancialPlannerGroup, Email = "fpg@x.com"
        });
        _userRepoMock.Setup(r => r.GetByFpgIdAndRoleAsync(fpgId, UserRoles.FinancialPlanner))
            .ReturnsAsync(new List<UserDocument>
            {
                new() { UserId = Guid.NewGuid(), Role = UserRoles.FinancialPlanner }
            });

        await Assert.ThrowsAsync<ConflictException>(() => _sut.DeleteFpgAdminUserAsync(userId));

        _fpgRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        _userRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeleteFpgAdminUser_WrongRole_ThrowsUnauthorized()
    {
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new UserDocument
        {
            UserId = userId, Role = UserRoles.User
        });

        await Assert.ThrowsAsync<UnauthorizedException>(() => _sut.DeleteFpgAdminUserAsync(userId));
    }

    [Fact]
    public async Task ListFpgAdminUsers_ReturnsUsersWithRoleFpg_OrderedByNewest()
    {
        var older = new UserDocument
        {
            UserId = Guid.NewGuid(), Email = "a@x.com", FirstName = "A", LastName = "One",
            Role = UserRoles.FinancialPlannerGroup, CreatedAt = new DateTime(2025, 1, 1),
            FpgId = Guid.NewGuid()
        };
        var newer = new UserDocument
        {
            UserId = Guid.NewGuid(), Email = "b@x.com", FirstName = "B", LastName = "Two",
            Role = UserRoles.FinancialPlannerGroup, CreatedAt = new DateTime(2026, 5, 1),
            FpgId = Guid.NewGuid()
        };
        _userRepoMock.Setup(r => r.GetAllByRoleAsync(UserRoles.FinancialPlannerGroup))
            .ReturnsAsync(new List<UserDocument> { older, newer });

        var result = await _sut.ListFpgAdminUsersAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("b@x.com", result[0].Email);
        Assert.Equal("a@x.com", result[1].Email);
    }
}
