using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.DTOs;
using OrderService.Application.Features.Orders.Commands.CreateOrder;
using OrderService.Domain.Enums;

namespace OrderService.Tests.Integration;

/// <summary>
/// 6.8 — MediatR pipeline inside the real host: ValidationBehavior must throw
/// ValidationException (→ API 400) before any handler runs. Requires Docker
/// (Testcontainers PostgreSQL via <see cref="CustomWebApplicationFactory"/>).
/// </summary>
public sealed class OrderSenderIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OrderSenderIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Send_InvalidCreateCommand_ThrowsValidationException_BeforeHandler()
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var invalid = new CreateOrderRequest
        {
            CargoType = nameof(CargoType.General),
            Deadline = DateTime.UtcNow.AddDays(5),
            WeightKg = 0, // violates GreaterThan(0)
            Origin = "Kyiv, UA",
            Destination = "Warsaw, PL"
        };

        var act = () => sender.Send(new CreateOrderCommand(Guid.NewGuid(), invalid));

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().Contain(e => e.PropertyName.Contains(nameof(CreateOrderRequest.WeightKg)));
    }
}
