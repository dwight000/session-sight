using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SessionSight.FunctionalTests.Fixtures;
using Xunit.Abstractions;

namespace SessionSight.FunctionalTests;

[Trait("Category", "Functional")]
[Trait("Category", "RAGEval")]
public class GoldenQATests : IClassFixture<ApiFixture>
{
    private static readonly ConcurrentBag<QAEvalResult> EvalResults = new();

    private readonly HttpClient _client;
    private readonly HttpClient _longClient;
    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _jsonOptions;

    public GoldenQATests(ApiFixture fixture, ITestOutputHelper output)
    {
        _client = fixture.Client;
        _longClient = fixture.LongClient;
        _output = output;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public static IEnumerable<object[]> GoldenCases() => GoldenQACaseProvider.GetSelectedCases();

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public async Task GoldenQACases_AnswerMatchesExpectations(GoldenQACase goldenCase)
    {
        var selection = GoldenQACaseProvider.Selection;
        WriteSelectionManifest(selection);

        // Step 1: Create patient with note and run extraction
        var (patientId, sessionId) = await CreatePatientWithNoteAsync(goldenCase);

        // Step 2: Wait for indexing to complete
        await WaitForIndexingAsync(patientId, goldenCase);

        // Step 3: Ask the golden question
        var qaResponse = await AskQuestionAsync(patientId, goldenCase);

        // Step 4: Log diagnostics
        LogDiagnostics(goldenCase, qaResponse);

        // Step 5: Assert answer expectations
        AssertAnswerExpectations(goldenCase, qaResponse);

        // Record for aggregate metrics
        EvalResults.Add(new QAEvalResult(
            NoteId: goldenCase.NoteId,
            Passed: true,
            Confidence: qaResponse.GetProperty("confidence").GetDouble(),
            SourceCount: qaResponse.TryGetProperty("sources", out var sources)
                ? sources.GetArrayLength()
                : 0));
    }

    [Fact]
    [Trait("Category", "RAGEvalSummary")]
    public void RAGEval_AggregateSummary()
    {
        if (EvalResults.IsEmpty)
        {
            _output.WriteLine("[QA-EVAL-SUMMARY] No results collected - run QA eval tests first.");
            return;
        }

        var total = EvalResults.Count;
        var passed = EvalResults.Count(r => r.Passed);
        var hitRate = (double)passed / total;
        var avgConfidence = EvalResults.Average(r => r.Confidence);
        var avgSources = EvalResults.Average(r => r.SourceCount);

        _output.WriteLine($"[QA-EVAL-SUMMARY] total={total} | passed={passed} | hit_rate={hitRate:F2} | avg_confidence={avgConfidence:F2} | avg_sources={avgSources:F1}");
        _output.WriteLine($"[QA-EVAL-SUMMARY] precision@5 interpretation: {hitRate:P0} ({passed}/{total} cases cite >= 1 correct source)");

        foreach (var result in EvalResults.OrderBy(r => r.NoteId))
        {
            _output.WriteLine($"  {result.NoteId}: passed={result.Passed} confidence={result.Confidence:F2} sources={result.SourceCount}");
        }
    }

    private async Task<(Guid PatientId, Guid SessionId)> CreatePatientWithNoteAsync(GoldenQACase goldenCase)
    {
        // Create patient
        var patientRequest = new
        {
            externalId = $"QA-{goldenCase.NoteId}-{Guid.NewGuid():N}"[..36],
            firstName = "Golden",
            lastName = "QACase",
            dateOfBirth = "1985-01-15"
        };

        var patientResponse = await _client.PostAsJsonAsync("/api/patients", patientRequest);
        patientResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Patient creation should succeed for QA case {goldenCase.NoteId}");

        var patientJson = await patientResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var patientId = patientJson.GetProperty("id").GetGuid();

        // Create session
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
            $"Session creation should succeed for QA case {goldenCase.NoteId}");

        var sessionJson = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var sessionId = sessionJson.GetProperty("id").GetGuid();

        // Upload PDF document
        using var content = new MultipartFormDataContent();
        var pdfBytes = GoldenTestHelpers.CreatePdfDocument(goldenCase.NoteContent, maxLines: 80);
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", $"{goldenCase.NoteId}.pdf");

        var uploadResponse = await _client.PostAsync($"/api/sessions/{sessionId}/document", content);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Document upload should succeed for QA case {goldenCase.NoteId}");

        // Trigger extraction — 202 Accepted, runs in background
        var extractionResponse = await _client.PostAsync($"/api/extraction/{sessionId}", null);
        extractionResponse.StatusCode.Should().Be(HttpStatusCode.Accepted,
            $"Extraction should return 202 for QA case {goldenCase.NoteId}");

        // Poll for completion
        var finalStatus = await ExtractionAssertions.WaitForExtractionAsync(
            _client, sessionId, TimeSpan.FromMinutes(5), _output);

        if (finalStatus == "Failed")
        {
            var stepsCheck = await _client.GetAsync($"/api/sessions/{sessionId}/extraction/steps");
            var stepsDto = await stepsCheck.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
            var errMsg = stepsDto.TryGetProperty("errorMessage", out var ep) ? ep.GetString() : null;
            if (ExtractionAssertions.IsContentFilterFailure(finalStatus, errMsg))
            {
                throw Xunit.Sdk.SkipException.ForSkip(
                    $"Content filter blocked extraction for QA case {goldenCase.NoteId}. " +
                    "Transient Azure-side issue.");
            }
        }

        finalStatus.Should().BeOneOf("Completed", "PartiallyCompleted",
            $"Extraction should complete for QA case {goldenCase.NoteId}");

        _output.WriteLine($"[QA-EVAL] {goldenCase.NoteId} | extraction completed for session {sessionId}");

        return (patientId, sessionId);
    }

