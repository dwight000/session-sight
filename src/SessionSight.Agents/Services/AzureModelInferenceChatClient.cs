using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.AI;
using SessionSight.Core.Resilience;

namespace SessionSight.Agents.Services;

/// <summary>
/// IChatClient adapter for Azure AI Model Inference API (non-OpenAI models on AIServices).
/// Uses HttpClient directly instead of Azure SDKs because:
///
/// 1. AzureOpenAIClient (Azure.AI.OpenAI) hardcodes /openai/deployments/{name}/chat/completions
///    which returns 404 for non-OpenAI models (e.g. Mistral) on kind:AIServices resources.
///    Non-OpenAI models are served at /models/chat/completions with model name in the body.
///
/// 2. ChatCompletionsClient (Azure.AI.Inference 1.0.0-beta.5) constructs /chat/completions
///    relative to the base URL. Appending /models to the base URL fixes the path but breaks
///    token acquisition — the SDK no longer recognizes the URL as a CognitiveServices endpoint
///    and requests a token with the wrong audience, resulting in 401.
///
/// This adapter handles both correctly: it constructs the /models/chat/completions URL and
/// acquires tokens with the https://cognitiveservices.azure.com scope. The response format
/// is identical to OpenAI's, so the mapping is straightforward.
///
/// When Azure.AI.Inference reaches GA with proper AIServices endpoint support, this can be
/// replaced with the SDK client.
/// </summary>
public sealed class AzureModelInferenceChatClient : IChatClient
{
    private static readonly HttpClient HttpClient = new();
    private static readonly TokenRequestContext TokenContext = new(["https://cognitiveservices.azure.com/.default"]);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _endpoint;
    private readonly string _modelName;
    private readonly DefaultAzureCredential _credential;

    public AzureModelInferenceChatClient(string endpoint, string modelName)
    {
        _endpoint = endpoint.TrimEnd('/');
        _modelName = modelName;
        _credential = new DefaultAzureCredential();
    }

    public ChatClientMetadata Metadata => new(nameof(AzureModelInferenceChatClient), null, _modelName);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var token = await _credential.GetTokenAsync(TokenContext, cancellationToken);

        var requestBody = new InferenceRequest
        {
            Model = _modelName,
            Messages = messages.Select(m =>
            {
                var role = "user";
                if (m.Role == ChatRole.System) role = "system";
                else if (m.Role == ChatRole.Assistant) role = "assistant";
                return new InferenceMessage { Role = role, Content = m.Text ?? string.Empty };
            }).ToList(),
            Temperature = options?.Temperature,
            MaxTokens = options?.MaxOutputTokens,
        };

        if (options?.ResponseFormat is ChatResponseFormatJson)
            requestBody.ResponseFormat = new InferenceResponseFormat { Type = "json_object" };

        var url = $"{_endpoint}/models/chat/completions?api-version=2024-05-01-preview";
        var jsonPayload = JsonSerializer.Serialize(requestBody, JsonOptions);

        var body = await SendWithRetryAsync(url, token.Token, jsonPayload, cancellationToken);

        var result = JsonSerializer.Deserialize<InferenceResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize inference response");

        var firstChoice = result.Choices?.FirstOrDefault();
        var content = firstChoice?.Message?.Content ?? string.Empty;
        var chatResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, content))
        {
            ModelId = result.Model,
            FinishReason = MapFinishReason(firstChoice?.FinishReason),
        };

        if (result.Usage is not null)
        {
            chatResponse.Usage = new UsageDetails
            {
                InputTokenCount = result.Usage.PromptTokens,
                OutputTokenCount = result.Usage.CompletionTokens,
                TotalTokenCount = result.Usage.TotalTokens,
            };
        }

        return chatResponse;
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Streaming not supported for model inference client.");
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }

    private static ChatFinishReason? MapFinishReason(string? finishReason) => finishReason switch
    {
        "stop" => ChatFinishReason.Stop,
        "length" => ChatFinishReason.Length,
        "content_filter" => ChatFinishReason.ContentFilter,
        "tool_calls" => ChatFinishReason.ToolCalls,
        _ => null,
    };

    private static async Task<string> SendWithRetryAsync(
        string url, string bearerToken, string jsonPayload, CancellationToken ct)
    {
        var maxAttempts = AzureRetryDefaults.MaxRetries + 1;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            request.Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
                return body;

            var statusCode = (int)response.StatusCode;
            var isLastAttempt = attempt == maxAttempts - 1;
            if (isLastAttempt || (statusCode != 429 && statusCode < 500))
                throw new HttpRequestException($"HTTP {statusCode} ({response.ReasonPhrase})\n\n{body}");

            var delay = AzureRetryDefaults.Delay * Math.Pow(2, attempt);
            var jitter = (Random.Shared.NextDouble() * 2 - 1) * AzureRetryDefaults.Jitter.TotalMilliseconds;
            var waitMs = Math.Min(delay.TotalMilliseconds + jitter, AzureRetryDefaults.MaxDelay.TotalMilliseconds);
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(waitMs, 0)), ct);
        }

        throw new UnreachableException("Retry loop exited without returning or throwing");
    }

    // ── Request/response DTOs (match Azure AI Model Inference API) ──

    private sealed class InferenceRequest
    {
        public string? Model { get; set; }
        public List<InferenceMessage>? Messages { get; set; }
        public float? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public InferenceResponseFormat? ResponseFormat { get; set; }
    }

    private sealed class InferenceMessage
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
    }

    private sealed class InferenceResponseFormat
    {
        public string? Type { get; set; }
    }

    private sealed record InferenceResponse(
        string? Model,
        List<InferenceChoice>? Choices,
        InferenceUsage? Usage);

    private sealed record InferenceChoice(InferenceMessage? Message, string? FinishReason);

    private sealed record InferenceUsage(
        int PromptTokens,
        int CompletionTokens,
        int TotalTokens);
}
