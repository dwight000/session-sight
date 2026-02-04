using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace SessionSight.Infrastructure.Search;

/// <summary>
/// Search index document representing a therapy session.
/// </summary>
public class SessionSearchDocument
{
    /// <summary>
    /// Unique document ID (typically session ID).
    /// </summary>
    [SimpleField(IsKey = true)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Session entity ID.
    /// </summary>
    [SimpleField(IsFilterable = true)]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Patient ID for filtering queries to a specific patient's sessions.
    /// </summary>
    [SimpleField(IsFilterable = true)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Date/time of the session.
    /// </summary>
    [SimpleField(IsFilterable = true, IsSortable = true)]
    public DateTimeOffset SessionDate { get; set; }

    /// <summary>
    /// Type of session (e.g., Individual, Group, Family).
    /// </summary>
    [SimpleField(IsFilterable = true, IsFacetable = true)]
    public string? SessionType { get; set; }

    /// <summary>
    /// Full text content of the session note for keyword search.
    /// </summary>
    [SearchableField]
    public string? Content { get; set; }

    /// <summary>
    /// Session summary for keyword search.
    /// </summary>
    [SearchableField]
    public string? Summary { get; set; }

    /// <summary>
    /// Primary diagnosis code/description.
    /// </summary>
    [SimpleField(IsFilterable = true, IsFacetable = true)]
    public string? PrimaryDiagnosis { get; set; }

    /// <summary>
    /// Therapeutic interventions used in the session.
    /// </summary>
    [SimpleField(IsFilterable = true, IsFacetable = true)]
    public IList<string>? Interventions { get; set; }

    /// <summary>
    /// Risk level assessment (e.g., None, Low, Moderate, High).
    /// </summary>
    [SimpleField(IsFilterable = true, IsFacetable = true)]
    public string? RiskLevel { get; set; }

    /// <summary>
    /// Numeric mood score (0-10 scale).
    /// </summary>
    [SimpleField(IsFilterable = true)]
    public int? MoodScore { get; set; }

    /// <summary>
    /// Vector embedding of the session content for semantic search.
    /// 3072 dimensions for text-embedding-3-large.
    /// </summary>
    [VectorSearchField(
        VectorSearchDimensions = 3072,
        VectorSearchProfileName = "vector-profile")]
    public IReadOnlyList<float>? ContentVector { get; set; }
}
