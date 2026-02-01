using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using SessionSight.Agents.Tools;
using SessionSight.Agents.Validation;
using SessionSight.Core.Enums;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Tests.Tools;

public class ValidateSchemaToolTests
{
    private readonly ISchemaValidator _mockValidator;
    private readonly ValidateSchemaTool _tool;

    public ValidateSchemaToolTests()
    {
        _mockValidator = Substitute.For<ISchemaValidator>();
        _tool = new ValidateSchemaTool(_mockValidator);
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        _tool.Name.Should().Be("validate_schema");
    }

    [Fact]
    public void Description_IsNotEmpty()
    {
        _tool.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InputSchema_IsValidJson()
    {
        var schema = _tool.InputSchema.ToString();
        var parsed = JsonDocument.Parse(schema);
        parsed.RootElement.GetProperty("type").GetString().Should().Be("object");
        parsed.RootElement.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain("extraction");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidExtraction_ReturnsSuccess()
    {
        var extraction = CreateValidExtraction();
        _mockValidator.Validate(Arg.Any<ClinicalExtraction>())
            .Returns(ValidationResult.Success());

        var input = BinaryData.FromObjectAsJson(new { extraction });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonSerializer.Deserialize<TestOutput>(result.Data.ToStream());
        output!.IsValid.Should().BeTrue();
        output.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidExtraction_ReturnsErrors()
    {
        var extraction = CreateValidExtraction();
        var validationErrors = new[]
        {
            new ValidationError("SessionInfo.SessionDate", "SessionDate is required"),
            new ValidationError("SessionInfo.SessionDurationMinutes", "Duration must be positive", ValidationSeverity.Warning)
        };
        _mockValidator.Validate(Arg.Any<ClinicalExtraction>())
            .Returns(ValidationResult.Failure(validationErrors));

        var input = BinaryData.FromObjectAsJson(new { extraction });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue(); // Tool execution succeeded
        var output = JsonSerializer.Deserialize<TestOutput>(result.Data.ToStream());
        output!.IsValid.Should().BeFalse();
        output.Errors.Should().HaveCount(2);
        output.Errors[0].Field.Should().Be("SessionInfo.SessionDate");
        output.Errors[0].Message.Should().Be("SessionDate is required");
        output.Errors[0].Severity.Should().Be("Error");
        output.Errors[1].Severity.Should().Be("Warning");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingExtraction_ReturnsError()
    {
        var input = BinaryData.FromObjectAsJson(new { });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("extraction");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidJson_ReturnsError()
    {
        var input = BinaryData.FromString("not valid json");

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid JSON");
    }

    [Fact]
    public async Task ExecuteAsync_CallsValidator()
    {
        var extraction = CreateValidExtraction();
        _mockValidator.Validate(Arg.Any<ClinicalExtraction>())
            .Returns(ValidationResult.Success());

        var input = BinaryData.FromObjectAsJson(new { extraction });

        await _tool.ExecuteAsync(input);

        _mockValidator.Received(1).Validate(Arg.Any<ClinicalExtraction>());
    }

    private static ClinicalExtraction CreateValidExtraction()
    {
        return new ClinicalExtraction
        {
            SessionInfo = new SessionInfoExtracted
            {
                SessionDate = new ExtractedField<DateOnly>
                {
                    Value = new DateOnly(2024, 1, 15),
                    Confidence = 0.95
                },
                SessionDurationMinutes = new ExtractedField<int>
                {
                    Value = 50,
                    Confidence = 0.90
                }
            },
            RiskAssessment = new RiskAssessmentExtracted
            {
                SuicidalIdeation = new ExtractedField<SuicidalIdeation>
                {
                    Value = SuicidalIdeation.None,
                    Confidence = 0.95
                },
                RiskLevelOverall = new ExtractedField<RiskLevelOverall>
                {
                    Value = RiskLevelOverall.Low,
                    Confidence = 0.95
                }
            }
        };
    }

    private class TestOutput
    {
        public bool IsValid { get; set; }
        public List<TestError> Errors { get; set; } = [];
    }

    private class TestError
    {
        public string Field { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
    }
}
