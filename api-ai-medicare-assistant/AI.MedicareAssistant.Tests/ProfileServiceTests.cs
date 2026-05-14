using Application.DTOs;
using Application.Services;
using Domain.Documents;
using Domain.Exceptions;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace AI.MedicareAssistant.Tests;

/// <summary>
/// Tests for ProfileService — get, save (create + update), mapping.
/// </summary>
public class ProfileServiceTests
{
    private readonly Mock<IProfileRepository> _profileRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly ProfileService _sut;

    public ProfileServiceTests()
    {
        _profileRepoMock = new Mock<IProfileRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _sut = new ProfileService(
            _profileRepoMock.Object,
            _userRepoMock.Object,
            Mock.Of<ILogger<ProfileService>>());
    }

    // ═══════ GetProfileAsync ═══════

    [Fact]
    public async Task GetProfile_ExistingProfile_ReturnsComplete()
    {
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(CreateTestUser(userId));
        _profileRepoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(CreateTestProfile(userId));

        var result = await _sut.GetProfileAsync(userId);

        Assert.True(result.IsProfileComplete);
        Assert.NotNull(result.Profile);
        Assert.Equal("John", result.Profile.FirstName);
        Assert.Equal("Doe", result.Profile.LastName);
    }

    [Fact]
    public async Task GetProfile_NoProfile_ReturnsIncomplete()
    {
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(CreateTestUser(userId));
        _profileRepoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((ProfileDocument?)null);

        var result = await _sut.GetProfileAsync(userId);

        Assert.False(result.IsProfileComplete);
        Assert.Null(result.Profile);
    }

    [Fact]
    public async Task GetProfile_RepoThrows_PropagatesException()
    {
        var userId = Guid.NewGuid();
        _profileRepoMock.Setup(r => r.GetByUserIdAsync(userId)).ThrowsAsync(new Exception("DB down"));

        await Assert.ThrowsAsync<Exception>(() => _sut.GetProfileAsync(userId));
    }

    // ═══════ SaveAsync ═══════

    [Fact]
    public async Task Save_ExistingUserAndProfile_UpdatesBoth()
    {
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(CreateTestUser(userId));
        _profileRepoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(CreateTestProfile(userId));

        await _sut.SaveAsync(userId, CreateTestDto());

        _userRepoMock.Verify(r => r.UpdateAsync(It.Is<UserDocument>(u =>
            u.UserId == userId && u.FirstName == "John" && u.LastName == "Doe")), Times.Once);
        _profileRepoMock.Verify(r => r.UpdateAsync(It.Is<ProfileDocument>(p =>
            p.UserId == userId && p.ZipCode == "80113" && p.IsProfileComplete)), Times.Once);
    }

    [Fact]
    public async Task Save_NoUserDocument_ThrowsNotFound()
    {
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync((UserDocument?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.SaveAsync(userId, CreateTestDto()));
    }

    [Fact]
    public async Task Save_NoProfileYet_CreatesNewProfileViaUpdateUpsert()
    {
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(CreateTestUser(userId));
        _profileRepoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((ProfileDocument?)null);

        await _sut.SaveAsync(userId, CreateTestDto());

        _profileRepoMock.Verify(r => r.UpdateAsync(It.Is<ProfileDocument>(p =>
            p.UserId == userId && p.IsProfileComplete)), Times.Once);
    }

    [Fact]
    public async Task Save_ReturnsMappedDto()
    {
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(CreateTestUser(userId));
        _profileRepoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(CreateTestProfile(userId));

        var result = await _sut.SaveAsync(userId, CreateTestDto());

        Assert.Equal("John", result.FirstName);
        Assert.Equal("Doe", result.LastName);
        Assert.Equal("80113", result.ZipCode);
    }

    [Fact]
    public async Task Save_SetsDateOfBirth()
    {
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(CreateTestUser(userId));
        _profileRepoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(CreateTestProfile(userId));

        await _sut.SaveAsync(userId, CreateTestDto());

        _profileRepoMock.Verify(r => r.UpdateAsync(It.Is<ProfileDocument>(p =>
            p.DateOfBirth == "1958-01-15")), Times.Once);
    }

    // ═══════ MapToDto ═══════

    [Fact]
    public void MapToDto_MapsAllFields()
    {
        var user = CreateTestUser(Guid.NewGuid());
        var profile = CreateTestProfile(user.UserId);
        var dto = ProfileService.MapToDto(user, profile);

        Assert.Equal(user.FirstName, dto.FirstName);
        Assert.Equal(user.LastName, dto.LastName);
        Assert.Equal(profile.CoverageYear, dto.CoverageYear);
        Assert.Equal(profile.Gender, dto.Gender);
        Assert.Equal(profile.ZipCode, dto.ZipCode);
        Assert.Equal(profile.County, dto.County);
        Assert.Equal(profile.DateOfBirth, dto.DateOfBirth);
    }

    // ═══════ Helpers ═══════

    private static ProfileDto CreateTestDto() => new()
    {
        FirstName = "John",
        LastName = "Doe",
        CoverageYear = 2026,
        HealthCondition = 3,
        TaxFilingStatus = "Single",
        MagiTier = "Tier 3",
        Gender = "M",
        TobaccoStatus = 0,
        DateOfBirth = "1958-01-15",
        Concierge = 0,
        LifeExpectancy = 95,
        AddressLine1 = "123 Main St",
        City = "Englewood",
        State = "CO",
        ZipCode = "80113",
        County = "Arapahoe",
        CountyCode = "08005",
        Latitude = 39.6478,
        Longitude = -104.9878
    };

    private static UserDocument CreateTestUser(Guid userId) => new()
    {
        UserId = userId,
        Email = "test@example.com",
        Phone = "5551234567",
        PasswordHash = "hashed",
        FirstName = "John",
        LastName = "Doe"
    };

    private static ProfileDocument CreateTestProfile(Guid userId) => new()
    {
        UserId = userId,
        IsProfileComplete = true,
        CoverageYear = 2026,
        HealthCondition = 3,
        TaxFilingStatus = "Single",
        MagiTier = "Tier 3",
        Gender = "M",
        TobaccoStatus = 0,
        DateOfBirth = "1958-01-15",
        Concierge = 0,
        LifeExpectancy = 95,
        AddressLine1 = "123 Main St",
        City = "Englewood",
        State = "CO",
        ZipCode = "80113",
        County = "Arapahoe",
        CountyCode = "08005",
        Latitude = 39.6478,
        Longitude = -104.9878
    };
}
