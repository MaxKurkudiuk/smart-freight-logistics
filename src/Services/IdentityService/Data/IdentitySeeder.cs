using IdentityService.Entities;
using IdentityService.Services;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Data;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

        // Ensure DB is created/migrated (works for both docker and local)
        await db.Database.MigrateAsync();

        if (!await db.Users.AnyAsync(u => u.Email == "admin@example.com"))
        {
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@example.com",
                FullName = "Admin",
                Role = "LogisticsManager",
                PasswordHash = hasher.HashPassword("Admin123!"),
                CreatedAt = DateTime.UtcNow
            });
            logger.LogInformation("Seeded LogisticsManager admin@example.com");
        }

        if (!await db.Users.AnyAsync(u => u.Email == "rpa@example.com"))
        {
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = "rpa@example.com",
                FullName = "RPA Bot",
                Role = "RpaBot",
                PasswordHash = hasher.HashPassword("RpaBot123!"),
                CreatedAt = DateTime.UtcNow
            });
            logger.LogInformation("Seeded RpaBot rpa@example.com");
        }

        await db.SaveChangesAsync();
    }
}
