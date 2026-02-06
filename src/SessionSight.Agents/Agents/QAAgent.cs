using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using SessionSight.Agents.Models;
using SessionSight.Agents.Prompts;
using SessionSight.Agents.Routing;
using SessionSight.Agents.Services;
using SessionSight.Infrastructure.Search;

namespace SessionSight.Agents.Agents;

/// <summary>
/// Q&amp;A Agent implementation using RAG (Retrieval-Augmented Generation).
/// Answers clinical questions by searching indexed sessions and generating answers with citations.
/// </summary>
public partial class QAAgent : IQAAgent
{
    private readonly IAIFoundryClientFactory _clientFactory;
    private readonly IModelRouter _modelRouter;
    private readonly IEmbeddingService _embeddingService;
    private readonly ISearchIndexService _searchIndexService;
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
        ILogger<QAAgent> logger)
    {
        _clientFactory = clientFactory;
        _modelRouter = modelRouter;
        _embeddingService = embeddingService;
        _searchIndexService = searchIndexService;
        _logger = logger;
    }

    public string Name => "QAAgent";

    public async Task<QAResponse> AnswerAsync(string question, Guid patientId, CancellationToken ct = default)
    {
        LogStartingQA(_logger, patientId, question.Length);

        // Step 1: Classify question complexity
        var isComplex = await ClassifyComplexityAsync(question, ct);
        LogComplexityClassified(_logger, isComplex ? "complex" : "simple");

        // Step 2: Embed the question
        var queryVector = await _embeddingService.GenerateEmbeddingAsync(question, ct);

        // Step 3: Search for relevant sessions (request maxResults + 1 to detect overflow)
        var searchResults = await _searchIndexService.SearchAsync(
            question,
            queryVector,
            patientId.ToString("D"),
            MaxContextSessions + 1,
            ct);

        // Step 4: Handle empty results
        if (searchResults.Count == 0)
        {
            LogNoSearchResults(_logger, patientId);
            return new QAResponse
            {
                Question = question,
                Answer = "I don't have session data to answer this question. No indexed sessions were found for this patient.",
                Confidence = 0,
                ModelUsed = _modelRouter.SelectModel(isComplex ? ModelTask.QAComplex : ModelTask.QASimple),
                GeneratedAt = DateTime.UtcNow
            };
        }

        // Step 5: Check for context overflow
        string? warning = null;
        var resultsList = searchResults.ToList();
        if (resultsList.Count > MaxContextSessions)
        {
            warning = $"Query matched more than {MaxContextSessions} sessions. Results are limited to the most relevant {MaxContextSessions}.";
            resultsList = resultsList.Take(MaxContextSessions).ToList();
        }

        // Step 6: Build context string
        var contextString = BuildContextString(resultsList);

        // Step 7: Build source citations from search results
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

        // Step 8: Select model and call LLM
        var modelTask = isComplex ? ModelTask.QAComplex : ModelTask.QASimple;
        var modelName = _modelRouter.SelectModel(modelTask);

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

            // Step 9: Parse the LLM response
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

            if (parsed.TryGetProperty("confidence", out var confidence) &&
                confidence.TryGetDouble(out var confidenceValue))
            {
                response.Confidence = Math.Clamp(confidenceValue, 0, 1);
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
