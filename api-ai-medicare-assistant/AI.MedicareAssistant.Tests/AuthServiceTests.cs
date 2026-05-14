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
/// Tests for AuthService — signin, forgot-password, reset-password.
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
            Mock.Of<IEmailService>(),
            new JwtTokenIssuer(_config));
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
