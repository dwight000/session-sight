using FluentAssertions;
using FluentValidation.TestHelper;
using SessionSight.Api.DTOs;
using SessionSight.Api.Validators;
using SessionSight.Core.Enums;

namespace SessionSight.Api.Tests.Validators;

public class CreateSessionValidatorTests
{
    private readonly CreateSessionValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        var request = new CreateSessionRequest(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 20),
            SessionType.Individual, SessionModality.InPerson, 50, 1);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyPatientId_Fails()
    {
        var request = new CreateSessionRequest(
            Guid.Empty, Guid.NewGuid(), new DateOnly(2026, 1, 20),
            SessionType.Individual, SessionModality.InPerson, 50, 1);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PatientId);
    }

    [Fact]
    public void Validate_InvalidEnum_Fails()
    {
        var request = new CreateSessionRequest(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 20),
            (SessionType)999, SessionModality.InPerson, 50, 1);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.SessionType);
    }

    [Fact]
    public void Validate_ZeroDuration_Fails()
    {
        var request = new CreateSessionRequest(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 20),
            SessionType.Individual, SessionModality.InPerson, 0, 1);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DurationMinutes);
    }

    [Fact]
    public void Validate_NullDuration_Passes()
    {
        var request = new CreateSessionRequest(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 20),
            SessionType.Individual, SessionModality.InPerson, null, 1);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.DurationMinutes);
    }
}
