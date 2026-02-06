using System.Text.Json;
using FluentAssertions;
using SessionSight.Core.Schema;
using SessionSight.Core.ValueObjects;

namespace SessionSight.Core.Tests.ValueObjects;

public class SourceMappingConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Deserialize_ObjectForm_ParsesAllFields()
    {
        var json = """{"source": {"text": "exact quote", "section": "risk", "startChar": 10, "endChar": 50}}""";
        var wrapper = JsonSerializer.Deserialize<SourceWrapper>(json, Options);

        wrapper!.Source.Should().NotBeNull();
        wrapper.Source!.Text.Should().Be("exact quote");
        wrapper.Source.Section.Should().Be("risk");
        wrapper.Source.StartChar.Should().Be(10);
        wrapper.Source.EndChar.Should().Be(50);
    }

    [Fact]
    public void Deserialize_StringForm_ParsesAsText()
    {
        var json = """{"source": "Session Note - January 15, 2026"}""";
        var wrapper = JsonSerializer.Deserialize<SourceWrapper>(json, Options);

        wrapper!.Source.Should().NotBeNull();
        wrapper.Source!.Text.Should().Be("Session Note - January 15, 2026");
        wrapper.Source.Section.Should().BeNull();
    }

    [Fact]
    public void Deserialize_Null_ReturnsNull()
    {
        var json = """{"source": null}""";
        var wrapper = JsonSerializer.Deserialize<SourceWrapper>(json, Options);

        wrapper!.Source.Should().BeNull();
    }

    [Fact]
    public void Deserialize_EmptyObject_ReturnsDefaults()
    {
        var json = """{"source": {}}""";
        var wrapper = JsonSerializer.Deserialize<SourceWrapper>(json, Options);

        wrapper!.Source.Should().NotBeNull();
        wrapper.Source!.Text.Should().BeEmpty();
    }

    [Fact]
    public void Serialize_RoundTrips_ObjectForm()
    {
        var source = new SourceMapping { Text = "quote", Section = "risk", StartChar = 5, EndChar = 20 };
        var json = JsonSerializer.Serialize(new SourceWrapper { Source = source }, Options);
        var deserialized = JsonSerializer.Deserialize<SourceWrapper>(json, Options);

        deserialized!.Source!.Text.Should().Be("quote");
        deserialized.Source.Section.Should().Be("risk");
        deserialized.Source.StartChar.Should().Be(5);
        deserialized.Source.EndChar.Should().Be(20);
    }

    [Fact]
    public void Deserialize_ExtractedField_WithStringSource()
    {
        var json = """{"value": "test", "confidence": 0.95, "source": "some text"}""";
        var field = JsonSerializer.Deserialize<ExtractedField<string>>(json, Options);

        field!.Value.Should().Be("test");
        field.Confidence.Should().Be(0.95);
        field.Source.Should().NotBeNull();
        field.Source!.Text.Should().Be("some text");
    }

    [Fact]
    public void Deserialize_ObjectWithUnknownProperties_SkipsThem()
    {
        var json = """{"source": {"text": "quote", "unknownProp": 42, "section": "risk"}}""";
        var wrapper = JsonSerializer.Deserialize<SourceWrapper>(json, Options);

        wrapper!.Source.Should().NotBeNull();
        wrapper.Source!.Text.Should().Be("quote");
        wrapper.Source.Section.Should().Be("risk");
    }

    private sealed class SourceWrapper
    {
        public SourceMapping? Source { get; set; }
    }
}
