using Domain.Constants;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Documents;

/// <summary>
/// MongoDB document for login / identity. Collection: <c>users</c>.
/// Personal, medical, and address data live on <see cref="ProfileDocument"/> in <c>userProfiles</c>,
/// joined by <see cref="UserId"/>.
/// </summary>
[BsonIgnoreExtraElements]
public class UserDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    /// <summary>Application-level Guid kept for FK compatibility with other collections.</summary>
    public Guid UserId { get; set; } = Guid.NewGuid();

    // ── Login credentials ──

    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsEmailVerified { get; set; }

    /// <summary>True when the account was created with a default password and must reset on next sign-in.</summary>
    public bool MustChangePassword { get; set; }

    // ── Display identity (set at user-creation; editable through the profile screen) ──

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";

    // ── Role & hierarchy ──

    /// <summary>Application role: <c>admin</c>, <c>financial_planner_group</c>, <c>financial_planner</c>, or <c>user</c>.</summary>
    public string Role { get; set; } = UserRoles.User;

    /// <summary>For users with role <c>financial_planner</c>: the FPG they belong to.</summary>
    public Guid? FpgId { get; set; }

    /// <summary>For users with role <c>user</c>: the FP that created them.</summary>
    public Guid? FpId { get; set; }

    // ── Audit fields ──

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "system";
    public string ModifiedBy { get; set; } = "system";
}
