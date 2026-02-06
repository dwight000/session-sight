using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace SessionSight.FunctionalTests.Fixtures;

/// <summary>
/// Shared field-level assertions for extracted data.
/// Verifies that pipeline produces correct clinical data from sample-note.pdf.
/// </summary>
internal static class ExtractionAssertions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static string? GetFieldValue(JsonElement section, string fieldName)
    {
        if (!section.TryGetProperty(fieldName, out var field))
            return null;
        if (!field.TryGetProperty("value", out var value))
            return null;
        return value.ValueKind == JsonValueKind.Null ? null : value.ToString();
    }

    internal static async Task AssertExtractionFields(HttpClient client, Guid sessionId)
    {
        var getResponse = await client.GetAsync($"/api/sessions/{sessionId}/extraction");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Should retrieve saved extraction");

        var dto = await getResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var data = dto.GetProperty("data");

        // Risk assessment (safety-critical - sample note says all None/Low)
        var risk = data.GetProperty("riskAssessment");
        GetFieldValue(risk, "suicidalIdeation").Should().Be("None",
            "Note says 'Suicidal ideation: None'");
        GetFieldValue(risk, "homicidalIdeation").Should().Be("None",
            "Note says 'Homicidal ideation: None'");
        GetFieldValue(risk, "riskLevelOverall").Should().Be("Low",
            "Note says 'Overall risk level: Low'");

        // Presenting concerns (sample note: anxiety + work stress)
        var concerns = data.GetProperty("presentingConcerns");
        var primaryConcern = GetFieldValue(concerns, "primaryConcern");
        primaryConcern.Should().NotBeNull("Note has a clear presenting concern");
        primaryConcern!.ToLowerInvariant().Should().Contain("anxi",
            "Note says 'ongoing anxiety related to work stress'");

        // Mood (sample note: "Current mood: 5/10")
        var mood = data.GetProperty("moodAssessment");
        var selfReportedMood = GetFieldValue(mood, "selfReportedMood");
        selfReportedMood.Should().NotBeNull("Note says 'Current mood: 5/10'");
        selfReportedMood.Should().Be("5", "Note says 'Current mood: 5/10'");

        // Overall confidence should be non-zero
        dto.GetProperty("overallConfidence").GetDouble().Should().BeGreaterThan(0,
            "Extraction should have non-zero confidence");
    }
}
