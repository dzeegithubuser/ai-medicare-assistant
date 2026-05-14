using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class ImpersonateRequest
{
    [Required]
    public Guid TargetUserId { get; set; }
}

public class ImpersonationResponse
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }

    /// <summary>The Financial Planner who initiated the impersonation.</summary>
    public Guid ActingAsUserId { get; set; }

    /// <summary>The end-user being impersonated.</summary>
    public Guid TargetUserId { get; set; }

    public string TargetEmail { get; set; } = "";
    public string TargetFirstName { get; set; } = "";
    public string TargetLastName { get; set; } = "";
}
