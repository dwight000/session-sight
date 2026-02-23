using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SessionSight.Agents.Orchestration;
using SessionSight.Api.Controllers;
using SessionSight.Api.DTOs;
using SessionSight.Core.Entities;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Tests.Controllers;

public class IngestionControllerTests
{
    private readonly Mock<IPatientRepository> _mockPatientRepo;
    private readonly Mock<ISessionRepository> _mockSessionRepo;
    private readonly Mock<IProcessingJobRepository> _mockProcessingJobRepo;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<ILogger<IngestionController>> _mockLogger;
    private readonly IngestionController _controller;

    public IngestionControllerTests()
    {
        _mockPatientRepo = new Mock<IPatientRepository>();
        _mockSessionRepo = new Mock<ISessionRepository>();
        _mockProcessingJobRepo = new Mock<IProcessingJobRepository>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockLogger = new Mock<ILogger<IngestionController>>();

        // Wire up scope factory -> scope -> service provider -> orchestrator + job repo
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockOrchestrator = new Mock<IExtractionOrchestrator>();
        mockServiceProvider.Setup(p => p.GetService(typeof(IExtractionOrchestrator)))
            .Returns(mockOrchestrator.Object);
        mockServiceProvider.Setup(p => p.GetService(typeof(IProcessingJobRepository)))
            .Returns(_mockProcessingJobRepo.Object);
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        _controller = new IngestionController(
            _mockPatientRepo.Object,
            _mockSessionRepo.Object,
            _mockProcessingJobRepo.Object,
            _mockScopeFactory.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessNote_EmptyPatientId_ReturnsBadRequest()
    {
        // Arrange
        var request = new ProcessNoteRequest(
            PatientId: "",
            BlobUri: "https://storage/blob",
            SessionDate: DateOnly.FromDateTime(DateTime.Today),
            FileName: "note.pdf"
        );

        // Act
        var result = await _controller.ProcessNote(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result.Result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("PatientId is required");
    }

    [Fact]
    public async Task ProcessNote_EmptyBlobUri_ReturnsBadRequest()
    {
        // Arrange
        var request = new ProcessNoteRequest(
            PatientId: "P12345",
            BlobUri: "",
            SessionDate: DateOnly.FromDateTime(DateTime.Today),
            FileName: "note.pdf"
        );

        // Act
        var result = await _controller.ProcessNote(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ProcessNote_EmptyFileName_ReturnsBadRequest()
    {
        // Arrange
        var request = new ProcessNoteRequest(
            PatientId: "P12345",
            BlobUri: "https://storage/blob",
            SessionDate: DateOnly.FromDateTime(DateTime.Today),
            FileName: ""
        );

        // Act
        var result = await _controller.ProcessNote(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ProcessNote_UsesGetOrCreateByExternalId()
    {
        // Arrange
        var existingPatient = new Patient
        {
            Id = Guid.NewGuid(),
            ExternalId = "P12345",
            FirstName = "John",
            LastName = "Doe"
        };
        var request = new ProcessNoteRequest(
            PatientId: "P12345",
            BlobUri: "https://storage/blob/note.pdf",
            SessionDate: DateOnly.FromDateTime(DateTime.Today),
            FileName: "note.pdf"
        );

        _mockPatientRepo.Setup(r => r.GetOrCreateByExternalIdAsync("P12345", "Unknown", "Patient"))
            .ReturnsAsync(existingPatient);
        _mockSessionRepo.Setup(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session s, CancellationToken _) => { s.Id = Guid.NewGuid(); return s; });

        // Act
        var result = await _controller.ProcessNote(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<AcceptedResult>();
        _mockPatientRepo.Verify(r => r.GetOrCreateByExternalIdAsync("P12345", "Unknown", "Patient"), Times.Once);
        _mockSessionRepo.Verify(r => r.AddAsync(It.Is<Session>(s => s.PatientId == existingPatient.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessNote_ValidRequest_ReturnsAcceptedWithSessionId()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var patient = new Patient { Id = Guid.NewGuid(), ExternalId = "P12345" };
        var request = new ProcessNoteRequest(
            PatientId: "P12345",
            BlobUri: "https://storage/blob/note.pdf",
            SessionDate: new DateOnly(2026, 1, 15),
            FileName: "note.pdf"
        );

        _mockPatientRepo.Setup(r => r.GetOrCreateByExternalIdAsync("P12345", "Unknown", "Patient"))
            .ReturnsAsync(patient);
        _mockSessionRepo.Setup(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session s, CancellationToken _) => { s.Id = sessionId; return s; });

        // Act
        var result = await _controller.ProcessNote(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<AcceptedResult>();
        var accepted = result.Result as AcceptedResult;
        accepted!.Value.Should().BeOfType<ProcessNoteResponse>();
        var response = accepted.Value as ProcessNoteResponse;
        response!.SessionId.Should().Be(sessionId);
        response.Message.Should().Contain("Processing started");
    }

    [Fact]
    public async Task ProcessNote_CreatesSessionWithCorrectData()
    {
        // Arrange
        var patient = new Patient { Id = Guid.NewGuid(), ExternalId = "P12345" };
        var sessionDate = new DateOnly(2026, 1, 15);
        var request = new ProcessNoteRequest(
            PatientId: "P12345",
            BlobUri: "https://storage/blob/note.pdf",
            SessionDate: sessionDate,
            FileName: "therapy-note.pdf"
        );

        Session? capturedSession = null;
        _mockPatientRepo.Setup(r => r.GetOrCreateByExternalIdAsync("P12345", "Unknown", "Patient"))
            .ReturnsAsync(patient);
        _mockSessionRepo.Setup(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Callback<Session, CancellationToken>((s, _) => capturedSession = s)
            .ReturnsAsync((Session s, CancellationToken _) => { s.Id = Guid.NewGuid(); return s; });

        // Act
        await _controller.ProcessNote(request, CancellationToken.None);

        // Assert
        capturedSession.Should().NotBeNull();
        capturedSession!.PatientId.Should().Be(patient.Id);
        capturedSession.SessionDate.Should().Be(sessionDate);
        capturedSession.Document.Should().NotBeNull();
        capturedSession.Document!.BlobUri.Should().Be("https://storage/blob/note.pdf");
        capturedSession.Document.OriginalFileName.Should().Be("therapy-note.pdf");
        capturedSession.Document.ContentType.Should().Be("application/pdf");
    }

    [Theory]
    [InlineData("note.pdf", "application/pdf")]
    [InlineData("note.PDF", "application/pdf")]
    [InlineData("note.doc", "application/msword")]
    [InlineData("note.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("note.txt", "text/plain")]
    [InlineData("note.rtf", "application/rtf")]
    [InlineData("note.xyz", "application/octet-stream")]
    [InlineData("note", "application/octet-stream")]
    public async Task ProcessNote_DifferentFileExtensions_SetsCorrectContentType(string fileName, string expectedContentType)
    {
        var patient = new Patient { Id = Guid.NewGuid(), ExternalId = "P12345" };
        Session? capturedSession = null;

        _mockPatientRepo.Setup(r => r.GetOrCreateByExternalIdAsync("P12345", "Unknown", "Patient"))
            .ReturnsAsync(patient);
        _mockSessionRepo.Setup(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Callback<Session, CancellationToken>((s, _) => capturedSession = s)
            .ReturnsAsync((Session s, CancellationToken _) => { s.Id = Guid.NewGuid(); return s; });

        var request = new ProcessNoteRequest(
            PatientId: "P12345",
            BlobUri: $"https://storage/blob/{fileName}",
            SessionDate: DateOnly.FromDateTime(DateTime.Today),
            FileName: fileName
        );

        await _controller.ProcessNote(request, CancellationToken.None);

        capturedSession.Should().NotBeNull();
        capturedSession!.Document!.ContentType.Should().Be(expectedContentType);
    }

    [Fact]
    public async Task ProcessNote_WhitespacePatientId_ReturnsBadRequest()
    {
        var request = new ProcessNoteRequest(
            PatientId: "   ",
            BlobUri: "https://storage/blob",
            SessionDate: DateOnly.FromDateTime(DateTime.Today),
            FileName: "note.pdf"
        );

        var result = await _controller.ProcessNote(request, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ProcessNote_WhitespaceBlobUri_ReturnsBadRequest()
    {
        var request = new ProcessNoteRequest(
            PatientId: "P12345",
            BlobUri: "   ",
            SessionDate: DateOnly.FromDateTime(DateTime.Today),
            FileName: "note.pdf"
        );

        var result = await _controller.ProcessNote(request, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ProcessNote_WhitespaceFileName_ReturnsBadRequest()
    {
        var request = new ProcessNoteRequest(
            PatientId: "P12345",
            BlobUri: "https://storage/blob",
            SessionDate: DateOnly.FromDateTime(DateTime.Today),
            FileName: "   "
        );

        var result = await _controller.ProcessNote(request, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ProcessNote_DuplicateJobKey_Returns202WithoutCreatingSession()
    {
        // Arrange
        var jobKey = "abc123def456";
        var request = new ProcessNoteRequest(
            PatientId: "P12345",
            BlobUri: "https://storage/blob/note.pdf",
            SessionDate: DateOnly.FromDateTime(DateTime.Today),
            FileName: "note.pdf",
            JobKey: jobKey
        );

        _mockProcessingJobRepo.Setup(r => r.GetByJobKeyAsync(jobKey))
            .ReturnsAsync(new ProcessingJob { Id = Guid.NewGuid(), JobKey = jobKey });

        // Act
        var result = await _controller.ProcessNote(request, CancellationToken.None);

        // Assert — returns 202 but does NOT create a session
        result.Result.Should().BeOfType<AcceptedResult>();
        var accepted = result.Result as AcceptedResult;
        var response = accepted!.Value as ProcessNoteResponse;
        response!.Message.Should().Contain("Already processed");
        _mockSessionRepo.Verify(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessNote_ValidJobKey_CreatesProcessingJob()
    {
        // Arrange
        var jobKey = "newjobkey123";
        var patient = new Patient { Id = Guid.NewGuid(), ExternalId = "P12345" };
        var request = new ProcessNoteRequest(
            PatientId: "P12345",
            BlobUri: "https://storage/blob/note.pdf",
            SessionDate: DateOnly.FromDateTime(DateTime.Today),
            FileName: "note.pdf",
            JobKey: jobKey
        );

        _mockProcessingJobRepo.Setup(r => r.GetByJobKeyAsync(jobKey))
            .ReturnsAsync(null as ProcessingJob);
        _mockProcessingJobRepo.Setup(r => r.CreateAsync(It.IsAny<ProcessingJob>()))
            .ReturnsAsync((ProcessingJob j) => j);
        _mockPatientRepo.Setup(r => r.GetOrCreateByExternalIdAsync("P12345", "Unknown", "Patient"))
            .ReturnsAsync(patient);
        _mockSessionRepo.Setup(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session s, CancellationToken _) => { s.Id = Guid.NewGuid(); return s; });

        // Act
        var result = await _controller.ProcessNote(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<AcceptedResult>();
        _mockProcessingJobRepo.Verify(r => r.CreateAsync(It.Is<ProcessingJob>(j => j.JobKey == jobKey)), Times.Once);
    }

    [Fact]
    public async Task ProcessNote_EmptyJobKey_SkipsIdempotencyCheck()
    {
        // Arrange
        var patient = new Patient { Id = Guid.NewGuid(), ExternalId = "P12345" };
        var request = new ProcessNoteRequest(
            PatientId: "P12345",
            BlobUri: "https://storage/blob/note.pdf",
            SessionDate: DateOnly.FromDateTime(DateTime.Today),
            FileName: "note.pdf",
            JobKey: null
        );

        _mockPatientRepo.Setup(r => r.GetOrCreateByExternalIdAsync("P12345", "Unknown", "Patient"))
            .ReturnsAsync(patient);
        _mockSessionRepo.Setup(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session s, CancellationToken _) => { s.Id = Guid.NewGuid(); return s; });

        // Act
        var result = await _controller.ProcessNote(request, CancellationToken.None);

        // Assert — session created, no job key check
        result.Result.Should().BeOfType<AcceptedResult>();
        _mockProcessingJobRepo.Verify(r => r.GetByJobKeyAsync(It.IsAny<string>()), Times.Never);
        _mockProcessingJobRepo.Verify(r => r.CreateAsync(It.IsAny<ProcessingJob>()), Times.Never);
        _mockSessionRepo.Verify(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
