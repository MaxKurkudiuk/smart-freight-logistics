using FluentValidation;

namespace OrderService.Application.Features.Orders.Commands.UpdateOrderStatus;

public sealed class UpdateOrderStatusCommandValidator : AbstractValidator<UpdateOrderStatusCommand>
{
    public UpdateOrderStatusCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();

        RuleFor(x => x.Request.NewStatus).IsInEnum();

        RuleFor(x => x.Request.Notes)
            .MaximumLength(500)
            .When(x => x.Request.Notes is not null);
    }
}
