using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace IdentityService.Data;

/// <summary>
/// Design-time factory to enable EF Core Migrations tool to correctly read User Secrets and assembly the DbContext.
/// </summary>
public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        // Force loading configuration including local user secrets from the machine
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .AddUserSecrets<IdentityDbContextFactory>() // Dynamically links your local user secrets storage
            .AddEnvironmentVariables()
            .Build();

        var baseConnectionString = configuration.GetConnectionString("IdentityDb") ?? throw new InvalidOperationException("Missing IdentityDb");
        var dbPassword = configuration["DatabaseSettings:Password"]
            ?? throw new InvalidOperationException("Missing DatabaseSettings:Password. Set via user-secrets / env var / KeyVault");

        var connectionBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Password = dbPassword
        };

        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseNpgsql(connectionBuilder.ConnectionString);

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
