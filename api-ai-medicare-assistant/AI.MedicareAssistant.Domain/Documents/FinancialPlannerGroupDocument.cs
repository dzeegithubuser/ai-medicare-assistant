using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Documents;

/// <summary>
/// MongoDB document representing a Financial Planner Group — a tenant that owns one or more
/// users with role <c>financial_planner</c>. Collection: <c>financialPlannerGroups</c>.
/// </summary>
public class FinancialPlannerGroupDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    /// <summary>Application-level Guid kept for FK compatibility with other collections.</summary>
    public Guid GroupId { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "system";
    public string ModifiedBy { get; set; } = "system";
}
