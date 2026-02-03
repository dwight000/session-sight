using FluentAssertions;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;

namespace SessionSight.Core.Tests.Entities;

public class EntityTests
{
    [Fact]
    public void ProcessingJob_DefaultValues_AreInitialized()
    {
        var job = new ProcessingJob();
        job.Id.Should().Be(Guid.Empty);
        job.JobKey.Should().BeEmpty();
        job.Status.Should().Be(default(JobStatus));
        job.CreatedAt.Should().Be(default);
        job.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void ProcessingJob_CanSetAllProperties()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var completedAt = DateTime.UtcNow.AddMinutes(5);

        var job = new ProcessingJob
        {
            Id = id,
            JobKey = "job-123",
            Status = JobStatus.Completed,
            CreatedAt = createdAt,
            CompletedAt = completedAt
        };

        job.Id.Should().Be(id);
        job.JobKey.Should().Be("job-123");
        job.Status.Should().Be(JobStatus.Completed);
        job.CreatedAt.Should().Be(createdAt);
        job.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void Therapist_DefaultValues_AreInitialized()
    {
        var therapist = new Therapist();
        therapist.Id.Should().Be(Guid.Empty);
        therapist.Name.Should().BeEmpty();
        therapist.LicenseNumber.Should().BeNull();
        therapist.Credentials.Should().BeNull();
        therapist.IsActive.Should().BeTrue();
        therapist.CreatedAt.Should().Be(default);
        therapist.Sessions.Should().NotBeNull();
        therapist.Sessions.Should().BeEmpty();
    }

    [Fact]
    public void Therapist_CanSetAllProperties()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        var therapist = new Therapist
        {
            Id = id,
            Name = "Dr. Smith",
            LicenseNumber = "LIC-12345",
            Credentials = "PhD, LMFT",
            IsActive = false,
            CreatedAt = createdAt
        };

        therapist.Id.Should().Be(id);
        therapist.Name.Should().Be("Dr. Smith");
        therapist.LicenseNumber.Should().Be("LIC-12345");
        therapist.Credentials.Should().Be("PhD, LMFT");
        therapist.IsActive.Should().BeFalse();
        therapist.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Therapist_Sessions_CanBePopulated()
    {
        var therapist = new Therapist { Name = "Dr. Smith" };
        var session = new Session { Id = Guid.NewGuid() };
        therapist.Sessions.Add(session);

        therapist.Sessions.Should().HaveCount(1);
        therapist.Sessions.Should().Contain(session);
    }

    [Fact]
    public void SessionDocument_DefaultValues_AreInitialized()
    {
        var doc = new SessionDocument();

        doc.Id.Should().Be(Guid.Empty);
        doc.SessionId.Should().Be(Guid.Empty);
        doc.OriginalFileName.Should().BeEmpty();
        doc.BlobUri.Should().BeEmpty();
        doc.ContentType.Should().BeEmpty();
        doc.FileSizeBytes.Should().Be(0);
        doc.ExtractedText.Should().BeNull();
        doc.Status.Should().Be(default(DocumentStatus));
        doc.UploadedAt.Should().Be(default);
        doc.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public void SessionDocument_CanSetAllProperties()
    {
        var id = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var uploadedAt = DateTime.UtcNow;
        var processedAt = DateTime.UtcNow.AddMinutes(5);

        var doc = new SessionDocument
        {
            Id = id,
            SessionId = sessionId,
            OriginalFileName = "therapy-note.pdf",
            BlobUri = "https://storage.blob.core.windows.net/docs/abc.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024000,
            ExtractedText = "Session content...",
            Status = DocumentStatus.Completed,
            UploadedAt = uploadedAt,
            ProcessedAt = processedAt
        };

        doc.Id.Should().Be(id);
        doc.SessionId.Should().Be(sessionId);
        doc.OriginalFileName.Should().Be("therapy-note.pdf");
        doc.BlobUri.Should().Be("https://storage.blob.core.windows.net/docs/abc.pdf");
        doc.ContentType.Should().Be("application/pdf");
        doc.FileSizeBytes.Should().Be(1024000);
        doc.ExtractedText.Should().Be("Session content...");
        doc.Status.Should().Be(DocumentStatus.Completed);
        doc.UploadedAt.Should().Be(uploadedAt);
        doc.ProcessedAt.Should().Be(processedAt);
    }

    [Fact]
    public void Session_DefaultValues_AreInitialized()
    {
        var session = new Session();

        session.Id.Should().Be(Guid.Empty);
        session.PatientId.Should().Be(Guid.Empty);
        session.TherapistId.Should().Be(Guid.Empty);
        session.SessionDate.Should().Be(default);
        session.SessionType.Should().Be(default(SessionType));
        session.Modality.Should().Be(default(SessionModality));
        session.DurationMinutes.Should().BeNull();
        session.SessionNumber.Should().Be(0);
        session.Document.Should().BeNull();
        session.Extraction.Should().BeNull();
    }

    [Fact]
    public void Session_CanSetAllProperties()
    {
        var id = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var therapistId = Guid.NewGuid();
        var sessionDate = DateOnly.FromDateTime(DateTime.Today);
        var createdAt = DateTime.UtcNow;

        var session = new Session
        {
            Id = id,
            PatientId = patientId,
            TherapistId = therapistId,
            SessionDate = sessionDate,
            SessionType = SessionType.Individual,
            Modality = SessionModality.InPerson,
            DurationMinutes = 50,
            SessionNumber = 5,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        session.Id.Should().Be(id);
        session.PatientId.Should().Be(patientId);
        session.TherapistId.Should().Be(therapistId);
        session.SessionDate.Should().Be(sessionDate);
        session.SessionType.Should().Be(SessionType.Individual);
        session.Modality.Should().Be(SessionModality.InPerson);
        session.DurationMinutes.Should().Be(50);
        session.SessionNumber.Should().Be(5);
        session.CreatedAt.Should().Be(createdAt);
        session.UpdatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Session_CanSetDocument()
    {
        var session = new Session();
        var document = new SessionDocument { Id = Guid.NewGuid() };

        session.Document = document;

        session.Document.Should().Be(document);
    }

    [Fact]
    public void ExtractionResult_DefaultValues_AreInitialized()
    {
        var extraction = new ExtractionResult();

        extraction.Id.Should().Be(Guid.Empty);
        extraction.SessionId.Should().Be(Guid.Empty);
        extraction.SchemaVersion.Should().Be("1.0.0");
        extraction.ModelUsed.Should().BeEmpty();
        extraction.OverallConfidence.Should().Be(0);
        extraction.RequiresReview.Should().BeFalse();
        extraction.ExtractedAt.Should().Be(default);
        extraction.Data.Should().NotBeNull();
    }

    [Fact]
    public void ExtractionResult_CanSetAllProperties()
    {
        var id = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var extractedAt = DateTime.UtcNow;

        var extraction = new ExtractionResult
        {
            Id = id,
            SessionId = sessionId,
            SchemaVersion = "2.0.0",
            ModelUsed = "gpt-4o",
            OverallConfidence = 0.95,
            RequiresReview = true,
            ExtractedAt = extractedAt
        };

        extraction.Id.Should().Be(id);
        extraction.SessionId.Should().Be(sessionId);
        extraction.SchemaVersion.Should().Be("2.0.0");
        extraction.ModelUsed.Should().Be("gpt-4o");
        extraction.OverallConfidence.Should().Be(0.95);
        extraction.RequiresReview.Should().BeTrue();
        extraction.ExtractedAt.Should().Be(extractedAt);
    }

    [Fact]
    public void Patient_DefaultValues_AreInitialized()
    {
        var patient = new Patient();

        patient.Id.Should().Be(Guid.Empty);
        patient.ExternalId.Should().BeEmpty();
        patient.FirstName.Should().BeEmpty();
        patient.LastName.Should().BeEmpty();
        patient.DateOfBirth.Should().Be(default);
        patient.CreatedAt.Should().Be(default);
        patient.UpdatedAt.Should().Be(default);
        patient.Sessions.Should().NotBeNull();
        patient.Sessions.Should().BeEmpty();
    }

    [Fact]
    public void Patient_CanSetAllProperties()
    {
        var id = Guid.NewGuid();
        var dob = new DateOnly(1990, 5, 15);
        var createdAt = DateTime.UtcNow;

        var patient = new Patient
        {
            Id = id,
            ExternalId = "EXT-123",
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = dob,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        patient.Id.Should().Be(id);
        patient.ExternalId.Should().Be("EXT-123");
        patient.FirstName.Should().Be("John");
        patient.LastName.Should().Be("Doe");
        patient.DateOfBirth.Should().Be(dob);
        patient.CreatedAt.Should().Be(createdAt);
        patient.UpdatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Patient_Sessions_CanBePopulated()
    {
        var patient = new Patient();
        var session1 = new Session { Id = Guid.NewGuid() };
        var session2 = new Session { Id = Guid.NewGuid() };

        patient.Sessions.Add(session1);
        patient.Sessions.Add(session2);

        patient.Sessions.Should().HaveCount(2);
        patient.Sessions.Should().Contain(session1);
        patient.Sessions.Should().Contain(session2);
    }
}
