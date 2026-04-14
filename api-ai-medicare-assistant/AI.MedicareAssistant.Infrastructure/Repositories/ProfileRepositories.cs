using Domain.Entities;
using Domain.Interfaces;
using Infrastructure.Data;

namespace Infrastructure.Repositories;

public class ProfileRepository : Repository<Profile>, IProfileRepository
{
    public ProfileRepository(AppDbContext db) : base(db) { }
}
