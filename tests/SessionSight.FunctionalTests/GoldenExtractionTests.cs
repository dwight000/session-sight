using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using SessionSight.FunctionalTests.Fixtures;
using Xunit.Abstractions;

namespace SessionSight.FunctionalTests;

[Trait("Category", "Functional")]
public class GoldenExtractionTests : IClassFixture<ApiFixture>
{
    private const string PreviewDirectory = "/tmp/sessionsight/golden-previews";
    private const int MaxSavedPreviewFiles = 5;
    private static readonly object PreviewLock = new();
    private static readonly string PreviewRunStamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    private static bool _previewDirectoryReset;
    private static int _savedPreviewCount;

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
        if (!triggerResult.ShouldContinueAssertions)
        {
            return;
        }

        var extractionDto = await GetExtractionDtoAsync(sessionId);
        var extractionData = extractionDto.GetProperty("data");
        var stageOutputs = BuildStageOutputs(triggerResult.Response, extractionData);
        WriteRiskDiagnostics(goldenCase, triggerResult.Response);

        AssertExpectedRiskFields(goldenCase, stageOutputs);
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
        var pdfBytes = CreatePdfDocument(noteContent);
        TrySavePreviewPdf(goldenCase.NoteId, pdfBytes, _output);
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

    private static void TrySavePreviewPdf(string noteId, byte[] pdfBytes, ITestOutputHelper output)
    {
        string? outputPath = null;

        lock (PreviewLock)
        {
            if (!_previewDirectoryReset)
            {
                Directory.CreateDirectory(PreviewDirectory);
                foreach (var existingFile in Directory.GetFiles(PreviewDirectory, "*.pdf", SearchOption.TopDirectoryOnly))
                {
                    File.Delete(existingFile);
                }

                _savedPreviewCount = 0;
                _previewDirectoryReset = true;
            }

            if (_savedPreviewCount >= MaxSavedPreviewFiles)
            {
                return;
            }

            var fileName = $"{PreviewRunStamp}-{_savedPreviewCount + 1:D2}-{noteId}.pdf";
            outputPath = Path.Combine(PreviewDirectory, fileName);
            File.WriteAllBytes(outputPath, pdfBytes);
            _savedPreviewCount++;
        }

        output.WriteLine($"Saved golden preview PDF: {outputPath}");
    }

    private static byte[] CreatePdfDocument(string noteContent)
    {
        var lines = WrapTextForPdf(noteContent, maxLineLength: 95)
            .Take(42)
            .ToList();

        if (lines.Count == 0)
        {
            lines.Add("(empty)");
        }

        var streamBuilder = new StringBuilder();
        streamBuilder.AppendLine("BT");
        streamBuilder.AppendLine("/F1 11 Tf");
        streamBuilder.AppendLine("14 TL");
        streamBuilder.AppendLine("50 760 Td");

        for (var i = 0; i < lines.Count; i++)
        {
            streamBuilder.Append('(')
                .Append(EscapePdfString(lines[i]))
                .AppendLine(") Tj");

            if (i < lines.Count - 1)
            {
                streamBuilder.AppendLine("T*");
            }
        }

        streamBuilder.AppendLine("ET");
        var streamContent = streamBuilder.ToString();
        var streamLength = Encoding.ASCII.GetByteCount(streamContent);

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {streamLength} >>\nstream\n{streamContent}endstream"
        };

        using var memory = new MemoryStream();
        var offsets = new List<long> { 0 };

