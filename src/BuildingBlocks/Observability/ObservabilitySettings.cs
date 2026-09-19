namespace BuildingBlocks.Observability;

/// <summary>
/// 7.1 settings bound from the "Observability" configuration section.
/// </summary>
public sealed class ObservabilitySettings
{
    public string ServiceName { get; set; } = string.Empty;
    public string? OtlpEndpoint { get; set; }
    public bool EnableConsole { get; set; }
    public bool EnablePrometheus { get; set; }
}
