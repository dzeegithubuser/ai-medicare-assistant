using Application.DTOs;
using Application.Services;
using Domain.Entities;
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
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(CreateTestProfile(userId));

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
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((Profile?)null);

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
    public async Task Save_NewProfile_CallsCreate()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((Profile?)null);

        await _sut.SaveAsync(userId, CreateTestDto());

        _repoMock.Verify(r => r.CreateAsync(It.Is<Profile>(p =>
            p.UserId == userId && p.FirstName == "John")), Times.Once);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Profile>()), Times.Never);
    }

    [Fact]
    public async Task Save_ExistingProfile_CallsUpdate()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(CreateTestProfile(userId));

        await _sut.SaveAsync(userId, CreateTestDto());

        _repoMock.Verify(r => r.UpdateAsync(It.Is<Profile>(p =>
            p.UserId == userId)), Times.Once);
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<Profile>()), Times.Never);
    }

    [Fact]
    public async Task Save_ReturnsMappedDto()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((Profile?)null);

        var result = await _sut.SaveAsync(userId, CreateTestDto());

        Assert.Equal("John", result.FirstName);
        Assert.Equal("Doe", result.LastName);
        Assert.Equal("80113", result.ZipCode);
    }

    [Fact]
    public async Task Save_ParsesDateOfBirth()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((Profile?)null);

        await _sut.SaveAsync(userId, CreateTestDto());

        _repoMock.Verify(r => r.CreateAsync(It.Is<Profile>(p =>
            p.DateOfBirth == new DateOnly(1958, 1, 15))), Times.Once);
    }

    [Fact]
    public async Task Save_InvalidDateOfBirth_ThrowsFormatException()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((Profile?)null);

        var dto = CreateTestDto();
        dto.DateOfBirth = "not-a-date";

        await Assert.ThrowsAsync<FormatException>(() => _sut.SaveAsync(userId, dto));
    }

    // ═══════ MapToDto ═══════

    [Fact]
    public void MapToDto_MapsAllFields()
    {
        var entity = CreateTestProfile(Guid.NewGuid());
        var dto = ProfileService.MapToDto(entity);

        Assert.Equal(entity.FirstName, dto.FirstName);
        Assert.Equal(entity.LastName, dto.LastName);
        Assert.Equal(entity.CoverageYear, dto.CoverageYear);
        Assert.Equal(entity.Gender, dto.Gender);
        Assert.Equal(entity.ZipCode, dto.ZipCode);
        Assert.Equal(entity.County, dto.County);
        Assert.Equal(entity.DateOfBirth.ToString("yyyy-MM-dd"), dto.DateOfBirth);
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

    private static Profile CreateTestProfile(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        FirstName = "John",
        LastName = "Doe",
        CoverageYear = 2026,
        HealthCondition = 3,
        TaxFilingStatus = "Single",
        MagiTier = "Tier 3",
        Gender = "M",
        TobaccoStatus = 0,
        DateOfBirth = new DateOnly(1958, 1, 15),
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
