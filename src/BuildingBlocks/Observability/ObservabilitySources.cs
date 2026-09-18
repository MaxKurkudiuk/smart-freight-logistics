namespace BuildingBlocks.Observability;

/// <summary>
/// 7.3 per-service OTel sources — extra ActivitySources (Npgsql, MassTransit)
/// and Meter names (SmartFreight.*) beyond the built-in AspNetCore/HttpClient/Runtime.
/// </summary>
public sealed class ObservabilitySources
{
    public List<string> TraceSources { get; } = [];
    public List<string> MeterNames { get; } = [];
}
