using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;
using SessionSight.Agents.Services;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using AgentExtractionResult = SessionSight.Agents.Models.ExtractionResult;

namespace SessionSight.Agents.Orchestration;

/// <summary>
/// Groups agent dependencies for the extraction orchestrator.
/// </summary>
public record ExtractionAgents(
    IIntakeAgent Intake,
    IClinicalExtractorAgent Extractor,
    IRiskAssessorAgent RiskAssessor,
    ISummarizerAgent Summarizer);

/// <summary>
/// Orchestrates the full extraction pipeline from document parsing through risk assessment.
/// </summary>
public partial class ExtractionOrchestrator : IExtractionOrchestrator
{
    private readonly IDocumentParser _documentParser;
    private readonly ExtractionAgents _agents;
    private readonly ISessionRepository _sessionRepository;
    private readonly IDocumentStorage _documentStorage;
    private readonly ISessionIndexingService _sessionIndexingService;
    private readonly ILogger<ExtractionOrchestrator> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExtractionOrchestrator(
        IDocumentParser documentParser,
        ExtractionAgents agents,
        ISessionRepository sessionRepository,
        IDocumentStorage documentStorage,
        ISessionIndexingService sessionIndexingService,
        ILogger<ExtractionOrchestrator> logger)
    {
        _documentParser = documentParser;
        _agents = agents;
        _sessionRepository = sessionRepository;
        _documentStorage = documentStorage;
        _sessionIndexingService = sessionIndexingService;
        _logger = logger;
    }

    public async Task<OrchestrationResult> ProcessSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var modelsUsed = new List<string>();

        LogStartingExtraction(_logger, sessionId);

