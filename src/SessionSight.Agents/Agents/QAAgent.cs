using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using SessionSight.Agents.Helpers;
using SessionSight.Agents.Models;
using SessionSight.Agents.Prompts;
using SessionSight.Agents.Routing;
using SessionSight.Agents.Services;
using SessionSight.Agents.Tools;
using SessionSight.Infrastructure.Search;

namespace SessionSight.Agents.Agents;

/// <summary>
/// Q&amp;A Agent implementation using RAG (Retrieval-Augmented Generation).
/// Simple questions use single-shot RAG; complex questions use an agentic loop with tools.
/// </summary>
public partial class QAAgent : IQAAgent
{
    private readonly IAIFoundryClientFactory _clientFactory;
    private readonly IModelRouter _modelRouter;
    private readonly IEmbeddingService _embeddingService;
    private readonly ISearchIndexService _searchIndexService;
    private readonly AgentLoopRunner _agentLoopRunner;
    private readonly SearchSessionsTool _searchSessionsTool;
    private readonly GetSessionDetailTool _getSessionDetailTool;
    private readonly GetPatientTimelineTool _getPatientTimelineTool;
    private readonly AggregateMetricsTool _aggregateMetricsTool;
    private readonly CompareSessionsTool _compareSessionsTool;
    private readonly ILogger<QAAgent> _logger;

    internal const int MaxContextSessions = 10;

    private static readonly JsonSerializerOptions JsonOptions = SharedJsonOptions.AgentDefault;

#pragma warning disable S107 // Constructor parameters - DI requires explicit dependencies for testability
    public QAAgent(
        IAIFoundryClientFactory clientFactory,
        IModelRouter modelRouter,
        IEmbeddingService embeddingService,
        ISearchIndexService searchIndexService,
        AgentLoopRunner agentLoopRunner,
        SearchSessionsTool searchSessionsTool,
        GetSessionDetailTool getSessionDetailTool,
        GetPatientTimelineTool getPatientTimelineTool,
        AggregateMetricsTool aggregateMetricsTool,
        CompareSessionsTool compareSessionsTool,
        ILogger<QAAgent> logger)
#pragma warning restore S107
    {
        _clientFactory = clientFactory;
        _modelRouter = modelRouter;
        _embeddingService = embeddingService;
        _searchIndexService = searchIndexService;
        _agentLoopRunner = agentLoopRunner;
        _searchSessionsTool = searchSessionsTool;
        _getSessionDetailTool = getSessionDetailTool;
        _getPatientTimelineTool = getPatientTimelineTool;
        _aggregateMetricsTool = aggregateMetricsTool;
        _compareSessionsTool = compareSessionsTool;
        _logger = logger;
    }

    public string Name => "QAAgent";

    public async Task<QAResponse> AnswerAsync(string question, Guid patientId, CancellationToken ct = default)
    {
        LogStartingQA(_logger, patientId, question.Length);

        // Classify question complexity
        var isComplex = await ClassifyComplexityAsync(question, ct);
        LogComplexityClassified(_logger, isComplex ? "complex" : "simple");

        var diagnostics = new QADiagnostics { IsComplex = isComplex };

        if (isComplex)
        {
            return await AnswerComplexAsync(question, patientId, diagnostics, ct);
        }

        return await AnswerSimpleAsync(question, patientId, diagnostics, ct);
    }

