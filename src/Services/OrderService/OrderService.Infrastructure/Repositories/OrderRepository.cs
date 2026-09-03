using Microsoft.EntityFrameworkCore;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;
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
}
