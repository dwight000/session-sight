using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SessionSight.Agents.Orchestration;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;

namespace SessionSight.Agents.Services;

public record ExtractionJob(Guid SessionId, string? JobKey = null);

public partial class ExtractionJobDispatcher : BackgroundService, IExtractionJobDispatcher
{
    private const int MaxConcurrency = 3;
    private const int QueueCapacity = 20;

    private readonly Channel<ExtractionJob> _channel = Channel.CreateBounded<ExtractionJob>(
        new BoundedChannelOptions(QueueCapacity) { FullMode = BoundedChannelFullMode.Wait });

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExtractionJobDispatcher> _logger;

    public ExtractionJobDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<ExtractionJobDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public ValueTask EnqueueAsync(Guid sessionId, string? jobKey = null)
    {
        var job = new ExtractionJob(sessionId, jobKey);
        return _channel.Writer.TryWrite(job)
            ? ValueTask.CompletedTask
            : _channel.Writer.WriteAsync(job);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = Enumerable.Range(0, MaxConcurrency)
            .Select(_ => ProcessJobsAsync(stoppingToken));
        await Task.WhenAll(workers);
    }

    private async Task ProcessJobsAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessSingleJobAsync(job, stoppingToken);
        }
    }

    private async Task ProcessSingleJobAsync(ExtractionJob job, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IExtractionOrchestrator>();
            LogStartingExtraction(job.SessionId);

            var result = await orchestrator.ProcessSessionAsync(job.SessionId, stoppingToken);

            if (!result.Success)
                LogExtractionFailed(job.SessionId, result.ErrorMessage);
            else
                LogExtractionCompleted(job.SessionId);

            if (job.JobKey is not null)
            {
                var successStatus = result.IsPartiallyCompleted
                    ? JobStatus.PartiallyCompleted
                    : JobStatus.Completed;
                var jobStatus = result.Success ? successStatus : JobStatus.Failed;
                var jobRepo = scope.ServiceProvider.GetRequiredService<IProcessingJobRepository>();
                await jobRepo.UpdateStatusAsync(job.JobKey, jobStatus);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            LogShutdownCancelled(job.SessionId);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var docRepo = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                await docRepo.UpdateDocumentStatusAsync(job.SessionId, DocumentStatus.Failed,
                    failureKind: FailureKind.Transient,
                    errorMessage: "Server shutting down — retry automatically",
                    ct: CancellationToken.None); // Intentionally not propagating — best effort during shutdown
            }
            catch
            {
                // Best effort during shutdown
            }
        }
        catch (Exception ex)
        {
            LogExtractionCrashed(ex, job.SessionId);
            if (job.JobKey is not null)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var jobRepo = scope.ServiceProvider.GetRequiredService<IProcessingJobRepository>();
                    await jobRepo.UpdateStatusAsync(job.JobKey, JobStatus.Failed);
                }
                catch (Exception updateEx)
                {
                    LogJobStatusUpdateFailed(updateEx, job.JobKey);
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting extraction for session {SessionId}")]
    private partial void LogStartingExtraction(Guid sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Extraction completed for session {SessionId}")]
    private partial void LogExtractionCompleted(Guid sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Extraction failed for session {SessionId}: {Error}")]
    private partial void LogExtractionFailed(Guid sessionId, string? error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Extraction cancelled during shutdown for session {SessionId}")]
    private partial void LogShutdownCancelled(Guid sessionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Extraction crashed for session {SessionId}")]
    private partial void LogExtractionCrashed(Exception exception, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to update job status for key {JobKey}")]
    private partial void LogJobStatusUpdateFailed(Exception exception, string jobKey);
}
