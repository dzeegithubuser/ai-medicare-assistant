using Domain.Documents;

namespace Domain.Interfaces;

/// <summary>
/// Repository over the <c>userProfiles</c> collection.
/// </summary>
public interface IProfileRepository
{
    Task<ProfileDocument?> GetByUserIdAsync(Guid userId);
    Task<ProfileDocument> CreateAsync(ProfileDocument entity);
    Task UpdateAsync(ProfileDocument entity);
    Task<bool> ExistsByUserIdAsync(Guid userId);
    Task DeleteByUserIdAsync(Guid userId);
}
