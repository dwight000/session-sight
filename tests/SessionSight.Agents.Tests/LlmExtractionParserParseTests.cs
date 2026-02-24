using FluentAssertions;

namespace SessionSight.Agents.Tests;

public class LlmExtractionParserParseTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Parse_NullOrWhitespace_ReturnsNull(string? json)
    {
        var result = LlmExtractionParser.Parse(json!);
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_ValidEmptyObject_ReturnsExtraction()
    {
        // Strict deserialization should succeed on a valid JSON object
        var result = LlmExtractionParser.Parse("{}");
        result.Should().NotBeNull();
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsNull()
    {
        // Completely invalid JSON fails both strict and lenient
        var result = LlmExtractionParser.Parse("not json at all {{{");
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_ValidObjectWithUnknownFields_ReturnsExtraction()
    {
        // Strict deserialization fails on unknown fields, lenient parsing succeeds
        var json = """{"unknownSection": {"unknownField": "value"}, "demographics": {}}""";
        var result = LlmExtractionParser.Parse(json);
        result.Should().NotBeNull();
    }

    [Fact]
    public void Parse_NonObjectRootElement_ReturnsNull()
    {
        // JSON array at root — strict fails, lenient ParseFromElement returns null for non-object
        var result = LlmExtractionParser.Parse("[1,2,3]");
        result.Should().BeNull();
    }
}
