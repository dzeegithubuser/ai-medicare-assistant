using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using Application.DTOs;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Application.Services;

public class AuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IEmailService _emailService;

    public AuthService(
        IUserRepository userRepo,
        IConfiguration config,
        ILogger<AuthService> logger,
        IMemoryCache cache,
        IEmailService emailService)
    {
        _userRepo = userRepo;
        _config = config;
        _logger = logger;
        _cache = cache;
        _emailService = emailService;
    }

    public async Task<AuthResponse> SignUpAsync(SignUpRequest request)
    {
        _logger.LogInformation("Sign-up attempt for email {Email}", request.Email);

        if (await _userRepo.EmailExistsAsync(request.Email))
        {
            _logger.LogWarning("Sign-up failed — email already registered: {Email}", request.Email);
            return new AuthResponse { Success = false, Message = "Email already registered." };
        }

        if (await _userRepo.PhoneExistsAsync(request.Phone))
        {
            _logger.LogWarning("Sign-up failed — phone already registered: {Phone}", request.Phone);
            return new AuthResponse { Success = false, Message = "Phone number already registered." };
        }

        var user = new User
        {
            Email = request.Email.ToLowerInvariant(),
            Phone = request.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedBy = request.Email.ToLowerInvariant(),
            ModifiedBy = request.Email.ToLowerInvariant()
        };

        await _userRepo.CreateAsync(user);

        var token = GenerateJwtToken(user);

        _logger.LogInformation("User registered successfully: {UserId} ({Email})", user.Id, user.Email);

        return new AuthResponse
        {
            Success = true,
            Message = "Account created successfully.",
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(GetTokenExpiryHours()),
            User = MapToDto(user)
        };
    }

    public async Task<AuthResponse> SignInAsync(SignInRequest request)
    {
        _logger.LogInformation("Sign-in attempt for email {Email}", request.Email);
        var totalSw = Stopwatch.StartNew();

        var normalizedEmail = request.Email.ToLowerInvariant();
        var lookupSw = Stopwatch.StartNew();
        var user = await _userRepo.GetByEmailAsync(normalizedEmail);
        lookupSw.Stop();

        var verifySw = Stopwatch.StartNew();
        var isValid = user is not null && BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        verifySw.Stop();

        if (user is null || !isValid)
        {
            totalSw.Stop();
            _logger.LogInformation(
                "Sign-in perf (failed) lookupMs={LookupMs} verifyMs={VerifyMs} totalMs={TotalMs}",
                lookupSw.ElapsedMilliseconds, verifySw.ElapsedMilliseconds, totalSw.ElapsedMilliseconds);
            _logger.LogWarning("Sign-in failed — invalid credentials for {Email}", request.Email);
            return new AuthResponse { Success = false, Message = "Invalid email or password." };
        }

        var token = GenerateJwtToken(user);
        totalSw.Stop();
        _logger.LogInformation(
            "Sign-in perf (success) lookupMs={LookupMs} verifyMs={VerifyMs} totalMs={TotalMs}",
            lookupSw.ElapsedMilliseconds, verifySw.ElapsedMilliseconds, totalSw.ElapsedMilliseconds);

        _logger.LogInformation("User signed in: {UserId} ({Email})", user.Id, user.Email);

        return new AuthResponse
        {
            Success = true,
            Message = "Signed in successfully.",
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(GetTokenExpiryHours()),
            User = MapToDto(user)
        };
    }

    public async Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        _logger.LogInformation("Forgot-password request for email {Email}", request.Email);

        var user = await _userRepo.GetByEmailAsync(request.Email.ToLowerInvariant());

        // Always return success to prevent email enumeration
        if (user is null)
        {
            _logger.LogInformation("Forgot-password — email not found (returning success to prevent enumeration)");
            return new AuthResponse { Success = true, Message = "If that email is registered, you will receive a password reset email shortly." };
        }

        var resetToken = GeneratePasswordResetToken(user.Id);
        var frontendBaseUrl = _config["Email:FrontendBaseUrl"] ?? "http://localhost:4200";
        var resetLink = $"{frontendBaseUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}";

        try
        {
            await _emailService.SendPasswordResetAsync(user.Email, user.Email, resetLink);
            _logger.LogInformation("Password reset email sent to {Email}", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
        }

        return new AuthResponse
        {
            Success = true,
            Message = "If that email is registered, you will receive a password reset email shortly."
        };
    }

    public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        _logger.LogInformation("Password reset attempt");

        var principal = ValidatePasswordResetToken(request.Token);
        if (principal is null)
        {
            _logger.LogWarning("Password reset failed — invalid or expired token");
            return new AuthResponse { Success = false, Message = "Invalid or expired reset token." };
        }

        var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (jti is null)
            return new AuthResponse { Success = false, Message = "Invalid reset token." };

        // Reject if this token has already been used
        var cacheKey = $"used_reset:{jti}";
        if (_cache.TryGetValue(cacheKey, out _))
        {
            _logger.LogWarning("Password reset failed — token already used (jti={Jti})", jti);
            return new AuthResponse { Success = false, Message = "This reset link has already been used. Please request a new one." };
        }

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return new AuthResponse { Success = false, Message = "Invalid reset token." };

        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null)
            return new AuthResponse { Success = false, Message = "User not found." };

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.ModifiedBy = user.Email;
        user.ModifiedDate = DateTime.UtcNow;

        await _userRepo.UpdateAsync(user);

        // Blacklist the token so it cannot be reused within its remaining lifetime
        var tokenExpiry = principal.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        var ttl = tokenExpiry is not null && long.TryParse(tokenExpiry, out var exp)
            ? DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime - DateTime.UtcNow
            : TimeSpan.FromMinutes(30);
        _cache.Set(cacheKey, true, ttl > TimeSpan.Zero ? ttl : TimeSpan.FromSeconds(1));

        _logger.LogInformation("Password reset successful for user {UserId}", userId);

        return new AuthResponse { Success = true, Message = "Password reset successfully." };
    }

    public async Task<AuthResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        _logger.LogInformation("Change-password attempt for user {UserId}", userId);

        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null)
            return new AuthResponse { Success = false, Message = "User not found." };

        if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
        {
            _logger.LogWarning("Change-password failed — incorrect old password for user {UserId}", userId);
            return new AuthResponse { Success = false, Message = "Current password is incorrect." };
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.ModifiedBy = user.Email;
        user.ModifiedDate = DateTime.UtcNow;

        await _userRepo.UpdateAsync(user);

        _logger.LogInformation("Password changed successfully for user {UserId}", userId);

        return new AuthResponse { Success = true, Message = "Password changed successfully." };
    }

    private string GenerateJwtToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(GetTokenExpiryHours()),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GeneratePasswordResetToken(Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("purpose", "password-reset")
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal? ValidatePasswordResetToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!)),
                ValidateIssuer = true,
                ValidIssuer = _config["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _config["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            var purposeClaim = principal.FindFirst("purpose")?.Value;
            return purposeClaim == "password-reset" ? principal : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Password-reset token validation failed");
            return null;
        }
    }

    private int GetTokenExpiryHours() =>
        int.TryParse(_config["Jwt:ExpiryHours"], out var hours) ? hours : 24;

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        Phone = user.Phone
    };
}
