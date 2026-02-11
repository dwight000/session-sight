using FluentAssertions;
using SessionSight.Core.ValueObjects;

namespace SessionSight.Core.Tests.ValueObjects;

public class ValueObjectTests
{
    [Fact]
    public void SourceMapping_DefaultValues_AreInitialized()
    {
        var mapping = new SourceMapping();

        mapping.Text.Should().BeEmpty();
        mapping.StartChar.Should().Be(0);
        mapping.EndChar.Should().Be(0);
        mapping.Section.Should().BeNull();
    }

    [Fact]
    public void SourceMapping_CanSetAllProperties()
    {
        var mapping = new SourceMapping
        {
            Text = "Patient reports anxiety",
            StartChar = 100,
            EndChar = 125,
            Section = "Chief Complaint"
        };

        mapping.Text.Should().Be("Patient reports anxiety");
        mapping.StartChar.Should().Be(100);
        mapping.EndChar.Should().Be(125);
        mapping.Section.Should().Be("Chief Complaint");
    }
}
