using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace IdentityService.Data;

/// <summary>
/// Design-time factory for EF Core CLI (migrations). Uses IConfiguration-backed secure store:
/// appsettings.json + appsettings.{Env}.json + User Secrets + Environment Variables
/// Password is injected via DatabaseSettings:Password -> NpgsqlConnectionStringBuilder.
/// </summary>
public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var basePath = ResolveBasePath();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .AddUserSecrets<IdentityDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var baseConnectionString = configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException(
                "Connection string 'IdentityDb' not found. Ensure appsettings.json contains ConnectionStrings:IdentityDb.");

        var dbPassword = configuration["DatabaseSettings:Password"];

        var connectionBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString);

        // IConfiguration-backed secure store: override only if secret is present
        // Allows local fallback when password already embedded (e.g. placeholder replaced via env)
        if (!string.IsNullOrWhiteSpace(dbPassword))
        {
            connectionBuilder.Password = dbPassword;
        }
        else if (string.IsNullOrWhiteSpace(connectionBuilder.Password) || connectionBuilder.Password == "YOUR_SECRET_PASSWORD")
        {
            throw new InvalidOperationException(
                "Database password not found. Set 'DatabaseSettings:Password' via User Secrets (dotnet user-secrets set \"DatabaseSettings:Password\" \"<pwd>\") or env var 'DatabaseSettings__Password'.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseNpgsql(connectionBuilder.ConnectionString);

        return new IdentityDbContext(optionsBuilder.Options);
    }

    private static string ResolveBasePath()
    {
        var current = Directory.GetCurrentDirectory();

        // EF tools may run from solution root when using --project. Fallback to project directory if appsettings.json not found.
        if (File.Exists(Path.Combine(current, "appsettings.json")))
        {
            return current;
        }

        var candidate = Path.Combine(current, "src", "Services", "IdentityService");
        if (File.Exists(Path.Combine(candidate, "appsettings.json")))
        {
            return candidate;
        }

        // Last fallback: directory of this assembly (bin/Debug/net10.0) -> walk up to project root
        var assemblyDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(assemblyDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "appsettings.json")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        return current;
    }
}
