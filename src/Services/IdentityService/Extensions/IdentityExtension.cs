using IdentityService.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IdentityService.Extensions;

public static class IdentityExtension
{
    public static WebApplicationBuilder AddDbContext(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(builder.GetIdentityDbConnectionString()));

        return builder;
    }

    public static string GetIdentityDbConnectionString(this WebApplicationBuilder builder)
    {
        var baseConnectionString = builder.Configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException("Connection string 'IdentityDb' not found.");

        var dbPassword = builder.Configuration["DatabaseSettings:Password"];

        var connectionBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString);
        if (!string.IsNullOrWhiteSpace(dbPassword))
        {
            connectionBuilder.Password = dbPassword;
        }

        return connectionBuilder.ConnectionString;
    }
}
