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
