using Application.DTOs;
using Application.Services;
using Domain.Documents;
using Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace AI.MedicareAssistant.Tests;

/// <summary>
/// Tests for AuthService — signup, signin, forgot-password, reset-password.
/// </summary>
public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "TestSecretKeyAtLeast32CharactersLong!@#ForUnitTests",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Jwt:ExpiryHours"] = "1"
            })
            .Build();

        _sut = new AuthService(
            _userRepoMock.Object,
            _config,
            Mock.Of<ILogger<AuthService>>(),
            _cache,
            Mock.Of<IEmailService>());
    }

    // ═══════ SignUp ═══════

    [Fact]
    public async Task SignUp_ValidRequest_ReturnsSuccess()
    {
        _userRepoMock.Setup(r => r.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userRepoMock.Setup(r => r.PhoneExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userRepoMock.Setup(r => r.CreateAsync(It.IsAny<UserDocument>()))
            .ReturnsAsync((UserDocument u) => u);

        var result = await _sut.SignUpAsync(new SignUpRequest
        {
            Email = "test@example.com",
            Phone = "5551234567",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        });

        Assert.True(result.Success);
        Assert.Contains("Registration successful", result.Message);
        _userRepoMock.Verify(r => r.CreateAsync(It.IsAny<UserDocument>()), Times.Once);
    }

    [Fact]
    public async Task SignUp_DuplicateEmail_ReturnsFailure()
    {
        _userRepoMock.Setup(r => r.EmailExistsAsync("dupe@example.com")).ReturnsAsync(true);

        var result = await _sut.SignUpAsync(new SignUpRequest
        {
            Email = "dupe@example.com",
            Phone = "5551234567",
            Password = "Password123!"
        });

        Assert.False(result.Success);
        Assert.Contains("Email already", result.Message);
        _userRepoMock.Verify(r => r.CreateAsync(It.IsAny<UserDocument>()), Times.Never);
    }

    [Fact]
    public async Task SignUp_DuplicatePhone_ReturnsFailure()
    {
        _userRepoMock.Setup(r => r.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userRepoMock.Setup(r => r.PhoneExistsAsync("5551234567")).ReturnsAsync(true);

        var result = await _sut.SignUpAsync(new SignUpRequest
        {
            Email = "new@example.com",
            Phone = "5551234567",
            Password = "Password123!"
        });

        Assert.False(result.Success);
        Assert.Contains("Phone", result.Message);
    }

    [Fact]
    public async Task SignUp_NormalizesEmailToLowercase()
    {
        _userRepoMock.Setup(r => r.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userRepoMock.Setup(r => r.PhoneExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userRepoMock.Setup(r => r.CreateAsync(It.IsAny<UserDocument>()))
            .ReturnsAsync((UserDocument u) => u);

        await _sut.SignUpAsync(new SignUpRequest
        {
            Email = "TEST@EXAMPLE.COM",
            Phone = "5551234567",
            Password = "Password123!"
        });

        _userRepoMock.Verify(r => r.CreateAsync(
            It.Is<UserDocument>(u => u.Email == "test@example.com")), Times.Once);
    }

    [Fact]
    public async Task SignUp_HashesPassword()
    {
        _userRepoMock.Setup(r => r.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userRepoMock.Setup(r => r.PhoneExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userRepoMock.Setup(r => r.CreateAsync(It.IsAny<UserDocument>()))
            .ReturnsAsync((UserDocument u) => u);

        await _sut.SignUpAsync(new SignUpRequest
        {
            Email = "test@example.com",
            Phone = "5551234567",
            Password = "Password123!"
        });

        _userRepoMock.Verify(r => r.CreateAsync(
            It.Is<UserDocument>(u => u.PasswordHash != "Password123!" && u.PasswordHash.StartsWith("$2"))),
            Times.Once);
    }

    // ═══════ SignIn ═══════

    [Fact]
    public async Task SignIn_ValidCredentials_ReturnsSuccessWithToken()
    {
        var user = CreateTestUser("test@example.com", "Password123!");
        _userRepoMock.Setup(r => r.GetByEmailAsync("test@example.com")).ReturnsAsync(user);

        var result = await _sut.SignInAsync(new SignInRequest
        {
            Email = "test@example.com",
            Password = "Password123!"
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Token);
        Assert.NotEmpty(result.Token);
    }

    [Fact]
    public async Task SignIn_WrongPassword_ReturnsFailure()
    {
        var user = CreateTestUser("test@example.com", "Password123!");
        _userRepoMock.Setup(r => r.GetByEmailAsync("test@example.com")).ReturnsAsync(user);

        var result = await _sut.SignInAsync(new SignInRequest
        {
            Email = "test@example.com",
            Password = "WrongPassword!"
        });

        Assert.False(result.Success);
        Assert.Contains("Invalid", result.Message);
    }

    [Fact]
    public async Task SignIn_NonexistentUser_ReturnsFailure()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((UserDocument?)null);

        var result = await _sut.SignInAsync(new SignInRequest
        {
            Email = "nobody@example.com",
            Password = "Password123!"
        });

        Assert.False(result.Success);
        Assert.Contains("Invalid", result.Message);
    }

    [Fact]
    public async Task SignIn_AlwaysQueriesRepository()
    {
        var user = CreateTestUser("test@example.com", "Pass123!");
        _userRepoMock.Setup(r => r.GetByEmailAsync("test@example.com")).ReturnsAsync(user);

        // Two sign-in calls — both should hit the repo (no caching of password hashes)
        await _sut.SignInAsync(new SignInRequest { Email = "test@example.com", Password = "Pass123!" });
        await _sut.SignInAsync(new SignInRequest { Email = "test@example.com", Password = "Pass123!" });

        _userRepoMock.Verify(r => r.GetByEmailAsync("test@example.com"), Times.Exactly(2));
    }

    // ═══════ ForgotPassword ═══════

    [Fact]
    public async Task ForgotPassword_ExistingEmail_ReturnsSuccess()
    {
        var user = CreateTestUser("test@example.com", "Pass123!");
        _userRepoMock.Setup(r => r.GetByEmailAsync("test@example.com")).ReturnsAsync(user);

        var result = await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "test@example.com" });

        Assert.True(result.Success);
        Assert.Contains("reset email", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForgotPassword_NonexistentEmail_StillReturnsSuccess()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((UserDocument?)null);

        var result = await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "nobody@example.com" });

        Assert.True(result.Success);
        Assert.Null(result.Token); // no token generated for nonexistent email
    }

    // ═══════ ResetPassword ═══════

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsFailure()
    {
        var result = await _sut.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = "completely-invalid-token",
            NewPassword = "NewPassword456!",
            ConfirmPassword = "NewPassword456!"
        });

        Assert.False(result.Success);
        Assert.Contains("Invalid", result.Message);
    }

    // ═══════ Helpers ═══════

    private static UserDocument CreateTestUser(string email, string password) => new()
    {
        UserId = Guid.NewGuid(),
        Email = email.ToLowerInvariant(),
        Phone = "5551234567",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        IsEmailVerified = true
    };
}
