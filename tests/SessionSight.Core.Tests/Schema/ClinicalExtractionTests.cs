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
}
