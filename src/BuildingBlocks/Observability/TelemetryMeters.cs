using System.Diagnostics.Metrics;

namespace BuildingBlocks.Observability;

/// <summary>
/// 7.3 shared meters — instruments are no-ops until a MeterListener (OTel) enables them.
/// </summary>
public static class TelemetryMeters
{
    public const string OrdersMeterName = "SmartFreight.Orders";

    private static readonly Meter OrdersMeter = new(OrdersMeterName, "1.0.0");

    public static readonly Counter<long> OrdersCreated = OrdersMeter.CreateCounter<long>(
        "orders.created",
        description: "Orders persisted via CreateOrderCommandHandler");
}
