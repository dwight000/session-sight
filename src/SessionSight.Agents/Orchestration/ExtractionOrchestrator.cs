using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly IExtractionStepRepository _stepRepository;
    private readonly IDocumentStorage _documentStorage;
    private readonly ISessionIndexingService _sessionIndexingService;
    // Used by LLM trace gating (B-095 future)
    private readonly PipelineDiagnosticsOptions _diagOptions;
    private readonly ILogger<ExtractionOrchestrator> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExtractionOrchestrator(
        IDocumentParser documentParser,
        ExtractionAgents agents,
        ISessionRepository sessionRepository,
        IExtractionStepRepository stepRepository,
        IDocumentStorage documentStorage,
        ISessionIndexingService sessionIndexingService,
        IOptions<PipelineDiagnosticsOptions> diagOptions,
        ILogger<ExtractionOrchestrator> logger)
    {
        _documentParser = documentParser;
        _agents = agents;
        _sessionRepository = sessionRepository;
        _stepRepository = stepRepository;
        _documentStorage = documentStorage;
        _sessionIndexingService = sessionIndexingService;
        _diagOptions = diagOptions.Value;
        _logger = logger;
    }

#pragma warning disable S3776 // Cognitive complexity - orchestrator has sequential pipeline steps
    public async Task<OrchestrationResult> ProcessSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
