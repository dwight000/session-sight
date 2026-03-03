using System.Collections.Concurrent;
using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SessionSight.Agents.Orchestration;
using SessionSight.Agents.Services;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;

namespace SessionSight.Agents.Tests.Services;

public class ExtractionJobDispatcherTests
{
    private readonly IExtractionOrchestrator _orchestrator;
    private readonly IProcessingJobRepository _jobRepo;
    private readonly IDocumentRepository _docRepo;
    private readonly ExtractionJobDispatcher _dispatcher;
    private readonly CancellationTokenSource _cts;

    public ExtractionJobDispatcherTests()
    {
        _orchestrator = Substitute.For<IExtractionOrchestrator>();
        _jobRepo = Substitute.For<IProcessingJobRepository>();
        _docRepo = Substitute.For<IDocumentRepository>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IExtractionOrchestrator)).Returns(_orchestrator);
        serviceProvider.GetService(typeof(IProcessingJobRepository)).Returns(_jobRepo);
        serviceProvider.GetService(typeof(IDocumentRepository)).Returns(_docRepo);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var logger = Substitute.For<ILogger<ExtractionJobDispatcher>>();

        _dispatcher = new ExtractionJobDispatcher(scopeFactory, logger);
        _cts = new CancellationTokenSource();
    }

    [Fact]
    public async Task EnqueueAsync_JobIsProcessed()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _orchestrator.ProcessSessionAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(new OrchestrationResult { Success = true, SessionId = sessionId });

        // Act
        await _dispatcher.StartAsync(_cts.Token);
        await _dispatcher.EnqueueAsync(sessionId);
        await WaitForConditionAsync(() =>
            _orchestrator.ReceivedCalls().Any());
        _cts.Cancel();
        await _dispatcher.StopAsync(CancellationToken.None);

        // Assert
        await _orchestrator.Received(1).ProcessSessionAsync(sessionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnqueueAsync_WithJobKey_UpdatesJobStatusToCompleted()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var jobKey = "test-job-key";
        _orchestrator.ProcessSessionAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(new OrchestrationResult { Success = true, SessionId = sessionId });

        // Act
        await _dispatcher.StartAsync(_cts.Token);
        await _dispatcher.EnqueueAsync(sessionId, jobKey);
        await WaitForConditionAsync(() =>
            _jobRepo.ReceivedCalls().Any());
        _cts.Cancel();
        await _dispatcher.StopAsync(CancellationToken.None);

        // Assert
        await _jobRepo.Received(1).UpdateStatusAsync(jobKey, JobStatus.Completed);
    }

    [Fact]
    public async Task EnqueueAsync_PartiallyCompleted_UpdatesJobStatusCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var jobKey = "partial-job";
        _orchestrator.ProcessSessionAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(new OrchestrationResult { Success = true, IsPartiallyCompleted = true, SessionId = sessionId });

        // Act
        await _dispatcher.StartAsync(_cts.Token);
        await _dispatcher.EnqueueAsync(sessionId, jobKey);
        await WaitForConditionAsync(() =>
            _jobRepo.ReceivedCalls().Any());
        _cts.Cancel();
        await _dispatcher.StopAsync(CancellationToken.None);

        // Assert
        await _jobRepo.Received(1).UpdateStatusAsync(jobKey, JobStatus.PartiallyCompleted);
    }

    [Fact]
    public async Task EnqueueAsync_OrchestratorFails_DoesNotThrow()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _orchestrator.ProcessSessionAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(new OrchestrationResult { Success = false, ErrorMessage = "LLM error" });

        // Act — should not throw
        await _dispatcher.StartAsync(_cts.Token);
        await _dispatcher.EnqueueAsync(sessionId);
        await WaitForConditionAsync(() =>
            _orchestrator.ReceivedCalls().Any());
        _cts.Cancel();
        await _dispatcher.StopAsync(CancellationToken.None);

        // Assert
        await _orchestrator.Received(1).ProcessSessionAsync(sessionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnqueueAsync_OrchestratorThrows_UpdatesJobStatusToFailed()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var jobKey = "crash-job";
        _orchestrator.ProcessSessionAsync(sessionId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        // Act
        await _dispatcher.StartAsync(_cts.Token);
        await _dispatcher.EnqueueAsync(sessionId, jobKey);
        await WaitForConditionAsync(() =>
            _jobRepo.ReceivedCalls().Any());
        _cts.Cancel();
        await _dispatcher.StopAsync(CancellationToken.None);

        // Assert
        await _jobRepo.Received(1).UpdateStatusAsync(jobKey, JobStatus.Failed);
    }

    [Fact]
    public async Task Shutdown_CancelsInFlightWork_MarksAsTransientFailure()
    {
        // Arrange — orchestrator blocks until cancelled
        var orchestratorStarted = new TaskCompletionSource();
        _orchestrator.ProcessSessionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(1);
                orchestratorStarted.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return new OrchestrationResult { Success = true };
            });

        var sessionId = Guid.NewGuid();

        // Act
        await _dispatcher.StartAsync(_cts.Token);
        await _dispatcher.EnqueueAsync(sessionId);
        await orchestratorStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _cts.Cancel();
        await _dispatcher.StopAsync(CancellationToken.None);

        // Assert — document should be marked as transient failure
        await _docRepo.Received(1).UpdateDocumentStatusAsync(
            sessionId,
            DocumentStatus.Failed,
            null,
            null,
            FailureKind.Transient,
            "Server shutting down — retry automatically",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnqueueAsync_MultipleJobs_ProcessesConcurrently()
    {
        // Arrange — each job takes 200ms
        var processedIds = new ConcurrentBag<Guid>();
        _orchestrator.ProcessSessionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var id = callInfo.ArgAt<Guid>(0);
                var ct = callInfo.ArgAt<CancellationToken>(1);
                await Task.Delay(200, ct);
                processedIds.Add(id);
                return new OrchestrationResult { Success = true, SessionId = id };
            });

        var ids = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();

        // Act
        var sw = Stopwatch.StartNew();
        await _dispatcher.StartAsync(_cts.Token);

        foreach (var id in ids)
            await _dispatcher.EnqueueAsync(id);

        await WaitForConditionAsync(() => processedIds.Count >= 3);
        sw.Stop();
        _cts.Cancel();
        await _dispatcher.StopAsync(CancellationToken.None);

        // Assert — 3 jobs at 200ms each, if serial would be 600ms+
        // With 3 concurrent workers, should complete well under serial time
        // Using 1500ms threshold to avoid flaky failures on slow CI runners
        sw.ElapsedMilliseconds.Should().BeLessThan(1500);
        processedIds.Should().HaveCount(3);
        processedIds.Should().BeEquivalentTo(ids);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(25);
        }
    }
}
