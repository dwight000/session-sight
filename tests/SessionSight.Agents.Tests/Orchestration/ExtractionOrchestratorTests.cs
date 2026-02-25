using System.Text.Json;
using Azure;
using Azure.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;
using SessionSight.Agents.Orchestration;
using SessionSight.Agents.Services;
using SessionSight.Core.Enums;
using SessionSight.Core.Exceptions;
using SessionSight.Core.Interfaces;
using SessionSight.Core.Schema;
using AgentModels = SessionSight.Agents.Models;
using AgentExtractionResult = SessionSight.Agents.Models.ExtractionResult;
using CoreEntities = SessionSight.Core.Entities;

namespace SessionSight.Agents.Tests.Orchestration;

public class ExtractionOrchestratorTests
{
    private readonly IDocumentParser _documentParser;
    private readonly IIntakeAgent _intakeAgent;
    private readonly IClinicalExtractorAgent _extractorAgent;
    private readonly IRiskAssessorAgent _riskAssessor;
    private readonly ISummarizerAgent _summarizer;
    private readonly ISessionRepository _sessionRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IExtractionResultRepository _extractionResultRepository;
    private readonly IExtractionStepRepository _stepRepository;
    private readonly IDocumentStorage _documentStorage;
    private readonly ISessionIndexingService _sessionIndexingService;
    private readonly ILogger<ExtractionOrchestrator> _logger;
    private readonly ExtractionOrchestrator _orchestrator;

    public ExtractionOrchestratorTests()
    {
        _documentParser = Substitute.For<IDocumentParser>();
        _intakeAgent = Substitute.For<IIntakeAgent>();
        _extractorAgent = Substitute.For<IClinicalExtractorAgent>();
        _riskAssessor = Substitute.For<IRiskAssessorAgent>();
        _summarizer = Substitute.For<ISummarizerAgent>();
        _sessionRepository = Substitute.For<ISessionRepository>();
        _documentRepository = Substitute.For<IDocumentRepository>();
        _extractionResultRepository = Substitute.For<IExtractionResultRepository>();
        _stepRepository = Substitute.For<IExtractionStepRepository>();
        _documentStorage = Substitute.For<IDocumentStorage>();
        _sessionIndexingService = Substitute.For<ISessionIndexingService>();
        _logger = Substitute.For<ILogger<ExtractionOrchestrator>>();

        // Default: summarizer returns a valid summary
        _summarizer.SummarizeSessionAsync(Arg.Any<AgentExtractionResult>(), Arg.Any<CancellationToken>())
            .Returns(new SessionSummary { OneLiner = "Test summary", ModelUsed = "gpt-4o-mini" });

        // Default: atomic transition succeeds
        _documentRepository.TryTransitionDocumentStatusAsync(
            Arg.Any<Guid>(), DocumentStatus.Pending, DocumentStatus.Processing)
            .Returns(true);

        var agents = new ExtractionAgents(_intakeAgent, _extractorAgent, _riskAssessor, _summarizer);
        var diagOptions = Options.Create(new PipelineDiagnosticsOptions());
        _orchestrator = new ExtractionOrchestrator(
            _documentParser,
            agents,
            _sessionRepository,
            _documentRepository,
            _extractionResultRepository,
            _stepRepository,
            _documentStorage,
            _sessionIndexingService,
            diagOptions,
            _logger);
    }

