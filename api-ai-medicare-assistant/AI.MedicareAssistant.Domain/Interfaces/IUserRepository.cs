using Domain.Documents;

namespace Domain.Interfaces;

public interface IUserRepository
{
    Task<UserDocument?> GetByEmailAsync(string email);
    Task<UserDocument?> GetByPhoneAsync(string phone);
    Task<UserDocument?> GetByIdAsync(Guid id);
    Task<UserDocument> CreateAsync(UserDocument user);
    Task UpdateAsync(UserDocument user);
    Task<bool> EmailExistsAsync(string email);
    Task<bool> PhoneExistsAsync(string phone);
}
