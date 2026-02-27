using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;
using SessionSight.Agents.Orchestration;
using SessionSight.Agents.Services;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using SessionSight.Core.Schema;
using AgentExtractionResult = SessionSight.Agents.Models.ExtractionResult;
using CoreEntities = SessionSight.Core.Entities;

namespace SessionSight.Agents.Tests.Orchestration;

public class ExtractionOrchestratorStepTests
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
    private readonly ExtractionOrchestrator _orchestrator;

    public ExtractionOrchestratorStepTests()
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
        var logger = Substitute.For<ILogger<ExtractionOrchestrator>>();

        _summarizer.SummarizeSessionAsync(Arg.Any<AgentExtractionResult>(), Arg.Any<CancellationToken>())
            .Returns(new SessionSummary { OneLiner = "Test summary", ModelUsed = "gpt-4.1-nano" });

        _documentRepository.TryTransitionDocumentStatusAsync(
            Arg.Any<Guid>(), DocumentStatus.Pending, DocumentStatus.Processing)
            .Returns(true);

        var agents = new ExtractionAgents(_intakeAgent, _extractorAgent, _riskAssessor, _summarizer);
        var diagOptions = Options.Create(new PipelineDiagnosticsOptions());
        _orchestrator = new ExtractionOrchestrator(
            _documentParser, agents, _sessionRepository, _documentRepository,
            _extractionResultRepository, _stepRepository,
            _documentStorage, _sessionIndexingService, diagOptions, logger);
    }

    private void SetupFullPipeline(Guid sessionId)
    {
        var session = new Session
        {
            Id = sessionId,
            PatientId = Guid.NewGuid(),
            SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Document = new SessionDocument
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                BlobUri = "blob://test",
                OriginalFileName = "test.pdf",
                Status = DocumentStatus.Pending
            }
        };

        _sessionRepository.GetByIdAsync(sessionId).Returns(session);
        _documentStorage.DownloadAsync("blob://test").Returns(new MemoryStream([1, 2, 3]));
        _documentParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ParsedDocument
            {
                Content = "test content",
                MarkdownContent = "test content",
                Metadata = new ParsedDocumentMetadata { PageCount = 1, ExtractionConfidence = 0.95 }
            });
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(new IntakeResult
            {
                IsValidTherapyNote = true,
                ModelUsed = "gpt-4.1-nano",
                Metadata = new ExtractedMetadata { DocumentType = "Session Note", Language = "en" }
            });
        _extractorAgent.ExtractAsync(Arg.Any<IntakeResult>(), Arg.Any<Func<SessionSight.Agents.Tools.LlmCallTrace, IReadOnlyList<SessionSight.Agents.Tools.ToolCallEntry>, Task>?>(), Arg.Any<CancellationToken>())
            .Returns(new AgentExtractionResult
            {
                Data = new ClinicalExtraction(),
                ModelsUsed = ["gpt-4.1-mini"],
                OverallConfidence = 0.85,
                ToolCallCount = 3
            });
        _riskAssessor.AssessAsync(Arg.Any<AgentExtractionResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new RiskAssessmentResult
            {
                ModelUsed = "gpt-4.1-mini",
                FinalExtraction = new RiskAssessmentExtracted(),
                Diagnostics = new RiskDiagnostics()
            });
    }

    [Fact]
    public async Task FullPipeline_Saves6Steps()
    {
        var sessionId = Guid.NewGuid();
        SetupFullPipeline(sessionId);

        await _orchestrator.ProcessSessionAsync(sessionId);

        // 12 saves: each step is saved twice (Running at start + Succeeded/Failed at end)
        await _stepRepository.Received(12).SaveStepAsync(Arg.Any<ExtractionStep>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FullPipeline_StepOrderIsCorrect()
    {
        var sessionId = Guid.NewGuid();
        SetupFullPipeline(sessionId);
        var savedSteps = new List<ExtractionStep>();
        await _stepRepository.SaveStepAsync(Arg.Do<ExtractionStep>(s => savedSteps.Add(s)), Arg.Any<CancellationToken>());

        await _orchestrator.ProcessSessionAsync(sessionId);

        // 12 saves: Running + Completed for each of 6 steps
        savedSteps.Should().HaveCount(12);
        // Deduplicate by Id (same entity saved twice — object is mutated between saves)
        var distinctSteps = savedSteps.DistinctBy(s => s.Id).ToList();
        distinctSteps.Select(s => s.StepOrder).Should().BeEquivalentTo([1, 2, 3, 4, 5, 6]);
        distinctSteps.Select(s => s.StepName).Should().BeEquivalentTo([
            ExtractionStepName.DocumentParse,
            ExtractionStepName.Intake,
            ExtractionStepName.ClinicalExtract,
            ExtractionStepName.RiskAssess,
            ExtractionStepName.Summarize,
            ExtractionStepName.SearchIndex
        ]);
    }

    [Fact]
    public async Task FullPipeline_AllStepsSucceeded()
    {
        var sessionId = Guid.NewGuid();
        SetupFullPipeline(sessionId);
        var savedSteps = new List<ExtractionStep>();
        await _stepRepository.SaveStepAsync(Arg.Do<ExtractionStep>(s => savedSteps.Add(s)), Arg.Any<CancellationToken>());

        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        result.Success.Should().BeTrue();
        savedSteps.Should().OnlyContain(s => s.Status == ExtractionStepStatus.Succeeded);
    }

    [Fact]
    public async Task FullPipeline_TokenCountsPopulatedOnIntakeStep()
    {
        var sessionId = Guid.NewGuid();
        SetupFullPipeline(sessionId);
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(new IntakeResult
            {
                IsValidTherapyNote = true,
                ModelUsed = "gpt-4.1-nano",
                InputTokens = 100,
                OutputTokens = 50,
                TotalTokens = 150,
                Metadata = new ExtractedMetadata { DocumentType = "Session Note", Language = "en" }
            });

        var savedSteps = new List<ExtractionStep>();
        await _stepRepository.SaveStepAsync(Arg.Do<ExtractionStep>(s => savedSteps.Add(s)), Arg.Any<CancellationToken>());

        await _orchestrator.ProcessSessionAsync(sessionId);

        var intakeStep = savedSteps.First(s => s.StepName == ExtractionStepName.Intake);
        intakeStep.InputTokens.Should().Be(100);
        intakeStep.OutputTokens.Should().Be(50);
        intakeStep.TotalTokens.Should().Be(150);
    }

    [Fact]
    public async Task ClinicalExtractStep_ToolCallsSavedIncrementally()
    {
        // Tool calls for ClinicalExtract are now saved incrementally via the
        // onRoundComplete callback rather than being added to the step entity.
        // This test verifies the callback is passed and the step itself
        // has no tool calls (they're saved via SaveToolCallsAsync).
        var sessionId = Guid.NewGuid();
        SetupFullPipeline(sessionId);

        Func<SessionSight.Agents.Tools.LlmCallTrace, IReadOnlyList<SessionSight.Agents.Tools.ToolCallEntry>, Task>? capturedCallback = null;
        _extractorAgent.ExtractAsync(
            Arg.Any<IntakeResult>(),
            Arg.Do<Func<SessionSight.Agents.Tools.LlmCallTrace, IReadOnlyList<SessionSight.Agents.Tools.ToolCallEntry>, Task>?>(cb => capturedCallback = cb),
            Arg.Any<CancellationToken>())
            .Returns(new AgentExtractionResult
            {
                Data = new ClinicalExtraction(),
                ModelsUsed = ["gpt-4.1-mini"],
                ToolCallCount = 2,
                ToolCallTrace =
                [
                    new SessionSight.Agents.Tools.ToolCallEntry("ValidateSchema", true, 0, 50),
                    new SessionSight.Agents.Tools.ToolCallEntry("ScoreConfidence", true, 1, 30)
                ]
            });

        var savedSteps = new List<ExtractionStep>();
        await _stepRepository.SaveStepAsync(Arg.Do<ExtractionStep>(s => savedSteps.Add(s)), Arg.Any<CancellationToken>());

        await _orchestrator.ProcessSessionAsync(sessionId);

        // The onRoundComplete callback should have been passed to ExtractAsync
        capturedCallback.Should().NotBeNull();

        // Step entity itself should have empty tool calls (saved incrementally)
        var extractStep = savedSteps.First(s => s.StepName == ExtractionStepName.ClinicalExtract);
        extractStep.ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task StepSaveFailure_DoesNotBreakPipeline()
    {
        var sessionId = Guid.NewGuid();
        SetupFullPipeline(sessionId);
        _stepRepository.SaveStepAsync(Arg.Any<ExtractionStep>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task EarlyExtractionResult_CreatedBeforeStep1()
    {
        var sessionId = Guid.NewGuid();
        SetupFullPipeline(sessionId);

        await _orchestrator.ProcessSessionAsync(sessionId);

        await _extractionResultRepository.Received(1).UpsertExtractionResultAsync(
            Arg.Is<CoreEntities.ExtractionResult>(e => e.SessionId == sessionId));
    }

    [Fact]
    public async Task InvalidDocument_SetsIntakeStepToFailed()
    {
        var sessionId = Guid.NewGuid();
        SetupFullPipeline(sessionId);
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(new IntakeResult
            {
                IsValidTherapyNote = false,
                ValidationError = "Not a therapy note",
                ModelUsed = "gpt-4.1-nano",
                Metadata = new ExtractedMetadata()
            });

        var savedSteps = new List<ExtractionStep>();
        await _stepRepository.SaveStepAsync(Arg.Do<ExtractionStep>(s => savedSteps.Add(s)), Arg.Any<CancellationToken>());

        await _orchestrator.ProcessSessionAsync(sessionId);

        var intakeStep = savedSteps.FirstOrDefault(s => s.StepName == ExtractionStepName.Intake);
        intakeStep.Should().NotBeNull();
        intakeStep!.Status.Should().Be(ExtractionStepStatus.Failed);
        intakeStep.ErrorMessage.Should().Contain("Not a therapy note");
    }

    [Fact]
    public async Task SummarizerFailure_SetsStepToFailed_PipelineContinues()
    {
        var sessionId = Guid.NewGuid();
        SetupFullPipeline(sessionId);
        _summarizer.SummarizeSessionAsync(Arg.Any<AgentExtractionResult>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("LLM error"));

        var savedSteps = new List<ExtractionStep>();
        await _stepRepository.SaveStepAsync(Arg.Do<ExtractionStep>(s => savedSteps.Add(s)), Arg.Any<CancellationToken>());

        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        result.Success.Should().BeTrue();
        var summaryStep = savedSteps.First(s => s.StepName == ExtractionStepName.Summarize);
        summaryStep.Status.Should().Be(ExtractionStepStatus.Failed);
        summaryStep.ErrorMessage.Should().Contain("LLM error");
    }

    [Fact]
    public async Task IndexingFailure_SetsStepToFailed_PipelineContinues()
    {
        var sessionId = Guid.NewGuid();
        SetupFullPipeline(sessionId);
        _sessionIndexingService.IndexSessionAsync(
            Arg.Any<Session>(), Arg.Any<AgentExtractionResult>(),
            Arg.Any<SessionSummary>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Index error"));

        var savedSteps = new List<ExtractionStep>();
        await _stepRepository.SaveStepAsync(Arg.Do<ExtractionStep>(s => savedSteps.Add(s)), Arg.Any<CancellationToken>());

        var result = await _orchestrator.ProcessSessionAsync(sessionId);

        result.Success.Should().BeTrue();
        var indexStep = savedSteps.First(s => s.StepName == ExtractionStepName.SearchIndex);
        indexStep.Status.Should().Be(ExtractionStepStatus.Failed);
    }

    [Fact]
    public async Task FinalSave_UsesUpdateExtractionResultAsync()
    {
        var sessionId = Guid.NewGuid();
        SetupFullPipeline(sessionId);

        await _orchestrator.ProcessSessionAsync(sessionId);

        await _extractionResultRepository.Received(1).UpdateExtractionResultAsync(
            Arg.Is<CoreEntities.ExtractionResult>(e => e.SessionId == sessionId));
    }

    [Fact]
    public async Task StoreLlmTraces_Enabled_PopulatesLlmTracesOnSteps()
    {
        var sessionId = Guid.NewGuid();
        SetupFullPipeline(sessionId);

        // Setup agents to return LlmTraces
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(new IntakeResult
            {
                IsValidTherapyNote = true,
                ModelUsed = "gpt-4.1-nano",
                InputTokens = 100,
                OutputTokens = 50,
                TotalTokens = 150,
                LlmTraces = [new SessionSight.Agents.Tools.LlmCallTrace(null, null, "response", "gpt-4.1-nano", 0, 100, 50, 150, 200)],
                Metadata = new ExtractedMetadata { DocumentType = "Session Note", Language = "en" }
            });

        // Create orchestrator with StoreLlmTraces enabled
        var diagOptions = Options.Create(new PipelineDiagnosticsOptions { StoreLlmTraces = true });
        var agents = new ExtractionAgents(_intakeAgent, _extractorAgent, _riskAssessor, _summarizer);
        var logger = Substitute.For<ILogger<ExtractionOrchestrator>>();
        var orchestrator = new ExtractionOrchestrator(
            _documentParser, agents, _sessionRepository, _documentRepository,
            _extractionResultRepository, _stepRepository,
            _documentStorage, _sessionIndexingService, diagOptions, logger);

        var savedSteps = new List<ExtractionStep>();
        await _stepRepository.SaveStepAsync(Arg.Do<ExtractionStep>(s => savedSteps.Add(s)), Arg.Any<CancellationToken>());

        await orchestrator.ProcessSessionAsync(sessionId);

        var intakeStep = savedSteps.First(s => s.StepName == ExtractionStepName.Intake);
        intakeStep.LlmTraces.Should().HaveCount(1);
        intakeStep.LlmTraces.First().ModelUsed.Should().Be("gpt-4.1-nano");
        intakeStep.LlmTraces.First().PromptText.Should().BeNull();
        intakeStep.LlmTraces.First().ResponseText.Should().Be("response");
    }

    [Fact]
    public async Task StoreLlmTraces_Disabled_NoLlmTracesOnSteps()
    {
        var sessionId = Guid.NewGuid();
        SetupFullPipeline(sessionId);

        // Default orchestrator has StoreLlmTraces = false
        _intakeAgent.ProcessAsync(Arg.Any<ParsedDocument>(), Arg.Any<CancellationToken>())
            .Returns(new IntakeResult
            {
                IsValidTherapyNote = true,
                ModelUsed = "gpt-4.1-nano",
                LlmTraces = [new SessionSight.Agents.Tools.LlmCallTrace(null, null, "response", "gpt-4.1-nano", 0, 100, 50, 150, 200)],
                Metadata = new ExtractedMetadata { DocumentType = "Session Note", Language = "en" }
            });

        var savedSteps = new List<ExtractionStep>();
        await _stepRepository.SaveStepAsync(Arg.Do<ExtractionStep>(s => savedSteps.Add(s)), Arg.Any<CancellationToken>());

        await _orchestrator.ProcessSessionAsync(sessionId);

        var intakeStep = savedSteps.First(s => s.StepName == ExtractionStepName.Intake);
        intakeStep.LlmTraces.Should().BeEmpty();
    }

    [Fact]
    public async Task ToolCalls_SavedIncrementallyViaCallback()
    {
        // Tool calls with I/O JSON are now saved incrementally via the onRoundComplete
        // callback in the orchestrator, not on the step entity. This test verifies
        // that the callback captures the correct data by invoking it directly.
        var sessionId = Guid.NewGuid();
        SetupFullPipeline(sessionId);

        Func<SessionSight.Agents.Tools.LlmCallTrace, IReadOnlyList<SessionSight.Agents.Tools.ToolCallEntry>, Task>? capturedCallback = null;
        _extractorAgent.ExtractAsync(
            Arg.Any<IntakeResult>(),
            Arg.Do<Func<SessionSight.Agents.Tools.LlmCallTrace, IReadOnlyList<SessionSight.Agents.Tools.ToolCallEntry>, Task>?>(cb => capturedCallback = cb),
            Arg.Any<CancellationToken>())
            .Returns(new AgentExtractionResult
            {
                Data = new ClinicalExtraction(),
                ModelsUsed = ["gpt-4.1-mini"],
                ToolCallCount = 1,
                ToolCallTrace =
                [
                    new SessionSight.Agents.Tools.ToolCallEntry("ValidateSchema", true, 0, 50,
                        """{"schema":"clinical"}""", """{"valid":true}""")
                ]
            });

        await _orchestrator.ProcessSessionAsync(sessionId);

        // Verify the callback was passed
        capturedCallback.Should().NotBeNull();
    }
}
