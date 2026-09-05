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
    // Ephemeral, isolated Testcontainers DB — no sync with docker/.env or local dev passwords
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sfl_order_db_test")
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
        builder.UseEnvironment("Testing");

        // Set env vars early — Program.cs AddOrderAuth reads builder.Configuration before ConfigureAppConfiguration InMemory is applied in some factory versions
        Environment.SetEnvironmentVariable("JwtSettings__Secret", JwtHelper.Secret);
        Environment.SetEnvironmentVariable("JwtSettings__Issuer", JwtHelper.Issuer);
        Environment.SetEnvironmentVariable("JwtSettings__Audience", JwtHelper.Audience);
        Environment.SetEnvironmentVariable("ConnectionStrings__OrderDb", _dbContainer.GetConnectionString());

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var dict = new Dictionary<string, string?>
            {
                // Isolated DB connection from Testcontainers — password already in connection string
                ["ConnectionStrings:OrderDb"] = _dbContainer.GetConnectionString(),
                ["DatabaseSettings:Password"] = "",
                ["JwtSettings:Secret"] = JwtHelper.Secret,
                ["JwtSettings:Issuer"] = JwtHelper.Issuer,
                ["JwtSettings:Audience"] = JwtHelper.Audience,
                ["JwtSettings:ExpiryInMinutes"] = "60"
                // No RabbitMq section — Program.cs uses InMemory when Environment=Testing (fully isolated, no broker)
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
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }
}
