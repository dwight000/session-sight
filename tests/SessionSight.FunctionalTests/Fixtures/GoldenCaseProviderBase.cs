using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SessionSight.FunctionalTests.Fixtures;

internal static class GoldenCaseProviderBase
{
    private static readonly TimeSpan DailyBoundaryEastern = TimeSpan.FromHours(7);

    internal static string FindRepositoryRoot()
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

    internal static List<T> SelectDeterministicSubset<T>(
        IReadOnlyCollection<T> cases,
        string dayKey,
        int count,
        Func<T, string> noteIdSelector)
    {
        return cases
            .OrderBy(testCase => ComputeStableSortKey(dayKey, noteIdSelector(testCase)), StringComparer.Ordinal)
            .ThenBy(testCase => noteIdSelector(testCase), StringComparer.Ordinal)
            .Take(Math.Min(count, cases.Count))
            .ToList();
    }

    internal static GoldenMode ParseMode(string? value)
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

    internal static DateTime ResolveEffectiveDate(string? value)
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

    internal static int ParsePositiveInt(string? value, int defaultValue, string variableName)
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

    internal static GoldenExpectedOutcome ParseExpectedOutcome(string? value, string filePath)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return GoldenExpectedOutcome.ExtractionSuccess;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "extraction_success" => GoldenExpectedOutcome.ExtractionSuccess,
            "content_filter_blocked" => GoldenExpectedOutcome.ContentFilterBlocked,
            "content_filter_optional" => GoldenExpectedOutcome.ContentFilterOptional,
            _ => throw new InvalidOperationException(
                $"Invalid expected_outcome '{value}' in {filePath}. Expected 'extraction_success', 'content_filter_blocked', or 'content_filter_optional'.")
        };
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

    private static string ComputeStableSortKey(string dayKey, string noteId)
    {
        var payload = Encoding.UTF8.GetBytes($"{dayKey}::{noteId}");
        var hash = SHA256.HashData(payload);
        return Convert.ToHexString(hash);
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
}
