using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SessionSight.FunctionalTests.Fixtures;

internal static class GoldenQACaseProvider
{
    private const int DefaultSmokeCount = 2;
    private const string GoldenRootRelativePath = "plan/data/synthetic/golden-files/qa-eval";
    private const string GoldenFilePattern = "*_v1.json";

    private static readonly Lazy<GoldenQASelection> SelectionLazy = new(LoadSelection);

    internal static GoldenQASelection Selection => SelectionLazy.Value;

    internal static IEnumerable<object[]> GetSelectedCases() =>
        Selection.SelectedCases.Select(testCase => new object[] { testCase });

    private static GoldenQASelection LoadSelection()
    {
        var repositoryRoot = GoldenCaseProviderBase.FindRepositoryRoot();
        var goldenDirectory = Path.Combine(repositoryRoot, GoldenRootRelativePath);

        if (!Directory.Exists(goldenDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Golden QA directory not found: {goldenDirectory}");
        }

        var allFiles = Directory.GetFiles(goldenDirectory, GoldenFilePattern, SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (allFiles.Length == 0)
        {
            throw new InvalidOperationException(
                $"No v1 golden files found in: {goldenDirectory} (pattern '{GoldenFilePattern}').");
        }

        var allCases = allFiles.Select(LoadCase).ToList();
        var filter = Environment.GetEnvironmentVariable("GOLDEN_FILTER");
        var filteredCases = ApplyFilter(allCases, filter);
        var mode = GoldenCaseProviderBase.ParseMode(Environment.GetEnvironmentVariable("GOLDEN_MODE"));
        var effectiveDate = GoldenCaseProviderBase.ResolveEffectiveDate(Environment.GetEnvironmentVariable("GOLDEN_DATE"));
        var smokeCount = GoldenCaseProviderBase.ParsePositiveInt(
            Environment.GetEnvironmentVariable("GOLDEN_COUNT"), DefaultSmokeCount, "GOLDEN_COUNT");

        List<GoldenQACase> selectedCases = mode == GoldenMode.Full
            ? filteredCases
            : GoldenCaseProviderBase.SelectDeterministicSubset(
                filteredCases,
                effectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                smokeCount,
                c => c.NoteId);

        if (selectedCases.Count == 0)
        {
            throw new InvalidOperationException(
                $"Golden QA case selection produced no cases. GOLDEN_FILTER='{filter ?? "(null)"}', mode='{mode}'.");
        }

        return new GoldenQASelection(
            Mode: mode,
            EffectiveDateUtc: effectiveDate,
            RepositoryRoot: repositoryRoot,
            GoldenDirectory: goldenDirectory,
            CorpusCount: allCases.Count,
            CandidateCount: filteredCases.Count,
            SelectedCount: selectedCases.Count,
            Filter: filter,
            SelectedCases: selectedCases);
    }

    private static List<GoldenQACase> ApplyFilter(IReadOnlyCollection<GoldenQACase> cases, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return cases.ToList();
        }

        var filtered = cases.Where(testCase =>
                testCase.NoteId.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                testCase.TestType.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                testCase.ClinicalProfile.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                testCase.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (filtered.Count == 0)
        {
            throw new InvalidOperationException(
                $"GOLDEN_FILTER '{filter}' matched no QA cases.");
        }

        return filtered;
    }

    private static GoldenQACase LoadCase(string filePath)
    {
        var content = File.ReadAllTextAsync(filePath).GetAwaiter().GetResult();
        var parsed = JsonSerializer.Deserialize<GoldenQAFileV1>(content, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to parse golden QA file: {filePath}");

        if (string.IsNullOrWhiteSpace(parsed.NoteId))
        {
            throw new InvalidOperationException($"Missing 'note_id' in {filePath}");
        }

        if (string.IsNullOrWhiteSpace(parsed.NoteContent))
        {
            throw new InvalidOperationException($"Missing 'note_content' in {filePath}");
        }

        if (string.IsNullOrWhiteSpace(parsed.Question))
        {
            throw new InvalidOperationException($"Missing 'question' in {filePath}");
        }

        if (parsed.ExpectedAnswer is null)
        {
            throw new InvalidOperationException($"Missing 'expected_answer' in {filePath}");
        }

        return new GoldenQACase(
            NoteId: parsed.NoteId,
            NoteContent: parsed.NoteContent,
            TestType: parsed.TestType ?? "unknown",
            ClinicalProfile: parsed.ClinicalProfile ?? "unknown",
            Question: parsed.Question,
            ExpectedAnswer: new GoldenQAExpectedAnswer(
                MustContain: parsed.ExpectedAnswer.MustContain ?? [],
                MustNotContain: parsed.ExpectedAnswer.MustNotContain ?? [],
                MinConfidence: parsed.ExpectedAnswer.MinConfidence,
                ExpectSourcesCountGte: parsed.ExpectedAnswer.ExpectSourcesCountGte),
            ExpectedPath: parsed.ExpectedPath ?? "simple",
            FilePath: filePath,
            FileName: Path.GetFileName(filePath));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class GoldenQAFileV1
    {
        [JsonPropertyName("note_id")]
        public string NoteId { get; init; } = string.Empty;

        [JsonPropertyName("reasoning")]
        public string? Reasoning { get; init; }

        [JsonPropertyName("note_content")]
        public string NoteContent { get; init; } = string.Empty;

        [JsonPropertyName("test_type")]
        public string? TestType { get; init; }

        [JsonPropertyName("clinical_profile")]
        public string? ClinicalProfile { get; init; }

        [JsonPropertyName("question")]
        public string Question { get; init; } = string.Empty;

        [JsonPropertyName("expected_answer")]
        public GoldenQAExpectedAnswerFile? ExpectedAnswer { get; init; }

        [JsonPropertyName("expected_path")]
        public string? ExpectedPath { get; init; }
    }

    private sealed class GoldenQAExpectedAnswerFile
    {
        [JsonPropertyName("must_contain")]
        public List<string>? MustContain { get; init; }

        [JsonPropertyName("must_not_contain")]
        public List<string>? MustNotContain { get; init; }

        [JsonPropertyName("min_confidence")]
        public double MinConfidence { get; init; }

        [JsonPropertyName("expect_sources_count_gte")]
        public int ExpectSourcesCountGte { get; init; }
    }
}

public sealed record GoldenQACase(
    string NoteId,
    string NoteContent,
    string TestType,
    string ClinicalProfile,
    string Question,
    GoldenQAExpectedAnswer ExpectedAnswer,
    string ExpectedPath,
    string FilePath,
    string FileName)
{
    public override string ToString() => NoteId;
}

public sealed record GoldenQAExpectedAnswer(
    IReadOnlyList<string> MustContain,
    IReadOnlyList<string> MustNotContain,
    double MinConfidence,
    int ExpectSourcesCountGte);

internal sealed record GoldenQASelection(
    GoldenMode Mode,
    DateTime EffectiveDateUtc,
    string RepositoryRoot,
    string GoldenDirectory,
    int CorpusCount,
    int CandidateCount,
    int SelectedCount,
    string? Filter,
    IReadOnlyList<GoldenQACase> SelectedCases);
