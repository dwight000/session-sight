using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SessionSight.Infrastructure.Search;

/// <summary>
/// Background service that ensures the search index exists on application startup.
/// Errors are logged but don't prevent app startup (graceful degradation).
/// </summary>
public partial class SearchIndexInitializer : IHostedService
{
    private readonly ISearchIndexService _searchIndexService;
    private readonly ILogger<SearchIndexInitializer> _logger;

    public SearchIndexInitializer(
        ISearchIndexService searchIndexService,
        ILogger<SearchIndexInitializer> logger)
    {
        _searchIndexService = searchIndexService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            LogInitializing(_logger);
            await _searchIndexService.EnsureIndexExistsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Log error but don't prevent app startup - search is non-critical
            LogInitializationFailed(_logger, ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing search index...")]
    private static partial void LogInitializing(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to initialize search index. Search functionality may be unavailable.")]
    private static partial void LogInitializationFailed(ILogger logger, Exception ex);
}
