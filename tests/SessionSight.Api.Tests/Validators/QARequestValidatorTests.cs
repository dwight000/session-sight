using FluentValidation.TestHelper;
using SessionSight.Agents.Models;
using SessionSight.Api.Validators;

namespace SessionSight.Api.Tests.Validators;

public class QARequestValidatorTests
{
    private readonly QARequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        var request = new QARequest { Question = "How is the patient doing?" };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyQuestion_Fails()
    {
        var request = new QARequest { Question = "" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Question);
    }

    [Fact]
    public void Validate_QuestionTooLong_Fails()
    {
        var request = new QARequest { Question = new string('a', 2001) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Question);
    }

    [Fact]
    public void Validate_QuestionAtMaxLength_Passes()
    {
        var request = new QARequest { Question = new string('a', 2000) };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
