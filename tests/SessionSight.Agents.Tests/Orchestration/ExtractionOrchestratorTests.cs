using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;
using SessionSight.Agents.Orchestration;
using SessionSight.Agents.Services;
using SessionSight.Core.Enums;
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
    private readonly IDocumentStorage _documentStorage;
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
        _documentStorage = Substitute.For<IDocumentStorage>();
        _logger = Substitute.For<ILogger<ExtractionOrchestrator>>();

        // Default: summarizer returns a valid summary
        _summarizer.SummarizeSessionAsync(Arg.Any<AgentExtractionResult>(), Arg.Any<CancellationToken>())
            .Returns(new SessionSummary { OneLiner = "Test summary", ModelUsed = "gpt-4o-mini" });

        _orchestrator = new ExtractionOrchestrator(
            _documentParser,
            _intakeAgent,
            _extractorAgent,
            _riskAssessor,
            _summarizer,
            _sessionRepository,
            _documentStorage,
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
        // Verify document status was updated to Processing then Failed
        await _sessionRepository.Received().UpdateDocumentStatusAsync(sessionId, DocumentStatus.Processing, null);
        await _sessionRepository.Received().UpdateDocumentStatusAsync(sessionId, DocumentStatus.Failed, null);
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
        // Verify document status was updated to Processing then Failed
        await _sessionRepository.Received().UpdateDocumentStatusAsync(sessionId, DocumentStatus.Processing, null);
        await _sessionRepository.Received().UpdateDocumentStatusAsync(sessionId, DocumentStatus.Failed, null);
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
        await _sessionRepository.Received().UpdateDocumentStatusAsync(sessionId, DocumentStatus.Processing, null);
        // Extraction result saved
        await _sessionRepository.Received().SaveExtractionResultAsync(Arg.Any<CoreEntities.ExtractionResult>());
        // Completed status set with extracted text
        await _sessionRepository.Received().UpdateDocumentStatusAsync(sessionId, DocumentStatus.Completed, Arg.Any<string>());
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
}
