using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace OrderService.Tests.Integration;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sfl_order_db_test")
        .WithUsername("sfl_admin_test")
        .WithPassword("SecretPassword123!")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => _dbContainer.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _dbContainer.StartAsync();
    }

    public new async ValueTask DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Also set env var as fallback (host builder reads env vars after appsettings)
        Environment.SetEnvironmentVariable("JwtSettings__Secret", JwtHelper.Secret);
        Environment.SetEnvironmentVariable("JwtSettings__Issuer", JwtHelper.Issuer);
        Environment.SetEnvironmentVariable("JwtSettings__Audience", JwtHelper.Audience);
        Environment.SetEnvironmentVariable("ConnectionStrings__OrderDb", _dbContainer.GetConnectionString());

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var dict = new Dictionary<string, string?>
            {
                ["ConnectionStrings:OrderDb"] = _dbContainer.GetConnectionString(),
                // Password already in connection string; leave empty to avoid override
                ["DatabaseSettings:Password"] = "",
                ["JwtSettings:Secret"] = JwtHelper.Secret,
                ["JwtSettings:Issuer"] = JwtHelper.Issuer,
                ["JwtSettings:Audience"] = JwtHelper.Audience,
                ["JwtSettings:ExpiryInMinutes"] = "60"
            };
            config.AddInMemoryCollection(dict);
        });

        builder.UseSetting("ConnectionStrings:OrderDb", _dbContainer.GetConnectionString());
    }

    /// <summary>
    /// Call after factory creation to apply migrations (ensures Testcontainers DB is ready).
    /// </summary>
    public void EnsureMigrated()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        db.Database.Migrate();
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        // Use EnsureCreated for Testcontainers (faster & avoids migration assembly lookup timing issues)
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }
}
