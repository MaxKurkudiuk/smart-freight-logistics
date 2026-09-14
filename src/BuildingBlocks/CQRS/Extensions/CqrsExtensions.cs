using System.Reflection;
using BuildingBlocks.CQRS.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.CQRS.Extensions;

public static class CqrsExtensions
{
    /// <summary>
    /// 6.1 shared CQRS registration — MediatR handlers + FluentValidation validators
    /// + ValidationBehavior + LoggingBehavior pipeline.
    /// Call from Program.cs: services.AddCqrs(typeof(CreateOrderCommand).Assembly).
    /// </summary>
    public static IServiceCollection AddCqrs(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(assemblies));
        services.AddValidatorsFromAssemblies(assemblies);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        return services;
    }
}
