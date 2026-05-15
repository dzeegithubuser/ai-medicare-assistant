using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class FpgSummaryDto
{
    public Guid GroupId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateFpgRequest
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    [MaxLength(500)]
    public string? Description { get; set; }
}

public class CreateFpgAdminUserRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    [Required, MaxLength(50)]
    public string FirstName { get; set; } = "";

    [Required, MaxLength(50)]
    public string LastName { get; set; } = "";

    [Required, MaxLength(20), Phone]
    public string Phone { get; set; } = "";

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = "";
}

/// <summary>Generic role-management user summary returned by admin/FPG endpoints.</summary>
public class UserSummaryDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Role { get; set; } = "";
    public Guid? FpgId { get; set; }
    public Guid? FpId { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
}
