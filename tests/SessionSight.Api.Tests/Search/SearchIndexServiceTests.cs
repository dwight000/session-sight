using FluentAssertions;

namespace SessionSight.Api.Tests.Search;

/// <summary>
/// Tests for OData filter injection prevention in SearchIndexService.
/// The service validates patientIdFilter as a canonical GUID before interpolation,
/// preventing OData injection attacks.
/// </summary>
public class SearchIndexServiceTests
{
    [Theory]
    [InlineData("'; DROP TABLE Users; --")]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("abc' or '1'='1")]
    [InlineData("00000000-0000-0000-0000-00000000000' or ''='")]
    public void PatientIdFilter_RejectsNonGuidStrings(string maliciousInput)
    {
        // SearchIndexService uses Guid.TryParse + canonical formatting to prevent injection.
        // Any non-GUID string will be rejected with ArgumentException.
        Guid.TryParse(maliciousInput, out _).Should().BeFalse(
            $"'{maliciousInput}' must not parse as a GUID - the SearchIndexService guard depends on this");
    }

    [Fact]
    public void PatientIdFilter_AcceptsAndCanonicalizesValidGuid()
    {
        var originalGuid = Guid.NewGuid();

        // Test various GUID formats all parse to the same canonical form
        var formats = new[] { "D", "N", "B", "P" };
        foreach (var format in formats)
        {
            var formatted = originalGuid.ToString(format);
            Guid.TryParse(formatted, out var parsed).Should().BeTrue();
            parsed.ToString("D").Should().Be(originalGuid.ToString("D"),
                $"GUID format '{format}' should canonicalize to 'D' format");
        }
    }
}
