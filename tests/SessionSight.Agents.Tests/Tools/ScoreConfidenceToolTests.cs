using System.Text.Json;
using FluentAssertions;
using SessionSight.Agents.Tools;
using SessionSight.Agents.Validation;
using SessionSight.Core.Enums;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Tests.Tools;

public class ScoreConfidenceToolTests
{
    private readonly ConfidenceCalculator _calculator;
    private readonly ScoreConfidenceTool _tool;

    public ScoreConfidenceToolTests()
    {
        _calculator = new ConfidenceCalculator();  // Real implementation
        _tool = new ScoreConfidenceTool(_calculator);
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        _tool.Name.Should().Be("score_confidence");
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
    public async Task ExecuteAsync_WithValidExtraction_ReturnsConfidenceScore()
    {
        var extraction = CreateExtractionWithConfidence(0.9);
        var input = BinaryData.FromObjectAsJson(new { extraction });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonSerializer.Deserialize<TestOutput>(result.Data.ToStream());
        output!.OverallConfidence.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithLowConfidenceFields_ReturnsFieldList()
    {
        var extraction = CreateExtractionWithConfidence(0.5);
        var input = BinaryData.FromObjectAsJson(new { extraction, threshold = 0.7 });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonSerializer.Deserialize<TestOutput>(result.Data.ToStream());
        output!.LowConfidenceFields.Should().NotBeEmpty();
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
    public async Task ExecuteAsync_WithHighConfidenceRiskFields_ReturnsFalseForLowConfidenceRisk()
    {
        var extraction = new ClinicalExtraction
        {
            RiskAssessment = new RiskAssessmentExtracted
            {
                SuicidalIdeation = new ExtractedField<SuicidalIdeation>
                {
                    Value = SuicidalIdeation.ActiveNoPlan,
                    Confidence = 0.95  // High confidence
                },
                RiskLevelOverall = new ExtractedField<RiskLevelOverall>
                {
                    Value = RiskLevelOverall.High,
                    Confidence = 0.95  // High confidence
                }
            }
        };
        var input = BinaryData.FromObjectAsJson(new { extraction });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonSerializer.Deserialize<TestOutput>(result.Data.ToStream());
        output!.HasLowConfidenceRiskFields.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithLowConfidenceRiskFields_ReturnsTrueForLowConfidenceRisk()
    {
        var extraction = new ClinicalExtraction
        {
            RiskAssessment = new RiskAssessmentExtracted
            {
                SuicidalIdeation = new ExtractedField<SuicidalIdeation>
                {
                    Value = SuicidalIdeation.ActiveNoPlan,
                    Confidence = 0.7  // Below threshold
                },
                RiskLevelOverall = new ExtractedField<RiskLevelOverall>
                {
                    Value = RiskLevelOverall.High,
                    Confidence = 0.8  // Below threshold
                }
            }
        };
        var input = BinaryData.FromObjectAsJson(new { extraction });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonSerializer.Deserialize<TestOutput>(result.Data.ToStream());
        output!.HasLowConfidenceRiskFields.Should().BeTrue();
    }

    private static ClinicalExtraction CreateExtractionWithConfidence(double confidence)
    {
        return new ClinicalExtraction
        {
            SessionInfo = new SessionInfoExtracted
            {
                SessionDate = new ExtractedField<DateOnly>
                {
                    Value = DateOnly.FromDateTime(DateTime.Today),
                    Confidence = confidence
                }
            }
        };
    }

    private class TestOutput
    {
        public double OverallConfidence { get; set; }
        public List<string> LowConfidenceFields { get; set; } = [];
        public bool HasLowConfidenceRiskFields { get; set; }
        public double Threshold { get; set; }
    }
}