#pragma warning restore S3776
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

        // Atomic transition: only one caller can move Pending → Processing.
        // If transition fails, probe the DB to check if status is already Processing
        // (e.g. ExtractionController retry set Failed→Processing before calling us).
        // NOTE: can't use session.Document.Status — tracked entity may be stale after
        // caller's ExecuteUpdateAsync bypassed the EF change tracker.
        var transitioned = await _sessionRepository.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Pending, DocumentStatus.Processing);
        if (!transitioned)
        {
            var alreadyProcessing = await _sessionRepository.TryTransitionDocumentStatusAsync(
                sessionId, DocumentStatus.Processing, DocumentStatus.Processing);
            if (!alreadyProcessing)
            {
                return new OrchestrationResult
                {
                    Success = false,
                    SessionId = sessionId,
                    ErrorMessage = "Extraction already in progress or completed"
                };
            }
        }

        var extractionId = Guid.NewGuid();

        try
        {
            // Early creation: upsert clears any old extraction + cascaded steps, then inserts placeholder.
            // Inside try so upsert failure sets doc status to Failed (not stuck at Processing).
            var placeholder = new SessionSight.Core.Entities.ExtractionResult
            {
                Id = extractionId,
                SessionId = session.Id,
                SchemaVersion = "1.0.0",
                ModelUsed = string.Empty,
                ExtractedAt = DateTime.UtcNow,
                Data = new Core.Schema.ClinicalExtraction()
            };
            await _sessionRepository.UpsertExtractionResultAsync(placeholder);
            // Step 1: Download blob and parse with Document Intelligence
            var step1 = BeginStep(extractionId, ExtractionStepName.DocumentParse, 1, "azure-doc-intel");
            var sw1 = Stopwatch.StartNew();
            ParsedDocument parsedDoc;
            try
            {
                LogDownloadingDocument(_logger, session.Document.BlobUri);
                await using var stream = await _documentStorage.DownloadAsync(session.Document.BlobUri);
                parsedDoc = await _documentParser.ParseAsync(stream, session.Document.OriginalFileName, ct);
                sw1.Stop();

                CompleteStep(step1, sw1.ElapsedMilliseconds);
                step1.ResultSummaryJson = JsonSerializer.Serialize(new
                {
                    pageCount = parsedDoc.Metadata.PageCount,
                    ocrConfidence = parsedDoc.Metadata.ExtractionConfidence,
                    fileSizeBytes = parsedDoc.Metadata.FileSizeBytes
                }, JsonOptions);

                LogDocumentParsed(_logger, parsedDoc.Metadata.PageCount, parsedDoc.Metadata.ExtractionConfidence);
            }
            catch (Exception ex)
            {
                sw1.Stop();
                FailStep(step1, sw1.ElapsedMilliseconds, ex.Message);
                throw;
            }
            finally
            {
                await TrySaveStepAsync(step1);
            }

            // Step 2: Intake Agent - metadata extraction and validation
            var step2 = BeginStep(extractionId, ExtractionStepName.Intake, 2, string.Empty);
            var sw2 = Stopwatch.StartNew();
            IntakeResult intakeResult;
            try
            {
                LogRunningIntakeAgent(_logger);
                intakeResult = await _agents.Intake.ProcessAsync(parsedDoc, ct);
                sw2.Stop();
                modelsUsed.Add(intakeResult.ModelUsed);
                step2.ModelUsed = intakeResult.ModelUsed;
                step2.InputTokens = intakeResult.InputTokens;
                step2.OutputTokens = intakeResult.OutputTokens;
                step2.TotalTokens = intakeResult.TotalTokens;

                if (!intakeResult.IsValidTherapyNote)
                {
                    FailStep(step2, sw2.ElapsedMilliseconds, intakeResult.ValidationError ?? "Invalid document");
                    step2.ResultSummaryJson = JsonSerializer.Serialize(new
                    {
                        isValid = false,
                        documentType = intakeResult.Metadata.DocumentType,
                        validationError = intakeResult.ValidationError
                    }, JsonOptions);
                }
                else
                {
                    CompleteStep(step2, sw2.ElapsedMilliseconds);
                    step2.ResultSummaryJson = JsonSerializer.Serialize(new
                    {
                        isValid = true,
                        documentType = intakeResult.Metadata.DocumentType,
                        sessionDate = intakeResult.Metadata.SessionDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                        language = intakeResult.Metadata.Language,
                        estimatedWordCount = intakeResult.Metadata.EstimatedWordCount
                    }, JsonOptions);
                }

                PopulateLlmTraces(step2, intakeResult.LlmTraces);
            }
            catch (Exception ex)
            {
                sw2.Stop();
                FailStep(step2, sw2.ElapsedMilliseconds, ex.Message);
                throw;
            }
            finally
            {
                await TrySaveStepAsync(step2);
            }

            if (!intakeResult.IsValidTherapyNote)
            {
                LogDocumentValidationFailed(_logger, intakeResult.ValidationError);
                await _sessionRepository.TryTransitionDocumentStatusAsync(
                    sessionId, DocumentStatus.Processing, DocumentStatus.Failed);

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
            var step3 = BeginStep(extractionId, ExtractionStepName.ClinicalExtract, 3, string.Empty);
            var sw3 = Stopwatch.StartNew();
            AgentExtractionResult extractionResult;
            try
            {
                LogRunningClinicalExtractor(_logger);
                extractionResult = await _agents.Extractor.ExtractAsync(intakeResult, ct);
                sw3.Stop();
                extractionResult.SessionId = sessionId.ToString("D", System.Globalization.CultureInfo.InvariantCulture);
                modelsUsed.AddRange(extractionResult.ModelsUsed);

                step3.ModelUsed = string.Join(", ", extractionResult.ModelsUsed.Distinct());
                step3.InputTokens = extractionResult.InputTokens;
                step3.OutputTokens = extractionResult.OutputTokens;
                step3.TotalTokens = extractionResult.TotalTokens;

                // Populate tool calls from extraction trace
                foreach (var tc in extractionResult.ToolCallTrace)
                {
                    step3.ToolCalls.Add(new ExtractionToolCall
                    {
                        Id = Guid.NewGuid(),
                        StepId = step3.Id,
                        ToolName = tc.ToolName,
                        LoopRound = tc.LoopRound,
                        Succeeded = tc.Succeeded,
                        DurationMs = tc.DurationMs,
                        CalledAt = step3.StartedAt.AddMilliseconds(tc.DurationMs),
                        InputJson = tc.InputJson,
                        OutputJson = tc.OutputJson
                    });
                }

                // Fail pipeline on JSON parse failure
                if (extractionResult.Errors.Any(e => e.Contains("Failed to parse extraction JSON", StringComparison.Ordinal)))
                {
                    FailStep(step3, sw3.ElapsedMilliseconds, "Failed to parse extraction JSON from agent response");
                }
                else
                {
                    CompleteStep(step3, sw3.ElapsedMilliseconds);
                }

                step3.ResultSummaryJson = JsonSerializer.Serialize(new
                {
                    fieldCount = CountExtractedFields(extractionResult),
                    overallConfidence = extractionResult.OverallConfidence,
                    toolCallCount = extractionResult.ToolCallCount,
                    lowConfidenceFields = extractionResult.LowConfidenceFields
                }, JsonOptions);

                PopulateLlmTraces(step3, extractionResult.LlmTraces);
            }
            catch (Exception ex)
            {
                sw3.Stop();
                FailStep(step3, sw3.ElapsedMilliseconds, ex.Message);
                throw;
            }
            finally
            {
                await TrySaveStepAsync(step3);
            }

            if (extractionResult.Errors.Any(e => e.Contains("Failed to parse extraction JSON", StringComparison.Ordinal)))
            {
                LogExtractionParseFailed(_logger, sessionId);
                await _sessionRepository.TryTransitionDocumentStatusAsync(
                    sessionId, DocumentStatus.Processing, DocumentStatus.Failed);
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
            var step4 = BeginStep(extractionId, ExtractionStepName.RiskAssess, 4, string.Empty);
            var sw4 = Stopwatch.StartNew();
            RiskAssessmentResult riskResult;
            try
            {
                LogRunningRiskAssessor(_logger);
                riskResult = await _agents.RiskAssessor.AssessAsync(
                    extractionResult, parsedDoc.MarkdownContent, ct);
                sw4.Stop();
                modelsUsed.Add(riskResult.ModelUsed);

                step4.ModelUsed = riskResult.ModelUsed;
                step4.InputTokens = riskResult.InputTokens;
                step4.OutputTokens = riskResult.OutputTokens;
                step4.TotalTokens = riskResult.TotalTokens;

                CompleteStep(step4, sw4.ElapsedMilliseconds);
                step4.ResultSummaryJson = JsonSerializer.Serialize(new
                {
                    riskLevel = riskResult.DeterminedRiskLevel.ToString(),
                    requiresReview = riskResult.RequiresReview,
                    discrepancyCount = riskResult.Discrepancies.Count,
                    guardrailApplied = (riskResult.Diagnostics.HomicidalGuardrailApplied || riskResult.Diagnostics.SelfHarmGuardrailApplied),
                    reviewReasons = riskResult.ReviewReasons,
                    fieldDecisions = riskResult.Diagnostics.Decisions.Select(d => new
                    {
                        d.Field,
                        d.OriginalValue,
                        d.ReExtractedValue,
                        d.FinalValue,
                        d.RuleApplied
                    })
                }, JsonOptions);

                PopulateLlmTraces(step4, riskResult.LlmTraces);
            }
            catch (Exception ex)
            {
                sw4.Stop();
                FailStep(step4, sw4.ElapsedMilliseconds, ex.Message);
                throw;
            }
            finally
            {
                await TrySaveStepAsync(step4);
            }

            // Merge risk assessment into extraction result
            if (riskResult.RequiresReview)
            {
                extractionResult.RequiresReview = true;
                foreach (var reason in riskResult.ReviewReasons)
                {
                    extractionResult.LowConfidenceFields.Add($"Risk: {reason}");
                }
            }
            extractionResult.Data.RiskAssessment = riskResult.FinalExtraction;

            // Step 5: Generate session summary
            var step5 = BeginStep(extractionId, ExtractionStepName.Summarize, 5, string.Empty);
            var sw5 = Stopwatch.StartNew();
            SessionSummary? sessionSummary = null;
            try
            {
                LogRunningSummarizer(_logger);
                sessionSummary = await _agents.Summarizer.SummarizeSessionAsync(extractionResult, ct);
                sw5.Stop();
                modelsUsed.Add(sessionSummary.ModelUsed);

                step5.ModelUsed = sessionSummary.ModelUsed;
                step5.InputTokens = sessionSummary.InputTokens;
                step5.OutputTokens = sessionSummary.OutputTokens;
                step5.TotalTokens = sessionSummary.TotalTokens;

                CompleteStep(step5, sw5.ElapsedMilliseconds);
                step5.ResultSummaryJson = JsonSerializer.Serialize(new
                {
                    oneLiner = sessionSummary.OneLiner,
                    interventionsUsed = sessionSummary.InterventionsUsed
                }, JsonOptions);

                PopulateLlmTraces(step5, sessionSummary.LlmTraces);
            }
            catch (Exception ex)
            {
                sw5.Stop();
                FailStep(step5, sw5.ElapsedMilliseconds, ex.Message);
                LogSummarizerError(_logger, ex, sessionId);
                // Summary generation failure is non-fatal - continue
            }
            finally
            {
                await TrySaveStepAsync(step5);
            }

            // Step 6: Index session for search (embedding + search index)
            var step6 = BeginStep(extractionId, ExtractionStepName.SearchIndex, 6, "text-embedding-3-large");
            var sw6 = Stopwatch.StartNew();
            try
            {
                LogIndexingStarted(_logger, sessionId);
                await _sessionIndexingService.IndexSessionAsync(session, extractionResult, sessionSummary, ct);
                sw6.Stop();
                CompleteStep(step6, sw6.ElapsedMilliseconds);
                step6.ResultSummaryJson = JsonSerializer.Serialize(new { indexed = true }, JsonOptions);
                LogIndexingCompleted(_logger, sessionId);
            }
            catch (Exception ex)
            {
                sw6.Stop();
                FailStep(step6, sw6.ElapsedMilliseconds, ex.Message);
                step6.ResultSummaryJson = JsonSerializer.Serialize(new
                {
                    indexed = false,
                    errorReason = Truncate(ex.Message, 500)
                }, JsonOptions);
                LogIndexingError(_logger, ex, sessionId);
                // Indexing failure is non-fatal - continue
            }
            finally
            {
                await TrySaveStepAsync(step6);
            }

            // Final save: update the placeholder row (preserves step rows)
            await SaveExtractionAsync(
                session,
                extractionId,
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
                ExtractionId = extractionId,
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

            try
            {
                await _sessionRepository.TryTransitionDocumentStatusAsync(
                    sessionId, DocumentStatus.Processing, DocumentStatus.Failed);
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

    private static ExtractionStep BeginStep(Guid extractionId, ExtractionStepName stepName, int order, string model)
    {
        return new ExtractionStep
        {
            Id = Guid.NewGuid(),
            ExtractionId = extractionId,
            StepName = stepName,
            Status = ExtractionStepStatus.Running,
            StepOrder = order,
            StartedAt = DateTime.UtcNow,
            ModelUsed = model
        };
    }

    private static void CompleteStep(ExtractionStep step, long durationMs)
    {
        step.Status = ExtractionStepStatus.Succeeded;
        step.CompletedAt = DateTime.UtcNow;
        step.DurationMs = durationMs;
    }

    private static void FailStep(ExtractionStep step, long durationMs, string error)
    {
        step.Status = ExtractionStepStatus.Failed;
        step.CompletedAt = DateTime.UtcNow;
        step.DurationMs = durationMs;
        step.ErrorMessage = Truncate(error, 2000);
    }

    private async Task TrySaveStepAsync(ExtractionStep step)
    {
        try
        {
            await _stepRepository.SaveStepAsync(step);
        }
        catch (Exception ex)
        {
            LogStepSaveError(_logger, ex, step.StepName, step.ExtractionId);
        }
    }

    private void PopulateLlmTraces(ExtractionStep step, IReadOnlyList<Tools.LlmCallTrace> traces)
    {
        if (!_diagOptions.StoreLlmTraces || traces.Count == 0)
            return;

        foreach (var trace in traces)
        {
            step.LlmTraces.Add(new ExtractionLlmTrace
            {
                Id = Guid.NewGuid(),
                StepId = step.Id,
                ModelUsed = trace.ModelUsed,
                LoopRound = trace.LoopRound,
                InputTokens = trace.InputTokens,
                OutputTokens = trace.OutputTokens,
                TotalTokens = trace.TotalTokens,
                DurationMs = trace.DurationMs,
                PromptText = trace.PromptText,
                ResponseText = trace.ResponseText,
                CalledAt = step.StartedAt.AddMilliseconds(trace.DurationMs)
            });
        }
    }

    private async Task SaveExtractionAsync(
        Session session,
        Guid extractionId,
        AgentExtractionResult agentResult,
        List<string> modelsUsed,
        SessionSummary? sessionSummary,
        RiskDiagnostics? riskDiagnostics,
        RiskAssessmentResult? riskResult = null)
    {
        var reviewReasons = agentResult.LowConfidenceFields
            .Where(f => f.StartsWith("Risk:", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Load the placeholder and update in place to preserve step rows
        var entity = new SessionSight.Core.Entities.ExtractionResult
        {
            Id = extractionId,
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

        await _sessionRepository.UpdateExtractionResultAsync(entity);
    }

    private static int CountExtractedFields(AgentExtractionResult result)
    {
        if (result.Data is null) return 0;

        var count = 0;
        var sectionProperties = typeof(Core.Schema.ClinicalExtraction).GetProperties()
            .Where(p => p.PropertyType.GetProperties().Any(sp =>
                sp.PropertyType.IsGenericType &&
                sp.PropertyType.GetGenericTypeDefinition() == typeof(Core.Schema.ExtractedField<>)));

        foreach (var sectionProp in sectionProperties)
        {
            var section = sectionProp.GetValue(result.Data);
            if (section is null) continue;

            var fieldProps = section.GetType().GetProperties()
                .Where(p => p.PropertyType.IsGenericType &&
                            p.PropertyType.GetGenericTypeDefinition() == typeof(Core.Schema.ExtractedField<>));

            foreach (var fieldProp in fieldProps)
            {
                var field = fieldProp.GetValue(section);
                if (field is null) continue;

                var confidenceProp = field.GetType().GetProperty("Confidence");
                if (confidenceProp?.GetValue(field) is double confidence && confidence > 0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength];
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to save step {StepName} for extraction {ExtractionId}")]
    private static partial void LogStepSaveError(ILogger logger, Exception exception, ExtractionStepName stepName, Guid extractionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Step {StepName} save diagnostic: ToolCalls={StepToolCallCount}, LlmTraces={StepLlmTraceCount}, ResultTrace={ResultTraceCount}")]
    private static partial void LogStepDiagnostic(ILogger logger, ExtractionStepName stepName, int stepToolCallCount, int stepLlmTraceCount, int resultTraceCount);
}
