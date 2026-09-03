using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderService.Infrastructure.Data;

namespace OrderService.API.Extensions;

public static class OrderApiExtensions
{
    public static WebApplicationBuilder AddOrderDbContext(this WebApplicationBuilder builder)
    {
        var baseConnectionString = builder.Configuration.GetConnectionString("OrderDb")
            ?? throw new InvalidOperationException("Connection string 'OrderDb' not found.");

        var dbPassword = builder.Configuration["DatabaseSettings:Password"];

        var connectionBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString);
        if (!string.IsNullOrWhiteSpace(dbPassword))
            connectionBuilder.Password = dbPassword;

        builder.Services.AddDbContext<OrderDbContext>(options =>
            options.UseNpgsql(connectionBuilder.ConnectionString));

        return builder;
    }
}
