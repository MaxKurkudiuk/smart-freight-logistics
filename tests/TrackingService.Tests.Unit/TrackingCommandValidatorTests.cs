using FluentAssertions;
using FluentValidation;
using TrackingService.Application.DTOs;
using TrackingService.Application.Features.Tracking.Commands.UpdateTracking;

namespace TrackingService.Tests.Unit;

/// <summary>
/// 6.8 — MediatR pipeline validator (runs in ValidationBehavior before the handler;
/// ValidationException → API 400). Mirrors the DTO DataAnnotations.
/// </summary>
public sealed class TrackingCommandValidatorTests
{
    private static UpdateTrackingRequest ValidRequest() => new()
    {
        Latitude = 50.45,
        Longitude = 30.52,
        SpeedKmh = 60,
        Notes = "Kyiv depot"
    };

    [Fact]
    public void UpdateTrackingValidator_EmptyOrderId_ThrowsValidationException()
    {
        var validator = new UpdateTrackingCommandValidator();
        var cmd = new UpdateTrackingCommand(Guid.Empty, ValidRequest());

        Action act = () => validator.ValidateAndThrow(cmd);

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateTrackingCommand.OrderId));
    }

    [Fact]
    public void UpdateTrackingValidator_LatitudeOutOfRange_ThrowsValidationException()
    {
        var validator = new UpdateTrackingCommandValidator();
        var cmd = new UpdateTrackingCommand(Guid.NewGuid(), ValidRequest() with { Latitude = 91 });

        Action act = () => validator.ValidateAndThrow(cmd);

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().Contain(e => e.PropertyName.Contains(nameof(UpdateTrackingRequest.Latitude)));
    }

    [Fact]
    public void UpdateTrackingValidator_LongitudeOutOfRange_ThrowsValidationException()
    {
        var validator = new UpdateTrackingCommandValidator();
        var cmd = new UpdateTrackingCommand(Guid.NewGuid(), ValidRequest() with { Longitude = 181 });

        Action act = () => validator.ValidateAndThrow(cmd);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void UpdateTrackingValidator_NegativeSpeed_ThrowsValidationException()
    {
        var validator = new UpdateTrackingCommandValidator();
        var cmd = new UpdateTrackingCommand(Guid.NewGuid(), ValidRequest() with { SpeedKmh = -1 });

        Action act = () => validator.ValidateAndThrow(cmd);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void UpdateTrackingValidator_NotesTooLong_ThrowsValidationException()
    {
        var validator = new UpdateTrackingCommandValidator();
        var cmd = new UpdateTrackingCommand(Guid.NewGuid(), ValidRequest() with { Notes = new string('x', 501) });

        Action act = () => validator.ValidateAndThrow(cmd);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void UpdateTrackingValidator_ValidRequest_Passes()
    {
        var validator = new UpdateTrackingCommandValidator();

        validator.Validate(new UpdateTrackingCommand(Guid.NewGuid(), ValidRequest())).IsValid.Should().BeTrue();
    }
}
