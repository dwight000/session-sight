using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SessionSight.Agents.Models;
using SessionSight.Agents.Services;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Schema;
using SessionSight.Infrastructure.Search;
using AgentExtractionResult = SessionSight.Agents.Models.ExtractionResult;

namespace SessionSight.Agents.Tests.Services;

public class SessionIndexingServiceTests
{
    private readonly IEmbeddingService _embeddingService;
    private readonly ISearchIndexService _searchIndexService;
    private readonly ILogger<SessionIndexingService> _logger;
    private readonly SessionIndexingService _service;

    public SessionIndexingServiceTests()
    {
        _embeddingService = Substitute.For<IEmbeddingService>();
        _searchIndexService = Substitute.For<ISearchIndexService>();
        _logger = Substitute.For<ILogger<SessionIndexingService>>();

        _service = new SessionIndexingService(
            _embeddingService,
            _searchIndexService,
            _logger);
    }

    [Fact]
    public async Task IndexSessionAsync_ValidData_CallsSearchIndexService()
    {
        // Arrange
        var session = CreateTestSession();
        var extraction = CreateTestExtractionResult();
        var summary = new SessionSummary
        {
            KeyPoints = "Patient discussed anxiety management strategies",
            ModelUsed = "gpt-4o-mini"
        };

        var expectedVector = new float[3072];
        Array.Fill(expectedVector, 0.1f);
        _embeddingService.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expectedVector);

        // Act
        await _service.IndexSessionAsync(session, extraction, summary);

        // Assert
        await _searchIndexService.Received(1)
            .IndexDocumentAsync(Arg.Is<SessionSearchDocument>(doc =>
                doc.SessionId == session.Id.ToString() &&
                doc.PatientId == session.PatientId.ToString() &&
                doc.ContentVector != null &&
                doc.ContentVector.Count == 3072),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexSessionAsync_NullSummary_StillIndexes()
    {
        // Arrange
        var session = CreateTestSession();
        var extraction = CreateTestExtractionResult();

        var expectedVector = new float[3072];
        Array.Fill(expectedVector, 0.1f);
        _embeddingService.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expectedVector);

        // Act
        await _service.IndexSessionAsync(session, extraction, null);

        // Assert
        await _searchIndexService.Received(1)
            .IndexDocumentAsync(Arg.Any<SessionSearchDocument>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexSessionAsync_EmptyEmbedding_SkipsVectorField()
    {
        // Arrange
        var session = CreateTestSession();
        var extraction = CreateTestExtractionResult();

        _embeddingService.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<float>());

        // Act
        await _service.IndexSessionAsync(session, extraction, null);

        // Assert
        await _searchIndexService.Received(1)
            .IndexDocumentAsync(Arg.Is<SessionSearchDocument>(doc => doc.ContentVector == null),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexSessionAsync_ComposesEmbeddingTextFromExtraction()
    {
        // Arrange
        var session = CreateTestSession();
        var extraction = CreateTestExtractionResult();
        extraction.Data.PresentingConcerns.PrimaryConcern = new ExtractedField<string>
        {
            Value = "Anxiety about work"
        };
        extraction.Data.MoodAssessment.SelfReportedMood = new ExtractedField<int>
        {
            Value = 6
        };

        var capturedText = string.Empty;
        _embeddingService.GenerateEmbeddingAsync(Arg.Do<string>(t => capturedText = t), Arg.Any<CancellationToken>())
            .Returns(new float[3072]);

        // Act
        await _service.IndexSessionAsync(session, extraction, null);

        // Assert
        capturedText.Should().Contain("Concerns: Anxiety about work");
        capturedText.Should().Contain("Mood: 6/10");
    }

    [Fact]
    public async Task IndexSessionAsync_IncludesSummaryKeyPointsInEmbeddingText()
    {
        // Arrange
        var session = CreateTestSession();
        var extraction = CreateTestExtractionResult();
        var summary = new SessionSummary
        {
            KeyPoints = "Patient made progress on coping strategies",
            ModelUsed = "gpt-4o-mini"
        };

        var capturedText = string.Empty;
        _embeddingService.GenerateEmbeddingAsync(Arg.Do<string>(t => capturedText = t), Arg.Any<CancellationToken>())
            .Returns(new float[3072]);

        // Act
        await _service.IndexSessionAsync(session, extraction, summary);

        // Assert
        capturedText.Should().Contain("Summary: Patient made progress on coping strategies");
    }

    [Fact]
    public async Task IndexSessionAsync_PopulatesSearchDocumentFields()
    {
        // Arrange
        var session = CreateTestSession();
        var extraction = CreateTestExtractionResult();
        extraction.Data.Diagnoses.PrimaryDiagnosis = new ExtractedField<string>
        {
            Value = "Generalized Anxiety Disorder"
        };
        extraction.Data.Interventions.TechniquesUsed = new ExtractedField<List<TechniqueUsed>>
        {
            Value = new List<TechniqueUsed> { TechniqueUsed.Cbt, TechniqueUsed.Mindfulness }
        };

        SessionSearchDocument? capturedDoc = null;
        _embeddingService.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new float[3072]);
        _searchIndexService.IndexDocumentAsync(Arg.Do<SessionSearchDocument>(d => capturedDoc = d), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _service.IndexSessionAsync(session, extraction, null);

        // Assert
        capturedDoc.Should().NotBeNull();
        capturedDoc!.PrimaryDiagnosis.Should().Be("Generalized Anxiety Disorder");
        capturedDoc.Interventions.Should().Contain("Cbt");
        capturedDoc.Interventions.Should().Contain("Mindfulness");
    }

    private static Session CreateTestSession()
    {
        return new Session
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            SessionDate = new DateOnly(2024, 6, 15)
        };
    }

    private static AgentExtractionResult CreateTestExtractionResult()
    {
        return new AgentExtractionResult
        {
            SessionId = Guid.NewGuid().ToString(),
            Data = new ClinicalExtraction
            {
                SessionInfo = new SessionInfoExtracted
                {
                    SessionType = new ExtractedField<SessionType> { Value = SessionType.Individual }
                },
                PresentingConcerns = new PresentingConcernsExtracted
                {
                    PrimaryConcern = new ExtractedField<string> { Value = "Test concern" }
                },
                MoodAssessment = new MoodAssessmentExtracted
                {
                    SelfReportedMood = new ExtractedField<int> { Value = 5 }
                },
                RiskAssessment = new RiskAssessmentExtracted
                {
                    RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low }
                },
                Interventions = new InterventionsExtracted
                {
                    TechniquesUsed = new ExtractedField<List<TechniqueUsed>> { Value = new List<TechniqueUsed>() }
                },
                Diagnoses = new DiagnosesExtracted(),
                TreatmentProgress = new TreatmentProgressExtracted
                {
                    ProgressRatingOverall = new ExtractedField<ProgressRatingOverall> { Value = ProgressRatingOverall.Stable }
                }
            }
        };
    }
}
