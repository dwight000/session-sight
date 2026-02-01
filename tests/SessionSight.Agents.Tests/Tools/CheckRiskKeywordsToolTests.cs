using System.Text.Json;
using FluentAssertions;
using SessionSight.Agents.Tools;
using SessionSight.Agents.Validation;

namespace SessionSight.Agents.Tests.Tools;

public class CheckRiskKeywordsToolTests
{
    private readonly CheckRiskKeywordsTool _tool = new();

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        _tool.Name.Should().Be("check_risk_keywords");
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
            .Should().Contain("text");
    }

    [Fact]
    public async Task ExecuteAsync_WithSuicidalKeywords_ReturnsMatches()
    {
        var input = BinaryData.FromObjectAsJson(new { text = "Patient reports thoughts of suicide and says they want to die." });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonSerializer.Deserialize<TestOutput>(result.Data.ToStream());
        output!.HasAnyMatches.Should().BeTrue();
        output.SuicidalMatches.Should().Contain("suicide");
        output.SuicidalMatches.Should().Contain("want to die");
    }

    [Fact]
    public async Task ExecuteAsync_WithSelfHarmKeywords_ReturnsMatches()
    {
        var input = BinaryData.FromObjectAsJson(new { text = "Patient has history of cutting and self-harm." });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonSerializer.Deserialize<TestOutput>(result.Data.ToStream());
        output!.HasAnyMatches.Should().BeTrue();
        output.SelfHarmMatches.Should().Contain("cutting");
        output.SelfHarmMatches.Should().Contain("self-harm");
    }

    [Fact]
    public async Task ExecuteAsync_WithHomicidalKeywords_ReturnsMatches()
    {
        var input = BinaryData.FromObjectAsJson(new { text = "Patient reports homicidal thoughts." });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonSerializer.Deserialize<TestOutput>(result.Data.ToStream());
        output!.HasAnyMatches.Should().BeTrue();
        output.HomicidalMatches.Should().Contain("homicidal");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoKeywords_ReturnsNoMatches()
    {
        var input = BinaryData.FromObjectAsJson(new { text = "Patient reports feeling better. Mood is stable." });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonSerializer.Deserialize<TestOutput>(result.Data.ToStream());
        output!.HasAnyMatches.Should().BeFalse();
        output.SuicidalMatches.Should().BeEmpty();
        output.SelfHarmMatches.Should().BeEmpty();
        output.HomicidalMatches.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingText_ReturnsError()
    {
        var input = BinaryData.FromObjectAsJson(new { });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("text");
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
    public async Task ExecuteAsync_MatchesUnderlyingImplementation()
    {
        var text = "Patient is suicidal with cutting history and homicidal thoughts.";
        var input = BinaryData.FromObjectAsJson(new { text });

        var toolResult = await _tool.ExecuteAsync(input);
        var directResult = DangerKeywordChecker.Check(text);

        var output = JsonSerializer.Deserialize<TestOutput>(toolResult.Data.ToStream());
        output!.SuicidalMatches.Should().BeEquivalentTo(directResult.SuicidalMatches);
        output.SelfHarmMatches.Should().BeEquivalentTo(directResult.SelfHarmMatches);
        output.HomicidalMatches.Should().BeEquivalentTo(directResult.HomicidalMatches);
        output.HasAnyMatches.Should().Be(directResult.HasAnyMatches);
    }

    private class TestOutput
    {
        public List<string> SuicidalMatches { get; set; } = [];
        public List<string> SelfHarmMatches { get; set; } = [];
        public List<string> HomicidalMatches { get; set; } = [];
        public bool HasAnyMatches { get; set; }
    }
}
