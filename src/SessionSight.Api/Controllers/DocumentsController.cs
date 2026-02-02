using Microsoft.AspNetCore.Mvc;
using SessionSight.Api.DTOs;
using SessionSight.Api.Mapping;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Controllers;

[ApiController]
[Route("api/sessions/{sessionId:guid}")]
public class DocumentsController : ControllerBase
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IDocumentStorage _documentStorage;

    public DocumentsController(
        ISessionRepository sessionRepository,
        IDocumentStorage documentStorage)
    {
        _sessionRepository = sessionRepository;
        _documentStorage = documentStorage;
    }

    [HttpPost("document")]
    public async Task<ActionResult<UploadDocumentResponse>> UploadDocument(Guid sessionId, IFormFile file)
    {
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

        await _sessionRepository.AddDocumentAsync(session, document);

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
}
