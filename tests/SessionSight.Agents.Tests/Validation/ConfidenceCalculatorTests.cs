using FluentAssertions;
using SessionSight.Agents.Validation;
using SessionSight.Core.Enums;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Tests.Validation;

public class ConfidenceCalculatorTests
{
    [Fact]
    public void Calculate_AllFieldsWithConfidence_ReturnsAverage()
    {
        var extraction = new ClinicalExtraction
        {
            SessionInfo = new SessionInfoExtracted
            {
                SessionDate = new ExtractedField<DateOnly>
                {
                    Value = new DateOnly(2024, 1, 15),
                    Confidence = 0.90
                },
                SessionType = new ExtractedField<SessionType>
                {
                    Value = SessionType.Individual,
                    Confidence = 0.80
                }
            },
            RiskAssessment = new RiskAssessmentExtracted
            {
                SuicidalIdeation = new ExtractedField<SuicidalIdeation>
                {
                    Value = SuicidalIdeation.None,
                    Confidence = 0.95
                }
            },
            MoodAssessment = new MoodAssessmentExtracted(),
            PresentingConcerns = new PresentingConcernsExtracted(),
            MentalStatusExam = new MentalStatusExamExtracted(),
            Interventions = new InterventionsExtracted(),
            Diagnoses = new DiagnosesExtracted(),
            TreatmentProgress = new TreatmentProgressExtracted(),
            NextSteps = new NextStepsExtracted()
        };

        var result = ConfidenceCalculator.Calculate(extraction);

        // Fields with non-default values: SessionDate (0.90), SessionType (0.80)
        // SuicidalIdeation.None is the default value and won't be counted
        // Average of 0.90, 0.80 = 0.85
        result.Should().BeApproximately(0.85, 0.01);
    }

    [Fact]
    public void Calculate_NoFieldsWithValues_ReturnsZero()
    {
        var extraction = new ClinicalExtraction();

        var result = ConfidenceCalculator.Calculate(extraction);

        result.Should().Be(0.0);
    }

