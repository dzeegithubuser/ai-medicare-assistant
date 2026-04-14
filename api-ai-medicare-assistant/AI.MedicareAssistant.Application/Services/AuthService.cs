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

    public AuthService(
        IUserRepository userRepo,
        IConfiguration config,
        ILogger<AuthService> logger,
        IMemoryCache cache)
    {
        _userRepo = userRepo;
        _config = config;
        _logger = logger;
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
            return new AuthResponse { Success = true, Message = "If that email is registered, a reset token has been generated." };
        }

        var resetToken = GeneratePasswordResetToken(user.Id);

        // In production, send this token via email. For now, return it in the response.
        return new AuthResponse
        {
            Success = true,
            Message = "Password reset token generated. In production, this would be sent via email.",
            Token = resetToken
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

        _logger.LogInformation("Password reset successful for user {UserId}", userId);

        return new AuthResponse { Success = true, Message = "Password reset successfully." };
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
