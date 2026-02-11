using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SessionSight.FunctionalTests.Fixtures;

internal static class GoldenNonRiskCaseProvider
{
    private const int DefaultSmokeCount = 3;
    private const string GoldenRootRelativePath = "plan/data/synthetic/golden-files/non-risk-extraction";
    private const string GoldenFilePattern = "*_v1.json";

    private static readonly Lazy<GoldenNonRiskSelection> SelectionLazy = new(LoadSelection);

    internal static GoldenNonRiskSelection Selection => SelectionLazy.Value;

    internal static IEnumerable<object[]> GetSelectedCases() =>
        Selection.SelectedCases.Select(testCase => new object[] { testCase });

    private static GoldenNonRiskSelection LoadSelection()
    {
        var repositoryRoot = GoldenCaseProviderBase.FindRepositoryRoot();
        var goldenDirectory = Path.Combine(repositoryRoot, GoldenRootRelativePath);

        if (!Directory.Exists(goldenDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Golden non-risk directory not found: {goldenDirectory}");
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

        List<GoldenNonRiskCase> selectedCases = mode == GoldenMode.Full
            ? filteredCases
            : GoldenCaseProviderBase.SelectDeterministicSubset(
                filteredCases,
                effectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                smokeCount,
                c => c.NoteId);

        if (selectedCases.Count == 0)
        {
            throw new InvalidOperationException(
                $"Golden non-risk case selection produced no cases. GOLDEN_FILTER='{filter ?? "(null)"}', mode='{mode}'.");
        }

        return new GoldenNonRiskSelection(
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

    private static List<GoldenNonRiskCase> ApplyFilter(IReadOnlyCollection<GoldenNonRiskCase> cases, string? filter)
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
                $"GOLDEN_FILTER '{filter}' matched no non-risk cases.");
        }

        return filtered;
    }

    private static GoldenNonRiskCase LoadCase(string filePath)
    {
        var content = File.ReadAllTextAsync(filePath).GetAwaiter().GetResult();
        var parsed = JsonSerializer.Deserialize<GoldenNonRiskFileV1>(content, JsonOptions)
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

        var expectedSections = ParseExpectedSections(parsed, filePath);
        var assertSections = ParseAssertTargets(parsed.AssertSections, "assert_sections", filePath);
        var assertFields = ParseAssertTargets(parsed.AssertFields, "assert_fields", filePath);
        var expectedOutcome = GoldenCaseProviderBase.ParseExpectedOutcome(parsed.ExpectedOutcome, filePath);

        return new GoldenNonRiskCase(
            NoteId: parsed.NoteId,
            NoteContent: parsed.NoteContent,
            TestType: parsed.TestType,
            ExpectedOutcome: expectedOutcome,
            ExpectedSections: expectedSections,
            AssertSections: assertSections,
            AssertFields: assertFields,
            FilePath: filePath,
            FileName: Path.GetFileName(filePath));
    }

    private static IReadOnlyDictionary<string, GoldenSectionExpectation> ParseExpectedSections(
        GoldenNonRiskFileV1 parsed,
        string filePath)
    {
        if (parsed.ExpectedSections is null || parsed.ExpectedSections.Count == 0)
        {
            throw new InvalidOperationException($"Missing 'expected_sections' in {filePath}");
        }

        var sections = new Dictionary<string, GoldenSectionExpectation>(StringComparer.OrdinalIgnoreCase);
        foreach (var (sectionName, sectionFields) in parsed.ExpectedSections)
        {
            if (string.IsNullOrWhiteSpace(sectionName))
            {
                throw new InvalidOperationException($"expected_sections contains empty section name in {filePath}");
            }

            if (sectionFields is null || sectionFields.Count == 0)
            {
                throw new InvalidOperationException(
                    $"expected_sections '{sectionName}' has no fields in {filePath}");
            }

            var fieldMap = new Dictionary<string, GoldenFieldExpectation>(StringComparer.OrdinalIgnoreCase);
            foreach (var (fieldKey, fieldFile) in sectionFields)
            {
                if (string.IsNullOrWhiteSpace(fieldKey))
                {
                    throw new InvalidOperationException(
                        $"expected_sections '{sectionName}' has an empty field name in {filePath}");
                }

                var acceptedValues = (fieldFile.Accept ?? [])
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .ToList();

                if (acceptedValues.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"expected_sections '{sectionName}' field '{fieldKey}' has empty accept[] in {filePath}");
                }

                var matchMode = ParseMatchMode(fieldFile.Match, sectionName, fieldKey, filePath);
                fieldMap[fieldKey] = new GoldenFieldExpectation(acceptedValues, matchMode);
            }

            if (!sections.TryAdd(sectionName, new GoldenSectionExpectation(sectionName, fieldMap)))
            {
                throw new InvalidOperationException(
                    $"Duplicate section '{sectionName}' in expected_sections for {filePath}");
            }
        }

        return sections;
    }

