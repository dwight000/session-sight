using FluentAssertions;
using SessionSight.Core.Schema;

namespace SessionSight.Core.Tests.Schema;

public class ClinicalExtractionTests
{
    [Fact]
    public void ClinicalExtraction_DefaultConstruction_AllSubclassesNotNull()
    {
        var extraction = new ClinicalExtraction();
        extraction.SessionInfo.Should().NotBeNull();
        extraction.PresentingConcerns.Should().NotBeNull();
        extraction.MoodAssessment.Should().NotBeNull();
        extraction.RiskAssessment.Should().NotBeNull();
        extraction.MentalStatusExam.Should().NotBeNull();
        extraction.Interventions.Should().NotBeNull();
        extraction.Diagnoses.Should().NotBeNull();
        extraction.TreatmentProgress.Should().NotBeNull();
        extraction.NextSteps.Should().NotBeNull();
        extraction.Metadata.Should().NotBeNull();
    }

    [Fact]
    public void ExtractionMetadata_DefaultValues_AreCorrect()
    {
        var metadata = new ExtractionMetadata();

        metadata.ExtractionTimestamp.Should().Be(default);
        metadata.ExtractionModel.Should().BeEmpty();
        metadata.ExtractionVersion.Should().Be("1.0.0");
        metadata.OverallConfidence.Should().Be(0);
        metadata.LowConfidenceFields.Should().BeEmpty();
        metadata.RequiresReview.Should().BeFalse();
        metadata.ExtractionNotes.Should().BeNull();
    }

    [Fact]
    public void ExtractionMetadata_CanSetAllProperties()
    {
        var timestamp = DateTime.UtcNow;

        var metadata = new ExtractionMetadata
        {
            ExtractionTimestamp = timestamp,
            ExtractionModel = "gpt-4o",
            ExtractionVersion = "1.0.0",
            OverallConfidence = 0.92,
            LowConfidenceFields = new List<string> { "sessionDate", "patientId" },
            RequiresReview = true
        };

        metadata.ExtractionTimestamp.Should().Be(timestamp);
        metadata.ExtractionModel.Should().Be("gpt-4o");
        metadata.ExtractionVersion.Should().Be("1.0.0");
        metadata.OverallConfidence.Should().Be(0.92);
        metadata.LowConfidenceFields.Should().HaveCount(2);
        metadata.RequiresReview.Should().BeTrue();
    }

    [Fact]
    public void SessionInfoExtracted_DefaultValues_AreInitialized()
    {
        var sessionInfo = new SessionInfoExtracted();

        sessionInfo.PatientId.Should().NotBeNull();
        sessionInfo.SessionDate.Should().NotBeNull();
        sessionInfo.SessionNumber.Should().NotBeNull();
        sessionInfo.SessionType.Should().NotBeNull();
        sessionInfo.SessionModality.Should().NotBeNull();
        sessionInfo.SessionDurationMinutes.Should().NotBeNull();
        sessionInfo.SessionStartTime.Should().NotBeNull();
        sessionInfo.SessionEndTime.Should().NotBeNull();
        sessionInfo.TherapistId.Should().NotBeNull();
    }
}
