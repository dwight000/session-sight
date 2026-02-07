using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
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
    private readonly ILogger<QAAgent> _logger;

    internal const int MaxContextSessions = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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
        ILogger<QAAgent> logger)
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
        _logger = logger;
    }

    public string Name => "QAAgent";

    public async Task<QAResponse> AnswerAsync(string question, Guid patientId, CancellationToken ct = default)
    {
        LogStartingQA(_logger, patientId, question.Length);

        // Classify question complexity
        var isComplex = await ClassifyComplexityAsync(question, ct);
        LogComplexityClassified(_logger, isComplex ? "complex" : "simple");

        if (isComplex)
        {
            return await AnswerComplexAsync(question, patientId, ct);
        }

        return await AnswerSimpleAsync(question, patientId, ct);
    }

    private async Task<QAResponse> AnswerSimpleAsync(string question, Guid patientId, CancellationToken ct)
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
            return new QAResponse
            {
                Question = question,
                Answer = "I don't have session data to answer this question. No indexed sessions were found for this patient.",
                Confidence = 0,
                ModelUsed = _modelRouter.SelectModel(ModelTask.QASimple),
                GeneratedAt = DateTime.UtcNow
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

        // Select model and call LLM
        var modelName = _modelRouter.SelectModel(ModelTask.QASimple);

        try
        {
            var chatClient = _clientFactory.CreateChatClient(modelName);
            var prompt = QAPrompts.GetAnswerPrompt(question, contextString);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(QAPrompts.SystemPrompt),
                new UserChatMessage(prompt)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.2f,
                MaxOutputTokenCount = 1024
            };

            var response = await chatClient.CompleteChatAsync(messages, options, ct);
            var content = response.Value.Content[0].Text;

            var qaResponse = ParseQAResponse(content);
            qaResponse.Question = question;
            qaResponse.ModelUsed = modelName;
            qaResponse.Sources = sources;
            qaResponse.Warning = warning;
            qaResponse.GeneratedAt = DateTime.UtcNow;

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
                GeneratedAt = DateTime.UtcNow
            };
        }
    }

    private async Task<QAResponse> AnswerComplexAsync(string question, Guid patientId, CancellationToken ct)
    {
        var modelName = _modelRouter.SelectModel(ModelTask.QAComplex);

        try
        {
            var chatClient = _clientFactory.CreateChatClient(modelName);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(QAPrompts.AgenticSystemPrompt),
                new UserChatMessage(QAPrompts.GetAgenticUserPrompt(question, patientId))
            };

            // Scope tools to the requested patient to prevent cross-patient data access
            _searchSessionsTool.RequiredPatientId = patientId;
            _getSessionDetailTool.AllowedPatientId = patientId;

            IAgentTool[] tools =
            [
                _searchSessionsTool,
                _getSessionDetailTool,
                _getPatientTimelineTool,
                _aggregateMetricsTool
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

            // Build sources from citedSessionIds in the parsed response
            BuildAgenticSources(qaResponse, loopResult.Content ?? string.Empty);

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
                GeneratedAt = DateTime.UtcNow
            };
        }
    }

    private static void BuildAgenticSources(QAResponse response, string content)
    {
        try
        {
            var json = SummarizerAgent.ExtractJson(content);
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

    private async Task<bool> ClassifyComplexityAsync(string question, CancellationToken ct)
    {
        try
        {
            var modelName = _modelRouter.SelectModel(ModelTask.QASimple);
            var chatClient = _clientFactory.CreateChatClient(modelName);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(QAPrompts.ComplexityPrompt),
                new UserChatMessage(question)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0f,
                MaxOutputTokenCount = 10
            };

            var response = await chatClient.CompleteChatAsync(messages, options, ct);
            var result = response.Value.Content[0].Text.Trim().ToLowerInvariant();

            return result.Contains("complex", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            LogComplexityError(_logger, ex);
            return false; // Default to simple on error
        }
    }

    private static string BuildContextString(
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
        var json = SummarizerAgent.ExtractJson(content);

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
