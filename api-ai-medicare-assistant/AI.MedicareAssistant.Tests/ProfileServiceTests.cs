using Application.DTOs;
using Application.Services;
using Domain.Documents;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace AI.MedicareAssistant.Tests;

/// <summary>
/// Tests for ProfileService — get, save (create + update), mapping.
/// </summary>
public class ProfileServiceTests
{
    private readonly Mock<IProfileRepository> _repoMock;
    private readonly ProfileService _sut;

    public ProfileServiceTests()
    {
        _repoMock = new Mock<IProfileRepository>();
        _sut = new ProfileService(_repoMock.Object, Mock.Of<ILogger<ProfileService>>());
    }

    // ═══════ GetProfileAsync ═══════

    [Fact]
    public async Task GetProfile_ExistingProfile_ReturnsComplete()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(CreateTestUserDocument(userId));

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
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((UserDocument?)null);

        var result = await _sut.GetProfileAsync(userId);

        Assert.False(result.IsProfileComplete);
        Assert.Null(result.Profile);
    }

    [Fact]
    public async Task GetProfile_RepoThrows_PropagatesException()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ThrowsAsync(new Exception("DB down"));

        await Assert.ThrowsAsync<Exception>(() => _sut.GetProfileAsync(userId));
    }

    // ═══════ SaveAsync ═══════

    [Fact]
    public async Task Save_ExistingUser_CallsUpdate()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(CreateTestUserDocument(userId));

        await _sut.SaveAsync(userId, CreateTestDto());

        _repoMock.Verify(r => r.UpdateAsync(It.Is<UserDocument>(p =>
            p.UserId == userId && p.FirstName == "John")), Times.Once);
    }

    [Fact]
    public async Task Save_NoUserDocument_ThrowsInvalidOperation()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((UserDocument?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SaveAsync(userId, CreateTestDto()));
    }

    [Fact]
    public async Task Save_ReturnsMappedDto()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(CreateTestUserDocument(userId));

        var result = await _sut.SaveAsync(userId, CreateTestDto());

        Assert.Equal("John", result.FirstName);
        Assert.Equal("Doe", result.LastName);
        Assert.Equal("80113", result.ZipCode);
    }

    [Fact]
    public async Task Save_SetsDateOfBirth()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(CreateTestUserDocument(userId));

        await _sut.SaveAsync(userId, CreateTestDto());

        _repoMock.Verify(r => r.UpdateAsync(It.Is<UserDocument>(p =>
            p.DateOfBirth == "1958-01-15")), Times.Once);
    }

    // ═══════ MapToDto ═══════

    [Fact]
    public void MapToDto_MapsAllFields()
    {
        var entity = CreateTestUserDocument(Guid.NewGuid());
        entity.IsProfileComplete = true;
        var dto = ProfileService.MapToDto(entity);

        Assert.Equal(entity.FirstName, dto.FirstName);
        Assert.Equal(entity.LastName, dto.LastName);
        Assert.Equal(entity.CoverageYear, dto.CoverageYear);
        Assert.Equal(entity.Gender, dto.Gender);
        Assert.Equal(entity.ZipCode, dto.ZipCode);
        Assert.Equal(entity.County, dto.County);
        Assert.Equal(entity.DateOfBirth, dto.DateOfBirth);
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

    private static UserDocument CreateTestUserDocument(Guid userId) => new()
    {
        UserId = userId,
        Email = "test@example.com",
        Phone = "5551234567",
        PasswordHash = "hashed",
        IsProfileComplete = true,
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
}
