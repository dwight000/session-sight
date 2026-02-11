using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SessionSight.FunctionalTests.Fixtures;
using Xunit.Abstractions;

namespace SessionSight.FunctionalTests;

[Trait("Category", "Functional")]
public class GoldenNonRiskExtractionTests : IClassFixture<ApiFixture>
{
    private static readonly PreviewTracker Preview = new("/tmp/sessionsight/golden-nonrisk-previews");

    private readonly HttpClient _client;
    private readonly HttpClient _longClient;
    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _jsonOptions;

    public GoldenNonRiskExtractionTests(ApiFixture fixture, ITestOutputHelper output)
    {
        _client = fixture.Client;
        _longClient = fixture.LongClient;
        _output = output;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public static IEnumerable<object[]> GoldenCases() => GoldenNonRiskCaseProvider.GetSelectedCases();

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public async Task GoldenNonRiskCases_ExtractionMatchesExpectedFields(GoldenNonRiskCase goldenCase)
    {
        var selection = GoldenNonRiskCaseProvider.Selection;
        WriteSelectionManifest(selection);

        var sessionId = await CreateSessionWithNoteAsync(goldenCase);
        var triggerResult = await TriggerExtractionAsync(goldenCase, sessionId);
        if (!triggerResult.ShouldContinueAssertions)
        {
            return;
        }

        var extractionDto = await GetExtractionDtoAsync(sessionId);
        var extractionData = extractionDto.GetProperty("data");

        AssertExpectedSections(goldenCase, extractionData);
    }

    private async Task<Guid> CreateSessionWithNoteAsync(GoldenNonRiskCase goldenCase)
    {
        var patientRequest = new
        {
            externalId = $"NR-{goldenCase.NoteId}-{Guid.NewGuid():N}"[..36],
            firstName = "Golden",
            lastName = "NonRiskCase",
            dateOfBirth = "1990-01-01"
        };

        var patientResponse = await _client.PostAsJsonAsync("/api/patients", patientRequest);
        patientResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Patient creation should succeed for golden case {goldenCase.NoteId}");

        var patientJson = await patientResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var patientId = patientJson.GetProperty("id").GetGuid();

        var sessionRequest = new
        {
            patientId,
            therapistId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            sessionDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            sessionType = "Individual",
            modality = "InPerson",
            sessionNumber = 1
        };

        var sessionResponse = await _client.PostAsJsonAsync("/api/sessions", sessionRequest);
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Session creation should succeed for golden case {goldenCase.NoteId}");

        var sessionJson = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var sessionId = sessionJson.GetProperty("id").GetGuid();

        using var content = new MultipartFormDataContent();
        var pdfBytes = GoldenTestHelpers.CreatePdfDocument(goldenCase.NoteContent, maxLines: 80);
        Preview.TrySavePreviewPdf(goldenCase.NoteId, pdfBytes, _output);
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", $"{goldenCase.NoteId}.pdf");

        var uploadResponse = await _client.PostAsync($"/api/sessions/{sessionId}/document", content);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Document upload should succeed for golden case {goldenCase.NoteId}");

        return sessionId;
    }

    private async Task<TriggerExtractionResult> TriggerExtractionAsync(GoldenNonRiskCase goldenCase, Guid sessionId)
    {
        var extractionResponse = await _longClient.PostAsync($"/api/extraction/{sessionId}", null);
        extractionResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Extraction endpoint should return 200 for golden case {goldenCase.NoteId}");

        var extractionJson = await extractionResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var success = extractionJson.GetProperty("success").GetBoolean();

        if (!success)
        {
            var errorMessage = extractionJson.TryGetProperty("errorMessage", out var errProp)
                ? errProp.GetString()
                : "Unknown error";

            if (goldenCase.ExpectedOutcome is GoldenExpectedOutcome.ContentFilterBlocked or GoldenExpectedOutcome.ContentFilterOptional)
            {
                var normalizedError = errorMessage ?? string.Empty;
                normalizedError.Should().Contain("content_filter",
                    $"golden case {goldenCase.NoteId} expects content filter blocking.");
                _output.WriteLine(
                    $"Golden case {goldenCase.NoteId} matched expected content filter path: {normalizedError}");
                return new TriggerExtractionResult(
                    ShouldContinueAssertions: false,
                    Response: extractionJson);
            }

            throw new InvalidOperationException(
                $"Golden case {goldenCase.NoteId} extraction failed. Error: {errorMessage}");
        }

        if (goldenCase.ExpectedOutcome == GoldenExpectedOutcome.ContentFilterBlocked)
        {
            throw new InvalidOperationException(
                $"Golden case {goldenCase.NoteId} expected content filter blocking but extraction succeeded.");
        }

        return new TriggerExtractionResult(
            ShouldContinueAssertions: true,
            Response: extractionJson);
    }

    private async Task<JsonElement> GetExtractionDtoAsync(Guid sessionId)
    {
        var getResponse = await _client.GetAsync($"/api/sessions/{sessionId}/extraction");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Should retrieve saved extraction for session {sessionId}");

        return await getResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
    }

    private void AssertExpectedSections(GoldenNonRiskCase goldenCase, JsonElement extractionData)
    {
        var assertSections = ResolveAssertSections(goldenCase);

        foreach (var sectionName in assertSections)
        {
            var sectionFound = goldenCase.ExpectedSections.TryGetValue(sectionName, out var expectedSection);
            sectionFound.Should().BeTrue(
                $"Golden case {goldenCase.NoteId} missing expected_sections for section '{sectionName}' in {goldenCase.FilePath}");
            expectedSection.Should().NotBeNull();
            var expectedSectionValue = expectedSection!;

            extractionData.TryGetProperty(sectionName, out var actualSection)
                .Should().BeTrue(
                    $"Golden case {goldenCase.NoteId} section '{sectionName}' not found in extraction data.");

            var assertFields = ResolveAssertFields(goldenCase, expectedSectionValue);
            foreach (var fieldKey in assertFields.OrderBy(f => f, StringComparer.Ordinal))
            {
                expectedSectionValue.Fields.TryGetValue(fieldKey, out var expectedField)
                    .Should().BeTrue(
                        $"Golden case {goldenCase.NoteId} section '{sectionName}' missing expected field '{fieldKey}'.");

                AssertFieldByMatchMode(goldenCase, sectionName, fieldKey, expectedField!, actualSection);
            }
        }
    }

    private void AssertFieldByMatchMode(
        GoldenNonRiskCase goldenCase,
        string sectionName,
        string fieldKey,
        GoldenFieldExpectation expected,
        JsonElement actualSection)
    {
        var context = $"golden case {goldenCase.NoteId} ({goldenCase.TestType}) section '{sectionName}' field '{fieldKey}' from {goldenCase.FileName}";

        switch (expected.Match)
        {
            case GoldenMatchMode.Exact:
                AssertExact(actualSection, fieldKey, expected.Accept, context);
                break;
            case GoldenMatchMode.ContainsAny:
                AssertContainsAny(actualSection, fieldKey, expected.Accept, context);
                break;
            case GoldenMatchMode.AnyValue:
                AssertAnyValue(actualSection, fieldKey, expected.Accept, context);
                break;
            case GoldenMatchMode.AnyKeyword:
                AssertAnyKeyword(actualSection, fieldKey, expected.Accept, context);
                break;
            default:
                throw new InvalidOperationException($"Unsupported match mode '{expected.Match}' for {context}");
        }
    }

    private static void AssertExact(
        JsonElement section, string fieldKey, IReadOnlyList<string> accept, string context)
    {
        var actualValue = ExtractionAssertions.GetFieldValue(section, fieldKey);
        var normalizedAccept = accept.ToHashSet(StringComparer.OrdinalIgnoreCase);

        normalizedAccept.Should().Contain(actualValue,
            $"expected {fieldKey} in [{string.Join(", ", normalizedAccept)}] (exact match) — {context}");
    }

    private void AssertContainsAny(
        JsonElement section, string fieldKey, IReadOnlyList<string> accept, string context)
    {
        var actualValue = ExtractionAssertions.GetFieldValue(section, fieldKey);
        if (actualValue is null)
        {
            _output.WriteLine($"WARN: {fieldKey} is null for {context}; skipping contains_any check.");
            return;
        }

        var lower = actualValue.ToLowerInvariant();
        var anyMatch = accept.Any(keyword =>
            lower.Contains(keyword.ToLowerInvariant(), StringComparison.Ordinal));

        anyMatch.Should().BeTrue(
            $"expected {fieldKey} value '{actualValue}' to contain any of [{string.Join(", ", accept)}] — {context}");
    }

    private void AssertAnyValue(
        JsonElement section, string fieldKey, IReadOnlyList<string> accept, string context)
    {
        var actualValues = ExtractionAssertions.GetArrayValues(section, fieldKey);
        if (actualValues.Count == 0)
        {
            _output.WriteLine($"WARN: {fieldKey} array is empty for {context}; skipping any_value check.");
            return;
        }

        var acceptSet = accept.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var anyMatch = actualValues.Any(v => acceptSet.Contains(v));

        anyMatch.Should().BeTrue(
            $"expected {fieldKey} array [{string.Join(", ", actualValues)}] to contain at least one of [{string.Join(", ", accept)}] — {context}");
    }

    private void AssertAnyKeyword(
        JsonElement section, string fieldKey, IReadOnlyList<string> accept, string context)
    {
        var actualValues = ExtractionAssertions.GetArrayValues(section, fieldKey);
        if (actualValues.Count == 0)
        {
            _output.WriteLine($"WARN: {fieldKey} array is empty for {context}; skipping any_keyword check.");
            return;
        }

        var joined = string.Join(" ", actualValues).ToLowerInvariant();
        var anyMatch = accept.Any(keyword =>
            joined.Contains(keyword.ToLowerInvariant(), StringComparison.Ordinal));

        anyMatch.Should().BeTrue(
            $"expected {fieldKey} array joined '{joined}' to contain any keyword of [{string.Join(", ", accept)}] — {context}");
    }

    private static IReadOnlyCollection<string> ResolveAssertSections(GoldenNonRiskCase goldenCase)
    {
        if (goldenCase.AssertSections.Any(s =>
                string.Equals(s, "all", StringComparison.OrdinalIgnoreCase)))
        {
            return goldenCase.ExpectedSections.Keys.ToList();
        }

        var requested = goldenCase.AssertSections.ToList();
        if (requested.Count == 0)
        {
            throw new InvalidOperationException(
                $"Golden case {goldenCase.NoteId} has empty assert_sections in {goldenCase.FilePath}");
        }

        return requested;
    }

    private static IReadOnlyCollection<string> ResolveAssertFields(
        GoldenNonRiskCase goldenCase,
        GoldenSectionExpectation sectionExpectation)
    {
        if (goldenCase.AssertFields.Any(f =>
                string.Equals(f, "all", StringComparison.OrdinalIgnoreCase)))
        {
            return sectionExpectation.Fields.Keys.ToList();
        }

        var requested = goldenCase.AssertFields.ToList();
        if (requested.Count == 0)
        {
            throw new InvalidOperationException(
                $"Golden case {goldenCase.NoteId} has empty assert_fields in {goldenCase.FilePath}");
        }

        return requested;
    }

    private void WriteSelectionManifest(GoldenNonRiskSelection selection)
    {
        _output.WriteLine(
            $"Golden non-risk selection mode={selection.Mode.ToString().ToLowerInvariant()}, date={selection.EffectiveDateUtc:yyyy-MM-dd}, corpus={selection.CorpusCount}, candidates={selection.CandidateCount}, selected={selection.SelectedCount}, filter={selection.Filter ?? "(none)"}");
        _output.WriteLine("Selected cases: " + string.Join(", ", selection.SelectedCases.Select(c => c.NoteId)));
    }

    private sealed record TriggerExtractionResult(
        bool ShouldContinueAssertions,
        JsonElement Response);
}
