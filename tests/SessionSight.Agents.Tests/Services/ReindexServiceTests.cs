using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SessionSight.Agents.Models;
using SessionSight.Agents.Services;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using SessionSight.Core.Schema;
using AgentExtractionResult = SessionSight.Agents.Models.ExtractionResult;
using CoreEntities = SessionSight.Core.Entities;

namespace SessionSight.Agents.Tests.Services;

public class ReindexServiceTests
{
    private readonly ISessionIndexingService _indexingService;
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<ReindexService> _logger;
    private readonly ReindexService _service;

    public ReindexServiceTests()
    {
        _indexingService = Substitute.For<ISessionIndexingService>();
        _documentRepository = Substitute.For<IDocumentRepository>();
        _logger = Substitute.For<ILogger<ReindexService>>();
        _service = new ReindexService(_indexingService, _documentRepository, _logger);
    }

    [Fact]
    public async Task ReindexSessionsAsync_NoExtraction_Skipped()
    {
        var session = CreateSession(hasExtraction: false);

        var result = await _service.ReindexSessionsAsync([session]);

        result.Should().Be(new ReindexResult(0, 0, 1));
        await _indexingService.DidNotReceive()
            .IndexSessionAsync(Arg.Any<CoreEntities.Session>(), Arg.Any<AgentExtractionResult>(),
                Arg.Any<SessionSummary?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReindexSessionsAsync_SuccessfulReindex_IndexedAndStatusUpdated()
    {
        var session = CreateSession(hasExtraction: true);

        var result = await _service.ReindexSessionsAsync([session]);

        result.Should().Be(new ReindexResult(1, 0, 0));
        await _indexingService.Received(1)
            .IndexSessionAsync(session, Arg.Any<AgentExtractionResult>(),
                Arg.Any<SessionSummary?>(), Arg.Any<CancellationToken>());
        await _documentRepository.Received(1)
            .UpdateDocumentStatusAsync(session.Id, DocumentStatus.Completed,
                indexingStatus: IndexingStatus.Indexed, ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReindexSessionsAsync_IndexingFails_StatusSetToFailed()
    {
        var session = CreateSession(hasExtraction: true);
        _indexingService.IndexSessionAsync(Arg.Any<CoreEntities.Session>(), Arg.Any<AgentExtractionResult>(),
                Arg.Any<SessionSummary?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Search index down"));

        var result = await _service.ReindexSessionsAsync([session]);

        result.Should().Be(new ReindexResult(0, 1, 0));
        await _documentRepository.Received(1)
            .UpdateDocumentStatusAsync(session.Id, DocumentStatus.Completed,
                indexingStatus: IndexingStatus.Failed, ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReindexSessionsAsync_MixedSessions_CorrectCounts()
    {
        var noExtraction = CreateSession(hasExtraction: false);
        var success = CreateSession(hasExtraction: true);
        var failure = CreateSession(hasExtraction: true);

        _indexingService.IndexSessionAsync(failure, Arg.Any<AgentExtractionResult>(),
                Arg.Any<SessionSummary?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("fail"));

        var result = await _service.ReindexSessionsAsync([noExtraction, success, failure]);

        result.Should().Be(new ReindexResult(1, 1, 1));
    }

    [Fact]
    public async Task ReindexSessionsAsync_WithSummaryJson_DeserializedAndPassed()
    {
        var summary = new SessionSummary
        {
            SessionId = Guid.NewGuid(),
            OneLiner = "Test summary",
            KeyPoints = "Key point 1"
        };
        var summaryJson = JsonSerializer.Serialize(summary, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var session = CreateSession(hasExtraction: true, summaryJson: summaryJson);

        await _service.ReindexSessionsAsync([session]);

        await _indexingService.Received(1)
            .IndexSessionAsync(session, Arg.Any<AgentExtractionResult>(),
                Arg.Is<SessionSummary?>(s => s != null && s.OneLiner == "Test summary"),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReindexSessionsAsync_Cancellation_Propagates()
    {
        var session = CreateSession(hasExtraction: true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _service.ReindexSessionsAsync([session], cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReindexSessionsAsync_FailedStatusUpdateAfterIndexFailure_ContinuesProcessing()
    {
        var failSession = CreateSession(hasExtraction: true);
        var successSession = CreateSession(hasExtraction: true);

        _indexingService.IndexSessionAsync(failSession, Arg.Any<AgentExtractionResult>(),
                Arg.Any<SessionSummary?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("index fail"));
        _documentRepository.UpdateDocumentStatusAsync(failSession.Id, Arg.Any<DocumentStatus>(),
                indexingStatus: IndexingStatus.Failed, ct: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("status update fail"));

        var result = await _service.ReindexSessionsAsync([failSession, successSession]);

        result.Should().Be(new ReindexResult(1, 1, 0));
    }

    private static CoreEntities.Session CreateSession(bool hasExtraction, string? summaryJson = null)
    {
        var sessionId = Guid.NewGuid();
        return new CoreEntities.Session
        {
            Id = sessionId,
            PatientId = Guid.NewGuid(),
            Document = new CoreEntities.SessionDocument
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Status = DocumentStatus.Completed,
                IndexingStatus = IndexingStatus.Failed
            },
            Extraction = hasExtraction
                ? new CoreEntities.ExtractionResult
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    Data = new ClinicalExtraction(),
                    OverallConfidence = 0.9,
                    RequiresReview = false,
                    SummaryJson = summaryJson
                }
                : null
        };
    }
}
