using Microsoft.EntityFrameworkCore;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Infrastructure.Data;

namespace OrderService.Infrastructure.Repositories;

public sealed class OrderRepository(OrderDbContext db) : IOrderRepository
{
    private readonly OrderDbContext _db = db;

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Orders
            .Include(o => o.History)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<Order>> ListByClientAsync(Guid clientId, CancellationToken ct = default)
        => await _db.Orders
            .AsNoTracking()
            .Include(o => o.History)
            .Where(o => o.ClientId == clientId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Order>> ListAllAsync(CancellationToken ct = default)
        => await _db.Orders
            .AsNoTracking()
            .Include(o => o.History)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

    public Task AddAsync(Order order, CancellationToken ct = default)
    {
        _db.Orders.Add(order);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public async Task<bool> TryUpdateStatusWithHistoryAsync(Guid orderId, OrderStatus newStatus, DateTime updatedAt, StatusHistory history, CancellationToken ct = default)
    {
        var affected = await _db.Orders
            .Where(o => o.Id == orderId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, newStatus)
                .SetProperty(o => o.UpdatedAt, updatedAt), ct);

        if (affected == 0) return false;

        _db.StatusHistories.Add(history);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
