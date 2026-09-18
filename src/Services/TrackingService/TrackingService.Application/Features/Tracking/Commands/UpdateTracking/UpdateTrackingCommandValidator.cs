using FluentValidation;

namespace TrackingService.Application.Features.Tracking.Commands.UpdateTracking;

public sealed class UpdateTrackingCommandValidator : AbstractValidator<UpdateTrackingCommand>
{
    public UpdateTrackingCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Request.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Request.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.Request.SpeedKmh)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Request.SpeedKmh.HasValue);
        RuleFor(x => x.Request.Notes).MaximumLength(500);
    }
}
