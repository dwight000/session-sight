using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SessionSight.FunctionalTests.Fixtures;

internal static class GoldenRiskCaseProvider
{
    private const int DefaultSmokeCount = 5;
    private static readonly TimeSpan DailyBoundaryEastern = TimeSpan.FromHours(7);
    private const string GoldenRootRelativePath = "plan/data/synthetic/golden-files/risk-assessment";

    private static readonly Lazy<GoldenRiskSelection> SelectionLazy = new(LoadSelection);

    internal static GoldenRiskSelection Selection => SelectionLazy.Value;

    internal static IEnumerable<object[]> GetSelectedCases() =>
        Selection.SelectedCases.Select(testCase => new object[] { testCase });

    private static GoldenRiskSelection LoadSelection()
    {
        var repositoryRoot = FindRepositoryRoot();
        var goldenDirectory = Path.Combine(repositoryRoot, GoldenRootRelativePath);

        if (!Directory.Exists(goldenDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Golden risk directory not found: {goldenDirectory}");
        }

        var allFiles = Directory.GetFiles(goldenDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (allFiles.Length == 0)
        {
            throw new InvalidOperationException(
                $"No golden files found in: {goldenDirectory}");
        }

        var allCases = allFiles.Select(LoadCase).ToList();
        var filter = Environment.GetEnvironmentVariable("GOLDEN_FILTER");
        var filteredCases = ApplyFilter(allCases, filter);
        var mode = ParseMode(Environment.GetEnvironmentVariable("GOLDEN_MODE"));
        var effectiveDate = ResolveEffectiveDate(Environment.GetEnvironmentVariable("GOLDEN_DATE"));
        var smokeCount = ParsePositiveInt(Environment.GetEnvironmentVariable("GOLDEN_COUNT"), DefaultSmokeCount, "GOLDEN_COUNT");

        List<GoldenRiskCase> selectedCases = mode == GoldenMode.Full
            ? filteredCases
            : SelectDeterministicSubset(filteredCases, effectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), smokeCount);

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

    private static List<GoldenRiskCase> SelectDeterministicSubset(
        IReadOnlyCollection<GoldenRiskCase> cases,
        string dayKey,
        int count)
    {
        return cases
            .OrderBy(testCase => ComputeStableSortKey(dayKey, testCase.NoteId), StringComparer.Ordinal)
            .ThenBy(testCase => testCase.NoteId, StringComparer.Ordinal)
            .Take(Math.Min(count, cases.Count))
            .ToList();
    }

    private static string ComputeStableSortKey(string dayKey, string noteId)
    {
        var payload = Encoding.UTF8.GetBytes($"{dayKey}::{noteId}");
        var hash = SHA256.HashData(payload);
        return Convert.ToHexString(hash);
    }

    private static GoldenRiskCase LoadCase(string filePath)
    {
        var content = File.ReadAllTextAsync(filePath).GetAwaiter().GetResult();
        var parsed = JsonSerializer.Deserialize<GoldenRiskFile>(content, JsonOptions)
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

        if (parsed.ExpectedExtraction is null || parsed.ExpectedExtraction.Count == 0)
        {
            throw new InvalidOperationException($"Missing 'expected_extraction' in {filePath}");
        }

        var expectedExtraction = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in parsed.ExpectedExtraction)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"Expected extraction value for key '{key}' is null/empty in {filePath}");
            }

            expectedExtraction[key] = value;
        }

        return new GoldenRiskCase(
            NoteId: parsed.NoteId,
            NoteContent: parsed.NoteContent,
            TestType: parsed.TestType,
            ExpectedExtraction: expectedExtraction,
            FilePath: filePath,
            FileName: Path.GetFileName(filePath));
    }

    private static string FindRepositoryRoot()
    {
        foreach (var candidate in EnumerateCandidateStartingPoints())
        {
            var current = new DirectoryInfo(candidate);
            while (current is not null)
            {
                var solutionPath = Path.Combine(current.FullName, "session-sight.sln");
                if (File.Exists(solutionPath))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new InvalidOperationException(
            "Could not find repository root containing session-sight.sln.");
    }

    private static IEnumerable<string> EnumerateCandidateStartingPoints()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        if (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            yield return currentDirectory;
        }

        var baseDirectory = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            yield return baseDirectory;
        }
    }

    private static GoldenMode ParseMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return GoldenMode.Smoke;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "smoke" => GoldenMode.Smoke,
            "full" => GoldenMode.Full,
            _ => throw new InvalidOperationException(
                $"Invalid GOLDEN_MODE '{value}'. Expected 'smoke' or 'full'.")
        };
    }

    private static DateTime ResolveEffectiveDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ResolveOperationalDateAtEasternBoundary(DateTimeOffset.UtcNow);
        }

        if (!DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            throw new InvalidOperationException(
                $"Invalid GOLDEN_DATE '{value}'. Expected format yyyy-MM-dd.");
        }

        return parsed.Date;
    }

    private static DateTime ResolveOperationalDateAtEasternBoundary(DateTimeOffset utcNow)
    {
        var easternTimeZone = GetEasternTimeZone();
        var easternNow = TimeZoneInfo.ConvertTime(utcNow, easternTimeZone);
        var operationalDate = easternNow.TimeOfDay < DailyBoundaryEastern
            ? easternNow.Date.AddDays(-1)
            : easternNow.Date;

        return operationalDate;
    }

    private static TimeZoneInfo GetEasternTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
    }

    private static int ParsePositiveInt(string? value, int defaultValue, string variableName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            throw new InvalidOperationException(
                $"Invalid {variableName} '{value}'. Expected a positive integer.");
        }

        return parsed;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class GoldenRiskFile
    {
        [JsonPropertyName("note_id")]
        public string NoteId { get; init; } = string.Empty;

        [JsonPropertyName("note_content")]
        public string NoteContent { get; init; } = string.Empty;

        [JsonPropertyName("expected_extraction")]
        public Dictionary<string, string> ExpectedExtraction { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        [JsonPropertyName("test_type")]
        public string TestType { get; init; } = string.Empty;
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
    IReadOnlyDictionary<string, string> ExpectedExtraction,
    string FilePath,
    string FileName)
{
    public override string ToString() => NoteId;
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