    private async Task<QAResponse> AnswerSimpleAsync(string question, Guid patientId, QADiagnostics diagnostics, CancellationToken ct)
    {
        // Embed the question
        var queryVector = await _embeddingService.GenerateEmbeddingAsync(question, ct);

        // Search for relevant sessions (request maxResults + 1 to detect overflow)
        var searchResults = await _searchIndexService.SearchAsync(
            question,
            queryVector,
            patientId.ToString("D"),
            MaxContextSessions + 1,
            ct);

        // Handle empty results
        if (searchResults.Count == 0)
        {
            LogNoSearchResults(_logger, patientId);
            diagnostics.SearchResultCount = 0;
            return new QAResponse
            {
                Question = question,
                Answer = "I don't have session data to answer this question. No indexed sessions were found for this patient.",
                Confidence = 0,
                ModelUsed = _modelRouter.SelectModel(ModelTask.QASimple),
                GeneratedAt = DateTime.UtcNow,
                Diagnostics = diagnostics
            };
        }

        // Check for context overflow
        string? warning = null;
        var resultsList = searchResults.ToList();
        if (resultsList.Count > MaxContextSessions)
        {
            warning = $"Query matched more than {MaxContextSessions} sessions. Results are limited to the most relevant {MaxContextSessions}.";
            resultsList = resultsList.Take(MaxContextSessions).ToList();
        }

        // Build context string
        var contextString = BuildContextString(resultsList);

        // Build source citations from search results
        var sources = resultsList
            .Select(r => new SourceCitation
            {
                SessionId = r.Document.SessionId,
                SessionDate = r.Document.SessionDate,
                SessionType = r.Document.SessionType,
                Summary = r.Document.Summary,
                RelevanceScore = r.Score ?? 0
            })
            .ToList();

        diagnostics.SearchResultCount = resultsList.Count;

        // Select model and call LLM
        var modelName = _modelRouter.SelectModel(ModelTask.QASimple);

        try
        {
            var chatClient = _clientFactory.CreateChatClient(modelName);
            var prompt = QAPrompts.GetAnswerPrompt(question, contextString);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, QAPrompts.SystemPrompt),
                new(ChatRole.User, prompt)
            };

            var options = new ChatOptions
            {
                Temperature = 0.2f,
                MaxOutputTokens = 1024
            };

            var response = await chatClient.GetResponseAsync(messages, options, ct);
            var content = response.Text!;

            var qaResponse = ParseQAResponse(content);
            qaResponse.Question = question;
            qaResponse.ModelUsed = modelName;
            qaResponse.Sources = sources;
            qaResponse.Warning = warning;
            qaResponse.GeneratedAt = DateTime.UtcNow;

            diagnostics.Reasoning = ParseReasoning(content);
            qaResponse.Diagnostics = diagnostics;

