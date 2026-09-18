using FluentAssertions;
using FluentValidation;
using OrderService.Application.DTOs;
using OrderService.Application.Features.Orders.Commands.CreateOrder;
using OrderService.Application.Features.Orders.Commands.UpdateOrderStatus;
using OrderService.Application.Features.Orders.Queries.GetOrderById;
using OrderService.Domain.Enums;

namespace OrderService.Tests.Unit;

/// <summary>
/// 6.8 — MediatR pipeline validators (run in ValidationBehavior before handlers;
/// ValidationException → API 400). Each case asserts the pipeline-facing contract.
/// </summary>
public sealed class OrderCommandValidatorTests
{
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
    public void CreateValidator_EmptyClientId_ThrowsValidationException()
    {
        var validator = new CreateOrderCommandValidator();
        var cmd = new CreateOrderCommand(Guid.Empty, ValidRequest());

        Action act = () => validator.ValidateAndThrow(cmd);

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.ClientId));
    }

    [Fact]
    public void CreateValidator_EmptyCargoType_ThrowsValidationException()
    {
        var validator = new CreateOrderCommandValidator();
        var cmd = new CreateOrderCommand(Guid.NewGuid(), ValidRequest() with { CargoType = "" });

        Action act = () => validator.ValidateAndThrow(cmd);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void CreateValidator_WeightNotPositive_ThrowsValidationException()
    {
        var validator = new CreateOrderCommandValidator();
        var cmd = new CreateOrderCommand(Guid.NewGuid(), ValidRequest() with { WeightKg = 0 });

        Action act = () => validator.ValidateAndThrow(cmd);

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().Contain(e => e.PropertyName.Contains(nameof(CreateOrderRequest.WeightKg)));
    }

    [Fact]
    public void CreateValidator_SameOriginDestination_ThrowsValidationException()
    {
        var validator = new CreateOrderCommandValidator();
        var cmd = new CreateOrderCommand(Guid.NewGuid(), ValidRequest() with { Origin = "Kyiv, UA", Destination = "KYIV, ua" });

        Action act = () => validator.ValidateAndThrow(cmd);

        act.Should().Throw<ValidationException>()
            .WithMessage("*Origin and Destination must differ*");
    }

    [Fact]
    public void CreateValidator_PastDeadline_ThrowsValidationException()
    {
        var validator = new CreateOrderCommandValidator();
        var cmd = new CreateOrderCommand(Guid.NewGuid(), ValidRequest() with { Deadline = DateTime.UtcNow.AddHours(-1) });

        Action act = () => validator.ValidateAndThrow(cmd);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void CreateValidator_ValidRequest_Passes()
    {
        var validator = new CreateOrderCommandValidator();

        validator.Validate(new CreateOrderCommand(Guid.NewGuid(), ValidRequest())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateStatusValidator_InvalidEnum_ThrowsValidationException()
    {
        var validator = new UpdateOrderStatusCommandValidator();
        var cmd = new UpdateOrderStatusCommand(Guid.NewGuid(), Guid.NewGuid(), "Client",
            new UpdateStatusRequest { NewStatus = (OrderStatus)999 });

        Action act = () => validator.ValidateAndThrow(cmd);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void UpdateStatusValidator_NotesTooLong_ThrowsValidationException()
    {
        var validator = new UpdateOrderStatusCommandValidator();
        var cmd = new UpdateOrderStatusCommand(Guid.NewGuid(), Guid.NewGuid(), "Client",
            new UpdateStatusRequest { NewStatus = OrderStatus.Cancelled, Notes = new string('x', 501) });

        Action act = () => validator.ValidateAndThrow(cmd);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void UpdateStatusValidator_ValidRequest_Passes()
    {
        var validator = new UpdateOrderStatusCommandValidator();
        var cmd = new UpdateOrderStatusCommand(Guid.NewGuid(), Guid.NewGuid(), "Client",
            new UpdateStatusRequest { NewStatus = OrderStatus.Cancelled, Notes = "client cancel" });

        validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetOrderByIdValidator_EmptyIds_ThrowValidationException()
    {
        var validator = new GetOrderByIdQueryValidator();

        Action emptyOrder = () => validator.ValidateAndThrow(new GetOrderByIdQuery(Guid.Empty, Guid.NewGuid(), "Client"));
        Action emptyRequester = () => validator.ValidateAndThrow(new GetOrderByIdQuery(Guid.NewGuid(), Guid.Empty, "Client"));

        emptyOrder.Should().Throw<ValidationException>();
        emptyRequester.Should().Throw<ValidationException>();
    }

    [Fact]
    public void GetOrderByIdValidator_ValidQuery_Passes()
    {
        var validator = new GetOrderByIdQueryValidator();

        validator.Validate(new GetOrderByIdQuery(Guid.NewGuid(), Guid.NewGuid(), "Client")).IsValid.Should().BeTrue();
    }
}
