namespace BuildingBlocks.EventBus;

/// <summary>
/// Stage 4.4 — binds to "RabbitMq" section. Non-secrets default (localhost:5672 /); secrets required via config.
/// Env overrides via RabbitMq__User / RabbitMq__Password / docker/.env RABBITMQ_* → RabbitMq__*.
/// </summary>
public sealed class RabbitMqSettings
{
    // Topology — safe defaults, not secrets
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VHost { get; set; } = "/";

    // Secrets — no defaults in code, must be supplied via user-secrets (dev) or env (prod)
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    // alias for User — allows RabbitMq:Username binding
    public string Username
    {
        get => User;
        set => User = value;
    }
}
