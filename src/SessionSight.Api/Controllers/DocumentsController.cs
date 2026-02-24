using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SessionSight.Agents.Services;
using SessionSight.Api.DTOs;
using SessionSight.Api.Mapping;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using SessionSight.Infrastructure.Search;

namespace SessionSight.Api.Controllers;

[ApiController]
[Route("api/sessions/{sessionId:guid}")]
public partial class DocumentsController : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".jpg", ".jpeg", ".png", ".tiff", ".bmp"
    };

    private readonly ISessionRepository _sessionRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IExtractionStepRepository _stepRepository;
    private readonly IDocumentStorage _documentStorage;
    private readonly ISearchIndexService _searchIndexService;
    private readonly ILogger<DocumentsController> _logger;
    private readonly DocumentIntelligenceOptions _docOptions;

    public DocumentsController(
        ISessionRepository sessionRepository,
        IDocumentRepository documentRepository,
        IExtractionStepRepository stepRepository,
        IDocumentStorage documentStorage,
        ISearchIndexService searchIndexService,
        ILogger<DocumentsController> logger,
        IOptions<DocumentIntelligenceOptions> docOptions)
    {
        _sessionRepository = sessionRepository;
        _documentRepository = documentRepository;
        _stepRepository = stepRepository;
        _documentStorage = documentStorage;
        _searchIndexService = searchIndexService;
        _logger = logger;
        _docOptions = docOptions.Value;
    }

    [HttpPost("document")]
    public async Task<ActionResult<UploadDocumentResponse>> UploadDocument(Guid sessionId, IFormFile file)
    {
        if (file.Length == 0)
            return BadRequest("File is empty.");

        if (file.Length > _docOptions.MaxFileSizeBytes)
            return BadRequest($"File size ({file.Length:N0} bytes) exceeds maximum allowed ({_docOptions.MaxFileSizeBytes:N0} bytes).");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            return BadRequest($"Unsupported file type '{extension}'. Allowed types: {string.Join(", ", AllowedExtensions.Order())}.");

        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session is null) return NotFound();

        if (session.Document is not null)
            return Conflict("A document already exists for this session.");

        var blobUri = await _documentStorage.UploadAsync(file.FileName, file.OpenReadStream(), file.ContentType);

        var document = new SessionDocument
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            OriginalFileName = file.FileName,
            BlobUri = blobUri,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            Status = DocumentStatus.Pending,
            UploadedAt = DateTime.UtcNow
        };

        await _documentRepository.AddDocumentAsync(session, document);

        return Created($"/api/sessions/{sessionId}/document",
            new UploadDocumentResponse(
                document.Id,
                sessionId,
                file.FileName,
                blobUri,
                DocumentStatus.Pending.ToString()));
    }

    [HttpGet("extraction")]
    public async Task<ActionResult<ExtractionResultDto>> GetExtraction(Guid sessionId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session is null) return NotFound();
        if (session.Extraction is null) return NotFound("No extraction result found for this session.");

        return Ok(session.Extraction.ToDto());
    }

    [HttpGet("extraction/steps")]
    public async Task<ActionResult<ExtractionStepsResponseDto>> GetExtractionSteps(Guid sessionId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session is null) return NotFound();
        if (session.Extraction is null) return NotFound("No extraction result found for this session.");

        var steps = await _stepRepository.GetStepsByExtractionIdAsync(session.Extraction.Id);
        var docStatus = session.Document?.Status.ToString();
        var failureKind = session.Document?.FailureKind.ToString();
        var errorMessage = session.Document?.ErrorMessage;
        return Ok(steps.ToStepsDto(session.Extraction.Id, docStatus, failureKind, errorMessage));
    }

    [HttpDelete("document")]
    public async Task<IActionResult> DeleteDocument(Guid sessionId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session is null) return NotFound();
        if (session.Document is null) return NotFound("No document found for this session.");

        // Delete blob
        await _documentStorage.DeleteAsync(session.Document.BlobUri);

        // Delete from search index (best-effort)
        try
        {
            await _searchIndexService.DeleteDocumentAsync(sessionId.ToString());
        }
        catch (Exception ex)
        {
            LogSearchIndexDeleteFailed(_logger, ex, sessionId);
        }

        // Delete from database (extraction + document)
        await _documentRepository.DeleteDocumentAsync(sessionId);

        return NoContent();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete search index entry for session {SessionId}")]
    private static partial void LogSearchIndexDeleteFailed(ILogger logger, Exception exception, Guid sessionId);
}
