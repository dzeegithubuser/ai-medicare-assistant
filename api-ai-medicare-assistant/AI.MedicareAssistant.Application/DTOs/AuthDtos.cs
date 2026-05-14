using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class SignInRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}

public class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
}

public class ResetPasswordRequest
{
    [Required]
    public string Token { get; set; } = "";

    [Required, MinLength(8), MaxLength(128)]
    public string NewPassword { get; set; } = "";

    [Required, Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = "";
}

public class ChangePasswordRequest
{
    [Required]
    public string OldPassword { get; set; } = "";

    [Required, MinLength(8), MaxLength(128)]
    public string NewPassword { get; set; } = "";

    [Required, Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = "";
}

public class AuthResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? Token { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public UserDto? User { get; set; }
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Role { get; set; } = "";
    public Guid? FpgId { get; set; }
    public Guid? FpId { get; set; }
    public bool MustChangePassword { get; set; }
}

public class VerifyEmailRequest
{
    [Required]
    public string Token { get; set; } = "";
}

public class ResendVerificationRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
}
