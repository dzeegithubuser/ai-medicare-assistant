using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class EndUserSummaryDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Phone { get; set; } = "";
    public Guid? FpId { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateEndUserRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    [Required, MaxLength(50)]
    public string FirstName { get; set; } = "";

    [Required, MaxLength(50)]
    public string LastName { get; set; } = "";

    [Required, MaxLength(20), Phone]
    public string Phone { get; set; } = "";

    /// <summary>
    /// Initial password chosen by the FP. The end-user is forced to change it on first sign-in
    /// (<c>MustChangePassword = true</c>), matching the admin/FPG/FP creation pattern.
    /// </summary>
    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = "";
}

public class RecommendationSummaryDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public string Type { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class RecommendationByUserDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public List<RecommendationSummaryDto> Recommendations { get; set; } = new();
}
