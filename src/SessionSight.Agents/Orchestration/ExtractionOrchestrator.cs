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
public class ExtractionOrchestrator : IExtractionOrchestrator
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

        _logger.LogInformation("Starting extraction for session {SessionId}", sessionId);

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

        // Update status to Processing
        session.Document.Status = DocumentStatus.Processing;
        await _sessionRepository.UpdateAsync(session);

        try
        {
            // Step 1: Download blob and parse with Document Intelligence
            _logger.LogDebug("Downloading document from {BlobUri}", session.Document.BlobUri);
            await using var stream = await _documentStorage.DownloadAsync(session.Document.BlobUri);
            var parsedDoc = await _documentParser.ParseAsync(stream, session.Document.OriginalFileName, ct);

            _logger.LogInformation(
                "Document parsed: {PageCount} pages, {Confidence:P0} confidence",
                parsedDoc.Metadata.PageCount,
                parsedDoc.Metadata.ExtractionConfidence);

            // Step 2: Intake Agent - metadata extraction and validation
            _logger.LogDebug("Running Intake Agent");
            var intakeResult = await _intakeAgent.ProcessAsync(parsedDoc, ct);
            modelsUsed.Add(intakeResult.ModelUsed);

            if (!intakeResult.IsValidTherapyNote)
            {
                _logger.LogWarning("Document validation failed: {Error}", intakeResult.ValidationError);
                session.Document.Status = DocumentStatus.Failed;
                await _sessionRepository.UpdateAsync(session);

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
            _logger.LogDebug("Running Clinical Extractor Agent");
            var extractionResult = await _extractorAgent.ExtractAsync(intakeResult, ct);
            extractionResult.SessionId = sessionId.ToString();
            modelsUsed.AddRange(extractionResult.ModelsUsed);

            // Step 4: Risk Assessor - safety validation
            _logger.LogDebug("Running Risk Assessor Agent");
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
            var savedExtraction = await SaveExtractionAsync(session, extractionResult, modelsUsed, ct);

            // Update document status to Completed
            session.Document.Status = DocumentStatus.Completed;
            session.Document.ProcessedAt = DateTime.UtcNow;
            session.Document.ExtractedText = parsedDoc.Content;
            await _sessionRepository.UpdateAsync(session);

            stopwatch.Stop();
            _logger.LogInformation(
                "Extraction completed for session {SessionId} in {Elapsed}ms. RequiresReview: {RequiresReview}",
                sessionId, stopwatch.ElapsedMilliseconds, extractionResult.RequiresReview);

            return new OrchestrationResult
            {
                Success = true,
                SessionId = sessionId,
                ExtractionId = savedExtraction.Id,
                RequiresReview = extractionResult.RequiresReview,
                ModelsUsed = modelsUsed,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Extraction failed for session {SessionId}", sessionId);

            session.Document.Status = DocumentStatus.Failed;
            await _sessionRepository.UpdateAsync(session);

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
        List<string> modelsUsed,
        CancellationToken ct)
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

        // Associate with session
        session.Extraction = entity;
        await _sessionRepository.UpdateAsync(session);

        return entity;
    }
}
