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

        // Fixed GUIDs for dev seeding cross-service correlation (OrderSeeder uses dev-client Id)
        var adminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var rpaId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var devClientId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        if (!await db.Users.AnyAsync(u => u.Email == "admin@example.com"))
        {
            db.Users.Add(new User
            {
                Id = adminId,
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
                Id = rpaId,
                Email = "rpa@example.com",
                FullName = "RPA Bot",
                Role = "RpaBot",
                PasswordHash = hasher.HashPassword("RpaBot123!"),
                CreatedAt = DateTime.UtcNow
            });
            logger.LogInformation("Seeded RpaBot rpa@example.com");
        }

        if (!await db.Users.AnyAsync(u => u.Email == "dev.client@example.com"))
        {
            db.Users.Add(new User
            {
                Id = devClientId,
                Email = "dev.client@example.com",
                FullName = "Dev Client",
                Role = "Client",
                PasswordHash = hasher.HashPassword("DevClient123!"),
                CreatedAt = DateTime.UtcNow
            });
            logger.LogInformation("Seeded Client dev.client@example.com for OrderSeeder");
        }

        await db.SaveChangesAsync();
    }
}
