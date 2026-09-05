using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.EventBus.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 4.4 shared infra — no consumers, only transport. Binds RabbitMqSettings from "RabbitMq".
    /// Call from Program.cs: builder.AddEventBus() (or services.AddEventBus(Configuration)).
    /// Retry: 3×1s Interval (plan).
    /// </summary>
    public static IServiceCollection AddEventBus(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqSettings>()
            .Bind(configuration.GetSection("RabbitMq"))
            .Validate(s => !string.IsNullOrWhiteSpace(s.Host), "RabbitMq:Host is required")
            .Validate(s => !string.IsNullOrWhiteSpace(s.User), "RabbitMq:User/Username is required — set via user-secrets RabbitMq:User or env RabbitMq__User")
            .Validate(s => !string.IsNullOrWhiteSpace(s.Password), "RabbitMq:Password is required — set via user-secrets RabbitMq:Password or env RabbitMq__Password")
            .ValidateOnStart();

        services.AddMassTransit(x =>
        {
            // consumers added per-service via AddConsumer<T> before this call (or via AddEventBus overload with configure)
            x.UsingRabbitMq((ctx, cfg) =>
            {
                var s = ctx.GetRequiredService<IOptions<RabbitMqSettings>>().Value;

                cfg.Host(s.Host, (ushort)s.Port, s.VHost, h =>
                {
                    h.Username(s.User);
                    h.Password(s.Password);
                });

                cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(1)));

                cfg.ConfigureEndpoints(ctx);
            });
        });

        return services;
    }

    public static WebApplicationBuilder AddEventBus(this WebApplicationBuilder builder)
        => AddEventBus(builder, _ => { });

    public static WebApplicationBuilder AddEventBus(this WebApplicationBuilder builder, Action<IBusRegistrationConfigurator> configure)
    {
        builder.Services.AddOptions<RabbitMqSettings>()
            .Bind(builder.Configuration.GetSection("RabbitMq"))
            .Validate(s => !string.IsNullOrWhiteSpace(s.Host), "RabbitMq:Host is required")
            .Validate(s => !string.IsNullOrWhiteSpace(s.User), "RabbitMq:User/Username is required — set via user-secrets RabbitMq:User or env RabbitMq__User")
            .Validate(s => !string.IsNullOrWhiteSpace(s.Password), "RabbitMq:Password is required — set via user-secrets RabbitMq:Password or env RabbitMq__Password")
            .ValidateOnStart();

        builder.Services.AddMassTransit(x =>
        {
            configure(x);

            x.UsingRabbitMq((ctx, cfg) =>
            {
                var s = ctx.GetRequiredService<IOptions<RabbitMqSettings>>().Value;

                cfg.Host(s.Host, (ushort)s.Port, s.VHost, h =>
                {
                    h.Username(s.User);
                    h.Password(s.Password);
                });

                cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(1)));

                cfg.ConfigureEndpoints(ctx);
            });
        });

        return builder;
    }
}
