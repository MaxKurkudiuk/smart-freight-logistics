using FluentValidation;

namespace OrderService.Application.Features.Orders.Commands.CreateOrder;

/// <summary>
/// 6.2 defense-in-depth — mirrors DTO DataAnnotations for MediatR pipeline
/// (ValidationBehavior throws ValidationException → API 400 before handler).
/// </summary>
public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();

        RuleFor(x => x.Request.CargoType)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Request.WeightKg)
            .GreaterThan(0);

        RuleFor(x => x.Request.Origin)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Request.Destination)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x)
            .Must(x => !x.Request.Origin.Trim().Equals(x.Request.Destination.Trim(), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Origin and Destination must differ.");

        RuleFor(x => x.Request.Deadline)
            .GreaterThan(DateTime.UtcNow)
            .When(x => x.Request.Deadline.HasValue);
    }
}
