using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;
using SessionSight.Agents.Routing;

namespace SessionSight.Agents.Services;

/// <summary>
/// Service for generating text embeddings using Azure OpenAI.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates a vector embedding for the given text.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A 3072-dimensional float array (text-embedding-3-large).</returns>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
}

/// <summary>
/// Azure OpenAI implementation of embedding generation.
/// Uses text-embedding-3-large model (3072 dimensions).
/// </summary>
public partial class EmbeddingService : IEmbeddingService
{
    private readonly EmbeddingClient _client;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly string _modelUsed;

    public EmbeddingService(
        IAIFoundryClientFactory factory,
        IModelRouter router,
        ILogger<EmbeddingService> logger)
    {
        _modelUsed = router.SelectModel(ModelTask.Embedding);
        _client = factory.CreateEmbeddingClient(_modelUsed);
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            LogEmptyTextSkipped(_logger);
            return Array.Empty<float>();
        }

        LogGeneratingEmbedding(_logger, text.Length, _modelUsed);

        // Add timeout to prevent indefinite hangs
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            var response = await _client.GenerateEmbeddingAsync(text, cancellationToken: cts.Token);
            var vector = response.Value.ToFloats().ToArray();

            LogEmbeddingGenerated(_logger, vector.Length);

            return vector;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            LogEmbeddingTimeout(_logger, _modelUsed);
            throw new TimeoutException($"Embedding generation timed out after 30 seconds using model {_modelUsed}");
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generating embedding for text of length {Length} using {Model}")]
    private static partial void LogGeneratingEmbedding(ILogger logger, int length, string model);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Embedding generated with {Dimensions} dimensions")]
    private static partial void LogEmbeddingGenerated(ILogger logger, int dimensions);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Empty text provided, skipping embedding generation")]
    private static partial void LogEmptyTextSkipped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Embedding generation timed out using model {Model}")]
    private static partial void LogEmbeddingTimeout(ILogger logger, string model);
}
