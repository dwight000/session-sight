using System.Text.Json;
using FluentAssertions;
using SessionSight.Agents.Helpers;

namespace SessionSight.Agents.Tests.Helpers;

public class LlmJsonHelperTests
{
    [Fact]
    public void ExtractJson_BareJson_ReturnsAsIs()
    {
        var json = """{"key": "value"}""";
        LlmJsonHelper.ExtractJson(json).Should().Be(json);
    }

    [Fact]
    public void ExtractJson_ProseBeforeCodeFence()
    {
        var input = """
            Here is the extraction result:
            ```json
            {"key": "value"}
            ```
            """;
        LlmJsonHelper.ExtractJson(input).Should().Be("""{"key": "value"}""");
    }

    [Fact]
    public void ExtractJson_JsonEmbeddedInProse()
    {
        var input = """I analyzed the note. {"key": "value"} That's my answer.""";
        LlmJsonHelper.ExtractJson(input).Should().Be("""{"key": "value"}""");
    }

    [Fact]
    public void ExtractJson_CodeFenceMiddleOfText()
    {
        var input = """
            Some prose before.
            ```json
            {"suicidalIdeation": {"value": "None"}}
            ```
            More text after.
            """;
        LlmJsonHelper.ExtractJson(input).Should().Be("""{"suicidalIdeation": {"value": "None"}}""");
    }

    [Fact]
    public void ExtractJson_CodeFenceAtStart()
    {
        var input = """
            ```json
            {"key": "value"}
            ```
            """;
        LlmJsonHelper.ExtractJson(input).Should().Be("""{"key": "value"}""");
    }

    [Fact]
    public void ExtractJson_GenericCodeFenceAtStart()
    {
        var input = """
            ```
            {"key": "value"}
            ```
            """;
        LlmJsonHelper.ExtractJson(input).Should().Be("""{"key": "value"}""");
    }

    [Fact]
    public void TryParseConfidence_Number()
    {
        var json = """{"c": 0.95}""";
        using var doc = JsonDocument.Parse(json);
        var result = LlmJsonHelper.TryParseConfidence(doc.RootElement.GetProperty("c"));
        result.Should().Be(0.95);
    }

    [Fact]
    public void TryParseConfidence_String()
    {
        var json = """{"c": "0.85"}""";
        using var doc = JsonDocument.Parse(json);
        var result = LlmJsonHelper.TryParseConfidence(doc.RootElement.GetProperty("c"));
        result.Should().Be(0.85);
    }

    [Fact]
    public void TryParseConfidence_Invalid_ReturnsNull()
    {
        var json = """{"c": "not_a_number"}""";
        using var doc = JsonDocument.Parse(json);
        var result = LlmJsonHelper.TryParseConfidence(doc.RootElement.GetProperty("c"));
        result.Should().BeNull();
    }

    [Fact]
    public void TryParseConfidence_Null_ReturnsNull()
    {
        var json = """{"c": null}""";
        using var doc = JsonDocument.Parse(json);
        var result = LlmJsonHelper.TryParseConfidence(doc.RootElement.GetProperty("c"));
        result.Should().BeNull();
    }

    [Fact]
    public void TryParseInt_FromNumber()
    {
        var json = """{"n": 42}""";
        using var doc = JsonDocument.Parse(json);
        var result = LlmJsonHelper.TryParseInt(doc.RootElement.GetProperty("n"));
        result.Should().Be(42);
    }

    [Fact]
    public void TryParseInt_FromFloat()
    {
        var json = """{"n": 42.7}""";
        using var doc = JsonDocument.Parse(json);
        var result = LlmJsonHelper.TryParseInt(doc.RootElement.GetProperty("n"));
        result.Should().Be(42);
    }

    [Fact]
    public void TryParseInt_FromString()
    {
        var json = """{"n": "500"}""";
        using var doc = JsonDocument.Parse(json);
        var result = LlmJsonHelper.TryParseInt(doc.RootElement.GetProperty("n"));
        result.Should().Be(500);
    }

    [Fact]
    public void TryParseInt_Invalid_ReturnsNull()
    {
        var json = """{"n": "abc"}""";
        using var doc = JsonDocument.Parse(json);
        var result = LlmJsonHelper.TryParseInt(doc.RootElement.GetProperty("n"));
        result.Should().BeNull();
    }

    [Fact]
    public void TryParseDouble_FromNumber()
    {
        var json = """{"n": 3.14}""";
        using var doc = JsonDocument.Parse(json);
        var result = LlmJsonHelper.TryParseDouble(doc.RootElement.GetProperty("n"));
        result.Should().Be(3.14);
    }

    [Fact]
    public void TryParseDouble_FromString()
    {
        var json = """{"n": "2.71"}""";
        using var doc = JsonDocument.Parse(json);
        var result = LlmJsonHelper.TryParseDouble(doc.RootElement.GetProperty("n"));
        result.Should().Be(2.71);
    }
}
