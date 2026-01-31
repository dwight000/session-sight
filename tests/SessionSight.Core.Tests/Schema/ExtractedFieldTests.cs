using FluentAssertions;
using SessionSight.Core.Schema;
using SessionSight.Core.ValueObjects;

namespace SessionSight.Core.Tests.Schema;

public class ExtractedFieldTests
{
    [Fact]
    public void ExtractedField_DefaultConfidence_IsZero()
    {
        var field = new ExtractedField<string>();
        field.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void ExtractedField_DefaultValue_IsNull()
    {
        var field = new ExtractedField<int?>();
        field.Value.Should().BeNull();
    }

    [Fact]
    public void ExtractedField_WithValue_StoresCorrectly()
    {
        var field = new ExtractedField<int> { Value = 7, Confidence = 0.95 };
        field.Value.Should().Be(7);
        field.Confidence.Should().Be(0.95);
    }

    [Fact]
    public void ExtractedField_SourceMapping_DefaultNull()
    {
        var field = new ExtractedField<string>();
        field.Source.Should().BeNull();
    }

    [Fact]
    public void ExtractedField_WithSourceMapping_StoresCorrectly()
    {
        var field = new ExtractedField<string>
        {
            Value = "anxiety",
            Confidence = 0.85,
            Source = new SourceMapping { Text = "Patient reports anxiety", StartChar = 10, EndChar = 33, Section = "assessment" }
        };
        field.Source.Should().NotBeNull();
        field.Source!.Text.Should().Be("Patient reports anxiety");
        field.Source.Section.Should().Be("assessment");
    }
}