            LogQACompleted(_logger, patientId, qaResponse.Confidence);
            return qaResponse;
        }
        catch (Exception ex)
        {
            LogQAError(_logger, ex, patientId);

            return new QAResponse
            {
                Question = question,
                Answer = "An error occurred while generating the answer. Please try again.",
                Confidence = 0,
                Sources = sources,
                ModelUsed = modelName,
                Warning = warning,
                GeneratedAt = DateTime.UtcNow,
                Diagnostics = diagnostics
            };
        }
    }

    private async Task<QAResponse> AnswerComplexAsync(string question, Guid patientId, QADiagnostics diagnostics, CancellationToken ct)
    {
        var modelName = _modelRouter.SelectModel(ModelTask.QAComplex);

        try
        {
            var chatClient = _clientFactory.CreateChatClient(modelName);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, QAPrompts.AgenticSystemPrompt),
                new(ChatRole.User, QAPrompts.GetAgenticUserPrompt(question, patientId))
            };

            // Scope tools to the requested patient to prevent cross-patient data access
            _searchSessionsTool.RequiredPatientId = patientId;
            _getSessionDetailTool.AllowedPatientId = patientId;
            _compareSessionsTool.AllowedPatientId = patientId;

            IAgentTool[] tools =
            [
                _searchSessionsTool,
                _getSessionDetailTool,
                _getPatientTimelineTool,
                _aggregateMetricsTool,
                _compareSessionsTool
            ];

            var loopResult = await _agentLoopRunner.RunAsync(chatClient, messages, tools, temperature: 0.2f, ct: ct);

            var qaResponse = loopResult.IsPartial
                ? new QAResponse
                {
                    Answer = $"Analysis incomplete: {loopResult.PartialReason}",
                    Confidence = 0
                }
                : ParseQAResponse(loopResult.Content ?? string.Empty);

            qaResponse.Question = question;
            qaResponse.ModelUsed = modelName;
            qaResponse.ToolCallCount = loopResult.ToolCallCount;
            qaResponse.GeneratedAt = DateTime.UtcNow;

            // Build sources from citedSessionIds in the parsed response,
            // fall back to session IDs from tool call results if none cited
            BuildAgenticSources(qaResponse, loopResult.Content ?? string.Empty);
            if (qaResponse.Sources is not { Count: > 0 })
                FallbackSourcesFromToolTrace(qaResponse, loopResult.ToolCallTrace);

            diagnostics.Reasoning = ParseReasoning(loopResult.Content ?? string.Empty);
            diagnostics.ToolCalls = loopResult.ToolCallTrace
                .Select(t => new QAToolCallEntry { ToolName = t.ToolName, Succeeded = t.Succeeded })
                .ToList();
            qaResponse.Diagnostics = diagnostics;

            LogQACompleted(_logger, patientId, qaResponse.Confidence);
            return qaResponse;
        }
        catch (Exception ex)
        {
            LogQAError(_logger, ex, patientId);

            return new QAResponse
            {
                Question = question,
                Answer = "An error occurred while generating the answer. Please try again.",
                Confidence = 0,
                ModelUsed = modelName,
                GeneratedAt = DateTime.UtcNow,
                Diagnostics = diagnostics
            };
        }
    }

    private static void BuildAgenticSources(QAResponse response, string content)
    {
        try
        {
            var json = LlmJsonHelper.ExtractJson(content);
            var parsed = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);

            if (parsed.TryGetProperty("citedSessionIds", out var cited) &&
                cited.ValueKind == JsonValueKind.Array)
            {
                response.Sources = cited.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => new SourceCitation
                    {
                        SessionId = e.GetString() ?? string.Empty
                    })
                    .Where(s => !string.IsNullOrEmpty(s.SessionId))
                    .ToList();
            }
        }
        catch (JsonException)
        {
            // Sources remain empty if parsing fails
        }
    }

    /// <summary>
    /// When the LLM omits citedSessionIds, extract session IDs from
    /// search_sessions / get_session_detail tool call outputs as fallback.
    /// </summary>
    internal static void FallbackSourcesFromToolTrace(QAResponse response, IReadOnlyList<Tools.ToolCallEntry> trace)
    {
        var sessionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in trace.Where(t =>
            t.Succeeded &&
            !string.IsNullOrEmpty(t.OutputJson) &&
            t.ToolName is "search_sessions" or "get_session_detail"))
        {
            ExtractSessionIdsFromJson(entry.OutputJson!, sessionIds);
        }

        if (sessionIds.Count > 0)
        {
            response.Sources = sessionIds
                .Select(id => new SourceCitation { SessionId = id })
                .ToList();
        }
    }

    private static void ExtractSessionIdsFromJson(string json, HashSet<string> sessionIds)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            ExtractSessionIdsFromElement(doc.RootElement, sessionIds);
        }
        catch (JsonException)
        {
            // Best-effort extraction
        }
    }

    private static void ExtractSessionIdsFromElement(JsonElement element, HashSet<string> sessionIds)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                // Look for "sessionId" or "SessionId" property
                if (element.TryGetProperty("sessionId", out var sid) && sid.ValueKind == JsonValueKind.String)
                {
                    var val = sid.GetString();
                    if (!string.IsNullOrEmpty(val))
                        sessionIds.Add(val);
                }
                else if (element.TryGetProperty("SessionId", out var sid2) && sid2.ValueKind == JsonValueKind.String)
                {
                    var val = sid2.GetString();
                    if (!string.IsNullOrEmpty(val))
                        sessionIds.Add(val);
                }

                foreach (var prop in element.EnumerateObject())
                    ExtractSessionIdsFromElement(prop.Value, sessionIds);
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    ExtractSessionIdsFromElement(item, sessionIds);
                break;
        }
    }

    private static string? ParseReasoning(string content)
    {
        try
        {
            var json = LlmJsonHelper.ExtractJson(content);
            var parsed = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);

            if (parsed.TryGetProperty("reasoning", out var reasoning) &&
                reasoning.ValueKind == JsonValueKind.String)
            {
                return reasoning.GetString();
            }
        }
        catch (JsonException)
        {
            // Reasoning is best-effort
        }

        return null;
    }

    private async Task<bool> ClassifyComplexityAsync(string question, CancellationToken ct)
    {
        try
        {
            var modelName = _modelRouter.SelectModel(ModelTask.QASimple);
            var chatClient = _clientFactory.CreateChatClient(modelName);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, QAPrompts.ComplexityPrompt),
                new(ChatRole.User, question)
            };

            var options = new ChatOptions
            {
                Temperature = 0f,
                MaxOutputTokens = 10
            };

            var response = await chatClient.GetResponseAsync(messages, options, ct);
            var result = response.Text!.Trim().ToLowerInvariant();

            return result.Contains("complex", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            LogComplexityError(_logger, ex);
            return false; // Default to simple on error
        }
    }

    internal static string BuildContextString(
        IReadOnlyList<Azure.Search.Documents.Models.SearchResult<SessionSearchDocument>> results)
    {
        var sb = new StringBuilder();

        foreach (var doc in results.Select(r => r.Document))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"--- Session: {doc.SessionId} ---");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Date: {doc.SessionDate:yyyy-MM-dd}");

            if (!string.IsNullOrEmpty(doc.SessionType))
                sb.AppendLine(CultureInfo.InvariantCulture, $"Type: {doc.SessionType}");

            if (!string.IsNullOrEmpty(doc.RiskLevel))
                sb.AppendLine(CultureInfo.InvariantCulture, $"Risk Level: {doc.RiskLevel}");

            if (!string.IsNullOrEmpty(doc.Summary))
            {
                sb.AppendLine("Summary:");
                sb.AppendLine(doc.Summary);
            }

            if (doc.Interventions is { Count: > 0 })
                sb.AppendLine(CultureInfo.InvariantCulture, $"Interventions: {string.Join(", ", doc.Interventions)}");

            if (!string.IsNullOrEmpty(doc.Content))
            {
                sb.AppendLine("Content:");
                sb.AppendLine(doc.Content);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    internal static QAResponse ParseQAResponse(string content)
    {
        var json = LlmJsonHelper.ExtractJson(content);

        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
            var response = new QAResponse();

            if (parsed.TryGetProperty("answer", out var answer))
                response.Answer = answer.GetString() ?? string.Empty;

            if (parsed.TryGetProperty("confidence", out var confidence))
            {
                var confidenceValue = LlmJsonHelper.TryParseConfidence(confidence);
                if (confidenceValue.HasValue)
                    response.Confidence = Math.Clamp(confidenceValue.Value, 0, 1);
            }

            // Parse citedSessionIds — we don't use them to filter sources
            // (sources come from search results), but we parse to validate the response

            return response;
        }
        catch (JsonException)
        {
            return new QAResponse
            {
                Answer = "Failed to parse the generated answer. Raw response: " + content,
                Confidence = 0
            };
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting Q&A for patient {PatientId}, question length={QuestionLength}")]
    private static partial void LogStartingQA(ILogger logger, Guid patientId, int questionLength);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Question classified as {Complexity}")]
    private static partial void LogComplexityClassified(ILogger logger, string complexity);

    [LoggerMessage(Level = LogLevel.Information, Message = "No search results found for patient {PatientId}")]
    private static partial void LogNoSearchResults(ILogger logger, Guid patientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Q&A completed for patient {PatientId}, confidence={Confidence}")]
    private static partial void LogQACompleted(ILogger logger, Guid patientId, double confidence);

    [LoggerMessage(Level = LogLevel.Error, Message = "Q&A failed for patient {PatientId}")]
    private static partial void LogQAError(ILogger logger, Exception exception, Guid patientId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Complexity classification failed, defaulting to simple")]
    private static partial void LogComplexityError(ILogger logger, Exception exception);
}
