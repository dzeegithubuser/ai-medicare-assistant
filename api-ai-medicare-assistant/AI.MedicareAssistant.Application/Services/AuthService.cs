using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using Application.DTOs;
using Application.Interfaces;
using Domain.Documents;
using Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IEmailService _emailService;
    private readonly IJwtTokenIssuer _jwt;

    public AuthService(
        IUserRepository userRepo,
        IConfiguration config,
        ILogger<AuthService> logger,
        IMemoryCache cache,
        IEmailService emailService,
        IJwtTokenIssuer jwt)
    {
        _userRepo = userRepo;
        _config = config;
        _logger = logger;
        _cache = cache;
        _emailService = emailService;
        _jwt = jwt;
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

        if (!user!.IsEmailVerified)
        {
            totalSw.Stop();
            _logger.LogWarning("Sign-in blocked — email not verified for {Email}", request.Email);
            return new AuthResponse { Success = false, Message = "Please verify your email address before signing in. Check your inbox for the verification link." };
        }

        var (token, expiresAt) = _jwt.Issue(user);
        totalSw.Stop();
        _logger.LogInformation(
            "Sign-in perf (success) lookupMs={LookupMs} verifyMs={VerifyMs} totalMs={TotalMs}",
            lookupSw.ElapsedMilliseconds, verifySw.ElapsedMilliseconds, totalSw.ElapsedMilliseconds);

        _logger.LogInformation("User signed in: {UserId} ({Email})", user.UserId, user.Email);

        return new AuthResponse
        {
            Success = true,
            Message = "Signed in successfully.",
            Token = token,
            ExpiresAt = expiresAt,
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

        var resetToken = GeneratePasswordResetToken(user.UserId);
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
        user.UpdatedAt = DateTime.UtcNow;

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
        user.MustChangePassword = false;
        user.ModifiedBy = user.Email;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepo.UpdateAsync(user);

        _logger.LogInformation("Password changed successfully for user {UserId}", userId);

        var (token, expiresAt) = _jwt.Issue(user);
        return new AuthResponse
        {
            Success = true,
            Message = "Password changed successfully.",
            Token = token,
            ExpiresAt = expiresAt,
            User = MapToDto(user)
        };
    }

    public async Task<AuthResponse> VerifyEmailAsync(string token)
    {
        _logger.LogInformation("Email verification attempt");

        var principal = ValidateEmailVerificationToken(token);
        if (principal is null)
        {
            _logger.LogWarning("Email verification failed — invalid or expired token");
            return new AuthResponse { Success = false, Message = "Invalid or expired verification link." };
        }

        var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (jti is null)
            return new AuthResponse { Success = false, Message = "Invalid verification token." };

        var cacheKey = $"used_verify:{jti}";
        if (_cache.TryGetValue(cacheKey, out _))
        {
            _logger.LogWarning("Email verification failed — token already used (jti={Jti})", jti);
            return new AuthResponse { Success = false, Message = "This verification link has already been used. Please request a new one." };
        }

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return new AuthResponse { Success = false, Message = "Invalid verification token." };

        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null)
            return new AuthResponse { Success = false, Message = "User not found." };

        if (user.IsEmailVerified)
        {
            _logger.LogInformation("Email already verified for user {UserId}", userId);
            return new AuthResponse { Success = true, Message = "Email verified successfully. You can now sign in." };
        }

        user.IsEmailVerified = true;
        user.ModifiedBy = user.Email;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user);

        var tokenExpiry = principal.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        var ttl = tokenExpiry is not null && long.TryParse(tokenExpiry, out var exp)
            ? DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime - DateTime.UtcNow
            : TimeSpan.FromHours(24);
        _cache.Set(cacheKey, true, ttl > TimeSpan.Zero ? ttl : TimeSpan.FromSeconds(1));

        _logger.LogInformation("Email verified successfully for user {UserId}", userId);

        return new AuthResponse { Success = true, Message = "Email verified successfully. You can now sign in." };
    }

    public async Task<AuthResponse> ResendVerificationAsync(string email)
    {
        _logger.LogInformation("Resend verification email request for {Email}", email);

        var user = await _userRepo.GetByEmailAsync(email.ToLowerInvariant());

        // Anti-enumeration: always return success regardless of whether email exists
        if (user is null || user.IsEmailVerified)
        {
            _logger.LogInformation("Resend verification — user not found or already verified (returning success to prevent enumeration)");
            return new AuthResponse { Success = true, Message = "If your email is registered and unverified, a new verification email has been sent." };
        }

        var verificationToken = GenerateEmailVerificationToken(user);
        var frontendBaseUrl = _config["Email:FrontendBaseUrl"] ?? "http://localhost:4200";
        var verificationLink = $"{frontendBaseUrl}/verify-email?token={Uri.EscapeDataString(verificationToken)}";

        try
        {
            await _emailService.SendEmailVerificationAsync(user.Email, user.Email, verificationLink);
            _logger.LogInformation("Resent verification email to {Email}", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resend verification email to {Email}", user.Email);
        }

        return new AuthResponse { Success = true, Message = "If your email is registered and unverified, a new verification email has been sent." };
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

    private string GenerateEmailVerificationToken(UserDocument user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim("purpose", "email-verification")
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal? ValidateEmailVerificationToken(string token)
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
            return purposeClaim == "email-verification" ? principal : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email-verification token validation failed");
            return null;
        }
    }

    private static string NormalizeUsPhone(string phone)
    {
        // Strip all non-digit characters
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        // If 11 digits starting with 1 (country code), drop the leading 1
        if (digits.Length == 11 && digits.StartsWith('1'))
            digits = digits[1..];

        // Store as plain 10-digit string for consistent uniqueness checks
        return digits.Length == 10 ? digits : phone.Trim();
    }

    private static UserDto MapToDto(UserDocument user) => new()
    {
        Id = user.UserId,
        Email = user.Email,
        Phone = user.Phone,
        Role = user.Role,
        FpgId = user.FpgId,
        FpId = user.FpId,
        MustChangePassword = user.MustChangePassword
    };
}
