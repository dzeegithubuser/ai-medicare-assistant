using Domain.Entities;
using Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public async Task<User?> GetByEmailAsync(string email) =>
        await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email);

    public async Task<User?> GetByPhoneAsync(string phone) =>
        await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Phone == phone);

    public async Task<User?> GetByIdAsync(Guid id) =>
        await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);

    public async Task<User> CreateAsync(User user)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task UpdateAsync(User user)
    {
        await _db.SaveChangesAsync();
    }

    public async Task<bool> EmailExistsAsync(string email) =>
        await _db.Users.AnyAsync(u => u.Email == email);

    public async Task<bool> PhoneExistsAsync(string phone) =>
        await _db.Users.AnyAsync(u => u.Phone == phone);
}
