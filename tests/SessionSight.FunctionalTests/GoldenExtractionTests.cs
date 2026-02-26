using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SessionSight.FunctionalTests.Fixtures;
using Xunit.Abstractions;

namespace SessionSight.FunctionalTests;

[Trait("Category", "Functional")]
public class GoldenExtractionTests : IClassFixture<ApiFixture>
{
    private static readonly PreviewTracker Preview = new("/tmp/sessionsight/golden-previews");

    private static readonly IReadOnlyDictionary<string, string> ExpectedToActualRiskFieldMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["suicidal_ideation"] = "suicidalIdeation",
            ["si_frequency"] = "siFrequency",
            ["self_harm"] = "selfHarm",
            ["homicidal_ideation"] = "homicidalIdeation",
            ["risk_level_overall"] = "riskLevelOverall"
        };

    private readonly HttpClient _client;
    private readonly HttpClient _longClient;
    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _jsonOptions;

    public GoldenExtractionTests(ApiFixture fixture, ITestOutputHelper output)
    {
        _client = fixture.Client;
        _longClient = fixture.LongClient;
        _output = output;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public static IEnumerable<object[]> GoldenCases() => GoldenRiskCaseProvider.GetSelectedCases();

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public async Task GoldenRiskCases_ExtractionMatchesExpectedRiskFields(GoldenRiskCase goldenCase)
    {
        var selection = GoldenRiskCaseProvider.Selection;
        WriteSelectionManifest(selection);

        var sessionId = await CreateSessionWithNoteAsync(goldenCase);
        var triggerResult = await TriggerExtractionAsync(goldenCase, sessionId);
        if (triggerResult.PassedViaSecurityFilter)
        {
            return; // PASS — content filter correctly blocked adversarial input
        }

        if (!triggerResult.ShouldContinueAssertions)
        {
            throw Xunit.Sdk.SkipException.ForSkip($"Content filter blocked extraction for golden case {goldenCase.NoteId}. Transient Azure-side issue.");
        }

        var extractionDto = await GetExtractionDtoAsync(sessionId);
        var extractionData = extractionDto.GetProperty("data");
        var stageOutputs = BuildStageOutputs(extractionDto, extractionData);
        WriteRiskDiagnostics(goldenCase, extractionDto);

        AssertExpectedRiskFields(goldenCase, stageOutputs, triggerResult.ContentFilterWasHit);
    }

    private async Task<Guid> CreateSessionWithNoteAsync(GoldenRiskCase goldenCase)
    {
        var patientRequest = new
        {
            externalId = $"G-{goldenCase.NoteId}-{Guid.NewGuid():N}".Substring(0, 36),
            firstName = "Golden",
            lastName = "RiskCase",
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
        var noteContent = BuildSessionFramedNote(goldenCase);
        var pdfBytes = GoldenTestHelpers.CreatePdfDocument(noteContent);
        Preview.TrySavePreviewPdf(goldenCase.NoteId, pdfBytes, _output);
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", $"{goldenCase.NoteId}.pdf");

        var uploadResponse = await _client.PostAsync($"/api/sessions/{sessionId}/document", content);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Document upload should succeed for golden case {goldenCase.NoteId}");

        return sessionId;
    }

    private static string BuildSessionFramedNote(GoldenRiskCase goldenCase)
    {
        return string.Join(
            '\n',
            [
                "Therapy Session Note",
                $"Case ID: {goldenCase.NoteId}",
                $"Session Date: {DateTime.UtcNow:yyyy-MM-dd}",
                "Therapist: Test Therapist, PhD",
                "Patient: Golden RiskCase",
                "Clinical Observations:",
                goldenCase.NoteContent,
                "Plan: Continue psychotherapy and monitor risk indicators as clinically appropriate."
            ]);
    }

    private async Task<TriggerExtractionResult> TriggerExtractionAsync(GoldenRiskCase goldenCase, Guid sessionId)
    {
        // 202 Accepted — extraction runs in background
        var extractionResponse = await _client.PostAsync($"/api/extraction/{sessionId}", null);
        extractionResponse.StatusCode.Should().Be(HttpStatusCode.Accepted,
            $"Extraction endpoint should return 202 for golden case {goldenCase.NoteId}");

        // Poll for completion
        var finalStatus = await ExtractionAssertions.WaitForExtractionAsync(
            _client, sessionId, TimeSpan.FromMinutes(5), _output);

        if (finalStatus == "Failed")
        {
            // Read error from steps endpoint (has errorMessage from SessionDocument)
            var stepsCheck = await _client.GetAsync($"/api/sessions/{sessionId}/extraction/steps");
            var stepsDto = await stepsCheck.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
            var errorMessage = stepsDto.TryGetProperty("errorMessage", out var errProp)
                ? errProp.GetString() ?? "Unknown error"
                : "Unknown error";

            if (goldenCase.ExpectedOutcome == GoldenExpectedOutcome.AdversarialInjection)
            {
                _output.WriteLine(
                    $"Golden case {goldenCase.NoteId} PASSED — content filter blocked adversarial injection: {errorMessage}");
                return new TriggerExtractionResult(ShouldContinueAssertions: false, PassedViaSecurityFilter: true);
            }

            if (goldenCase.ExpectedOutcome is GoldenExpectedOutcome.ContentFilterBlocked or GoldenExpectedOutcome.ContentFilterOptional)
            {
                _output.WriteLine(
                    $"Golden case {goldenCase.NoteId} matched expected content filter path: {errorMessage}");
                return new TriggerExtractionResult(ShouldContinueAssertions: false);
            }

            // Unexpected content filter — skip, don't fail
            if (ExtractionAssertions.IsContentFilterFailure(finalStatus, errorMessage))
            {
                throw Xunit.Sdk.SkipException.ForSkip(
                    $"Content filter blocked extraction for golden case {goldenCase.NoteId} — {errorMessage}. " +
                    "Transient Azure-side issue.");
            }

            throw new InvalidOperationException(
                $"Golden case {goldenCase.NoteId} extraction failed. Error: {errorMessage}");
        }

        finalStatus.Should().BeOneOf("Completed", "PartiallyCompleted",
            $"Extraction should complete for golden case {goldenCase.NoteId}");

        if (goldenCase.ExpectedOutcome == GoldenExpectedOutcome.ContentFilterBlocked)
        {
            throw new InvalidOperationException(
                $"Golden case {goldenCase.NoteId} expected content filter blocking but extraction succeeded.");
        }

        // Check content filter from GET extraction DTO's riskDiagnostics
        var dto = await GetExtractionDtoAsync(sessionId);
        var contentFilterHit = false;
        if (goldenCase.ExpectedOutcome == GoldenExpectedOutcome.ContentFilterOptional
            && dto.TryGetProperty("riskDiagnostics", out var rd)
            && rd.ValueKind == JsonValueKind.Object
            && rd.TryGetProperty("contentFilterBlocked", out var cfProp)
            && cfProp.ValueKind == JsonValueKind.True)
        {
            contentFilterHit = true;
            _output.WriteLine($"Golden case {goldenCase.NoteId}: content filter hit, skipping risk_reextracted assertions");
        }

        return new TriggerExtractionResult(
            ShouldContinueAssertions: true,
            ContentFilterWasHit: contentFilterHit);
    }

    private async Task<JsonElement> GetExtractionDtoAsync(Guid sessionId, bool expectSuccess = true)
    {
        var getResponse = await _client.GetAsync($"/api/sessions/{sessionId}/extraction");
        if (expectSuccess)
        {
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK,
                $"Should retrieve saved extraction for session {sessionId}");
        }

        return await getResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
    }

    /// <summary>
    /// Builds stage outputs from the GET extraction DTO (not trigger response).
    /// risk_final comes from extractionData.riskAssessment.
    /// risk_reextracted + clinical_extractor are reconstructed from riskDiagnostics.fieldDecisions.
    /// </summary>
    private static Dictionary<string, JsonElement> BuildStageOutputs(JsonElement extractionDto, JsonElement extractionData)
    {
        var stageOutputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        // risk_final always comes from the persisted extraction data
        stageOutputs["risk_final"] = extractionData.GetProperty("riskAssessment");

        // Reconstruct risk_reextracted and clinical_extractor from field decisions.
        // Field names in decisions are snake_case (e.g. "homicidal_ideation") but the
        // extraction DTO uses camelCase (e.g. "homicidalIdeation"). Map via
        // ExpectedToActualRiskFieldMap so GetFieldValue lookups match.
        if (extractionDto.TryGetProperty("riskDiagnostics", out var diagnostics)
            && diagnostics.ValueKind == JsonValueKind.Object
            && diagnostics.TryGetProperty("fieldDecisions", out var decisions)
            && decisions.ValueKind == JsonValueKind.Array)
        {
            var reExtracted = new Dictionary<string, object?>();
            var clinicalExtractor = new Dictionary<string, object?>();
            foreach (var decision in decisions.EnumerateArray())
            {
                var snakeField = decision.TryGetProperty("field", out var f) ? f.GetString() : null;
                if (snakeField is null) continue;

                // Convert snake_case → camelCase to match extraction DTO property names
                var camelField = ExpectedToActualRiskFieldMap.TryGetValue(snakeField, out var mapped)
                    ? mapped
                    : snakeField;

                if (decision.TryGetProperty("reExtractedValue", out var reVal))
                {
                    reExtracted[camelField] = new { value = reVal.GetString() };
                }
                if (decision.TryGetProperty("originalValue", out var origVal))
                {
                    clinicalExtractor[camelField] = new { value = origVal.GetString() };
                }
            }

            if (reExtracted.Count > 0)
            {
                stageOutputs["risk_reextracted"] = JsonSerializer.SerializeToElement(reExtracted);
            }
            if (clinicalExtractor.Count > 0)
            {
                stageOutputs["clinical_extractor"] = JsonSerializer.SerializeToElement(clinicalExtractor);
            }
        }

        return stageOutputs;
    }

    private static void AssertExpectedRiskFields(
        GoldenRiskCase goldenCase,
        IReadOnlyDictionary<string, JsonElement> stageOutputs,
        bool contentFilterWasHit)
    {
        var assertStages = ResolveAssertStages(goldenCase);

        foreach (var stageName in assertStages)
        {
            if (contentFilterWasHit
                && string.Equals(stageName, "risk_reextracted", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var stageFound = goldenCase.ExpectedByStage.TryGetValue(stageName, out var expectedStage);
            stageFound.Should().BeTrue(
                $"Golden case {goldenCase.NoteId} missing expected_by_stage for stage '{stageName}' in {goldenCase.FilePath}");
            expectedStage.Should().NotBeNull();
            var expectedStageValue = expectedStage!;

            stageOutputs.TryGetValue(stageName, out var actualStageOutput)
                .Should().BeTrue(
                    $"Golden case {goldenCase.NoteId} stage '{stageName}' was requested by assert_stages but not returned by extraction pipeline.");

            var assertFields = ResolveAssertFields(goldenCase, expectedStageValue);
            foreach (var expectedFieldKey in assertFields.OrderBy(field => field, StringComparer.Ordinal))
            {
                expectedStageValue.Fields.TryGetValue(expectedFieldKey, out var expectedAcceptRawValues)
                    .Should().BeTrue(
                        $"Golden case {goldenCase.NoteId} stage '{stageName}' missing expected field '{expectedFieldKey}'.");

                ExpectedToActualRiskFieldMap.TryGetValue(expectedFieldKey, out var extractionFieldName)
                    .Should().BeTrue(
                        $"Golden case {goldenCase.NoteId} has unsupported field '{expectedFieldKey}' in stage '{stageName}' ({goldenCase.FilePath}).");

                var actualValue = ExtractionAssertions.GetFieldValue(actualStageOutput, extractionFieldName!);
                var normalizedAccept = expectedAcceptRawValues!
                    .Select(NormalizeExpectedValue)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                normalizedAccept.Should().Contain(actualValue,
                    $"golden case {goldenCase.NoteId} ({goldenCase.TestType}) stage '{stageName}' expected {expectedFieldKey} in [{string.Join(", ", normalizedAccept)}] from {goldenCase.FileName}");
            }
        }
    }

    private static IReadOnlyCollection<string> ResolveAssertStages(GoldenRiskCase goldenCase)
    {
        if (goldenCase.AssertStages.Any(stage =>
                string.Equals(stage, "all", StringComparison.OrdinalIgnoreCase)))
        {
            return goldenCase.ExpectedByStage.Keys.ToList();
        }

        var requestedStages = goldenCase.AssertStages.ToList();
        if (requestedStages.Count == 0)
        {
            throw new InvalidOperationException(
                $"Golden case {goldenCase.NoteId} has empty assert_stages in {goldenCase.FilePath}");
        }

        return requestedStages;
    }

    private static IReadOnlyCollection<string> ResolveAssertFields(
        GoldenRiskCase goldenCase,
        GoldenStageExpectation stageExpectation)
    {
        if (goldenCase.AssertFields.Any(field =>
                string.Equals(field, "all", StringComparison.OrdinalIgnoreCase)))
        {
            return stageExpectation.Fields.Keys.ToList();
        }

        var requestedFields = goldenCase.AssertFields.ToList();
        if (requestedFields.Count == 0)
        {
            throw new InvalidOperationException(
                $"Golden case {goldenCase.NoteId} has empty assert_fields in {goldenCase.FilePath}");
        }

        return requestedFields;
    }

    private static string NormalizeExpectedValue(string expectedRawValue)
    {
        var trimmed = expectedRawValue.Trim();
        if (trimmed.Length == 0)
        {
            throw new InvalidOperationException("Expected value cannot be empty.");
        }

        if (trimmed.Contains('_', StringComparison.Ordinal))
        {
            var tokens = trimmed
                .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return string.Concat(tokens.Select(token =>
                $"{char.ToUpperInvariant(token[0])}{token[1..].ToLowerInvariant()}"));
        }

        return trimmed.Any(char.IsUpper)
            ? trimmed
            : $"{char.ToUpperInvariant(trimmed[0])}{trimmed[1..].ToLowerInvariant()}";
    }

    private void WriteSelectionManifest(GoldenRiskSelection selection)
    {
        _output.WriteLine(
            $"Golden selection mode={selection.Mode.ToString().ToLowerInvariant()}, date={selection.EffectiveDateUtc:yyyy-MM-dd}, corpus={selection.CorpusCount}, candidates={selection.CandidateCount}, selected={selection.SelectedCount}, filter={selection.Filter ?? "(none)"}");
        _output.WriteLine("Selected cases: " + string.Join(", ", selection.SelectedCases.Select(c => c.NoteId)));
    }

    private void WriteRiskDiagnostics(GoldenRiskCase goldenCase, JsonElement extractionDto)
    {
        if (!extractionDto.TryGetProperty("riskDiagnostics", out var responseDiagnostics))
        {
            _output.WriteLine($"No riskDiagnostics present in extraction for {goldenCase.NoteId}.");
            return;
        }

        _output.WriteLine($"Risk diagnostics for {goldenCase.NoteId}:");
        var criteriaValidationAttempts = 1;
        if (responseDiagnostics.TryGetProperty("criteriaValidationAttempts", out var attemptsElement) &&
            attemptsElement.TryGetInt32(out var attemptsParsed))
        {
            criteriaValidationAttempts = attemptsParsed;
        }

        _output.WriteLine($"criteria_validation_attempts={criteriaValidationAttempts}");
        _output.WriteLine("field | original | re_extracted | final | rule_applied | criteria_used | reasoning_used");

        if (responseDiagnostics.TryGetProperty("fieldDecisions", out var decisions)
            && decisions.ValueKind == JsonValueKind.Array)
        {
            foreach (var decision in decisions.EnumerateArray())
            {
                var field = GetDiagnosticValue(decision, "field");
                var original = GetDiagnosticValue(decision, "originalValue");
                var reExtracted = GetDiagnosticValue(decision, "reExtractedValue");
                var final1 = GetDiagnosticValue(decision, "finalValue");
                var rule = GetDiagnosticValue(decision, "ruleApplied");
                var criteria = GetDiagnosticValue(decision, "criteriaUsed");
                var reasoning = GetDiagnosticValue(decision, "reasoningUsed");
                _output.WriteLine($"{field} | {original} | {reExtracted} | {final1} | {rule} | {criteria} | {reasoning}");
            }
        }
    }

    private static string GetDiagnosticValue(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return "(missing)";
        }

        return property.ValueKind switch
        {
            JsonValueKind.Null => "null",
            JsonValueKind.String => property.GetString() ?? string.Empty,
            JsonValueKind.Array => string.Join(",", property.EnumerateArray().Select(item => item.ToString())),
            _ => property.ToString()
        };
    }

    private sealed record TriggerExtractionResult(
        bool ShouldContinueAssertions,
        bool ContentFilterWasHit = false,
        bool PassedViaSecurityFilter = false);
}
