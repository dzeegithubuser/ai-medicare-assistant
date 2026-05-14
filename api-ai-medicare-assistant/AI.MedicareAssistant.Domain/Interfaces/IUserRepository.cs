using Domain.Documents;

namespace Domain.Interfaces;

public interface IUserRepository
{
    Task<UserDocument?> GetByEmailAsync(string email);
    Task<UserDocument?> GetByPhoneAsync(string phone);
    Task<UserDocument?> GetByIdAsync(Guid id);
    Task<UserDocument> CreateAsync(UserDocument user);
    Task UpdateAsync(UserDocument user);
    Task DeleteAsync(Guid userId);
    Task<bool> EmailExistsAsync(string email);
    Task<bool> PhoneExistsAsync(string phone);

    /// <summary>End-users created by a given FP.</summary>
    Task<List<UserDocument>> GetByFpIdAsync(Guid fpUserId);

    /// <summary>Users matching a role within a given FPG (typically used to list FPs in a group).</summary>
    Task<List<UserDocument>> GetByFpgIdAndRoleAsync(Guid fpgId, string role);

    /// <summary>All users with the given role across the system (admin-scope queries).</summary>
    Task<List<UserDocument>> GetAllByRoleAsync(string role);

    /// <summary>End-users belonging to any FP in the given FPG (two-step join).</summary>
    Task<List<UserDocument>> GetEndUsersByFpgAsync(Guid fpgId);
}
