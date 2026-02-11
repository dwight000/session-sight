using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SessionSight.FunctionalTests.Fixtures;

internal static class GoldenRiskCaseProvider
{
    private const int DefaultSmokeCount = 2;
    private const string GoldenRootRelativePath = "plan/data/synthetic/golden-files/risk-assessment";
    private const string GoldenFilePattern = "*_v2.json";

    private static readonly Lazy<GoldenRiskSelection> SelectionLazy = new(LoadSelection);

    internal static GoldenRiskSelection Selection => SelectionLazy.Value;

    internal static IEnumerable<object[]> GetSelectedCases() =>
        Selection.SelectedCases.Select(testCase => new object[] { testCase });

    private static GoldenRiskSelection LoadSelection()
    {
        var repositoryRoot = GoldenCaseProviderBase.FindRepositoryRoot();
        var goldenDirectory = Path.Combine(repositoryRoot, GoldenRootRelativePath);

        if (!Directory.Exists(goldenDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Golden risk directory not found: {goldenDirectory}");
        }

        var allFiles = Directory.GetFiles(goldenDirectory, GoldenFilePattern, SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (allFiles.Length == 0)
        {
            throw new InvalidOperationException(
                $"No v2 golden files found in: {goldenDirectory} (pattern '{GoldenFilePattern}').");
        }

        var allCases = allFiles.Select(LoadCase).ToList();
        var filter = Environment.GetEnvironmentVariable("GOLDEN_FILTER");
        var filteredCases = ApplyFilter(allCases, filter);
        var mode = GoldenCaseProviderBase.ParseMode(Environment.GetEnvironmentVariable("GOLDEN_MODE"));
        var effectiveDate = GoldenCaseProviderBase.ResolveEffectiveDate(Environment.GetEnvironmentVariable("GOLDEN_DATE"));
        var smokeCount = GoldenCaseProviderBase.ParsePositiveInt(Environment.GetEnvironmentVariable("GOLDEN_COUNT"), DefaultSmokeCount, "GOLDEN_COUNT");

        List<GoldenRiskCase> selectedCases = mode == GoldenMode.Full
            ? filteredCases
            : GoldenCaseProviderBase.SelectDeterministicSubset(filteredCases, effectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), smokeCount, c => c.NoteId);

        if (selectedCases.Count == 0)
        {
            throw new InvalidOperationException(
                $"Golden case selection produced no cases. GOLDEN_FILTER='{filter ?? "(null)"}', mode='{mode}'.");
        }

        return new GoldenRiskSelection(
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

    private static List<GoldenRiskCase> ApplyFilter(IReadOnlyCollection<GoldenRiskCase> cases, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return cases.ToList();
        }

        var filtered = cases.Where(testCase =>
                testCase.NoteId.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                testCase.TestType.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                testCase.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (filtered.Count == 0)
        {
            throw new InvalidOperationException(
                $"GOLDEN_FILTER '{filter}' matched no cases.");
        }

        return filtered;
    }

    private static GoldenRiskCase LoadCase(string filePath)
    {
        var content = File.ReadAllTextAsync(filePath).GetAwaiter().GetResult();
        var parsed = JsonSerializer.Deserialize<GoldenRiskFileV2>(content, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to parse golden file: {filePath}");

        if (string.IsNullOrWhiteSpace(parsed.NoteId))
        {
            throw new InvalidOperationException($"Missing 'note_id' in {filePath}");
        }

        if (string.IsNullOrWhiteSpace(parsed.NoteContent))
        {
            throw new InvalidOperationException($"Missing 'note_content' in {filePath}");
        }

        if (string.IsNullOrWhiteSpace(parsed.TestType))
        {
            throw new InvalidOperationException($"Missing 'test_type' in {filePath}");
        }

        var expectedByStage = ParseExpectedByStage(parsed, filePath);
        var assertStages = ParseAssertTargets(parsed.AssertStages, "assert_stages", filePath);
        var assertFields = ParseAssertTargets(parsed.AssertFields, "assert_fields", filePath);
        var expectedOutcome = GoldenCaseProviderBase.ParseExpectedOutcome(parsed.ExpectedOutcome, filePath);

        return new GoldenRiskCase(
            NoteId: parsed.NoteId,
            NoteContent: parsed.NoteContent,
            TestType: parsed.TestType,
            ExpectedOutcome: expectedOutcome,
            ExpectedByStage: expectedByStage,
            AssertStages: assertStages,
            AssertFields: assertFields,
            FilePath: filePath,
            FileName: Path.GetFileName(filePath));
    }

    private static IReadOnlyDictionary<string, GoldenStageExpectation> ParseExpectedByStage(
        GoldenRiskFileV2 parsed,
        string filePath)
    {
        if (parsed.ExpectedByStage is null || parsed.ExpectedByStage.Count == 0)
        {
            throw new InvalidOperationException($"Missing 'expected_by_stage' in {filePath}");
        }

        var byStage = new Dictionary<string, GoldenStageExpectation>(StringComparer.OrdinalIgnoreCase);
        foreach (var stage in parsed.ExpectedByStage)
        {
            if (string.IsNullOrWhiteSpace(stage.Stage))
            {
                throw new InvalidOperationException($"expected_by_stage contains empty 'stage' in {filePath}");
            }

            if (stage.Fields is null || stage.Fields.Count == 0)
            {
                throw new InvalidOperationException(
                    $"expected_by_stage '{stage.Stage}' has no fields in {filePath}");
            }

            var fieldMap = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (fieldKey, fieldExpectations) in stage.Fields)
            {
                if (string.IsNullOrWhiteSpace(fieldKey))
                {
                    throw new InvalidOperationException(
                        $"expected_by_stage '{stage.Stage}' has an empty field name in {filePath}");
                }

                var acceptedValues = (fieldExpectations.Accept ?? [])
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .ToList();

                if (acceptedValues.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"expected_by_stage '{stage.Stage}' field '{fieldKey}' has empty accept[] in {filePath}");
                }

                fieldMap[fieldKey] = acceptedValues;
            }

            if (!byStage.TryAdd(stage.Stage, new GoldenStageExpectation(stage.Stage, fieldMap)))
            {
                throw new InvalidOperationException(
                    $"Duplicate stage '{stage.Stage}' in expected_by_stage for {filePath}");
            }
        }

        return byStage;
    }

    private static IReadOnlyList<string> ParseAssertTargets(
        List<string> targets,
        string fieldName,
        string filePath)
    {
        var normalized = targets
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList();

        if (normalized.Count == 0)
        {
            throw new InvalidOperationException($"'{fieldName}' must be non-empty in {filePath}");
        }

        return normalized;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class GoldenRiskFileV2
    {
        [JsonPropertyName("note_id")]
        public string NoteId { get; init; } = string.Empty;

        [JsonPropertyName("note_content")]
        public string NoteContent { get; init; } = string.Empty;

        [JsonPropertyName("test_type")]
        public string TestType { get; init; } = string.Empty;

        [JsonPropertyName("expected_outcome")]
        public string? ExpectedOutcome { get; init; }

        [JsonPropertyName("expected_by_stage")]
        public List<GoldenRiskStageFile> ExpectedByStage { get; init; } = [];

        [JsonPropertyName("assert_stages")]
        public List<string> AssertStages { get; init; } = [];

        [JsonPropertyName("assert_fields")]
        public List<string> AssertFields { get; init; } = [];
    }

    private sealed class GoldenRiskStageFile
    {
        [JsonPropertyName("stage")]
        public string Stage { get; init; } = string.Empty;

        [JsonPropertyName("fields")]
        public Dictionary<string, GoldenFieldExpectationFile> Fields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class GoldenFieldExpectationFile
    {
        [JsonPropertyName("accept")]
        public List<string> Accept { get; init; } = [];
    }

}

internal enum GoldenMode
{
    Smoke,
    Full
}

public sealed record GoldenRiskCase(
    string NoteId,
    string NoteContent,
    string TestType,
    GoldenExpectedOutcome ExpectedOutcome,
    IReadOnlyDictionary<string, GoldenStageExpectation> ExpectedByStage,
    IReadOnlyList<string> AssertStages,
    IReadOnlyList<string> AssertFields,
    string FilePath,
    string FileName)
{
    public override string ToString() => NoteId;
}

public sealed record GoldenStageExpectation(
    string Stage,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Fields);

public enum GoldenExpectedOutcome
{
    ExtractionSuccess,
    ContentFilterBlocked,
    ContentFilterOptional
}


internal sealed record GoldenRiskSelection(
    GoldenMode Mode,
    DateTime EffectiveDateUtc,
    string RepositoryRoot,
    string GoldenDirectory,
    int CorpusCount,
    int CandidateCount,
    int SelectedCount,
    string? Filter,
    IReadOnlyList<GoldenRiskCase> SelectedCases);
