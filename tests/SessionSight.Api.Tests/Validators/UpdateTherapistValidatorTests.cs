using FluentAssertions;
using FluentValidation.TestHelper;
using SessionSight.Api.DTOs;
using SessionSight.Api.Validators;

namespace SessionSight.Api.Tests.Validators;

public class UpdateTherapistValidatorTests
{
    private readonly UpdateTherapistValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        var request = new UpdateTherapistRequest("Dr. Smith", "LIC001", "PhD", true);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyName_Fails()
    {
        var request = new UpdateTherapistRequest("", "LIC001", "PhD", true);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_NameTooLong_Fails()
    {
        var request = new UpdateTherapistRequest(new string('A', 201), "LIC001", "PhD", true);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_LicenseNumberTooLong_Fails()
    {
        var request = new UpdateTherapistRequest("Dr. Smith", new string('L', 51), "PhD", true);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.LicenseNumber);
    }

    [Fact]
    public void Validate_CredentialsTooLong_Fails()
    {
        var request = new UpdateTherapistRequest("Dr. Smith", "LIC001", new string('C', 51), true);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Credentials);
    }
}
