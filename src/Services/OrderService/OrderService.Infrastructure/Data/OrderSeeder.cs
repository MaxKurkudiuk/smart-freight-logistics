using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;

namespace OrderService.Infrastructure.Data;

public static class OrderSeeder
{
    // Must match IdentitySeeder dev.client@example.com Id
    private static readonly Guid DevClientId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("OrderSeeder");

        await db.Database.MigrateAsync();

        if (await db.Orders.AnyAsync(o => o.ClientId == DevClientId))
        {
            logger.LogInformation("Dev orders already seeded for {ClientId} — skipping", DevClientId);
            return;
        }

        var now = DateTime.UtcNow;

        var orders = new[]
        {
            Order.Create(DevClientId, new CargoDetails
            {
                CargoType = nameof(CargoType.General),
                Deadline = now.AddDays(7),
                WeightKg = 120.5m,
                VolumeM3 = 2.3m,
                Origin = "Kyiv, UA",
                Destination = "Warsaw, PL",
                Description = "Dev seed — General cargo Kyiv->Warsaw",
                DeclaredValue = 5000
            }),
            Order.Create(DevClientId, new CargoDetails
            {
                CargoType = nameof(CargoType.Refrigerated),
                Deadline = now.AddDays(3),
                WeightKg = 500m,
                VolumeM3 = 10m,
                Origin = "Lviv, UA",
                Destination = "Odesa, UA",
                Description = "Dev seed — Refrigerated Lviv->Odesa",
                DeclaredValue = 15000
            }),
            Order.Create(DevClientId, new CargoDetails
            {
                CargoType = nameof(CargoType.Hazardous),
                Deadline = now.AddDays(10),
                WeightKg = 2000m,
                VolumeM3 = 15m,
                Origin = "Dnipro, UA",
                Destination = "Kharkiv, UA",
                Description = "Dev seed — Hazardous Dnipro->Kharkiv",
                DeclaredValue = 50000
            })
        };

        // Advance second order to Confirmed to demo state machine
        orders[1].TransitionTo(OrderStatus.Confirmed, DevClientId, "Seed: auto-confirmed for demo");

        foreach (var order in orders)
        {
            db.Orders.Add(order);
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} dev orders for {ClientId}", orders.Length, DevClientId);
    }
}
