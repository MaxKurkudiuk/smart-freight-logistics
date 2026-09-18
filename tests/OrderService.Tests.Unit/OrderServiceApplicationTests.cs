using BuildingBlocks.Caching;
using BuildingBlocks.EventBus.IntegrationEvents;
using FluentAssertions;
using MassTransit;
using Moq;
using OrderService.Application.DTOs;
using OrderService.Application.Features.Orders.Commands.CreateOrder;
using OrderService.Application.Features.Orders.Commands.UpdateOrderStatus;
using OrderService.Application.Features.Orders.Queries.GetOrderById;
using OrderService.Application.Features.Orders.Queries.ListOrders;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;

namespace OrderService.Tests.Unit;

/// <summary>
/// 6.8 — same scenarios as the Stage 5 service tests, now exercising the
/// MediatR handlers directly (Handle(command, ct)). Validation-pipeline
/// cases live in <see cref="OrderCommandValidatorTests"/>.
/// </summary>
public sealed class OrderServiceApplicationTests
{
    private static CargoDetails ValidCargo() => new()
    {
        CargoType = nameof(CargoType.General),
        Deadline = DateTime.UtcNow.AddDays(5),
        WeightKg = 10,
        VolumeM3 = 1,
        Origin = "Kyiv, UA",
        Destination = "Warsaw, PL",
        Description = "Test",
        DeclaredValue = 1000
    };

    private static CreateOrderRequest ValidRequest() => new()
    {
        CargoType = nameof(CargoType.General),
        Deadline = DateTime.UtcNow.AddDays(5),
        WeightKg = 10,
        VolumeM3 = 1,
        Origin = "Kyiv, UA",
        Destination = "Warsaw, PL",
        Description = "Test",
        DeclaredValue = 1000
    };