    [Fact]
    public async Task ProcessSessionAsync_SessionNotFound_ReturnsError()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _sessionRepository.GetByIdAsync(sessionId).Returns(null as CoreEntities.Session);

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task ProcessSessionAsync_NoDocument_ReturnsError()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new CoreEntities.Session { Id = sessionId, Document = null };
        _sessionRepository.GetByIdAsync(sessionId).Returns(session);

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no document");
    }

    [Fact]
    public async Task ProcessSessionAsync_TransitionFails_NotProcessing_ReturnsError()
    {
        // Arrange — status is Completed, both probes fail
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        _sessionRepository.GetByIdAsync(sessionId).Returns(session);
        _documentRepository.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Pending, DocumentStatus.Processing)
            .Returns(false);
        // Processing→Processing probe also fails (status is Completed in DB)
        _documentRepository.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Processing, DocumentStatus.Processing)
            .Returns(false);

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already in progress or completed");
    }

    [Fact]
    public async Task ProcessSessionAsync_StatusAlreadyProcessing_Proceeds()
    {
        // Arrange — simulates ExtractionController retry: caller already transitioned Failed→Processing.
        // Pending→Processing fails, but Processing→Processing probe succeeds.
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        var parsedDoc = CreateTestParsedDocument();
        var intakeResult = CreateTestIntakeResult(parsedDoc);
        var extractionResult = CreateTestExtractionResult();
        var riskResult = CreateTestRiskResult();

        _sessionRepository.GetByIdAsync(sessionId).Returns(session);
        _documentRepository.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Pending, DocumentStatus.Processing)
            .Returns(false);
        // Processing→Processing probe succeeds (DB status is Processing)
        _documentRepository.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Processing, DocumentStatus.Processing)
            .Returns(true);
        _documentStorage.DownloadAsync(Arg.Any<string>()).Returns(new MemoryStream());
        _documentParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(parsedDoc);
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(intakeResult);
        _extractorAgent.ExtractAsync(Arg.Any<IntakeResult>(), Arg.Any<CancellationToken>())
            .Returns(extractionResult);
        _riskAssessor.AssessAsync(Arg.Any<ExtractionResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(riskResult);

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert — pipeline proceeds successfully
        result.Success.Should().BeTrue();
        result.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task ProcessSessionAsync_InvalidDocument_SetsFailedStatus()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        var parsedDoc = CreateTestParsedDocument();
        var intakeResult = new IntakeResult
        {
            Document = parsedDoc,
            IsValidTherapyNote = false,
            ValidationError = "Not a therapy note",
            ModelUsed = "gpt-4o-mini"
        };

        _sessionRepository.GetByIdAsync(sessionId).Returns(session);
        _documentStorage.DownloadAsync(Arg.Any<string>()).Returns(new MemoryStream());
        _documentParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(parsedDoc);
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(intakeResult);

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid document");
        // Verify document status set to Failed with FailureKind.Permanent and descriptive error
        await _documentRepository.Received().TryTransitionDocumentStatusAsync(sessionId, DocumentStatus.Pending, DocumentStatus.Processing);
        await _documentRepository.Received().UpdateDocumentStatusAsync(
            sessionId, DocumentStatus.Failed,
            Arg.Any<string?>(),
            Arg.Any<IndexingStatus?>(),
            Arg.Is<FailureKind?>(k => k == FailureKind.Permanent),
            Arg.Is<string?>(m => m != null && m.Contains("therapy session note")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessSessionAsync_FullPipeline_ReturnsSuccess()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        var parsedDoc = CreateTestParsedDocument();
        var intakeResult = CreateTestIntakeResult(parsedDoc);
        var extractionResult = CreateTestExtractionResult();
        var riskResult = CreateTestRiskResult();

        _sessionRepository.GetByIdAsync(sessionId).Returns(session);
        _documentStorage.DownloadAsync(Arg.Any<string>()).Returns(new MemoryStream());
        _documentParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(parsedDoc);
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(intakeResult);
        _extractorAgent.ExtractAsync(Arg.Any<IntakeResult>(), Arg.Any<CancellationToken>())
            .Returns(extractionResult);
        _riskAssessor.AssessAsync(Arg.Any<ExtractionResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(riskResult);

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert
        result.Success.Should().BeTrue();
        result.SessionId.Should().Be(sessionId);
        result.ExtractionId.Should().NotBeEmpty();
        result.ModelsUsed.Should().NotBeEmpty();

        // Verify pipeline was called in order
        Received.InOrder(() =>
        {
            _documentStorage.DownloadAsync(Arg.Any<string>());
            _documentParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>());
            _extractorAgent.ExtractAsync(Arg.Any<IntakeResult>(), Arg.Any<CancellationToken>());
            _riskAssessor.AssessAsync(Arg.Any<ExtractionResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task ProcessSessionAsync_RiskReviewRequired_PropagatesFlag()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        var parsedDoc = CreateTestParsedDocument();
        var intakeResult = CreateTestIntakeResult(parsedDoc);
        var extractionResult = CreateTestExtractionResult();
        var riskResult = new RiskAssessmentResult
        {
            RequiresReview = true,
            ReviewReasons = new List<string> { "Suicidal ideation detected", "High risk score" },
            FinalExtraction = new RiskAssessmentExtracted(),
            ModelUsed = "gpt-4o"
        };

        _sessionRepository.GetByIdAsync(sessionId).Returns(session);
        _documentStorage.DownloadAsync(Arg.Any<string>()).Returns(new MemoryStream());
        _documentParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(parsedDoc);
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(intakeResult);
        _extractorAgent.ExtractAsync(Arg.Any<IntakeResult>(), Arg.Any<CancellationToken>())
            .Returns(extractionResult);
        _riskAssessor.AssessAsync(Arg.Any<ExtractionResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(riskResult);

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert
        result.Success.Should().BeTrue();
        result.RequiresReview.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessSessionAsync_ExtractionFails_SetsFailedStatusAndThrows()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        var parsedDoc = CreateTestParsedDocument();
        var intakeResult = CreateTestIntakeResult(parsedDoc);

        _sessionRepository.GetByIdAsync(sessionId).Returns(session);
        _documentStorage.DownloadAsync(Arg.Any<string>()).Returns(new MemoryStream());
        _documentParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(parsedDoc);
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(intakeResult);
        _extractorAgent.ExtractAsync(Arg.Any<IntakeResult>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("LLM call failed"));

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("LLM call failed");
        // Verify document status was updated to Processing then Failed with failure classification
        await _documentRepository.Received().TryTransitionDocumentStatusAsync(sessionId, DocumentStatus.Pending, DocumentStatus.Processing);
        await _documentRepository.Received().UpdateDocumentStatusAsync(
            sessionId, DocumentStatus.Failed,
            Arg.Any<string?>(),
            Arg.Any<IndexingStatus?>(),
            Arg.Is<FailureKind?>(k => k == FailureKind.Transient),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessSessionAsync_UpdatesDocumentStatus()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        var parsedDoc = CreateTestParsedDocument();
        var intakeResult = CreateTestIntakeResult(parsedDoc);
        var extractionResult = CreateTestExtractionResult();
        var riskResult = CreateTestRiskResult();

        _sessionRepository.GetByIdAsync(sessionId).Returns(session);
        _documentStorage.DownloadAsync(Arg.Any<string>()).Returns(new MemoryStream());
        _documentParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(parsedDoc);
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(intakeResult);
        _extractorAgent.ExtractAsync(Arg.Any<IntakeResult>(), Arg.Any<CancellationToken>())
            .Returns(extractionResult);
        _riskAssessor.AssessAsync(Arg.Any<ExtractionResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(riskResult);

        // Act
        await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert - verify document status updates and extraction save
        // Processing status set first
        await _documentRepository.Received().TryTransitionDocumentStatusAsync(sessionId, DocumentStatus.Pending, DocumentStatus.Processing);
        // Extraction result upserted
        await _extractionResultRepository.Received().UpsertExtractionResultAsync(Arg.Any<CoreEntities.ExtractionResult>());
        // Completed status set with extracted text and IndexingStatus
        await _documentRepository.Received().UpdateDocumentStatusAsync(
            sessionId, DocumentStatus.Completed, Arg.Any<string>(),
            Arg.Is<IndexingStatus?>(s => s == IndexingStatus.Indexed),
            Arg.Any<FailureKind?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessSessionAsync_ExtractionParseFailure_FailsPipeline()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        var parsedDoc = CreateTestParsedDocument();
        var intakeResult = CreateTestIntakeResult(parsedDoc);

        // Simulate JSON parse failure from ClinicalExtractorAgent
        var failedExtraction = new AgentExtractionResult
        {
            SessionId = Guid.NewGuid().ToString(),
            Data = new ClinicalExtraction(),
            RequiresReview = true,
            Errors = new List<string> { "Failed to parse extraction JSON from agent response" },
            ModelsUsed = new List<string> { "gpt-4o" }
        };

        _sessionRepository.GetByIdAsync(sessionId).Returns(session);
        _documentStorage.DownloadAsync(Arg.Any<string>()).Returns(new MemoryStream());
        _documentParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(parsedDoc);
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(intakeResult);
        _extractorAgent.ExtractAsync(Arg.Any<IntakeResult>(), Arg.Any<CancellationToken>())
            .Returns(failedExtraction);

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert — pipeline fails, status set to Failed via outer catch
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to parse extraction JSON");
        await _documentRepository.Received().UpdateDocumentStatusAsync(
            sessionId, DocumentStatus.Failed,
            Arg.Any<string?>(),
            Arg.Any<IndexingStatus?>(),
            Arg.Is<FailureKind?>(fk => fk == FailureKind.Transient),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        // Risk assessor should NOT run — empty extraction with defaulted risk fields is a safety risk
        await _riskAssessor.DidNotReceive().AssessAsync(
            Arg.Any<ExtractionResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessSessionAsync_SummarizeFailure_SetsPartiallyCompleted()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        var parsedDoc = CreateTestParsedDocument();
        var intakeResult = CreateTestIntakeResult(parsedDoc);
        var extractionResult = CreateTestExtractionResult();
        var riskResult = CreateTestRiskResult();

        _sessionRepository.GetByIdAsync(sessionId).Returns(session);
        _documentStorage.DownloadAsync(Arg.Any<string>()).Returns(new MemoryStream());
        _documentParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(parsedDoc);
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(intakeResult);
        _extractorAgent.ExtractAsync(Arg.Any<IntakeResult>(), Arg.Any<CancellationToken>())
            .Returns(extractionResult);
        _riskAssessor.AssessAsync(Arg.Any<AgentExtractionResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(riskResult);
        // Summarizer throws
        _summarizer.SummarizeSessionAsync(Arg.Any<AgentExtractionResult>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Content filter blocked"));

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert — pipeline succeeds but is partially completed
        result.Success.Should().BeTrue();
        result.IsPartiallyCompleted.Should().BeTrue();
        await _documentRepository.Received().UpdateDocumentStatusAsync(
            sessionId, DocumentStatus.PartiallyCompleted,
            Arg.Any<string?>(),
            Arg.Any<IndexingStatus?>(),
            Arg.Any<FailureKind?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessSessionAsync_IndexingFailure_SetsPartiallyCompleted()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        var parsedDoc = CreateTestParsedDocument();
        var intakeResult = CreateTestIntakeResult(parsedDoc);
        var extractionResult = CreateTestExtractionResult();
        var riskResult = CreateTestRiskResult();

        _sessionRepository.GetByIdAsync(sessionId).Returns(session);
        _documentStorage.DownloadAsync(Arg.Any<string>()).Returns(new MemoryStream());
        _documentParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(parsedDoc);
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(intakeResult);
        _extractorAgent.ExtractAsync(Arg.Any<IntakeResult>(), Arg.Any<CancellationToken>())
            .Returns(extractionResult);
        _riskAssessor.AssessAsync(Arg.Any<AgentExtractionResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(riskResult);
        // Indexing throws
        _sessionIndexingService.IndexSessionAsync(
            Arg.Any<CoreEntities.Session>(), Arg.Any<AgentExtractionResult>(),
            Arg.Any<SessionSummary?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("Embedding timed out"));

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert — pipeline succeeds but is partially completed with IndexingStatus.Failed
        result.Success.Should().BeTrue();
        result.IsPartiallyCompleted.Should().BeTrue();
        await _documentRepository.Received().UpdateDocumentStatusAsync(
            sessionId, DocumentStatus.PartiallyCompleted,
            Arg.Any<string?>(),
            Arg.Is<IndexingStatus?>(s => s == IndexingStatus.Failed),
            Arg.Any<FailureKind?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessSessionAsync_FullSuccess_SetsIndexedStatus()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        var parsedDoc = CreateTestParsedDocument();
        var intakeResult = CreateTestIntakeResult(parsedDoc);
        var extractionResult = CreateTestExtractionResult();
        var riskResult = CreateTestRiskResult();

        _sessionRepository.GetByIdAsync(sessionId).Returns(session);
        _documentStorage.DownloadAsync(Arg.Any<string>()).Returns(new MemoryStream());
        _documentParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(parsedDoc);
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(intakeResult);
        _extractorAgent.ExtractAsync(Arg.Any<IntakeResult>(), Arg.Any<CancellationToken>())
            .Returns(extractionResult);
        _riskAssessor.AssessAsync(Arg.Any<AgentExtractionResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(riskResult);

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert — full success sets Completed + Indexed
        result.Success.Should().BeTrue();
        result.IsPartiallyCompleted.Should().BeFalse();
        await _documentRepository.Received().UpdateDocumentStatusAsync(
            sessionId, DocumentStatus.Completed,
            Arg.Any<string?>(),
            Arg.Is<IndexingStatus?>(s => s == IndexingStatus.Indexed),
            Arg.Any<FailureKind?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    // Permanent: therapy note validation
    [InlineData("not a therapy note", FailureKind.Permanent)]
    [InlineData("Invalid document type", FailureKind.Permanent)]
    // Permanent: corrupt/unreadable
    [InlineData("could not be read", FailureKind.Permanent)]
    [InlineData("could not be parsed", FailureKind.Permanent)]
    [InlineData("corrupt PDF file", FailureKind.Permanent)]
    [InlineData("unreadable document", FailureKind.Permanent)]
    // Permanent: blob not found
    [InlineData("BlobNotFound", FailureKind.Permanent)]
    [InlineData("blob does not exist", FailureKind.Permanent)]
    [InlineData("404 Not Found for blob resource", FailureKind.Permanent)]
    // Transient: rate limit
    [InlineData("429 Too Many Requests", FailureKind.Transient)]
    [InlineData("rate limit exceeded", FailureKind.Transient)]
    [InlineData("TooManyRequests", FailureKind.Transient)]
    // Transient: content filter
    [InlineData("content filter blocked", FailureKind.Transient)]
    [InlineData("content_filter response", FailureKind.Transient)]
    // Transient: circuit breaker
    [InlineData("circuit breaker open", FailureKind.Transient)]
    // Transient: JSON parse
    [InlineData("Failed to parse extraction JSON", FailureKind.Transient)]
    [InlineData("Failed to parse response JSON data", FailureKind.Transient)]
    // Transient: server errors
    [InlineData("Internal Server Error", FailureKind.Transient)]
    [InlineData("502 Bad Gateway", FailureKind.Transient)]
    [InlineData("503 Service Unavailable", FailureKind.Transient)]
    [InlineData("504 Gateway Timeout", FailureKind.Transient)]
    // Transient: unknown
    [InlineData("Something unexpected happened", FailureKind.Transient)]
    public void ClassifyFailure_CategorizesCorrectly(string message, FailureKind expectedKind)
    {
        var ex = new InvalidOperationException(message);
        var (kind, _) = ExtractionOrchestrator.ClassifyFailure(ex);
        kind.Should().Be(expectedKind);
    }

    [Fact]
    public void ClassifyFailure_Timeout_IsTransient()
    {
        var ex = new TimeoutException("Operation timed out");
        var (kind, errorMessage) = ExtractionOrchestrator.ClassifyFailure(ex);
        kind.Should().Be(FailureKind.Transient);
        errorMessage.Should().Contain("timed out");
    }

    [Fact]
    public void ClassifyFailure_HttpRequestException_IsTransient()
    {
        var ex = new HttpRequestException("Connection refused");
        var (kind, errorMessage) = ExtractionOrchestrator.ClassifyFailure(ex);
        kind.Should().Be(FailureKind.Transient);
        errorMessage.Should().Contain("temporarily unavailable");
    }

    [Fact]
    public void ClassifyFailure_PermanentFailure_HasDescriptiveMessage()
    {
        var ex = new InvalidOperationException("This is not a therapy note");
        var (kind, errorMessage) = ExtractionOrchestrator.ClassifyFailure(ex);
        kind.Should().Be(FailureKind.Permanent);
        errorMessage.Should().Contain("therapy session note");
    }

    [Fact]
    public void ClassifyFailure_UnknownError_IsTransientWithMessage()
    {
        var ex = new InvalidOperationException("Completely unknown error XYZ");
        var (kind, errorMessage) = ExtractionOrchestrator.ClassifyFailure(ex);
        kind.Should().Be(FailureKind.Transient);
        errorMessage.Should().Contain("Unexpected error");
        errorMessage.Should().Contain("Completely unknown error XYZ");
    }

    [Fact]
    public void ClassifyFailure_OperationCanceled_IsTransient()
    {
        var ex = new OperationCanceledException("The operation was canceled");
        var (kind, errorMessage) = ExtractionOrchestrator.ClassifyFailure(ex);
        kind.Should().Be(FailureKind.Transient);
        errorMessage.Should().Contain("timed out");
    }

    [Fact]
    public void ClassifyFailure_CredentialUnavailable_IsTransient()
    {
        var ex = new InvalidOperationException("CredentialUnavailableException: No credentials available");
        var (kind, errorMessage) = ExtractionOrchestrator.ClassifyFailure(ex);
        kind.Should().Be(FailureKind.Transient);
        errorMessage.Should().Contain("Authentication error");
    }

    [Fact]
    public void ClassifyFailure_InternalServerError_IsTransient()
    {
        var ex = new InvalidOperationException("Internal Server Error from Azure");
        var (kind, errorMessage) = ExtractionOrchestrator.ClassifyFailure(ex);
        kind.Should().Be(FailureKind.Transient);
        errorMessage.Should().Contain("temporarily unavailable");
    }

    [Fact]
    public async Task ProcessSessionAsync_ExtractionFails_ClassifiesAndWritesFailureFields()
    {
        // Arrange — simulate a timeout exception that should be classified as Transient
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        var parsedDoc = CreateTestParsedDocument();
        var intakeResult = CreateTestIntakeResult(parsedDoc);

        _sessionRepository.GetByIdAsync(sessionId).Returns(session);
        _documentStorage.DownloadAsync(Arg.Any<string>()).Returns(new MemoryStream());
        _documentParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(parsedDoc);
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(intakeResult);
        _extractorAgent.ExtractAsync(Arg.Any<IntakeResult>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("Agent loop timed out"));

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert — failure is classified as Transient with timeout message
        result.Success.Should().BeFalse();
        await _documentRepository.Received().UpdateDocumentStatusAsync(
            sessionId, DocumentStatus.Failed,
            Arg.Any<string?>(),
            Arg.Any<IndexingStatus?>(),
            Arg.Is<FailureKind?>(k => k == FailureKind.Transient),
            Arg.Is<string?>(m => m != null && m.Contains("timed out")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessSessionAsync_StatusUpdateFails_DoesNotThrow()
    {
        // Arrange — extraction fails AND status update also fails
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        var parsedDoc = CreateTestParsedDocument();
        var intakeResult = CreateTestIntakeResult(parsedDoc);

        _sessionRepository.GetByIdAsync(sessionId).Returns(session);
        _documentStorage.DownloadAsync(Arg.Any<string>()).Returns(new MemoryStream());
        _documentParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(parsedDoc);
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(intakeResult);
        _extractorAgent.ExtractAsync(Arg.Any<IntakeResult>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("LLM failed"));
        // Status update also throws
        _documentRepository.UpdateDocumentStatusAsync(
            Arg.Any<Guid>(), Arg.Any<DocumentStatus>(),
            Arg.Any<string?>(), Arg.Any<IndexingStatus?>(),
            Arg.Any<FailureKind?>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB connection lost"));

        // Act — should not throw even though status update failed
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("LLM failed");
    }

    private static CoreEntities.Session CreateTestSession(Guid sessionId)
    {
        return new CoreEntities.Session
        {
            Id = sessionId,
            PatientId = Guid.NewGuid(),
            SessionDate = DateOnly.FromDateTime(DateTime.Today),
            Document = new CoreEntities.SessionDocument
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                BlobUri = "https://storage.blob.core.windows.net/docs/test.pdf",
                OriginalFileName = "test.pdf",
                Status = DocumentStatus.Pending
            }
        };
    }

    private static ParsedDocument CreateTestParsedDocument()
    {
        return new ParsedDocument
        {
            Content = "Patient discussed anxiety symptoms...",
            MarkdownContent = "# Session Note\n\nPatient discussed anxiety symptoms...",
            Metadata = new ParsedDocumentMetadata
            {
                PageCount = 2,
                FileFormat = "pdf",
                ExtractionConfidence = 0.95
            }
        };
    }

    private static IntakeResult CreateTestIntakeResult(ParsedDocument doc)
    {
        return new IntakeResult
        {
            Document = doc,
            IsValidTherapyNote = true,
            ModelUsed = "gpt-4o-mini",
            Metadata = new ExtractedMetadata
            {
                DocumentType = "Session Note",
                SessionDate = DateOnly.FromDateTime(DateTime.Today),
                Language = "en"
            }
        };
    }

    private static AgentModels.ExtractionResult CreateTestExtractionResult()
    {
        return new AgentModels.ExtractionResult
        {
            SessionId = Guid.NewGuid().ToString(),
            OverallConfidence = 0.85,
            RequiresReview = false,
            ModelsUsed = new List<string> { "gpt-4o", "gpt-4o-mini" },
            Data = new ClinicalExtraction()
        };
    }

    private static RiskAssessmentResult CreateTestRiskResult()
    {
        return new RiskAssessmentResult
        {
            RequiresReview = false,
            FinalExtraction = new RiskAssessmentExtracted(),
            ModelUsed = "gpt-4o"
        };
    }

    [Fact]
    public async Task ProcessSessionAsync_ResumeFromPartiallyCompleted_SkipsCoreSteps()
    {
        // Arrange — steps 1-4 already succeeded, step 6 (SearchIndex) failed
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        _sessionRepository.GetByIdAsync(sessionId).Returns(session);

        // Processing → Processing probe succeeds (caller already set Processing)
        _documentRepository.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Processing, DocumentStatus.Processing)
            .Returns(true);

        var existingExtraction = new CoreEntities.ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Data = new ClinicalExtraction(),
            OverallConfidence = 0.85,
            RequiresReview = false,
            SummaryJson = "{\"oneLiner\":\"Test\",\"modelUsed\":\"gpt-4o\"}",
            Steps = new List<CoreEntities.ExtractionStep>
            {
                new() { StepName = ExtractionStepName.DocumentParse, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.Intake, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.ClinicalExtract, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.RiskAssess, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.Summarize, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.SearchIndex, Status = ExtractionStepStatus.Failed },
            }
        };
        _extractionResultRepository.GetBySessionIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(existingExtraction);

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert — core steps should NOT have been called
        await _documentParser.DidNotReceive().ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _intakeAgent.DidNotReceive().ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>());
        await _extractorAgent.DidNotReceive().ExtractAsync(Arg.Any<IntakeResult>(), Arg.Any<CancellationToken>());
        await _riskAssessor.DidNotReceive().AssessAsync(Arg.Any<AgentExtractionResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        // SearchIndex should have been re-attempted
        await _sessionIndexingService.Received().IndexSessionAsync(
            Arg.Any<CoreEntities.Session>(),
            Arg.Any<AgentExtractionResult>(),
            Arg.Any<SessionSummary?>(),
            Arg.Any<CancellationToken>());

        result.Success.Should().BeTrue();
        result.ExtractionId.Should().Be(existingExtraction.Id);
    }

    [Fact]
    public async Task ProcessSessionAsync_NoExistingExtraction_DoesFullRun()
    {
        // Arrange — no prior extraction exists, should do a full run
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        var parsedDoc = CreateTestParsedDocument();
        var intakeResult = CreateTestIntakeResult(parsedDoc);
        var extractionResult = CreateTestExtractionResult();
        var riskResult = CreateTestRiskResult();

        _sessionRepository.GetByIdAsync(sessionId).Returns(session);
        _documentStorage.DownloadAsync(Arg.Any<string>()).Returns(new MemoryStream());
        _documentParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(parsedDoc);
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(intakeResult);
        _extractorAgent.ExtractAsync(Arg.Any<IntakeResult>(), Arg.Any<CancellationToken>())
            .Returns(extractionResult);
        _riskAssessor.AssessAsync(Arg.Any<AgentExtractionResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(riskResult);

        // GetBySessionIdAsync returns null — no prior extraction
        _extractionResultRepository.GetBySessionIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns((CoreEntities.ExtractionResult?)null);

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert — full run, all agents called
        await _documentParser.Received().ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _intakeAgent.Received().ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>());
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessSessionAsync_ResumeFromSummarizeFailed_ReRunsSummarizeAndIndex()
    {
        // Arrange — steps 1-4 succeeded, step 5 (Summarize) failed, step 6 (SearchIndex) also failed
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        _sessionRepository.GetByIdAsync(sessionId).Returns(session);

        _documentRepository.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Processing, DocumentStatus.Processing)
            .Returns(true);

        var existingExtraction = new CoreEntities.ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Data = new ClinicalExtraction(),
            OverallConfidence = 0.85,
            RequiresReview = false,
            Steps = new List<CoreEntities.ExtractionStep>
            {
                new() { StepName = ExtractionStepName.DocumentParse, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.Intake, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.ClinicalExtract, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.RiskAssess, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.Summarize, Status = ExtractionStepStatus.Failed },
                new() { StepName = ExtractionStepName.SearchIndex, Status = ExtractionStepStatus.Failed },
            }
        };
        _extractionResultRepository.GetBySessionIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(existingExtraction);

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert — summarize should be re-attempted
        await _summarizer.Received().SummarizeSessionAsync(
            Arg.Any<AgentExtractionResult>(), Arg.Any<CancellationToken>());
        // Indexing should also be re-attempted
        await _sessionIndexingService.Received().IndexSessionAsync(
            Arg.Any<CoreEntities.Session>(),
            Arg.Any<AgentExtractionResult>(),
            Arg.Any<SessionSummary?>(),
            Arg.Any<CancellationToken>());
        // Summary should be persisted
        await _extractionResultRepository.Received().UpdateExtractionSummaryAsync(
            existingExtraction.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessSessionAsync_ResumeWithSummarizeThrow_ReturnsPartiallyCompleted()
    {
        // Arrange — step 5 failed, re-run also throws → PartiallyCompleted
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        _sessionRepository.GetByIdAsync(sessionId).Returns(session);

        _documentRepository.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Processing, DocumentStatus.Processing)
            .Returns(true);

        var existingExtraction = new CoreEntities.ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Data = new ClinicalExtraction(),
            OverallConfidence = 0.85,
            RequiresReview = false,
            Steps = new List<CoreEntities.ExtractionStep>
            {
                new() { StepName = ExtractionStepName.DocumentParse, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.Intake, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.ClinicalExtract, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.RiskAssess, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.Summarize, Status = ExtractionStepStatus.Failed },
                new() { StepName = ExtractionStepName.SearchIndex, Status = ExtractionStepStatus.Succeeded },
            }
        };
        _extractionResultRepository.GetBySessionIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(existingExtraction);

        // Summarizer throws on re-run
        _summarizer.SummarizeSessionAsync(Arg.Any<AgentExtractionResult>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Content filter blocked"));

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert — should be PartiallyCompleted (summarize failed, but indexing already OK)
        result.Success.Should().BeTrue();
        result.IsPartiallyCompleted.Should().BeTrue();

        await _documentRepository.Received().UpdateDocumentStatusAsync(
            sessionId, DocumentStatus.PartiallyCompleted,
            Arg.Any<string?>(), Arg.Any<IndexingStatus?>(),
            Arg.Any<FailureKind?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessSessionAsync_ResumeIndexingThrows_ReturnsPartiallyCompleted()
    {
        // Arrange — step 6 failed, re-run also throws → PartiallyCompleted
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        _sessionRepository.GetByIdAsync(sessionId).Returns(session);

        _documentRepository.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Processing, DocumentStatus.Processing)
            .Returns(true);

        var existingExtraction = new CoreEntities.ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Data = new ClinicalExtraction(),
            OverallConfidence = 0.85,
            RequiresReview = false,
            SummaryJson = "{\"oneLiner\":\"Test\",\"modelUsed\":\"gpt-4o\"}",
            Steps = new List<CoreEntities.ExtractionStep>
            {
                new() { StepName = ExtractionStepName.DocumentParse, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.Intake, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.ClinicalExtract, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.RiskAssess, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.Summarize, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.SearchIndex, Status = ExtractionStepStatus.Failed },
            }
        };
        _extractionResultRepository.GetBySessionIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(existingExtraction);

        // Indexing throws on re-run
        _sessionIndexingService.IndexSessionAsync(
            Arg.Any<CoreEntities.Session>(), Arg.Any<AgentExtractionResult>(),
            Arg.Any<SessionSummary?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Service temporarily unavailable"));

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert — should be PartiallyCompleted (indexing failed)
        result.Success.Should().BeTrue();
        result.IsPartiallyCompleted.Should().BeTrue();
    }

    [Theory]
    [InlineData("Document is not a therapy note", FailureKind.Permanent, "Document does not appear to be a therapy session note")]
    [InlineData("Invalid document format", FailureKind.Permanent, "Document does not appear to be a therapy session note")]
    [InlineData("File could not be read", FailureKind.Permanent, "Document could not be read or parsed")]
    [InlineData("PDF is corrupt", FailureKind.Permanent, "Document could not be read or parsed")]
    [InlineData("BlobNotFound error", FailureKind.Permanent, "Source document no longer exists")]
    [InlineData("HTTP 429 TooManyRequests", FailureKind.Transient, "Service rate limit reached")]
    [InlineData("content filter triggered", FailureKind.Transient, "Content was flagged by safety filter")]
    [InlineData("circuit breaker is open", FailureKind.Transient, "Service temporarily unavailable (circuit breaker)")]
    [InlineData("Failed to parse extraction JSON", FailureKind.Transient, "Extraction produced invalid output")]
    [InlineData("Internal Server Error from Azure", FailureKind.Transient, "Service temporarily unavailable")]
    [InlineData("Something completely unexpected", FailureKind.Transient, "Unexpected error:")]
    public void ClassifyFailure_ReturnsCorrectKindAndMessage(string exceptionMessage, FailureKind expectedKind, string expectedMessageContains)
    {
        var (kind, message) = ExtractionOrchestrator.ClassifyFailure(new InvalidOperationException(exceptionMessage));

        kind.Should().Be(expectedKind);
        message.Should().Contain(expectedMessageContains);
    }

    [Fact]
    public void ClassifyFailure_TimeoutException_ReturnsTransient()
    {
        var (kind, message) = ExtractionOrchestrator.ClassifyFailure(new TimeoutException("Operation timed out"));

        kind.Should().Be(FailureKind.Transient);
        message.Should().Contain("timed out");
    }

    [Fact]
    public void ClassifyFailure_HttpRequestException_ReturnsTransient()
    {
        var (kind, message) = ExtractionOrchestrator.ClassifyFailure(new HttpRequestException("Network error"));

        kind.Should().Be(FailureKind.Transient);
        message.Should().Contain("Service temporarily unavailable");
    }

    [Fact]
    public void ClassifyFailure_RequestFailedException404_ReturnsPermanent()
    {
        var ex = new RequestFailedException(404, "Blob not found");
        var (kind, message) = ExtractionOrchestrator.ClassifyFailure(ex);

        kind.Should().Be(FailureKind.Permanent);
        message.Should().Contain("no longer exists");
    }

    [Fact]
    public void ClassifyFailure_RequestFailedException429_ReturnsTransient()
    {
        var ex = new RequestFailedException(429, "Rate limited");
        var (kind, message) = ExtractionOrchestrator.ClassifyFailure(ex);

        kind.Should().Be(FailureKind.Transient);
        message.Should().Contain("rate limit");
    }

    [Fact]
    public void ClassifyFailure_RequestFailedException500_ReturnsTransient()
    {
        var ex = new RequestFailedException(500, "Internal Server Error");
        var (kind, message) = ExtractionOrchestrator.ClassifyFailure(ex);

        kind.Should().Be(FailureKind.Transient);
        message.Should().Contain("temporarily unavailable");
    }

    [Fact]
    public void ClassifyFailure_DocumentValidationException_ReturnsPermanent()
    {
        var ex = new DocumentValidationException("Not a therapy note");
        var (kind, message) = ExtractionOrchestrator.ClassifyFailure(ex);

        kind.Should().Be(FailureKind.Permanent);
        message.Should().Contain("therapy session note");
    }

    [Fact]
    public void ClassifyFailure_CircuitBreakerOpenException_ReturnsTransient()
    {
        var ex = new CircuitBreakerOpenException("OpenAI", TimeSpan.FromSeconds(30));
        var (kind, message) = ExtractionOrchestrator.ClassifyFailure(ex);

        kind.Should().Be(FailureKind.Transient);
        message.Should().Contain("circuit breaker");
    }

    [Fact]
    public void ClassifyFailure_CredentialUnavailableException_ReturnsTransient()
    {
        var ex = new CredentialUnavailableException("No credentials available");
        var (kind, message) = ExtractionOrchestrator.ClassifyFailure(ex);

        kind.Should().Be(FailureKind.Transient);
        message.Should().Contain("Authentication error");
    }

    [Fact]
    public void ClassifyFailure_JsonException_ReturnsTransient()
    {
        var ex = new JsonException("Invalid JSON");
        var (kind, message) = ExtractionOrchestrator.ClassifyFailure(ex);

        kind.Should().Be(FailureKind.Transient);
        message.Should().Contain("invalid output");
    }

    [Fact]
    public async Task ProcessSessionAsync_DocumentParseThrows_FailsWithClassifiedError()
    {
        // Arrange — step 1 (DocumentParse) throws
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        _sessionRepository.GetByIdAsync(sessionId).Returns(session);
        _documentStorage.DownloadAsync(Arg.Any<string>()).Returns(new MemoryStream());
        _documentParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("File could not be read"));
        _extractionResultRepository.GetBySessionIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns((CoreEntities.ExtractionResult?)null);

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert
        result.Success.Should().BeFalse();
        await _documentRepository.Received().UpdateDocumentStatusAsync(
            sessionId, DocumentStatus.Failed,
            Arg.Any<string?>(), Arg.Any<IndexingStatus?>(),
            FailureKind.Permanent, Arg.Is<string?>(m => m!.Contains("could not be read")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessSessionAsync_RiskAssessThrows_FailsWithClassifiedError()
    {
        // Arrange — step 4 (RiskAssess) throws
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        var parsedDoc = CreateTestParsedDocument();
        var intakeResult = CreateTestIntakeResult(parsedDoc);
        var extractionResult = CreateTestExtractionResult();

        _sessionRepository.GetByIdAsync(sessionId).Returns(session);
        _documentStorage.DownloadAsync(Arg.Any<string>()).Returns(new MemoryStream());
        _documentParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(parsedDoc);
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(intakeResult);
        _extractorAgent.ExtractAsync(Arg.Any<IntakeResult>(), Arg.Any<CancellationToken>())
            .Returns(extractionResult);
        _riskAssessor.AssessAsync(Arg.Any<AgentExtractionResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("content filter triggered"));
        _extractionResultRepository.GetBySessionIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns((CoreEntities.ExtractionResult?)null);

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert
        result.Success.Should().BeFalse();
        await _documentRepository.Received().UpdateDocumentStatusAsync(
            sessionId, DocumentStatus.Failed,
            Arg.Any<string?>(), Arg.Any<IndexingStatus?>(),
            FailureKind.Transient, Arg.Is<string?>(m => m!.Contains("Content was flagged")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessSessionAsync_ResumeCrash_SetsFailedWithClassifiedError()
    {
        // Arrange — resume path, but final UpdateDocumentStatusAsync throws (covers outer catch)
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        _sessionRepository.GetByIdAsync(sessionId).Returns(session);

        _documentRepository.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Processing, DocumentStatus.Processing)
            .Returns(true);

        var existingExtraction = new CoreEntities.ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Data = new ClinicalExtraction(),
            OverallConfidence = 0.85,
            RequiresReview = false,
            SummaryJson = "{\"oneLiner\":\"Test\",\"modelUsed\":\"gpt-4o\"}",
            Steps = new List<CoreEntities.ExtractionStep>
            {
                new() { StepName = ExtractionStepName.DocumentParse, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.Intake, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.ClinicalExtract, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.RiskAssess, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.Summarize, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.SearchIndex, Status = ExtractionStepStatus.Succeeded },
            }
        };
        _extractionResultRepository.GetBySessionIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(existingExtraction);

        // The final UpdateDocumentStatusAsync (Completed) throws, forcing the outer catch
        _documentRepository.UpdateDocumentStatusAsync(
            sessionId, DocumentStatus.Completed,
            Arg.Any<string?>(), Arg.Any<IndexingStatus?>(),
            Arg.Any<FailureKind?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB connection lost"));

        // Act
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert — outer catch sets Failed
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("DB connection lost");
    }

    [Fact]
    public async Task ProcessSessionAsync_ResumeWithDuplicateSteps_UsesLatestStep()
    {
        // Arrange — steps 1-4 succeeded, step 5 (Summarize) has two Failed rows from prior retries
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        _sessionRepository.GetByIdAsync(sessionId).Returns(session);

        _documentRepository.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Processing, DocumentStatus.Processing)
            .Returns(true);

        var existingExtraction = new CoreEntities.ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Data = new ClinicalExtraction(),
            OverallConfidence = 0.85,
            RequiresReview = false,
            Steps = new List<CoreEntities.ExtractionStep>
            {
                new() { StepName = ExtractionStepName.DocumentParse, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.Intake, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.ClinicalExtract, Status = ExtractionStepStatus.Succeeded },
                new() { StepName = ExtractionStepName.RiskAssess, Status = ExtractionStepStatus.Succeeded },
                // Two failed Summarize steps from prior retries — should not throw ArgumentException
                new() { Id = Guid.NewGuid(), StepName = ExtractionStepName.Summarize, Status = ExtractionStepStatus.Failed, StartedAt = DateTime.UtcNow.AddHours(-2) },
                new() { Id = Guid.NewGuid(), StepName = ExtractionStepName.Summarize, Status = ExtractionStepStatus.Failed, StartedAt = DateTime.UtcNow.AddHours(-1) },
                new() { StepName = ExtractionStepName.SearchIndex, Status = ExtractionStepStatus.Failed },
            }
        };
        _extractionResultRepository.GetBySessionIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(existingExtraction);

        // Act — should NOT throw ArgumentException from ToDictionary
        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        // Assert
        result.Success.Should().BeTrue();
        await _summarizer.Received().SummarizeSessionAsync(
            Arg.Any<AgentExtractionResult>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateSessionSummaryAsync_ReturnsAndPersistsSummary()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var extractionId = Guid.NewGuid();
        var session = new CoreEntities.Session
        {
            Id = sessionId,
            Extraction = new CoreEntities.ExtractionResult
            {
                Id = extractionId,
                Data = new ClinicalExtraction(),
                OverallConfidence = 0.85,
                RequiresReview = false
            }
        };
        _sessionRepository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);

        var expectedSummary = new SessionSummary
        {
            OneLiner = "Anxiety session",
            ModelUsed = "gpt-4o-mini",
            InterventionsUsed = new List<string> { "CBT" }
        };
        _summarizer.SummarizeSessionAsync(Arg.Any<AgentExtractionResult>(), Arg.Any<CancellationToken>())
            .Returns(expectedSummary);

        // Act
        var result = await _orchestrator.GenerateSessionSummaryAsync(sessionId);

        // Assert
        result.OneLiner.Should().Be("Anxiety session");
        await _extractionResultRepository.Received().UpdateExtractionSummaryAsync(
            extractionId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateSessionSummaryAsync_NoExtraction_Throws()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _sessionRepository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(new CoreEntities.Session { Id = sessionId, Extraction = null });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _orchestrator.GenerateSessionSummaryAsync(sessionId));
    }
}
