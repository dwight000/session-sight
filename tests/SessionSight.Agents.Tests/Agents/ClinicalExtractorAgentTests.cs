using FluentAssertions;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Prompts;
using SessionSight.Core.Enums;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Tests.Agents;

public class ClinicalExtractorAgentTests
{
    [Fact]
    public void ExtractJson_PlainJson_ReturnsAsIs()
    {
        var json = """{"patientId": {"value": "P12345", "confidence": 0.95}}""";
        var result = ClinicalExtractorAgent.ExtractJson(json);
        result.Should().Be(json);
    }

    [Fact]
    public void ExtractJson_MarkdownCodeBlock_ExtractsJson()
    {
        var input = """
            ```json
            {"patientId": {"value": "P12345", "confidence": 0.95}}
            ```
            """;
        var result = ClinicalExtractorAgent.ExtractJson(input);
        result.Should().Be("""{"patientId": {"value": "P12345", "confidence": 0.95}}""");
    }

    [Fact]
    public void ExtractJson_GenericCodeBlock_ExtractsContent()
    {
        var input = """
            ```
            {"sessionType": {"value": "Individual", "confidence": 0.90}}
            ```
            """;
        var result = ClinicalExtractorAgent.ExtractJson(input);
        result.Should().Be("""{"sessionType": {"value": "Individual", "confidence": 0.90}}""");
    }

