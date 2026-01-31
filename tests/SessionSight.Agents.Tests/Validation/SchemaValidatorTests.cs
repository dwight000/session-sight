using FluentAssertions;
using SessionSight.Agents.Validation;
using SessionSight.Core.Enums;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Tests.Validation;

public class SchemaValidatorTests
{
    private readonly SchemaValidator _validator = new();

    [Fact]
    public void Validate_ValidExtraction_ReturnsSuccess()
    {
        var extraction = CreateValidExtraction();

        var result = _validator.Validate(extraction);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MissingSessionDate_ReturnsError()
    {
        var extraction = CreateValidExtraction();
        extraction.SessionInfo.SessionDate = new ExtractedField<DateOnly> { Value = default, Confidence = 0 };

        var result = _validator.Validate(extraction);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "SessionInfo.SessionDate");
    }

    [Fact]
    public void Validate_RiskIndicatorsWithoutOverallLevel_ReturnsError()
    {
        var extraction = CreateValidExtraction();
        extraction.RiskAssessment.SuicidalIdeation = new ExtractedField<SuicidalIdeation>
        {
            Value = SuicidalIdeation.Passive,
            Confidence = 0.95
        };
        extraction.RiskAssessment.RiskLevelOverall = new ExtractedField<RiskLevelOverall>
        {
            Value = default,
            Confidence = 0
        };

        var result = _validator.Validate(extraction);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "RiskAssessment.RiskLevelOverall");
    }

    [Fact]
    public void Validate_LowConfidenceSuicidalIdeation_ReturnsWarning()
    {
        var extraction = CreateValidExtraction();
        extraction.RiskAssessment.SuicidalIdeation = new ExtractedField<SuicidalIdeation>
        {
            Value = SuicidalIdeation.Passive,
            Confidence = 0.75 // Below 0.9 threshold
        };
        extraction.RiskAssessment.RiskLevelOverall = new ExtractedField<RiskLevelOverall>
        {
            Value = RiskLevelOverall.Moderate,
            Confidence = 0.95
        };

        var result = _validator.Validate(extraction);

        result.Errors.Should().ContainSingle(e =>
            e.Field == "RiskAssessment.SuicidalIdeation" &&
            e.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void Validate_LowConfidenceHighRisk_ReturnsWarning()
    {
        var extraction = CreateValidExtraction();
        extraction.RiskAssessment.RiskLevelOverall = new ExtractedField<RiskLevelOverall>
        {
            Value = RiskLevelOverall.High,
            Confidence = 0.75 // Below 0.9 threshold
        };

        var result = _validator.Validate(extraction);

        result.Errors.Should().ContainSingle(e =>
            e.Field == "RiskAssessment.RiskLevelOverall" &&
            e.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void Validate_MoodOutOfRange_ReturnsError()
    {
        var extraction = CreateValidExtraction();
        extraction.MoodAssessment.SelfReportedMood = new ExtractedField<int>
        {
            Value = 15, // Out of 1-10 range
            Confidence = 0.90
        };

        var result = _validator.Validate(extraction);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "MoodAssessment.SelfReportedMood");
    }

    [Fact]
    public void Validate_NegativeSessionDuration_ReturnsError()
    {
        var extraction = CreateValidExtraction();
        extraction.SessionInfo.SessionDurationMinutes = new ExtractedField<int>
        {
            Value = -30,
            Confidence = 0.90
        };

        var result = _validator.Validate(extraction);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "SessionInfo.SessionDurationMinutes");
    }

    [Fact]
    public void Validate_EndTimeBeforeStartTime_ReturnsError()
    {
        var extraction = CreateValidExtraction();
        extraction.SessionInfo.SessionStartTime = new ExtractedField<TimeOnly>
        {
            Value = new TimeOnly(14, 0),
            Confidence = 0.90
        };
        extraction.SessionInfo.SessionEndTime = new ExtractedField<TimeOnly>
        {
            Value = new TimeOnly(13, 0), // Before start time
            Confidence = 0.90
        };

        var result = _validator.Validate(extraction);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "SessionInfo.SessionEndTime");
    }

    [Fact]
    public void Validate_ActiveSIWithSafetyPlanNotNeeded_ReturnsWarning()
    {
        var extraction = CreateValidExtraction();
        extraction.RiskAssessment.SuicidalIdeation = new ExtractedField<SuicidalIdeation>
        {
            Value = SuicidalIdeation.ActiveWithPlan,
            Confidence = 0.95
        };
        extraction.RiskAssessment.SafetyPlanStatus = new ExtractedField<SafetyPlanStatus>
        {
            Value = SafetyPlanStatus.NotNeeded,
            Confidence = 0.90
        };
        extraction.RiskAssessment.RiskLevelOverall = new ExtractedField<RiskLevelOverall>
        {
            Value = RiskLevelOverall.High,
            Confidence = 0.95
        };

        var result = _validator.Validate(extraction);

        result.Errors.Should().Contain(e =>
            e.Field == "RiskAssessment.SafetyPlanStatus" &&
            e.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void Validate_HomeworkAssignedWithNotAssignedStatus_ReturnsWarning()
    {
        var extraction = CreateValidExtraction();
        extraction.Interventions.HomeworkCompletion = new ExtractedField<HomeworkCompletion>
        {
            Value = HomeworkCompletion.NotAssigned,
            Confidence = 0.90
        };
        extraction.Interventions.HomeworkAssigned = new ExtractedField<string>
        {
            Value = "Complete thought record",
            Confidence = 0.90
        };

        var result = _validator.Validate(extraction);

        result.Errors.Should().Contain(e =>
            e.Field == "Interventions.HomeworkAssigned" &&
            e.Severity == ValidationSeverity.Warning);
    }

    private static ClinicalExtraction CreateValidExtraction()
    {
        return new ClinicalExtraction
        {
            SessionInfo = new SessionInfoExtracted
            {
                SessionDate = new ExtractedField<DateOnly>
                {
                    Value = new DateOnly(2024, 1, 15),
                    Confidence = 0.95
                },
                SessionDurationMinutes = new ExtractedField<int>
                {
                    Value = 50,
                    Confidence = 0.90
                },
                SessionType = new ExtractedField<SessionType>
                {
                    Value = SessionType.Individual,
                    Confidence = 0.90
                }
            },
            RiskAssessment = new RiskAssessmentExtracted
            {
                SuicidalIdeation = new ExtractedField<SuicidalIdeation>
                {
                    Value = SuicidalIdeation.None,
                    Confidence = 0.95
                },
                SelfHarm = new ExtractedField<SelfHarm>
                {
                    Value = SelfHarm.None,
                    Confidence = 0.95
                },
                HomicidalIdeation = new ExtractedField<HomicidalIdeation>
                {
                    Value = HomicidalIdeation.None,
                    Confidence = 0.95
                },
                RiskLevelOverall = new ExtractedField<RiskLevelOverall>
                {
                    Value = RiskLevelOverall.Low,
                    Confidence = 0.95
                }
            },
            MoodAssessment = new MoodAssessmentExtracted
            {
                SelfReportedMood = new ExtractedField<int>
                {
                    Value = 7,
                    Confidence = 0.90
                }
            },
            Interventions = new InterventionsExtracted(),
            PresentingConcerns = new PresentingConcernsExtracted(),
            MentalStatusExam = new MentalStatusExamExtracted(),
            Diagnoses = new DiagnosesExtracted(),
            TreatmentProgress = new TreatmentProgressExtracted(),
            NextSteps = new NextStepsExtracted()
        };
    }
}