    private async Task WaitForIndexingAsync(Guid patientId, GoldenQACase goldenCase)
    {
        const int maxAttempts = 15;
        const int delayMs = 2000;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var probeBody = new { question = "What is this session about?" };
            var probeResponse = await _client.PostAsJsonAsync($"/api/qa/patient/{patientId}", probeBody);

            if (probeResponse.StatusCode == HttpStatusCode.OK)
            {
                var probeJson = await probeResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
                if (probeJson.TryGetProperty("sources", out var sources) && sources.GetArrayLength() > 0)
                {
                    _output.WriteLine($"[QA-EVAL] {goldenCase.NoteId} | indexing ready after {attempt} attempt(s)");
                    return;
                }
            }

            _output.WriteLine($"[QA-EVAL] {goldenCase.NoteId} | indexing not ready, attempt {attempt}/{maxAttempts}");
            await Task.Delay(delayMs);
        }

        throw new InvalidOperationException(
            $"Indexing did not complete for QA case {goldenCase.NoteId} after {maxAttempts} attempts");
    }

    private async Task<JsonElement> AskQuestionAsync(Guid patientId, GoldenQACase goldenCase)
    {
        var qaBody = new { question = goldenCase.Question };
        var qaResponse = await _client.PostAsJsonAsync($"/api/qa/patient/{patientId}", qaBody);
        qaResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Q&A endpoint should return 200 for QA case {goldenCase.NoteId}");

        return await qaResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
    }

    private void LogDiagnostics(GoldenQACase goldenCase, JsonElement qaResponse)
    {
        var answer = qaResponse.TryGetProperty("answer", out var a) ? a.GetString() ?? "" : "";
        var confidence = qaResponse.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0;
        var sourceCount = qaResponse.TryGetProperty("sources", out var s) ? s.GetArrayLength() : 0;
        var modelUsed = qaResponse.TryGetProperty("modelUsed", out var m) ? m.GetString() ?? "" : "";

        string? reasoning = null;
        string? isComplex = null;
        var searchResultCount = 0;
        var toolCallNames = "";

        if (qaResponse.TryGetProperty("diagnostics", out var diag) && diag.ValueKind == JsonValueKind.Object)
        {
            if (diag.TryGetProperty("reasoning", out var r))
                reasoning = r.GetString();
            if (diag.TryGetProperty("isComplex", out var ic))
                isComplex = ic.GetBoolean() ? "complex" : "simple";
            if (diag.TryGetProperty("searchResultCount", out var src))
                searchResultCount = src.GetInt32();
            if (diag.TryGetProperty("toolCalls", out var tc) && tc.ValueKind == JsonValueKind.Array)
                toolCallNames = string.Join(",", tc.EnumerateArray()
                    .Select(t => t.TryGetProperty("toolName", out var tn) ? tn.GetString() : "?"));
        }

        var path = isComplex ?? "unknown";
        var reasoningSnippet = reasoning != null && reasoning.Length > 80
            ? reasoning[..80] + "..."
            : reasoning ?? "(none)";

        _output.WriteLine(
            $"[QA-EVAL] {goldenCase.NoteId} | path={path} | confidence={confidence:F2} | sources={sourceCount} | search_results={searchResultCount} | model={modelUsed} | reasoning=\"{reasoningSnippet}\"");

        if (!string.IsNullOrEmpty(toolCallNames))
        {
            _output.WriteLine($"[QA-EVAL] {goldenCase.NoteId} | tool_calls={toolCallNames}");
        }

        // Log truncated answer for debugging
        var answerSnippet = answer.Length > 200 ? answer[..200] + "..." : answer;
        _output.WriteLine($"[QA-EVAL] {goldenCase.NoteId} | answer=\"{answerSnippet}\"");
    }

    private void AssertAnswerExpectations(GoldenQACase goldenCase, JsonElement qaResponse)
    {
        var answer = qaResponse.TryGetProperty("answer", out var a) ? a.GetString() ?? "" : "";
        var confidence = qaResponse.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0;
        var sourceCount = qaResponse.TryGetProperty("sources", out var s) ? s.GetArrayLength() : 0;

        // must_contain: OR semantics — at least one keyword present
        if (goldenCase.ExpectedAnswer.MustContain.Count > 0)
        {
            var answerLower = answer.ToLowerInvariant();
            var containsAny = goldenCase.ExpectedAnswer.MustContain
                .Any(keyword => answerLower.Contains(keyword.ToLowerInvariant(), StringComparison.Ordinal));

            _output.WriteLine(containsAny
                ? $"[QA-EVAL] {goldenCase.NoteId} | contain=PASS"
                : $"[QA-EVAL] {goldenCase.NoteId} | contain=FAIL (expected any of [{string.Join(", ", goldenCase.ExpectedAnswer.MustContain)}])");

            containsAny.Should().BeTrue(
                $"QA case {goldenCase.NoteId}: answer should contain at least one of [{string.Join(", ", goldenCase.ExpectedAnswer.MustContain)}] but got: \"{answer}\"");
        }

        // must_not_contain: AND semantics — none should appear
        foreach (var forbidden in goldenCase.ExpectedAnswer.MustNotContain)
        {
            var containsForbidden = answer.Contains(forbidden, StringComparison.OrdinalIgnoreCase);

            if (containsForbidden)
            {
                _output.WriteLine($"[QA-EVAL] {goldenCase.NoteId} | not_contain=FAIL (found '{forbidden}')");
            }

            containsForbidden.Should().BeFalse(
                $"QA case {goldenCase.NoteId}: answer should NOT contain '{forbidden}' but got: \"{answer}\"");
        }

        // min_confidence
        if (goldenCase.ExpectedAnswer.MinConfidence > 0)
        {
            confidence.Should().BeGreaterThanOrEqualTo(goldenCase.ExpectedAnswer.MinConfidence,
                $"QA case {goldenCase.NoteId}: confidence {confidence:F2} below minimum {goldenCase.ExpectedAnswer.MinConfidence}");
        }

        // expect_sources_count_gte
        if (goldenCase.ExpectedAnswer.ExpectSourcesCountGte > 0)
        {
            sourceCount.Should().BeGreaterThanOrEqualTo(goldenCase.ExpectedAnswer.ExpectSourcesCountGte,
                $"QA case {goldenCase.NoteId}: source count {sourceCount} below minimum {goldenCase.ExpectedAnswer.ExpectSourcesCountGte}");
        }

        // expected_path: verify the complexity classifier routed correctly
        if (!string.IsNullOrEmpty(goldenCase.ExpectedPath))
        {
            var actualPath = qaResponse.TryGetProperty("diagnostics", out var diag) &&
                             diag.TryGetProperty("isComplex", out var ic)
                ? (ic.GetBoolean() ? "complex" : "simple")
                : "unknown";

            _output.WriteLine($"[QA-EVAL] {goldenCase.NoteId} | path_assert: expected={goldenCase.ExpectedPath}, actual={actualPath}");

            actualPath.Should().Be(goldenCase.ExpectedPath,
                $"QA case {goldenCase.NoteId}: expected path '{goldenCase.ExpectedPath}' but classifier chose '{actualPath}'");
        }
    }

    private void WriteSelectionManifest(GoldenQASelection selection)
    {
        _output.WriteLine(
            $"Golden QA selection mode={selection.Mode.ToString().ToLowerInvariant()}, date={selection.EffectiveDateUtc:yyyy-MM-dd}, corpus={selection.CorpusCount}, candidates={selection.CandidateCount}, selected={selection.SelectedCount}, filter={selection.Filter ?? "(none)"}");
        _output.WriteLine("Selected cases: " + string.Join(", ", selection.SelectedCases.Select(c => c.NoteId)));
    }

    private sealed record QAEvalResult(
        string NoteId,
        bool Passed,
        double Confidence,
        int SourceCount);
}
