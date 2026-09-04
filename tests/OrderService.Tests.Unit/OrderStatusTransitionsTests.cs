using FluentAssertions;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;

namespace OrderService.Tests.Unit;

public sealed class OrderStatusTransitionsTests
{
    [Theory]
    [InlineData(OrderStatus.Created, OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Created, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Created, OrderStatus.InTransit, false)]
    [InlineData(OrderStatus.Created, OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.InTransit, true)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.InTransit, OrderStatus.Customs, true)]
    [InlineData(OrderStatus.InTransit, OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Customs, OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Customs, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Customs, OrderStatus.Confirmed, false)]
    public void CanTransit_ReturnsExpected(OrderStatus from, OrderStatus to, bool expected)
    {
        OrderStatusTransitions.CanTransit(from, to).Should().Be(expected);
    }

    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public void TerminalStates_HaveNoOutgoingTransitions(OrderStatus terminal)
    {
        foreach (OrderStatus target in Enum.GetValues<OrderStatus>())
        {
            if (terminal == target) continue; // self-transition is idempotent, tested separately
            OrderStatusTransitions.CanTransit(terminal, target).Should().BeFalse(
                $"terminal {terminal} should not transit to {target}");
        }
    }

    [Fact]
    public void Ensure_ThrowsDomainException_ForInvalidTransition()
    {
        var act = () => OrderStatusTransitions.Ensure(OrderStatus.Created, OrderStatus.Delivered);

        act.Should().Throw<DomainException>()
            .WithMessage("*Transition from Created to Delivered is not allowed*");
    }

    [Fact]
    public void Ensure_DoesNotThrow_ForValidTransition()
    {
        var act = () => OrderStatusTransitions.Ensure(OrderStatus.Created, OrderStatus.Confirmed);

        act.Should().NotThrow();
    }

    [Fact]
    public void Ensure_DoesNotThrow_ForIdempotentSelfTransition()
    {
        var act = () => OrderStatusTransitions.Ensure(OrderStatus.Created, OrderStatus.Created);

        act.Should().NotThrow();
    }

    [Fact]
    public void Ensure_Throws_ForTerminalToAny()
    {
        var act = () => OrderStatusTransitions.Ensure(OrderStatus.Delivered, OrderStatus.Cancelled);

        act.Should().Throw<DomainException>();
    }
}
