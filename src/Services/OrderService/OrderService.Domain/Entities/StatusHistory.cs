using OrderService.Domain.Enums;

namespace OrderService.Domain.Entities;

public sealed class StatusHistory
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order? Order { get; set; } // nullable — domain factory does not populate navigation; EF will fill on Include. Avoids NRE in unit tests.
    public OrderStatus FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public Guid ChangedBy { get; set; }
    public string? Notes { get; set; }
}