    private static OrderResponse SampleResponse(Guid clientId) => new()
    {
        Id = Guid.NewGuid(),
        ClientId = clientId,
        Status = OrderStatus.Created,
        Cargo = new CargoDetailsDto
        {
            CargoType = nameof(CargoType.General),
            Deadline = DateTime.UtcNow.AddDays(5),
            WeightKg = 10,
            VolumeM3 = 1,
            Origin = "Kyiv, UA",
            Destination = "Warsaw, PL",
            Description = "Test",
            DeclaredValue = 1000
        },
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Mock<IPublishEndpoint> MockPublish() => new();

    private static Mock<ICacheService> MockCache()
    {
        var m = new Mock<ICacheService>();
        m.Setup(c => c.GetAsync<OrderResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((OrderResponse?)null);
        m.Setup(c => c.GetAsync<IReadOnlyList<OrderResponse>>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<OrderResponse>?)null);
        m.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        m.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return m;
    }

    [Fact]
    public async Task CreateHandler_ShouldCreateOrder_AndSave()
    {
        var mockRepo = new Mock<IOrderRepository>();
        var mockPublish = MockPublish();
        Order? captured = null;
        mockRepo.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((o, _) => captured = o)
            .Returns(Task.CompletedTask);
        mockRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new CreateOrderCommandHandler(mockRepo.Object, mockPublish.Object, MockCache().Object);
        var clientId = Guid.NewGuid();

        var result = await handler.Handle(new CreateOrderCommand(clientId, ValidRequest()), CancellationToken.None);

        result.Should().NotBeNull();
        result.ClientId.Should().Be(clientId);
        result.Status.Should().Be(OrderStatus.Created);
        captured.Should().NotBeNull();
        captured!.ClientId.Should().Be(clientId);
        mockRepo.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
        mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateHandler_ShouldPublishIntegrationEvent()
    {
        var mockRepo = new Mock<IOrderRepository>();
        var mockPublish = MockPublish();
        mockRepo.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mockRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new CreateOrderCommandHandler(mockRepo.Object, mockPublish.Object, MockCache().Object);
        var clientId = Guid.NewGuid();
        var req = ValidRequest();

        await handler.Handle(new CreateOrderCommand(clientId, req), CancellationToken.None);

        mockPublish.Verify(p => p.Publish(It.Is<OrderCreatedIntegrationEvent>(e =>
            e.ClientId == clientId &&
            e.CargoType == req.CargoType &&
            e.WeightKg == req.WeightKg &&
            e.Origin == req.Origin.Trim() &&
            e.Destination == req.Destination.Trim()), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateHandler_ShouldThrow_WhenOriginEqualsDestination()
    {
        var mockRepo = new Mock<IOrderRepository>();
        var mockPublish = MockPublish();
        var handler = new CreateOrderCommandHandler(mockRepo.Object, mockPublish.Object, MockCache().Object);
        var req = ValidRequest() with { Origin = "Kyiv, UA", Destination = "kyiv, UA" };

        var act = () => handler.Handle(new CreateOrderCommand(Guid.NewGuid(), req), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Origin and Destination must differ*");
        mockRepo.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        mockPublish.Verify(p => p.Publish(It.IsAny<OrderCreatedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdHandler_ShouldReturnNull_WhenClientNotOwner()
    {
        var order = Order.Create(Guid.NewGuid(), ValidCargo());
        var mockRepo = new Mock<IOrderRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var handler = new GetOrderByIdQueryHandler(mockRepo.Object, MockCache().Object);
        var otherClient = Guid.NewGuid();

        var result = await handler.Handle(new GetOrderByIdQuery(order.Id, otherClient, "Client"), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdHandler_ShouldReturnOrder_WhenManager()
    {
        var order = Order.Create(Guid.NewGuid(), ValidCargo());
        var mockRepo = new Mock<IOrderRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var handler = new GetOrderByIdQueryHandler(mockRepo.Object, MockCache().Object);

        var result = await handler.Handle(new GetOrderByIdQuery(order.Id, Guid.NewGuid(), "LogisticsManager"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task GetByIdHandler_ShouldReturnCached_WithoutRepoHit_WhenCacheHit()
    {
        var clientId = Guid.NewGuid();
        var cached = SampleResponse(clientId);
        var mockRepo = new Mock<IOrderRepository>();
        var mockCache = MockCache();
        mockCache.Setup(c => c.GetAsync<OrderResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var handler = new GetOrderByIdQueryHandler(mockRepo.Object, mockCache.Object);

        var result = await handler.Handle(new GetOrderByIdQuery(cached.Id, clientId, "Client"), CancellationToken.None);

        result.Should().Be(cached);
        mockRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListHandler_ShouldFilterByClient_WhenClient()
    {
        var clientId = Guid.NewGuid();
        var mockReadRepo = new Mock<IOrderReadRepository>();
        mockReadRepo.Setup(r => r.ListAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderResponse> { SampleResponse(clientId) });

        var handler = new ListOrdersQueryHandler(mockReadRepo.Object, MockCache().Object);

        var result = await handler.Handle(new ListOrdersQuery(clientId, "Client"), CancellationToken.None);

        result.Should().HaveCount(1);
        result.First().ClientId.Should().Be(clientId);
        mockReadRepo.Verify(r => r.ListAsync(clientId, It.IsAny<CancellationToken>()), Times.Once);
        mockReadRepo.Verify(r => r.ListAsync(null, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListHandler_ShouldReturnAll_WhenManager()
    {
        var mockReadRepo = new Mock<IOrderReadRepository>();
        mockReadRepo.Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderResponse> { SampleResponse(Guid.NewGuid()), SampleResponse(Guid.NewGuid()) });

        var handler = new ListOrdersQueryHandler(mockReadRepo.Object, MockCache().Object);

        var result = await handler.Handle(new ListOrdersQuery(Guid.NewGuid(), "LogisticsManager"), CancellationToken.None);

        result.Should().HaveCount(2);
        mockReadRepo.Verify(r => r.ListAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListHandler_ShouldReturnCached_WithoutReadRepoHit_WhenCacheHit()
    {
        var clientId = Guid.NewGuid();
        var cached = new List<OrderResponse> { SampleResponse(clientId) };
        var mockReadRepo = new Mock<IOrderReadRepository>();
        var mockCache = MockCache();
        mockCache.Setup(c => c.GetAsync<IReadOnlyList<OrderResponse>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var handler = new ListOrdersQueryHandler(mockReadRepo.Object, mockCache.Object);

        var result = await handler.Handle(new ListOrdersQuery(clientId, "Client"), CancellationToken.None);

        result.Should().BeSameAs(cached);
        mockReadRepo.Verify(r => r.ListAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusHandler_ShouldThrowUnauthorized_WhenNotOwnerAndNotManager()
    {
        var owner = Guid.NewGuid();
        var order = Order.Create(owner, ValidCargo());
        var mockRepo = new Mock<IOrderRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var handler = new UpdateOrderStatusCommandHandler(mockRepo.Object, MockCache().Object);
        var other = Guid.NewGuid();

        var act = () => handler.Handle(
            new UpdateOrderStatusCommand(order.Id, other, "Client", new UpdateStatusRequest { NewStatus = OrderStatus.Cancelled }),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UpdateStatusHandler_ShouldSucceed_ForOwner_Cancel()
    {
        var owner = Guid.NewGuid();
        var order = Order.Create(owner, ValidCargo());
        var mockRepo = new Mock<IOrderRepository>();
        // Mock ExecuteUpdate path: TryUpdateStatusWithHistoryAsync returns true and GetById after returns updated order
        mockRepo.Setup(r => r.TryUpdateStatusWithHistoryAsync(
                It.IsAny<Guid>(), It.IsAny<OrderStatus>(), It.IsAny<DateTime>(), It.IsAny<StatusHistory>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        // After update, GetById returns order with updated status (we simulate)
        mockRepo.SetupSequence(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order) // first call for validation
            .ReturnsAsync(order); // second call for reload (handler patches status manually)

        var handler = new UpdateOrderStatusCommandHandler(mockRepo.Object, MockCache().Object);

        var result = await handler.Handle(
            new UpdateOrderStatusCommand(order.Id, owner, "Client", new UpdateStatusRequest { NewStatus = OrderStatus.Cancelled, Notes = "client cancel" }),
            CancellationToken.None);

        result.Status.Should().Be(OrderStatus.Cancelled);
        mockRepo.Verify(r => r.TryUpdateStatusWithHistoryAsync(
            order.Id, OrderStatus.Cancelled, It.IsAny<DateTime>(), It.IsAny<StatusHistory>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusHandler_ShouldThrowDomainException_ForInvalidTransition()
    {
        var owner = Guid.NewGuid();
        var order = Order.Create(owner, ValidCargo());
        var mockRepo = new Mock<IOrderRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var handler = new UpdateOrderStatusCommandHandler(mockRepo.Object, MockCache().Object);

        var act = () => handler.Handle(
            new UpdateOrderStatusCommand(order.Id, owner, "Client", new UpdateStatusRequest { NewStatus = OrderStatus.Delivered }),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task UpdateStatusHandler_ShouldBeIdempotent_WhenSameStatus()
    {
        var owner = Guid.NewGuid();
        var order = Order.Create(owner, ValidCargo()); // Created
        var mockRepo = new Mock<IOrderRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var handler = new UpdateOrderStatusCommandHandler(mockRepo.Object, MockCache().Object);

        var result = await handler.Handle(
            new UpdateOrderStatusCommand(order.Id, owner, "Client", new UpdateStatusRequest { NewStatus = OrderStatus.Created }),
            CancellationToken.None);

        result.Status.Should().Be(OrderStatus.Created);
        // Should not call TryUpdate when idempotent
        mockRepo.Verify(r => r.TryUpdateStatusWithHistoryAsync(It.IsAny<Guid>(), It.IsAny<OrderStatus>(), It.IsAny<DateTime>(), It.IsAny<StatusHistory>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
