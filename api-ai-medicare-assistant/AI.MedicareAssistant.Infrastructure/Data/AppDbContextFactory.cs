using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Data;

/// <summary>
/// Design-time factory for EF Core migrations. Allows 'dotnet ef migrations add'
/// to work without a running MySQL server.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Use a dummy connection string for migration generation only.
        // The real connection string is loaded from appsettings.json at runtime.
        optionsBuilder.UseMySql(
            "Server=localhost;Database=ai_medicare_assistant;User=root;Password=dummy;",
            new MySqlServerVersion(new Version(8, 0, 36))
        );

        return new AppDbContext(optionsBuilder.Options);
    }
}