        // Step 0: Get session with document
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session is null)
        {
            return new OrchestrationResult
            {
                Success = false,
                SessionId = sessionId,
                ErrorMessage = $"Session {sessionId} not found"
            };
        }

        if (session.Document is null)
        {
            return new OrchestrationResult
            {
                Success = false,
                SessionId = sessionId,
                ErrorMessage = "Session has no document uploaded"
            };
        }

        // Update status to Processing (direct document update to avoid Session concurrency issues)
        await _sessionRepository.UpdateDocumentStatusAsync(sessionId, DocumentStatus.Processing);

        try
        {
            // Step 1: Download blob and parse with Document Intelligence
            LogDownloadingDocument(_logger, session.Document.BlobUri);
            await using var stream = await _documentStorage.DownloadAsync(session.Document.BlobUri);
            var parsedDoc = await _documentParser.ParseAsync(stream, session.Document.OriginalFileName, ct);

            LogDocumentParsed(_logger, parsedDoc.Metadata.PageCount, parsedDoc.Metadata.ExtractionConfidence);

            // Step 2: Intake Agent - metadata extraction and validation
            LogRunningIntakeAgent(_logger);
            var intakeResult = await _agents.Intake.ProcessAsync(parsedDoc, ct);
            modelsUsed.Add(intakeResult.ModelUsed);

            if (!intakeResult.IsValidTherapyNote)
            {
                LogDocumentValidationFailed(_logger, intakeResult.ValidationError);
                await _sessionRepository.UpdateDocumentStatusAsync(sessionId, DocumentStatus.Failed);

                return new OrchestrationResult
                {
                    Success = false,
                    SessionId = sessionId,
                    ErrorMessage = $"Invalid document: {intakeResult.ValidationError}",
                    ModelsUsed = modelsUsed,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }

            // Step 3: Clinical Extractor - schema extraction
            LogRunningClinicalExtractor(_logger);
            var extractionResult = await _agents.Extractor.ExtractAsync(intakeResult, ct);
            extractionResult.SessionId = sessionId.ToString("D", System.Globalization.CultureInfo.InvariantCulture);
            modelsUsed.AddRange(extractionResult.ModelsUsed);

            // Fail pipeline on JSON parse failure — empty extraction with defaulted risk fields
            // is a safety false-negative (all risk = None/Low when data is actually unknown)
            if (extractionResult.Errors.Any(e => e.Contains("Failed to parse extraction JSON", StringComparison.Ordinal)))
            {
                LogExtractionParseFailed(_logger, sessionId);
                await _sessionRepository.UpdateDocumentStatusAsync(sessionId, DocumentStatus.Failed);
                return new OrchestrationResult
                {
                    Success = false,
                    SessionId = sessionId,
                    ErrorMessage = string.Join("; ", extractionResult.Errors),
                    ModelsUsed = modelsUsed,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }

            // Step 4: Risk Assessor - safety validation
            LogRunningRiskAssessor(_logger);
            var riskResult = await _agents.RiskAssessor.AssessAsync(
                extractionResult, parsedDoc.MarkdownContent, ct);
            modelsUsed.Add(riskResult.ModelUsed);

            // Step 5: Merge risk assessment into extraction result
            if (riskResult.RequiresReview)
            {
                extractionResult.RequiresReview = true;
                foreach (var reason in riskResult.ReviewReasons)
                {
                    extractionResult.LowConfidenceFields.Add($"Risk: {reason}");
                }
            }

            // Replace the risk assessment section with the final validated version
            extractionResult.Data.RiskAssessment = riskResult.FinalExtraction;

            // Step 5.5: Generate session summary
            LogRunningSummarizer(_logger);
            SessionSummary? sessionSummary = null;
            try
            {
                sessionSummary = await _agents.Summarizer.SummarizeSessionAsync(extractionResult, ct);
                modelsUsed.Add(sessionSummary.ModelUsed);
            }
            catch (Exception ex)
            {
                LogSummarizerError(_logger, ex, sessionId);
                // Summary generation failure is non-fatal - continue with extraction save
            }

            // Step 5.6: Index session for search (embedding + search index)
            try
            {
                LogIndexingStarted(_logger, sessionId);
                await _sessionIndexingService.IndexSessionAsync(session, extractionResult, sessionSummary, ct);
                LogIndexingCompleted(_logger, sessionId);
            }
            catch (Exception ex)
            {
                LogIndexingError(_logger, ex, sessionId);
                // Indexing failure is non-fatal - continue with extraction save
            }

            // Step 6: Save to database
            var savedExtraction = await SaveExtractionAsync(
                session,
                extractionResult,
                modelsUsed,
                sessionSummary,
                riskResult.Diagnostics,
                riskResult);

            // Update document status to Completed
            await _sessionRepository.UpdateDocumentStatusAsync(
                sessionId, DocumentStatus.Completed, parsedDoc.Content);

            stopwatch.Stop();
            LogExtractionCompleted(_logger, sessionId, stopwatch.ElapsedMilliseconds, extractionResult.RequiresReview);

            return new OrchestrationResult
            {
                Success = true,
                SessionId = sessionId,
                ExtractionId = savedExtraction.Id,
                RequiresReview = extractionResult.RequiresReview,
                ModelsUsed = modelsUsed,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ToolCallCount = extractionResult.ToolCallCount,
                RiskStageOutputs = new RiskStageOutputs
                {
                    ClinicalExtractor = riskResult.OriginalExtraction,
                    RiskReextracted = riskResult.ValidatedExtraction,
                    RiskFinal = riskResult.FinalExtraction
                },
                RiskDiagnostics = riskResult.Diagnostics
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogExtractionFailed(_logger, ex, sessionId);

            // Update document status to Failed (direct update avoids concurrency issues)
            try
            {
                await _sessionRepository.UpdateDocumentStatusAsync(sessionId, DocumentStatus.Failed);
            }
            catch (Exception updateEx)
            {
                LogStatusUpdateFailed(_logger, updateEx, sessionId);
            }

            return new OrchestrationResult
            {
                Success = false,
                SessionId = sessionId,
                ErrorMessage = ex.Message,
                ModelsUsed = modelsUsed,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private async Task<SessionSight.Core.Entities.ExtractionResult> SaveExtractionAsync(
        Session session,
        AgentExtractionResult agentResult,
        List<string> modelsUsed,
        SessionSummary? sessionSummary,
        RiskDiagnostics? riskDiagnostics,
        RiskAssessmentResult? riskResult = null)
    {
        // Convert agent result to entity
        var reviewReasons = agentResult.LowConfidenceFields
            .Where(f => f.StartsWith("Risk:", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var entity = new SessionSight.Core.Entities.ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            SchemaVersion = "1.0.0",
            ModelUsed = string.Join(", ", modelsUsed.Distinct()),
            OverallConfidence = agentResult.OverallConfidence,
            RequiresReview = agentResult.RequiresReview,
            ReviewStatus = agentResult.RequiresReview
                ? Core.Enums.ReviewStatus.Pending
                : Core.Enums.ReviewStatus.NotFlagged,
            ReviewReasons = reviewReasons,
            ExtractedAt = DateTime.UtcNow,
            Data = agentResult.Data,
            SummaryJson = sessionSummary != null
                ? JsonSerializer.Serialize(sessionSummary, JsonOptions)
                : null,
            GuardrailApplied = (riskDiagnostics?.HomicidalGuardrailApplied ?? false)
                || (riskDiagnostics?.SelfHarmGuardrailApplied ?? false),
            HomicidalGuardrailApplied = riskDiagnostics?.HomicidalGuardrailApplied ?? false,
            HomicidalGuardrailReason = riskDiagnostics?.HomicidalGuardrailReason,
            SelfHarmGuardrailApplied = riskDiagnostics?.SelfHarmGuardrailApplied ?? false,
            SelfHarmGuardrailReason = riskDiagnostics?.SelfHarmGuardrailReason,
            CriteriaValidationAttempts = riskDiagnostics?.CriteriaValidationAttemptsUsed ?? 1,
            DiscrepancyCount = riskResult?.Discrepancies.Count ?? 0,
            RiskFieldDecisionsJson = riskDiagnostics?.Decisions != null
                ? JsonSerializer.Serialize(riskDiagnostics.Decisions, JsonOptions)
                : null
        };

        // Direct insert avoids Session RowVersion concurrency issues
        await _sessionRepository.SaveExtractionResultAsync(entity);

        return entity;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting extraction for session {SessionId}")]
    private static partial void LogStartingExtraction(ILogger logger, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Downloading document from {BlobUri}")]
    private static partial void LogDownloadingDocument(ILogger logger, string blobUri);

    [LoggerMessage(Level = LogLevel.Information, Message = "Document parsed: {PageCount} pages, {Confidence:P0} confidence")]
    private static partial void LogDocumentParsed(ILogger logger, int pageCount, double confidence);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Running Intake Agent")]
    private static partial void LogRunningIntakeAgent(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Document validation failed: {Error}")]
    private static partial void LogDocumentValidationFailed(ILogger logger, string? error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Running Clinical Extractor Agent")]
    private static partial void LogRunningClinicalExtractor(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Running Risk Assessor Agent")]
    private static partial void LogRunningRiskAssessor(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Running Summarizer Agent")]
    private static partial void LogRunningSummarizer(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Summarizer Agent failed for session {SessionId}, continuing without summary")]
    private static partial void LogSummarizerError(ILogger logger, Exception exception, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting session indexing for session {SessionId}")]
    private static partial void LogIndexingStarted(ILogger logger, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session indexing completed for session {SessionId}")]
    private static partial void LogIndexingCompleted(ILogger logger, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Session indexing failed for session {SessionId}, continuing without indexing")]
    private static partial void LogIndexingError(ILogger logger, Exception exception, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Extraction completed for session {SessionId} in {Elapsed}ms. RequiresReview: {RequiresReview}")]
    private static partial void LogExtractionCompleted(ILogger logger, Guid sessionId, long elapsed, bool requiresReview);

    [LoggerMessage(Level = LogLevel.Error, Message = "Extraction parse failed for session {SessionId} - setting status to Failed")]
    private static partial void LogExtractionParseFailed(ILogger logger, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Extraction failed for session {SessionId}")]
    private static partial void LogExtractionFailed(ILogger logger, Exception exception, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to update document status to Failed for session {SessionId}")]
    private static partial void LogStatusUpdateFailed(ILogger logger, Exception exception, Guid sessionId);
}
