using System.Text.RegularExpressions;

namespace SessionSight.Agents.Validation;

/// <summary>
/// Checks therapy notes for danger keywords as a secondary safety net.
/// Flags cases where extraction indicates "None" but keywords are present.
/// </summary>
public static class DangerKeywordChecker
{
    /// <summary>
    /// Keywords indicating suicidal ideation.
    /// </summary>
    public static readonly string[] SuicidalKeywords =
    {
        "suicide", "suicidal", "kill myself", "end my life", "not worth living",
        "want to die", "better off dead", "no reason to live", "end it all",
        "take my own life", "not be here", "not being here", "wouldn't wake up", "wish I was dead"
    };

    /// <summary>
    /// Keywords indicating self-harm.
    /// </summary>
    public static readonly string[] SelfHarmKeywords =
    {
        "self-harm", "self harm", "cutting", "hurt myself",
        "burning myself", "scratching", "self-injury", "hurting myself",
        "harming myself", "cut myself", "burned myself",
        "attempted overdose", "overdose attempt", "overdosed"
    };

    /// <summary>
    /// Keywords indicating homicidal ideation.
    /// </summary>
    public static readonly string[] HomicidalKeywords =
    {
        "homicidal",
        "kill someone",
        "kill somebody",
        "hurt someone",
        "hurt somebody",
        "violent thoughts",
        "harm others",
        "harm other people",
        "kill them",
        "hurt them",
        "want to hurt others",
        "want to hurt someone",
        "want to hurt somebody",
        "thoughts of hurting others",
        "thoughts of hurting someone",
        "thoughts of hurting somebody",
        "thoughts of killing others",
        "thoughts of killing someone",
        "thoughts of killing somebody"
    };

    /// <summary>
    /// Checks the note text for danger keywords.
    /// </summary>
    /// <param name="noteText">The therapy note text to check.</param>
    /// <returns>Result containing all keyword matches found.</returns>
    public static KeywordCheckResult Check(string noteText)
    {
        if (string.IsNullOrWhiteSpace(noteText))
        {
            return new KeywordCheckResult();
        }

        var lowerText = noteText.ToLowerInvariant();
        var result = new KeywordCheckResult();

        result.SuicidalMatches.AddRange(
            SuicidalKeywords.Where(keyword => ContainsKeyword(lowerText, keyword)));

        result.SelfHarmMatches.AddRange(
            SelfHarmKeywords.Where(keyword => ContainsKeyword(lowerText, keyword)));

        result.HomicidalMatches.AddRange(
            HomicidalKeywords.Where(keyword => ContainsKeyword(lowerText, keyword)));

        return result;
    }

    /// <summary>
    /// Checks if a keyword is present in text, with word boundary matching.
    /// </summary>
    private static bool ContainsKeyword(string text, string keyword)
    {
        // Use word boundary matching to avoid partial matches
        // e.g., "suicidal" should not match within "antisuicidal"
        var pattern = $@"\b{Regex.Escape(keyword)}\b";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
    }
}

/// <summary>
/// Result of keyword checking.
/// </summary>
public class KeywordCheckResult
{
    /// <summary>
    /// Suicidal-related keywords found.
    /// </summary>
    public List<string> SuicidalMatches { get; set; } = new();

    /// <summary>
    /// Self-harm-related keywords found.
    /// </summary>
    public List<string> SelfHarmMatches { get; set; } = new();

    /// <summary>
    /// Homicidal-related keywords found.
    /// </summary>
    public List<string> HomicidalMatches { get; set; } = new();

    /// <summary>
    /// Whether any keywords were found.
    /// </summary>
    public bool HasAnyMatches =>
        SuicidalMatches.Count > 0 ||
        SelfHarmMatches.Count > 0 ||
        HomicidalMatches.Count > 0;

    /// <summary>
    /// Gets all matches combined into a single list.
    /// </summary>
    public List<string> AllMatches
    {
        get
        {
            var all = new List<string>();
            all.AddRange(SuicidalMatches.Select(k => $"suicidal:{k}"));
            all.AddRange(SelfHarmMatches.Select(k => $"self-harm:{k}"));
            all.AddRange(HomicidalMatches.Select(k => $"homicidal:{k}"));
            return all;
        }
    }
}
