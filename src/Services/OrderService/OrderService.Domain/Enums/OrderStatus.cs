namespace OrderService.Domain.Enums;

public enum OrderStatus
{
    Created = 0,
    Confirmed = 1,
    InTransit = 2,
    Customs = 3,
    Delivered = 4,
    Cancelled = 5
}