        WriteAscii(memory, "%PDF-1.4\n");

        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(memory.Position);
            WriteAscii(memory, $"{i + 1} 0 obj\n");
            WriteAscii(memory, objects[i]);
            WriteAscii(memory, "\nendobj\n");
        }

        var xrefPosition = memory.Position;
        WriteAscii(memory, $"xref\n0 {objects.Count + 1}\n");
        WriteAscii(memory, "0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            WriteAscii(memory, $"{offset:0000000000} 00000 n \n");
        }

        WriteAscii(memory, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
        WriteAscii(memory, $"startxref\n{xrefPosition}\n%%EOF");

        return memory.ToArray();
    }

    private static void WriteAscii(Stream stream, string content)
    {
        var bytes = Encoding.ASCII.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static IEnumerable<string> WrapTextForPdf(string content, int maxLineLength)
    {
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        foreach (var paragraph in normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var line = new StringBuilder();

            foreach (var word in words)
            {
                var safeWord = ToAscii(word);
                if (line.Length == 0)
                {
                    line.Append(safeWord);
                    continue;
                }

                if (line.Length + 1 + safeWord.Length > maxLineLength)
                {
                    yield return line.ToString();
                    line.Clear();
                    line.Append(safeWord);
                }
                else
                {
                    line.Append(' ').Append(safeWord);
                }
            }

            if (line.Length > 0)
            {
                yield return line.ToString();
            }
        }
    }

    private static string ToAscii(string input)
    {
        var builder = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            builder.Append(ch <= 0x7F ? ch : '?');
        }

        return builder.ToString();
    }

    private static string EscapePdfString(string line) =>
        line
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

    private async Task<TriggerExtractionResult> TriggerExtractionAsync(GoldenRiskCase goldenCase, Guid sessionId)
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

    private static Dictionary<string, JsonElement> BuildStageOutputs(JsonElement triggerResponse, JsonElement extractionData)
    {
        var stageOutputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        if (triggerResponse.TryGetProperty("riskStageOutputs", out var riskStages))
        {
            if (riskStages.TryGetProperty("clinicalExtractor", out var clinicalExtractor))
            {
                stageOutputs["clinical_extractor"] = clinicalExtractor;
            }

            if (riskStages.TryGetProperty("riskReextracted", out var riskReextracted))
            {
                stageOutputs["risk_reextracted"] = riskReextracted;
            }

            if (riskStages.TryGetProperty("riskFinal", out var riskFinal))
            {
                stageOutputs["risk_final"] = riskFinal;
            }
        }

        stageOutputs["risk_final"] = extractionData.GetProperty("riskAssessment");
        return stageOutputs;
    }

    private static void AssertExpectedRiskFields(
        GoldenRiskCase goldenCase,
        IReadOnlyDictionary<string, JsonElement> stageOutputs)
    {
        var assertStages = ResolveAssertStages(goldenCase);

        foreach (var stageName in assertStages)
        {
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

    private void WriteRiskDiagnostics(GoldenRiskCase goldenCase, JsonElement triggerResponse)
    {
        if (!triggerResponse.TryGetProperty("riskDiagnostics", out var responseDiagnostics))
        {
            _output.WriteLine($"No riskDiagnostics present in trigger response for {goldenCase.NoteId}.");
            return;
        }

        _output.WriteLine($"Risk diagnostics for {goldenCase.NoteId}:");
        var criteriaValidationAttemptsUsed = 1;
        if (responseDiagnostics.TryGetProperty("criteriaValidationAttemptsUsed", out var attemptsElement) &&
            attemptsElement.TryGetInt32(out var attemptsParsed))
        {
            criteriaValidationAttemptsUsed = attemptsParsed;
        }

        _output.WriteLine($"criteria_validation_attempts_used={criteriaValidationAttemptsUsed}");
        _output.WriteLine("field | original | re_extracted | final | rule_applied | criteria_used | reasoning_used");

        if (responseDiagnostics.TryGetProperty("decisions", out var decisions)
            && decisions.ValueKind == JsonValueKind.Array)
        {
            foreach (var decision in decisions.EnumerateArray())
            {
                var field = GetDiagnosticValue(decision, "field");
                var original = GetDiagnosticValue(decision, "originalValue");
                var reExtracted = GetDiagnosticValue(decision, "reExtractedValue");
                var final = GetDiagnosticValue(decision, "finalValue");
                var rule = GetDiagnosticValue(decision, "ruleApplied");
                var criteria = GetDiagnosticValue(decision, "criteriaUsed");
                var reasoning = GetDiagnosticValue(decision, "reasoningUsed");
                _output.WriteLine($"{field} | {original} | {reExtracted} | {final} | {rule} | {criteria} | {reasoning}");
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
        JsonElement Response);
}
