using Microsoft.EntityFrameworkCore;
using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;
using OrderService.Infrastructure.Data;

namespace OrderService.Infrastructure.ReadModels;

/// <summary>
/// 6.7 EF read path — single SELECT with column projection (owned Cargo inlined,
/// no StatusHistory join, AsNoTracking). Second layer stays <c>ICacheService</c>
/// in <c>ListOrdersQueryHandler</c> (order:list:* TTL 2m).
/// </summary>
public sealed class OrderReadRepository(OrderDbContext db) : IOrderReadRepository
{
    private readonly OrderDbContext _db = db;

    public async Task<IReadOnlyList<OrderResponse>> ListAsync(Guid? clientId, CancellationToken ct = default)
    {
        var query = _db.Orders.AsNoTracking();
        if (clientId.HasValue)
            query = query.Where(o => o.ClientId == clientId.Value);

        var models = await query
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderReadModel
            {
                Id = o.Id,
                ClientId = o.ClientId,
                Status = o.Status,
                CargoType = o.Cargo.CargoType,
                WeightKg = o.Cargo.WeightKg,
                Origin = o.Cargo.Origin,
                Destination = o.Cargo.Destination,
                Deadline = o.Cargo.Deadline,
                VolumeM3 = o.Cargo.VolumeM3,
                Description = o.Cargo.Description,
                DeclaredValue = o.Cargo.DeclaredValue,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt
            })
            .ToListAsync(ct);

        return models.Select(Map).ToList();
    }

    private static OrderResponse Map(OrderReadModel m) => new()
    {
        Id = m.Id,
        ClientId = m.ClientId,
        Status = m.Status,
        Cargo = new CargoDetailsDto
        {
            CargoType = m.CargoType,
            Deadline = m.Deadline,
            WeightKg = m.WeightKg,
            VolumeM3 = m.VolumeM3,
            Origin = m.Origin,
            Destination = m.Destination,
            Description = m.Description,
            DeclaredValue = m.DeclaredValue
        },
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };
}
