using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class FpSummaryDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Phone { get; set; } = "";
    public Guid? FpgId { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateFpRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    [Required, MaxLength(50)]
    public string FirstName { get; set; } = "";

    [Required, MaxLength(50)]
    public string LastName { get; set; } = "";

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = "";

    [Required, MaxLength(20)]
    [RegularExpression(@"^(\+1[\s.\-]?)?(\(?\d{3}\)?[\s.\-]?)(\d{3}[\s.\-]?\d{4})$",
        ErrorMessage = "Enter a valid US phone number (e.g. (555) 123-4567).")]
    public string Phone { get; set; } = "";
}

public class UpdateFpRequest
{
    [Required, MaxLength(50)]
    public string FirstName { get; set; } = "";

    [Required, MaxLength(50)]
    public string LastName { get; set; } = "";

    [Required, MaxLength(20)]
    [RegularExpression(@"^(\+1[\s.\-]?)?(\(?\d{3}\)?[\s.\-]?)(\d{3}[\s.\-]?\d{4})$",
        ErrorMessage = "Enter a valid US phone number (e.g. (555) 123-4567).")]
    public string Phone { get; set; } = "";
}
