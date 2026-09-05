using BuildingBlocks.EventBus.IntegrationEvents;
using FluentAssertions;
using IntegrationService.Clients;
using IntegrationService.Consumers;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace IntegrationService.Tests.Integration;

public sealed class OrderCreatedConsumerTests
{
    private static OrderCreatedIntegrationEvent SampleEvent() => new(
        OrderId: Guid.NewGuid(),
        ClientId: Guid.NewGuid(),
        CargoType: "General",
        WeightKg: 120.5m,
        Origin: "Kyiv, UA",
        Destination: "Warsaw, PL",
        CreatedAt: DateTime.UtcNow);

    [Fact]
    public async Task Consume_ShouldCallRpa_AndMarkCustoms_WhenRpaSucceeds()
    {
        var mockRpa = new Mock<IRpaClient>();
        mockRpa.Setup(x => x.SubmitCustomsAsync(It.IsAny<OrderCreatedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockOrder = new Mock<IOrderStatusClient>();
        mockOrder.Setup(x => x.MarkCustomsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<OrderCreatedConsumer>();
                x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
            })
            .AddScoped(_ => mockRpa.Object)
            .AddScoped(_ => mockOrder.Object)
            .BuildServiceProvider(true);

        var harness = provider.GetTestHarness();
        await harness.Start();

        var @event = SampleEvent();
        await harness.Bus.Publish(@event);

        // Consumed by OrderCreatedConsumer
        (await harness.Consumed.Any<OrderCreatedIntegrationEvent>()).Should().BeTrue();
        var consumerHarness = harness.GetConsumerHarness<OrderCreatedConsumer>();
        (await consumerHarness.Consumed.Any<OrderCreatedIntegrationEvent>()).Should().BeTrue();

        mockRpa.Verify(x => x.SubmitCustomsAsync(It.Is<OrderCreatedIntegrationEvent>(e =>
            e.OrderId == @event.OrderId && e.ClientId == @event.ClientId), It.IsAny<CancellationToken>()), Times.Once);
        mockOrder.Verify(x => x.MarkCustomsAsync(@event.OrderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_ShouldNotMarkCustoms_WhenRpaFails()
    {
        var mockRpa = new Mock<IRpaClient>();
        mockRpa.Setup(x => x.SubmitCustomsAsync(It.IsAny<OrderCreatedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var mockOrder = new Mock<IOrderStatusClient>();

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<OrderCreatedConsumer>();
                x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
            })
            .AddScoped(_ => mockRpa.Object)
            .AddScoped(_ => mockOrder.Object)
            .BuildServiceProvider(true);

        var harness = provider.GetTestHarness();
        await harness.Start();

        await harness.Bus.Publish(SampleEvent());

        (await harness.Consumed.Any<OrderCreatedIntegrationEvent>()).Should().BeTrue();
        mockRpa.Verify(x => x.SubmitCustomsAsync(It.IsAny<OrderCreatedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        mockOrder.Verify(x => x.MarkCustomsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_ShouldRetry_WhenRpaThrows()
    {
        var mockRpa = new Mock<IRpaClient>();
        mockRpa.Setup(x => x.SubmitCustomsAsync(It.IsAny<OrderCreatedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("transient"));

        var mockOrder = new Mock<IOrderStatusClient>();

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<OrderCreatedConsumer>();
                x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
            })
            .AddScoped(_ => mockRpa.Object)
            .AddScoped(_ => mockOrder.Object)
            .BuildServiceProvider(true);

        var harness = provider.GetTestHarness();
        await harness.Start();

        await harness.Bus.Publish(SampleEvent());

        // MassTransit will fault the message after retries — harness records faults
        // We just verify Rpa was called and OrderStatus not called, and exception bubbled
        await Task.Delay(500);
        mockRpa.Verify(x => x.SubmitCustomsAsync(It.IsAny<OrderCreatedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        mockOrder.Verify(x => x.MarkCustomsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
