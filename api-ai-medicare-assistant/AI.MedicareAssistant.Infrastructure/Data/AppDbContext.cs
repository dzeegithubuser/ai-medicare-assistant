using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Profile> Profiles => Set<Profile>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.Phone).IsUnique();
        });

        // Profile — 1:1 with User
        modelBuilder.Entity<Profile>(e =>
        {
            e.ToTable("profiles");
            e.HasOne(p => p.User)
             .WithOne(u => u.Profile)
             .HasForeignKey<Profile>(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });


    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.ModifiedDate = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
