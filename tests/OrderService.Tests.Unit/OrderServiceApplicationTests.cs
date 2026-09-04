using FluentAssertions;
using Moq;
using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;

namespace OrderService.Tests.Unit;

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

    [Fact]
    public async Task CreateAsync_ShouldCreateOrder_AndSave()
    {
        var mockRepo = new Mock<IOrderRepository>();
        Order? captured = null;
        mockRepo.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((o, _) => captured = o)
            .Returns(Task.CompletedTask);
        mockRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var svc = new Application.Services.OrderService(mockRepo.Object);
        var clientId = Guid.NewGuid();

        var result = await svc.CreateAsync(clientId, ValidRequest());

        result.Should().NotBeNull();
        result.ClientId.Should().Be(clientId);
        result.Status.Should().Be(OrderStatus.Created);
        captured.Should().NotBeNull();
        captured!.ClientId.Should().Be(clientId);
        mockRepo.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
        mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenOriginEqualsDestination()
    {
        var mockRepo = new Mock<IOrderRepository>();
        var svc = new Application.Services.OrderService(mockRepo.Object);
        var req = ValidRequest() with { Origin = "Kyiv, UA", Destination = "kyiv, UA" };

        var act = () => svc.CreateAsync(Guid.NewGuid(), req);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Origin and Destination must differ*");
        mockRepo.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenClientNotOwner()
    {
        var order = Order.Create(Guid.NewGuid(), ValidCargo());
        var mockRepo = new Mock<IOrderRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var svc = new Application.Services.OrderService(mockRepo.Object);
        var otherClient = Guid.NewGuid();

        var result = await svc.GetByIdAsync(order.Id, otherClient, "Client");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnOrder_WhenManager()
    {
        var order = Order.Create(Guid.NewGuid(), ValidCargo());
        var mockRepo = new Mock<IOrderRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var svc = new Application.Services.OrderService(mockRepo.Object);

        var result = await svc.GetByIdAsync(order.Id, Guid.NewGuid(), "LogisticsManager");

        result.Should().NotBeNull();
        result!.Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task ListAsync_ShouldFilterByClient_WhenClient()
    {
        var clientId = Guid.NewGuid();
        var order1 = Order.Create(clientId, ValidCargo());
        var order2 = Order.Create(Guid.NewGuid(), ValidCargo());

        var mockRepo = new Mock<IOrderRepository>();
        mockRepo.Setup(r => r.ListByClientAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order> { order1 });

        var svc = new Application.Services.OrderService(mockRepo.Object);

        var result = await svc.ListAsync(clientId, "Client");

        result.Should().HaveCount(1);
        result.First().ClientId.Should().Be(clientId);
        mockRepo.Verify(r => r.ListByClientAsync(clientId, It.IsAny<CancellationToken>()), Times.Once);
        mockRepo.Verify(r => r.ListAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListAsync_ShouldReturnAll_WhenManager()
    {
        var mockRepo = new Mock<IOrderRepository>();
        mockRepo.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order> { Order.Create(Guid.NewGuid(), ValidCargo()), Order.Create(Guid.NewGuid(), ValidCargo()) });

        var svc = new Application.Services.OrderService(mockRepo.Object);

        var result = await svc.ListAsync(Guid.NewGuid(), "LogisticsManager");

        result.Should().HaveCount(2);
        mockRepo.Verify(r => r.ListAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldThrowUnauthorized_WhenNotOwnerAndNotManager()
    {
        var owner = Guid.NewGuid();
        var order = Order.Create(owner, ValidCargo());
        var mockRepo = new Mock<IOrderRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var svc = new Application.Services.OrderService(mockRepo.Object);
        var other = Guid.NewGuid();

        var act = () => svc.UpdateStatusAsync(order.Id, other, "Client", new UpdateStatusRequest { NewStatus = OrderStatus.Cancelled });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldSucceed_ForOwner_Cancel()
    {
        var owner = Guid.NewGuid();
        var order = Order.Create(owner, ValidCargo());
        var mockRepo = new Mock<IOrderRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => order);
        // Mock ExecuteUpdate path: TryUpdateStatusWithHistoryAsync returns true and GetById after returns updated order
        mockRepo.Setup(r => r.TryUpdateStatusWithHistoryAsync(
                It.IsAny<Guid>(), It.IsAny<OrderStatus>(), It.IsAny<DateTime>(), It.IsAny<StatusHistory>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        // After update, GetById returns order with updated status (we simulate)
        mockRepo.SetupSequence(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order) // first call for validation
            .ReturnsAsync(order); // second call for reload (we patch status manually in service)

        var svc = new Application.Services.OrderService(mockRepo.Object);

        var result = await svc.UpdateStatusAsync(order.Id, owner, "Client", new UpdateStatusRequest { NewStatus = OrderStatus.Cancelled, Notes = "client cancel" });

        result.Status.Should().Be(OrderStatus.Cancelled);
        mockRepo.Verify(r => r.TryUpdateStatusWithHistoryAsync(
            order.Id, OrderStatus.Cancelled, It.IsAny<DateTime>(), It.IsAny<StatusHistory>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldThrowDomainException_ForInvalidTransition()
    {
        var owner = Guid.NewGuid();
        var order = Order.Create(owner, ValidCargo());
        var mockRepo = new Mock<IOrderRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var svc = new Application.Services.OrderService(mockRepo.Object);

        var act = () => svc.UpdateStatusAsync(order.Id, owner, "Client", new UpdateStatusRequest { NewStatus = OrderStatus.Delivered });

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldBeIdempotent_WhenSameStatus()
    {
        var owner = Guid.NewGuid();
        var order = Order.Create(owner, ValidCargo()); // Created
        var mockRepo = new Mock<IOrderRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var svc = new Application.Services.OrderService(mockRepo.Object);

        var result = await svc.UpdateStatusAsync(order.Id, owner, "Client", new UpdateStatusRequest { NewStatus = OrderStatus.Created });

        result.Status.Should().Be(OrderStatus.Created);
        // Should not call TryUpdate when idempotent
        mockRepo.Verify(r => r.TryUpdateStatusWithHistoryAsync(It.IsAny<Guid>(), It.IsAny<OrderStatus>(), It.IsAny<DateTime>(), It.IsAny<StatusHistory>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
