using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
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
    private readonly Mock<IExtractionOrchestrator> _mockOrchestrator;
    private readonly Mock<ILogger<IngestionController>> _mockLogger;
    private readonly IngestionController _controller;

    public IngestionControllerTests()
    {
        _mockPatientRepo = new Mock<IPatientRepository>();
        _mockSessionRepo = new Mock<ISessionRepository>();
        _mockOrchestrator = new Mock<IExtractionOrchestrator>();
        _mockLogger = new Mock<ILogger<IngestionController>>();
        _controller = new IngestionController(
            _mockPatientRepo.Object,
            _mockSessionRepo.Object,
            _mockOrchestrator.Object,
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
    public async Task ProcessNote_ExistingPatient_UsesExistingPatient()
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

        _mockPatientRepo.Setup(r => r.GetByExternalIdAsync("P12345"))
            .ReturnsAsync(existingPatient);
        _mockSessionRepo.Setup(r => r.AddAsync(It.IsAny<Session>()))
            .ReturnsAsync((Session s) => { s.Id = Guid.NewGuid(); return s; });

        // Act
        var result = await _controller.ProcessNote(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<AcceptedResult>();
        _mockPatientRepo.Verify(r => r.AddAsync(It.IsAny<Patient>()), Times.Never);
        _mockSessionRepo.Verify(r => r.AddAsync(It.Is<Session>(s => s.PatientId == existingPatient.Id)), Times.Once);
    }

    [Fact]
    public async Task ProcessNote_NewPatient_CreatesPatient()
    {
        // Arrange
        var newPatientId = Guid.NewGuid();
        var request = new ProcessNoteRequest(
            PatientId: "NEW123",
            BlobUri: "https://storage/blob/note.pdf",
            SessionDate: DateOnly.FromDateTime(DateTime.Today),
            FileName: "note.pdf"
        );

        _mockPatientRepo.Setup(r => r.GetByExternalIdAsync("NEW123"))
            .ReturnsAsync((Patient?)null);
        _mockPatientRepo.Setup(r => r.AddAsync(It.IsAny<Patient>()))
            .ReturnsAsync((Patient p) => { p.Id = newPatientId; return p; });
        _mockSessionRepo.Setup(r => r.AddAsync(It.IsAny<Session>()))
            .ReturnsAsync((Session s) => { s.Id = Guid.NewGuid(); return s; });

        // Act
        var result = await _controller.ProcessNote(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<AcceptedResult>();
        _mockPatientRepo.Verify(r => r.AddAsync(It.Is<Patient>(p =>
            p.ExternalId == "NEW123" &&
            p.FirstName == "Unknown" &&
            p.LastName == "Patient")), Times.Once);
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

        _mockPatientRepo.Setup(r => r.GetByExternalIdAsync("P12345"))
            .ReturnsAsync(patient);
        _mockSessionRepo.Setup(r => r.AddAsync(It.IsAny<Session>()))
            .ReturnsAsync((Session s) => { s.Id = sessionId; return s; });

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
        _mockPatientRepo.Setup(r => r.GetByExternalIdAsync("P12345"))
            .ReturnsAsync(patient);
        _mockSessionRepo.Setup(r => r.AddAsync(It.IsAny<Session>()))
            .Callback<Session>(s => capturedSession = s)
            .ReturnsAsync((Session s) => { s.Id = Guid.NewGuid(); return s; });

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

        _mockPatientRepo.Setup(r => r.GetByExternalIdAsync("P12345"))
            .ReturnsAsync(patient);
        _mockSessionRepo.Setup(r => r.AddAsync(It.IsAny<Session>()))
            .Callback<Session>(s => capturedSession = s)
            .ReturnsAsync((Session s) => { s.Id = Guid.NewGuid(); return s; });

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
}
