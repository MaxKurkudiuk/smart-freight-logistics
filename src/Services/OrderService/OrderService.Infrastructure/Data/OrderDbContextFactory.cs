using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace OrderService.Infrastructure.Data;

/// <summary>
/// Design-time factory for EF Core CLI (migrations). Mirrors IdentityDbContextFactory.
/// </summary>
public class OrderDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var basePath = ResolveBasePath();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .AddUserSecrets<OrderDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var baseConnectionString = configuration.GetConnectionString("OrderDb")
            ?? throw new InvalidOperationException(
                "Connection string 'OrderDb' not found. Ensure appsettings.json contains ConnectionStrings:OrderDb.");

        var dbPassword = configuration["DatabaseSettings:Password"];

        var connectionBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString);

        if (!string.IsNullOrWhiteSpace(dbPassword))
        {
            connectionBuilder.Password = dbPassword;
        }
        else if (string.IsNullOrWhiteSpace(connectionBuilder.Password) || connectionBuilder.Password == "YOUR_SECRET_PASSWORD")
        {
            throw new InvalidOperationException(
                "Database password not found. Set 'DatabaseSettings:Password' via User Secrets (dotnet user-secrets set \"DatabaseSettings:Password\" \"<pwd>\") or env var 'DatabaseSettings__Password'.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<OrderDbContext>();
        optionsBuilder.UseNpgsql(connectionBuilder.ConnectionString);

        return new OrderDbContext(optionsBuilder.Options);
    }

    private static string ResolveBasePath()
    {
        var current = Directory.GetCurrentDirectory();

        if (File.Exists(Path.Combine(current, "appsettings.json")))
            return current;

        var candidate = Path.Combine(current, "src", "Services", "OrderService", "OrderService.API");
        if (File.Exists(Path.Combine(candidate, "appsettings.json")))
            return candidate;

        var assemblyDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(assemblyDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "appsettings.json")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return current;
    }
}
