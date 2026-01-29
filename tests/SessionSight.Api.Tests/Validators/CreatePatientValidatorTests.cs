using FluentAssertions;
using FluentValidation.TestHelper;
using SessionSight.Api.DTOs;
using SessionSight.Api.Validators;

namespace SessionSight.Api.Tests.Validators;

public class CreatePatientValidatorTests
{
    private readonly CreatePatientValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        var request = new CreatePatientRequest("P001", "John", "Doe", new DateOnly(1990, 1, 1));
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyExternalId_Fails()
    {
        var request = new CreatePatientRequest("", "John", "Doe", new DateOnly(1990, 1, 1));
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ExternalId);
    }

    [Fact]
    public void Validate_EmptyFirstName_Fails()
    {
        var request = new CreatePatientRequest("P001", "", "Doe", new DateOnly(1990, 1, 1));
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void Validate_EmptyLastName_Fails()
    {
        var request = new CreatePatientRequest("P001", "John", "", new DateOnly(1990, 1, 1));
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }
}
