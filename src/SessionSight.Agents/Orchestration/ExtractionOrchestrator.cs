using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Services;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using AgentExtractionResult = SessionSight.Agents.Models.ExtractionResult;

namespace SessionSight.Agents.Orchestration;

/// <summary>
/// Orchestrates the full extraction pipeline from document parsing through risk assessment.
/// </summary>
public partial class ExtractionOrchestrator : IExtractionOrchestrator
{
    private readonly IDocumentParser _documentParser;
    private readonly IIntakeAgent _intakeAgent;
    private readonly IClinicalExtractorAgent _extractorAgent;
    private readonly IRiskAssessorAgent _riskAssessor;
    private readonly ISessionRepository _sessionRepository;
    private readonly IDocumentStorage _documentStorage;
    private readonly ILogger<ExtractionOrchestrator> _logger;

    public ExtractionOrchestrator(
        IDocumentParser documentParser,
        IIntakeAgent intakeAgent,
        IClinicalExtractorAgent extractorAgent,
        IRiskAssessorAgent riskAssessor,
        ISessionRepository sessionRepository,
        IDocumentStorage documentStorage,
        ILogger<ExtractionOrchestrator> logger)
    {
        _documentParser = documentParser;
        _intakeAgent = intakeAgent;
        _extractorAgent = extractorAgent;
        _riskAssessor = riskAssessor;
        _sessionRepository = sessionRepository;
        _documentStorage = documentStorage;
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
            var intakeResult = await _intakeAgent.ProcessAsync(parsedDoc, ct);
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
            var extractionResult = await _extractorAgent.ExtractAsync(intakeResult, ct);
            extractionResult.SessionId = sessionId.ToString("D", System.Globalization.CultureInfo.InvariantCulture);
            modelsUsed.AddRange(extractionResult.ModelsUsed);

            // Step 4: Risk Assessor - safety validation
            LogRunningRiskAssessor(_logger);
            var riskResult = await _riskAssessor.AssessAsync(
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

            // Step 6: Save to database
            var savedExtraction = await SaveExtractionAsync(session, extractionResult, modelsUsed);

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
                ToolCallCount = extractionResult.ToolCallCount
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

    private async Task<ExtractionResult> SaveExtractionAsync(
        Session session,
        AgentExtractionResult agentResult,
        List<string> modelsUsed)
    {
        // Convert agent result to entity
        var entity = new ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            SchemaVersion = "1.0.0",
            ModelUsed = string.Join(", ", modelsUsed.Distinct()),
            OverallConfidence = agentResult.OverallConfidence,
            RequiresReview = agentResult.RequiresReview,
            ExtractedAt = DateTime.UtcNow,
            Data = agentResult.Data
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Extraction completed for session {SessionId} in {Elapsed}ms. RequiresReview: {RequiresReview}")]
    private static partial void LogExtractionCompleted(ILogger logger, Guid sessionId, long elapsed, bool requiresReview);

    [LoggerMessage(Level = LogLevel.Error, Message = "Extraction failed for session {SessionId}")]
    private static partial void LogExtractionFailed(ILogger logger, Exception exception, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to update document status to Failed for session {SessionId}")]
    private static partial void LogStatusUpdateFailed(ILogger logger, Exception exception, Guid sessionId);
}
