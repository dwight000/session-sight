using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using SessionSight.Agents.Models;
using SessionSight.Agents.Prompts;
using SessionSight.Agents.Routing;
using SessionSight.Agents.Services;

namespace SessionSight.Agents.Agents;

/// <summary>
/// Interface for the Intake Agent, which validates documents and extracts metadata.
/// </summary>
public interface IIntakeAgent : ISessionSightAgent
{
    /// <summary>
    /// Processes a parsed document, validates it as a therapy note, and extracts metadata.
    /// </summary>
    /// <param name="document">The parsed document from Document Intelligence.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Intake result with validation status and extracted metadata.</returns>
    Task<IntakeResult> ProcessAsync(ParsedDocument document, CancellationToken cancellationToken = default);
}

/// <summary>
/// Intake Agent implementation. Uses gpt-4o-mini to validate documents
/// and extract metadata before passing to the extraction pipeline.
/// </summary>
public class IntakeAgent : IIntakeAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IAIFoundryClientFactory _clientFactory;
    private readonly IModelRouter _modelRouter;
    private readonly ILogger<IntakeAgent> _logger;

    public IntakeAgent(
        IAIFoundryClientFactory clientFactory,
        IModelRouter modelRouter,
        ILogger<IntakeAgent> logger)
    {
        _clientFactory = clientFactory;
        _modelRouter = modelRouter;
        _logger = logger;
    }

    public string Name => "IntakeAgent";

    public async Task<IntakeResult> ProcessAsync(ParsedDocument document, CancellationToken cancellationToken = default)
    {
        var modelName = _modelRouter.SelectModel(ModelTask.DocumentIntake);
        _logger.LogInformation("Processing document with {Model}", modelName);

        var chatClient = _clientFactory.CreateChatClient(modelName);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(IntakePrompts.SystemPrompt),
            new UserChatMessage(IntakePrompts.BuildUserPrompt(document))
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.1f,
            MaxOutputTokenCount = 1024
        };

        var response = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
        var content = response.Value.Content[0].Text;

        return ParseResponse(content, document, modelName);
    }

    internal static IntakeResult ParseResponse(string content, ParsedDocument document, string modelName)
    {
        // Extract JSON from response (handle potential markdown code blocks)
        var json = ExtractJson(content);

        try
        {
            var parsed = JsonSerializer.Deserialize<IntakeResponseDto>(json, JsonOptions);

            if (parsed == null)
            {
                return CreateErrorResult(document, modelName, "Failed to parse LLM response as JSON");
            }

            return new IntakeResult
            {
                Document = document,
                ModelUsed = modelName,
                IsValidTherapyNote = parsed.IsValidTherapyNote,
                ValidationError = parsed.ValidationError,
                Metadata = new ExtractedMetadata
                {
                    DocumentType = parsed.DocumentType ?? string.Empty,
                    SessionDate = TryParseDate(parsed.SessionDate),
                    PatientId = parsed.PatientId,
                    TherapistName = parsed.TherapistName,
                    Language = parsed.Language ?? "en",
                    EstimatedWordCount = parsed.EstimatedWordCount
                }
            };
        }
        catch (JsonException ex)
        {
            return CreateErrorResult(document, modelName, $"JSON parse error: {ex.Message}");
        }
    }

    private static IntakeResult CreateErrorResult(ParsedDocument document, string modelName, string error)
    {
        return new IntakeResult
        {
            Document = document,
            ModelUsed = modelName,
            IsValidTherapyNote = false,
            ValidationError = error,
            Metadata = new ExtractedMetadata()
        };
    }

    internal static string ExtractJson(string content)
    {
        // Try to extract JSON from markdown code block
        var trimmed = content.Trim();

        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            var endIndex = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (endIndex > 7)
            {
                return trimmed[7..endIndex].Trim();
            }
        }

        if (trimmed.StartsWith("```"))
        {
            var startIndex = trimmed.IndexOf('\n') + 1;
            var endIndex = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (endIndex > startIndex)
            {
                return trimmed[startIndex..endIndex].Trim();
            }
        }

        return trimmed;
    }

    private static DateOnly? TryParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        if (DateOnly.TryParse(dateStr, out var date))
            return date;

        return null;
    }

    /// <summary>
    /// DTO for deserializing the LLM response.
    /// </summary>
    internal class IntakeResponseDto
    {
        public bool IsValidTherapyNote { get; set; }
        public string? ValidationError { get; set; }
        public string? DocumentType { get; set; }
        public string? SessionDate { get; set; }
        public string? PatientId { get; set; }
        public string? TherapistName { get; set; }
        public string? Language { get; set; }
        public int EstimatedWordCount { get; set; }
    }
}