    private static GoldenMatchMode ParseMatchMode(string? value, string section, string field, string filePath)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return GoldenMatchMode.Exact;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "exact" => GoldenMatchMode.Exact,
            "contains_any" => GoldenMatchMode.ContainsAny,
            "any_value" => GoldenMatchMode.AnyValue,
            "any_keyword" => GoldenMatchMode.AnyKeyword,
            _ => throw new InvalidOperationException(
                $"Invalid match mode '{value}' for {section}.{field} in {filePath}. " +
                "Expected 'exact', 'contains_any', 'any_value', or 'any_keyword'.")
        };
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

    private sealed class GoldenNonRiskFileV1
    {
        [JsonPropertyName("note_id")]
        public string NoteId { get; init; } = string.Empty;

        [JsonPropertyName("note_content")]
        public string NoteContent { get; init; } = string.Empty;

        [JsonPropertyName("test_type")]
        public string TestType { get; init; } = string.Empty;

        [JsonPropertyName("expected_outcome")]
        public string? ExpectedOutcome { get; init; }

        [JsonPropertyName("expected_sections")]
        public Dictionary<string, Dictionary<string, GoldenFieldExpectationFile>> ExpectedSections { get; init; } = new();

        [JsonPropertyName("assert_sections")]
        public List<string> AssertSections { get; init; } = [];

        [JsonPropertyName("assert_fields")]
        public List<string> AssertFields { get; init; } = [];
    }

    private sealed class GoldenFieldExpectationFile
    {
        [JsonPropertyName("accept")]
        public List<string> Accept { get; init; } = [];

        [JsonPropertyName("match")]
        public string? Match { get; init; }
    }
}

public sealed record GoldenNonRiskCase(
    string NoteId,
    string NoteContent,
    string TestType,
    GoldenExpectedOutcome ExpectedOutcome,
    IReadOnlyDictionary<string, GoldenSectionExpectation> ExpectedSections,
    IReadOnlyList<string> AssertSections,
    IReadOnlyList<string> AssertFields,
    string FilePath,
    string FileName)
{
    public override string ToString() => NoteId;
}

public sealed record GoldenSectionExpectation(
    string Section,
    IReadOnlyDictionary<string, GoldenFieldExpectation> Fields);

public sealed record GoldenFieldExpectation(
    IReadOnlyList<string> Accept,
    GoldenMatchMode Match);

public enum GoldenMatchMode
{
    Exact,
    ContainsAny,
    AnyValue,
    AnyKeyword
}

internal sealed record GoldenNonRiskSelection(
    GoldenMode Mode,
    DateTime EffectiveDateUtc,
    string RepositoryRoot,
    string GoldenDirectory,
    int CorpusCount,
    int CandidateCount,
    int SelectedCount,
    string? Filter,
    IReadOnlyList<GoldenNonRiskCase> SelectedCases);
