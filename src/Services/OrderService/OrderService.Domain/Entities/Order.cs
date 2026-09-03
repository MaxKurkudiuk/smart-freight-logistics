using OrderService.Domain.Enums;

namespace OrderService.Domain.Entities;

public sealed class Order
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Created;
    public CargoDetails Cargo { get; set; } = new();
    private readonly List<StatusHistory> _history = [];
    public IReadOnlyCollection<StatusHistory> History => _history.AsReadOnly();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    private Order() { } // EF — _history backing field configured in OrderDbContext via UsePropertyAccessMode(Field)

    // TODO: picks TimeProvider for testable time in 3.7 (keep DateTime.UtcNow for MVP, add overload with DateTime? now param if needed)

    public static Order Create(Guid clientId, CargoDetails cargo)
    {
        if (clientId == Guid.Empty)
            throw new ArgumentException("ClientId is required.", nameof(clientId));
        ArgumentNullException.ThrowIfNull(cargo);
        if (cargo.WeightKg <= 0)
            throw new ArgumentException("WeightKg must be > 0.", nameof(cargo));
        if (string.IsNullOrWhiteSpace(cargo.Origin))
            throw new ArgumentException("Origin is required.", nameof(cargo));
        if (string.IsNullOrWhiteSpace(cargo.Destination))
            throw new ArgumentException("Destination is required.", nameof(cargo));
        if (cargo.Origin.Trim() == cargo.Destination.Trim())
            throw new ArgumentException("Origin and Destination must differ.", nameof(cargo));

        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Status = OrderStatus.Created,
            Cargo = cargo,
            CreatedAt = now,
            UpdatedAt = now
        };

        order._history.Add(new StatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            FromStatus = OrderStatus.Created,
            ToStatus = OrderStatus.Created,
            ChangedAt = now,
            ChangedBy = clientId,
            Notes = "Order created"
        });

        return order;
    }

    /// <summary>
    /// Transition is validated via OrderStatusTransitions (3.2). This method mutates state.
    /// </summary>
    public void TransitionTo(OrderStatus newStatus, Guid actorId, string? notes = null)
    {
        OrderStatusTransitions.Ensure(Status, newStatus);

        var from = Status;
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;

        _history.Add(new StatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = Id,
            FromStatus = from,
            ToStatus = newStatus,
            ChangedAt = UpdatedAt,
            ChangedBy = actorId,
            Notes = notes
        });
    }
}