    [Fact]
    public void ParseSectionResponse_ValidSessionInfo_ReturnsSection()
    {
        var json = """
            {
                "patientId": {"value": "P12345", "confidence": 0.95, "source": {"text": "Patient ID: P12345", "section": "header"}},
                "sessionDate": {"value": "2024-01-15", "confidence": 0.95, "source": {"text": "Date: January 15, 2024", "section": "header"}},
                "sessionType": {"value": "Individual", "confidence": 0.90, "source": {"text": "Individual therapy session", "section": "header"}},
                "sessionDurationMinutes": {"value": 50, "confidence": 0.85, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<SessionInfoExtracted>("SessionInfo", json);

        result.Should().NotBeNull();
        result.PatientId.Value.Should().Be("P12345");
        result.PatientId.Confidence.Should().Be(0.95);
        result.PatientId.Source.Should().NotBeNull();
        result.PatientId.Source!.Text.Should().Be("Patient ID: P12345");
        result.SessionDate.Value.Should().Be(new DateOnly(2024, 1, 15));
        result.SessionType.Value.Should().Be(SessionType.Individual);
        result.SessionDurationMinutes.Value.Should().Be(50);
    }

    [Fact]
    public void ParseSectionResponse_ValidRiskAssessment_ReturnsSection()
    {
        var json = """
            {
                "suicidalIdeation": {"value": "None", "confidence": 0.95, "source": {"text": "Denies SI", "section": "risk"}},
                "selfHarm": {"value": "Historical", "confidence": 0.90, "source": {"text": "History of self-harm in adolescence", "section": "risk"}},
                "homicidalIdeation": {"value": "None", "confidence": 0.95, "source": {"text": "Denies HI", "section": "risk"}},
                "riskLevelOverall": {"value": "Low", "confidence": 0.90, "source": {"text": "Overall risk assessed as low", "section": "risk"}},
                "protectiveFactors": {"value": ["supportive family", "stable employment"], "confidence": 0.85, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<RiskAssessmentExtracted>("RiskAssessment", json);

        result.Should().NotBeNull();
        result.SuicidalIdeation.Value.Should().Be(SuicidalIdeation.None);
        result.SuicidalIdeation.Confidence.Should().Be(0.95);
        result.SelfHarm.Value.Should().Be(SelfHarm.Historical);
        result.HomicidalIdeation.Value.Should().Be(HomicidalIdeation.None);
        result.RiskLevelOverall.Value.Should().Be(RiskLevelOverall.Low);
        result.ProtectiveFactors.Value.Should().HaveCount(2);
        result.ProtectiveFactors.Value.Should().Contain("supportive family");
    }

    [Fact]
    public void ParseSectionResponse_MalformedJson_ReturnsEmptySection()
    {
        var badJson = "not valid json at all";

        var result = ClinicalExtractorAgent.ParseSectionResponse<SessionInfoExtracted>("SessionInfo", badJson);

        result.Should().NotBeNull();
        result.PatientId.Value.Should().BeNull();
        result.PatientId.Confidence.Should().Be(0);
    }

    [Fact]
    public void ParseSectionResponse_NullValues_HandlesGracefully()
    {
        var json = """
            {
                "patientId": {"value": null, "confidence": 0.0, "source": null},
                "sessionDate": {"value": null, "confidence": 0.0, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<SessionInfoExtracted>("SessionInfo", json);

        result.Should().NotBeNull();
        result.PatientId.Value.Should().BeNull();
        result.SessionDate.Value.Should().Be(default);
    }

    [Fact]
    public void ParseSectionResponse_ListFields_ParsesCorrectly()
    {
        var json = """
            {
                "techniquesUsed": {"value": ["Cbt", "Mindfulness", "CognitiveRestructuring"], "confidence": 0.90, "source": null},
                "skillsTaught": {"value": ["deep breathing", "thought challenging"], "confidence": 0.85, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<InterventionsExtracted>("Interventions", json);

        result.Should().NotBeNull();
        result.TechniquesUsed.Value.Should().HaveCount(3);
        result.TechniquesUsed.Value.Should().Contain(TechniqueUsed.Cbt);
        result.TechniquesUsed.Value.Should().Contain(TechniqueUsed.Mindfulness);
        result.SkillsTaught.Value.Should().HaveCount(2);
        result.SkillsTaught.Value.Should().Contain("deep breathing");
    }

    [Fact]
    public void GetPromptForSection_AllSections_ReturnsPrompts()
    {
        var noteText = "Sample therapy note content";
        var sections = new[]
        {
            "SessionInfo", "PresentingConcerns", "MoodAssessment", "RiskAssessment",
            "MentalStatusExam", "Interventions", "Diagnoses", "TreatmentProgress", "NextSteps"
        };

        foreach (var section in sections)
        {
            var prompt = ClinicalExtractorAgent.GetPromptForSection(section, noteText);
            prompt.Should().NotBeNullOrEmpty($"Prompt for {section} should not be empty");
            prompt.Should().Contain(noteText, $"Prompt for {section} should contain note text");
        }
    }

    [Fact]
    public void GetPromptForSection_UnknownSection_ThrowsException()
    {
        var action = () => ClinicalExtractorAgent.GetPromptForSection("UnknownSection", "note text");
        action.Should().Throw<ArgumentException>().WithMessage("*Unknown section*");
    }

    [Fact]
    public void GetSessionInfoPrompt_ContainsFieldDescriptions()
    {
        var prompt = ExtractionPrompts.GetSessionInfoPrompt("Test note");

        prompt.Should().Contain("patientId");
        prompt.Should().Contain("sessionDate");
        prompt.Should().Contain("sessionType");
        prompt.Should().Contain("sessionModality");
        prompt.Should().Contain("Intake|Individual|Group|Family|Couples|Crisis|Assessment|Termination");
    }

    [Fact]
    public void GetRiskAssessmentPrompt_ContainsSafetyCriticalInstructions()
    {
        var prompt = ExtractionPrompts.GetRiskAssessmentPrompt("Test note");

        prompt.Should().Contain("safety-critical");
        prompt.Should().Contain("suicidalIdeation");
        prompt.Should().Contain("homicidalIdeation");
        prompt.Should().Contain("selfHarm");
        prompt.Should().Contain("riskLevelOverall");
        prompt.Should().Contain("0.90"); // Confidence threshold mentioned
    }

    [Fact]
    public void ParseSectionResponse_TimeOnlyField_ParsesCorrectly()
    {
        var json = """
            {
                "sessionStartTime": {"value": "14:30", "confidence": 0.90, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<SessionInfoExtracted>("SessionInfo", json);

        result.Should().NotBeNull();
        result.SessionStartTime.Value.Should().Be(new TimeOnly(14, 30));
    }

    [Fact]
    public void ParseSectionResponse_BoolField_ParsesCorrectly()
    {
        var json = """
            {
                "meansRestrictionDiscussed": {"value": true, "confidence": 0.95, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<RiskAssessmentExtracted>("RiskAssessment", json);

        result.Should().NotBeNull();
        result.MeansRestrictionDiscussed.Value.Should().BeTrue();
    }

    [Fact]
    public void ParseSectionResponse_IntField_ParsesCorrectly()
    {
        var json = """
            {
                "selfReportedMood": {"value": 7, "confidence": 0.85, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<MoodAssessmentExtracted>("MoodAssessment", json);

        result.Should().NotBeNull();
        result.SelfReportedMood.Value.Should().Be(7);
    }

    [Fact]
    public void ParseSectionResponse_EmptyStringList_ReturnsEmptyList()
    {
        var json = """
            {
                "protectiveFactors": {"value": [], "confidence": 0.50, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<RiskAssessmentExtracted>("RiskAssessment", json);

        result.Should().NotBeNull();
        result.ProtectiveFactors.Value.Should().BeEmpty();
    }

    [Fact]
    public void ParseSectionResponse_InvalidEnumValue_ReturnsNull()
    {
        var json = """
            {
                "sessionType": {"value": "InvalidType", "confidence": 0.90, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<SessionInfoExtracted>("SessionInfo", json);

        result.Should().NotBeNull();
        result.SessionType.Value.Should().Be(default);
    }

    [Fact]
    public void ParseSectionResponse_EmptyEnumList_ReturnsEmptyList()
    {
        var json = """
            {
                "techniquesUsed": {"value": [], "confidence": 0.50, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<InterventionsExtracted>("Interventions", json);

        result.Should().NotBeNull();
        result.TechniquesUsed.Value.Should().BeEmpty();
    }

    [Fact]
    public void ParseSectionResponse_DictionaryField_ParsesCorrectly()
    {
        var json = """
            {
                "goalProgress": {"value": {"Reduce anxiety": "Good progress", "Improve sleep": "Moderate"}, "confidence": 0.85, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<TreatmentProgressExtracted>("TreatmentProgress", json);

        result.Should().NotBeNull();
        result.GoalProgress.Value.Should().HaveCount(2);
        result.GoalProgress.Value.Should().ContainKey("Reduce anxiety");
        result.GoalProgress.Value["Reduce anxiety"].Should().Be("Good progress");
    }

    [Fact]
    public void ParseSectionResponse_InvalidDateFormat_ReturnsDefault()
    {
        var json = """
            {
                "sessionDate": {"value": "not-a-date", "confidence": 0.50, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<SessionInfoExtracted>("SessionInfo", json);

        result.Should().NotBeNull();
        result.SessionDate.Value.Should().Be(default);
    }

    [Fact]
    public void ParseSectionResponse_InvalidTimeFormat_ReturnsDefault()
    {
        var json = """
            {
                "sessionStartTime": {"value": "not-a-time", "confidence": 0.50, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<SessionInfoExtracted>("SessionInfo", json);

        result.Should().NotBeNull();
        result.SessionStartTime.Value.Should().Be(default);
    }

    [Fact]
    public void ParseSectionResponse_NonArrayForStringList_ReturnsEmptyList()
    {
        var json = """
            {
                "protectiveFactors": {"value": "not-an-array", "confidence": 0.50, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<RiskAssessmentExtracted>("RiskAssessment", json);

        result.Should().NotBeNull();
        result.ProtectiveFactors.Value.Should().BeEmpty();
    }

    [Fact]
    public void ParseSectionResponse_EmptyEnumString_ReturnsNull()
    {
        var json = """
            {
                "sessionType": {"value": "", "confidence": 0.50, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<SessionInfoExtracted>("SessionInfo", json);

        result.Should().NotBeNull();
        result.SessionType.Value.Should().Be(default);
    }

    [Fact]
    public void ParseSectionResponse_ValidRiskAssessment_ParsesAllFields()
    {
        var json = """
            {
                "suicidalIdeation": {"value": "Passive", "confidence": 0.92, "source": {"text": "Patient reports passive thoughts", "section": "risk"}},
                "selfHarm": {"value": "Historical", "confidence": 0.88, "source": null},
                "homicidalIdeation": {"value": "None", "confidence": 0.95, "source": null},
                "riskLevelOverall": {"value": "Moderate", "confidence": 0.85, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<RiskAssessmentExtracted>("RiskAssessment", json);

        result.Should().NotBeNull();
        result.SuicidalIdeation.Value.Should().Be(SuicidalIdeation.Passive);
        result.SuicidalIdeation.Confidence.Should().Be(0.92);
        result.SelfHarm.Value.Should().Be(SelfHarm.Historical);
        result.HomicidalIdeation.Value.Should().Be(HomicidalIdeation.None);
        result.RiskLevelOverall.Value.Should().Be(RiskLevelOverall.Moderate);
    }

    [Fact]
    public void ParseSectionResponse_BooleanField_ParsesCorrectly()
    {
        var json = """
            {
                "meansRestrictionDiscussed": {"value": true, "confidence": 0.90, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<RiskAssessmentExtracted>("RiskAssessment", json);

        result.Should().NotBeNull();
        result.MeansRestrictionDiscussed.Value.Should().BeTrue();
    }

    [Fact]
    public void ParseSectionResponse_TimeField_ParsesCorrectly()
    {
        var json = """
            {
                "sessionStartTime": {"value": "14:30:00", "confidence": 0.90, "source": null},
                "sessionEndTime": {"value": "15:20:00", "confidence": 0.85, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<SessionInfoExtracted>("SessionInfo", json);

        result.Should().NotBeNull();
        result.SessionStartTime.Value.Should().Be(new TimeOnly(14, 30, 0));
        result.SessionEndTime.Value.Should().Be(new TimeOnly(15, 20, 0));
    }

    [Fact]
    public void ParseSectionResponse_ModalityEnum_ParsesCorrectly()
    {
        var json = """
            {
                "sessionModality": {"value": "TelehealthVideo", "confidence": 0.92, "source": null}
            }
            """;

        var result = ClinicalExtractorAgent.ParseSectionResponse<SessionInfoExtracted>("SessionInfo", json);

        result.Should().NotBeNull();
        result.SessionModality.Value.Should().Be(SessionModality.TelehealthVideo);
    }
}
