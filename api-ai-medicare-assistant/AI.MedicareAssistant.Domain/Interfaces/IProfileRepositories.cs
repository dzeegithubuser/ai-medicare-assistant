using Domain.Documents;

namespace Domain.Interfaces;

/// <summary>
/// Profile repository operating on the profile fields embedded in <see cref="UserDocument"/>.
/// </summary>
public interface IProfileRepository
{
    Task<UserDocument?> GetByUserIdAsync(Guid userId);
    Task<UserDocument> CreateAsync(UserDocument entity);
    Task UpdateAsync(UserDocument entity);
    Task<bool> ExistsByUserIdAsync(Guid userId);
}
