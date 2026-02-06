using System.Globalization;
using System.Text.Json;
using SessionSight.Agents.Services;
using SessionSight.Infrastructure.Search;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Tool that searches indexed therapy sessions using hybrid vector + keyword search.
/// Used by the Q&amp;A agent for retrieval-augmented generation.
/// </summary>
public class SearchSessionsTool : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ISearchIndexService _searchIndexService;
    private readonly IEmbeddingService _embeddingService;

    public SearchSessionsTool(ISearchIndexService searchIndexService, IEmbeddingService embeddingService)
    {
        _searchIndexService = searchIndexService;
        _embeddingService = embeddingService;
    }

    /// <summary>
    /// When set, always filters search results to this patient regardless of LLM input.
    /// </summary>
    public Guid? RequiredPatientId { get; set; }

    public string Name => "search_sessions";

    public string Description => "Search indexed therapy sessions using semantic and keyword search. Returns matching sessions with relevance scores.";

    public BinaryData InputSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "The search query text"
                },
                "patientId": {
                    "type": "string",
                    "description": "Optional patient ID (GUID) to filter results to a specific patient"
                },
                "maxResults": {
                    "type": "integer",
                    "description": "Maximum number of results to return (default 5)"
                }
            },
            "required": ["query"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(BinaryData input, CancellationToken ct = default)
    {
        try
        {
            var request = await JsonSerializer.DeserializeAsync<SearchSessionsInput>(input.ToStream(), JsonOptions, ct);

            if (string.IsNullOrEmpty(request?.Query))
            {
                return ToolResult.Error("Missing required 'query' parameter");
            }

            string? patientFilter = null;
            if (RequiredPatientId.HasValue)
            {
                patientFilter = RequiredPatientId.Value.ToString("D", CultureInfo.InvariantCulture);
            }
            else if (!string.IsNullOrEmpty(request.PatientId))
            {
                if (!Guid.TryParse(request.PatientId, out var patientGuid))
                {
                    return ToolResult.Error("Invalid patientId format - must be a valid GUID");
                }
                patientFilter = patientGuid.ToString("D", CultureInfo.InvariantCulture);
            }

            var maxResults = request.MaxResults ?? 5;

            var queryVector = await _embeddingService.GenerateEmbeddingAsync(request.Query, ct);
            var searchResults = await _searchIndexService.SearchAsync(
                request.Query,
                queryVector,
                patientFilter,
                maxResults,
                ct);

            var results = searchResults
                .Select(r => new SearchSessionResult
                {
                    SessionId = r.Document.SessionId,
                    SessionDate = r.Document.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    SessionType = r.Document.SessionType,
                    Summary = r.Document.Summary,
                    RiskLevel = r.Document.RiskLevel,
                    RelevanceScore = r.Score ?? 0
                })
                .ToList();

            return ToolResult.Ok(new SearchSessionsOutput
            {
                Results = results,
                ResultCount = results.Count
            });
        }
        catch (JsonException ex)
        {
            return ToolResult.Error($"Invalid JSON input: {ex.Message}");
        }
    }
}

internal sealed class SearchSessionsInput
{
    public string? Query { get; set; }
    public string? PatientId { get; set; }
    public int? MaxResults { get; set; }
}

internal sealed class SearchSessionsOutput
{
    public List<SearchSessionResult> Results { get; set; } = [];
    public int ResultCount { get; set; }
}

internal sealed class SearchSessionResult
{
    public string SessionId { get; set; } = string.Empty;
    public string SessionDate { get; set; } = string.Empty;
    public string? SessionType { get; set; }
    public string? Summary { get; set; }
    public string? RiskLevel { get; set; }
    public double RelevanceScore { get; set; }
}