    [Fact]
    public void HasLowConfidenceRiskFields_AllHighConfidence_ReturnsFalse()
    {
        var extraction = new ClinicalExtraction
        {
            RiskAssessment = new RiskAssessmentExtracted
            {
                SuicidalIdeation = new ExtractedField<SuicidalIdeation>
                {
                    Value = SuicidalIdeation.Passive,
                    Confidence = 0.95
                },
                SelfHarm = new ExtractedField<SelfHarm>
                {
                    Value = SelfHarm.Historical,
                    Confidence = 0.92
                },
                RiskLevelOverall = new ExtractedField<RiskLevelOverall>
                {
                    Value = RiskLevelOverall.Moderate,
                    Confidence = 0.90
                }
            }
        };

        var result = ConfidenceCalculator.HasLowConfidenceRiskFields(extraction);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasLowConfidenceRiskFields_LowConfidenceSI_ReturnsTrue()
    {
        var extraction = new ClinicalExtraction
        {
            RiskAssessment = new RiskAssessmentExtracted
            {
                SuicidalIdeation = new ExtractedField<SuicidalIdeation>
                {
                    Value = SuicidalIdeation.Passive,
                    Confidence = 0.75 // Below 0.9
                }
            }
        };

        var result = ConfidenceCalculator.HasLowConfidenceRiskFields(extraction);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasLowConfidenceRiskFields_LowConfidenceHighRisk_ReturnsTrue()
    {
        var extraction = new ClinicalExtraction
        {
            RiskAssessment = new RiskAssessmentExtracted
            {
                RiskLevelOverall = new ExtractedField<RiskLevelOverall>
                {
                    Value = RiskLevelOverall.High,
                    Confidence = 0.80 // Below 0.9
                }
            }
        };

        var result = ConfidenceCalculator.HasLowConfidenceRiskFields(extraction);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasLowConfidenceRiskFields_NoneValues_ReturnsFalse()
    {
        var extraction = new ClinicalExtraction
        {
            RiskAssessment = new RiskAssessmentExtracted
            {
                SuicidalIdeation = new ExtractedField<SuicidalIdeation>
                {
                    Value = SuicidalIdeation.None, // None values are not checked
                    Confidence = 0.50
                },
                SelfHarm = new ExtractedField<SelfHarm>
                {
                    Value = SelfHarm.None,
                    Confidence = 0.50
                }
            }
        };

        var result = ConfidenceCalculator.HasLowConfidenceRiskFields(extraction);

        result.Should().BeFalse();
    }

    [Fact]
    public void GetLowConfidenceFields_BelowThreshold_ReturnsFieldNames()
    {
        var extraction = new ClinicalExtraction
        {
            SessionInfo = new SessionInfoExtracted
            {
                SessionDate = new ExtractedField<DateOnly>
                {
                    Value = new DateOnly(2024, 1, 15),
                    Confidence = 0.95 // Above threshold
                },
                PatientId = new ExtractedField<string>
                {
                    Value = "P12345",
                    Confidence = 0.50 // Below threshold
                }
            },
            MoodAssessment = new MoodAssessmentExtracted
            {
                SelfReportedMood = new ExtractedField<int>
                {
                    Value = 6,
                    Confidence = 0.60 // Below threshold
                }
            },
            RiskAssessment = new RiskAssessmentExtracted(),
            PresentingConcerns = new PresentingConcernsExtracted(),
            MentalStatusExam = new MentalStatusExamExtracted(),
            Interventions = new InterventionsExtracted(),
            Diagnoses = new DiagnosesExtracted(),
            TreatmentProgress = new TreatmentProgressExtracted(),
            NextSteps = new NextStepsExtracted()
        };

        var result = ConfidenceCalculator.GetLowConfidenceFields(extraction);

        result.Should().Contain("SessionInfo.PatientId");
        result.Should().Contain("MoodAssessment.SelfReportedMood");
        result.Should().NotContain("SessionInfo.SessionDate");
    }

    [Fact]
    public void GetLowConfidenceFields_CustomThreshold_UsesThreshold()
    {
        var extraction = new ClinicalExtraction
        {
            SessionInfo = new SessionInfoExtracted
            {
                SessionDate = new ExtractedField<DateOnly>
                {
                    Value = new DateOnly(2024, 1, 15),
                    Confidence = 0.85 // Below 0.9 but above 0.7
                }
            },
            RiskAssessment = new RiskAssessmentExtracted(),
            MoodAssessment = new MoodAssessmentExtracted(),
            PresentingConcerns = new PresentingConcernsExtracted(),
            MentalStatusExam = new MentalStatusExamExtracted(),
            Interventions = new InterventionsExtracted(),
            Diagnoses = new DiagnosesExtracted(),
            TreatmentProgress = new TreatmentProgressExtracted(),
            NextSteps = new NextStepsExtracted()
        };

        var resultWithDefaultThreshold = ConfidenceCalculator.GetLowConfidenceFields(extraction, threshold: 0.7);
        var resultWithHighThreshold = ConfidenceCalculator.GetLowConfidenceFields(extraction, threshold: 0.9);

        resultWithDefaultThreshold.Should().NotContain("SessionInfo.SessionDate");
        resultWithHighThreshold.Should().Contain("SessionInfo.SessionDate");
    }

    [Fact]
    public void HasLowConfidenceRiskFields_LowConfidenceSelfHarm_ReturnsTrue()
    {
        var extraction = new ClinicalExtraction
        {
            RiskAssessment = new RiskAssessmentExtracted
            {
                SelfHarm = new ExtractedField<SelfHarm>
                {
                    Value = SelfHarm.Current,
                    Confidence = 0.70
                }
            }
        };

        ConfidenceCalculator.HasLowConfidenceRiskFields(extraction).Should().BeTrue();
    }

    [Fact]
    public void HasLowConfidenceRiskFields_LowConfidenceHomicidalIdeation_ReturnsTrue()
    {
        var extraction = new ClinicalExtraction
        {
            RiskAssessment = new RiskAssessmentExtracted
            {
                HomicidalIdeation = new ExtractedField<HomicidalIdeation>
                {
                    Value = HomicidalIdeation.ActiveNoPlan,
                    Confidence = 0.70
                }
            }
        };

        ConfidenceCalculator.HasLowConfidenceRiskFields(extraction).Should().BeTrue();
    }
}
