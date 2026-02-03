using FluentAssertions;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;
using SessionSight.Core.Enums;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Tests.Agents;

public class RiskAssessorAgentTests
{
    [Fact]
    public void ExtractJson_PlainJson_ReturnsAsIs()
    {
        var json = """{"suicidalIdeation": {"value": "None", "confidence": 0.95}}""";
        var result = RiskAssessorAgent.ExtractJson(json);
        result.Should().Be(json);
    }

    [Fact]
    public void ExtractJson_MarkdownCodeBlock_ExtractsJson()
    {
        var input = """
            ```json
            {"suicidalIdeation": {"value": "None", "confidence": 0.95}}
            ```
            """;
        var result = RiskAssessorAgent.ExtractJson(input);
        result.Should().Be("""{"suicidalIdeation": {"value": "None", "confidence": 0.95}}""");
    }

    [Fact]
    public void ParseRiskResponse_ValidJson_ReturnsExtraction()
    {
        var json = """
            {
                "suicidalIdeation": {"value": "ActiveNoPlan", "confidence": 0.92, "source": {"text": "reports having suicidal thoughts", "section": "risk"}},
                "selfHarm": {"value": "Historical", "confidence": 0.90, "source": {"text": "history of cutting in adolescence", "section": "history"}},
                "homicidalIdeation": {"value": "None", "confidence": 0.95, "source": {"text": "denies HI", "section": "risk"}},
                "riskLevelOverall": {"value": "Moderate", "confidence": 0.90, "source": {"text": "moderate risk", "section": "assessment"}}
            }
            """;

        var result = RiskAssessorAgent.ParseRiskResponse(json);

        result.Should().NotBeNull();
        result!.SuicidalIdeation.Value.Should().Be(SuicidalIdeation.ActiveNoPlan);
        result.SuicidalIdeation.Confidence.Should().Be(0.92);
        result.SelfHarm.Value.Should().Be(SelfHarm.Historical);
        result.HomicidalIdeation.Value.Should().Be(HomicidalIdeation.None);
        result.RiskLevelOverall.Value.Should().Be(RiskLevelOverall.Moderate);
    }

    [Fact]
    public void ParseRiskResponse_MalformedJson_ReturnsNull()
    {
        var badJson = "not valid json at all";

        var result = RiskAssessorAgent.ParseRiskResponse(badJson);

        result.Should().BeNull();
    }

    [Fact]
    public void ParseRiskResponse_ListFields_ParsesCorrectly()
    {
        var json = """
            {
                "protectiveFactors": {"value": ["supportive family", "employment", "children"], "confidence": 0.85},
                "riskFactors": {"value": ["recent loss", "isolation"], "confidence": 0.90}
            }
            """;

        var result = RiskAssessorAgent.ParseRiskResponse(json);

        result.Should().NotBeNull();
        result!.ProtectiveFactors.Value.Should().HaveCount(3);
        result.ProtectiveFactors.Value.Should().Contain("supportive family");
        result.RiskFactors.Value.Should().HaveCount(2);
    }

    [Fact]
    public void FindDiscrepancies_SameValues_ReturnsEmpty()
    {
        var original = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.95 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.95 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.95 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.95 }
        };
        var reExtracted = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.93 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.93 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.93 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.93 }
        };

        var discrepancies = RiskAssessorAgent.FindDiscrepancies(original, reExtracted);

        discrepancies.Should().BeEmpty();
    }

    [Fact]
    public void FindDiscrepancies_DifferentValues_ReturnsDiscrepancies()
    {
        var original = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.95 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.95 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.95 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.95 }
        };
        var reExtracted = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.Passive, Confidence = 0.90 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.93 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.93 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Moderate, Confidence = 0.90 }
        };

        var discrepancies = RiskAssessorAgent.FindDiscrepancies(original, reExtracted);

        discrepancies.Should().HaveCount(2);
        discrepancies.Should().Contain(d => d.FieldName == "SuicidalIdeation");
        discrepancies.Should().Contain(d => d.FieldName == "RiskLevelOverall");
    }

    [Fact]
    public void SelectMoreSevere_SuicidalIdeation_ReturnsMoreSevere()
    {
        var result = RiskAssessorAgent.SelectMoreSevere("SuicidalIdeation", "None", "ActiveWithPlan");
        result.Should().Be("ActiveWithPlan");

        result = RiskAssessorAgent.SelectMoreSevere("SuicidalIdeation", "ActiveWithIntent", "Passive");
        result.Should().Be("ActiveWithIntent");
    }

    [Fact]
    public void SelectMoreSevere_SelfHarm_ReturnsMoreSevere()
    {
        var result = RiskAssessorAgent.SelectMoreSevere("SelfHarm", "Historical", "Current");
        result.Should().Be("Current");

        result = RiskAssessorAgent.SelectMoreSevere("SelfHarm", "Imminent", "None");
        result.Should().Be("Imminent");
    }

    [Fact]
    public void SelectMoreSevere_HomicidalIdeation_ReturnsMoreSevere()
    {
        var result = RiskAssessorAgent.SelectMoreSevere("HomicidalIdeation", "None", "ActiveWithPlan");
        result.Should().Be("ActiveWithPlan");
    }

    [Fact]
    public void SelectMoreSevere_RiskLevelOverall_ReturnsMoreSevere()
    {
        var result = RiskAssessorAgent.SelectMoreSevere("RiskLevelOverall", "Low", "High");
        result.Should().Be("High");

        result = RiskAssessorAgent.SelectMoreSevere("RiskLevelOverall", "Imminent", "Moderate");
        result.Should().Be("Imminent");
    }

    [Fact]
    public void GetSeverityScore_SuicidalIdeation_ReturnsCorrectScores()
    {
        RiskAssessorAgent.GetSeverityScore("SuicidalIdeation", "None").Should().Be(0);
        RiskAssessorAgent.GetSeverityScore("SuicidalIdeation", "Passive").Should().Be(1);
        RiskAssessorAgent.GetSeverityScore("SuicidalIdeation", "ActiveNoPlan").Should().Be(2);
        RiskAssessorAgent.GetSeverityScore("SuicidalIdeation", "ActiveWithPlan").Should().Be(3);
        RiskAssessorAgent.GetSeverityScore("SuicidalIdeation", "ActiveWithIntent").Should().Be(4);
    }

    [Fact]
    public void GetSeverityScore_SelfHarm_ReturnsCorrectScores()
    {
        RiskAssessorAgent.GetSeverityScore("SelfHarm", "None").Should().Be(0);
        RiskAssessorAgent.GetSeverityScore("SelfHarm", "Historical").Should().Be(1);
        RiskAssessorAgent.GetSeverityScore("SelfHarm", "Recent").Should().Be(2);
        RiskAssessorAgent.GetSeverityScore("SelfHarm", "Current").Should().Be(3);
        RiskAssessorAgent.GetSeverityScore("SelfHarm", "Imminent").Should().Be(4);
    }

    [Fact]
    public void GetSeverityScore_RiskLevelOverall_ReturnsCorrectScores()
    {
        RiskAssessorAgent.GetSeverityScore("RiskLevelOverall", "Low").Should().Be(0);
        RiskAssessorAgent.GetSeverityScore("RiskLevelOverall", "Moderate").Should().Be(1);
        RiskAssessorAgent.GetSeverityScore("RiskLevelOverall", "High").Should().Be(2);
        RiskAssessorAgent.GetSeverityScore("RiskLevelOverall", "Imminent").Should().Be(3);
    }

    [Fact]
    public void ConservativeMerge_MoreSevereInReExtracted_SelectsReExtracted()
    {
        var original = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.95 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.95 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.95 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.95 }
        };
        var reExtracted = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.ActiveNoPlan, Confidence = 0.92 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.Historical, Confidence = 0.90 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.93 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Moderate, Confidence = 0.90 }
        };

        var merged = RiskAssessorAgent.ConservativeMerge(original, reExtracted);

        merged.SuicidalIdeation.Value.Should().Be(SuicidalIdeation.ActiveNoPlan);
        merged.SelfHarm.Value.Should().Be(SelfHarm.Historical);
        merged.RiskLevelOverall.Value.Should().Be(RiskLevelOverall.Moderate);
    }

    [Fact]
    public void ConservativeMerge_MoreSevereInOriginal_SelectsOriginal()
    {
        var original = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.ActiveWithPlan, Confidence = 0.95 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.Current, Confidence = 0.95 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.ActiveWithPlan, Confidence = 0.95 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.High, Confidence = 0.95 }
        };
        var reExtracted = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.Passive, Confidence = 0.92 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.90 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.93 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.90 }
        };

        var merged = RiskAssessorAgent.ConservativeMerge(original, reExtracted);

        merged.SuicidalIdeation.Value.Should().Be(SuicidalIdeation.ActiveWithPlan);
        merged.SelfHarm.Value.Should().Be(SelfHarm.Current);
        merged.HomicidalIdeation.Value.Should().Be(HomicidalIdeation.ActiveWithPlan);
        merged.RiskLevelOverall.Value.Should().Be(RiskLevelOverall.High);
    }

    [Fact]
    public void ConservativeMerge_CombinesListFields()
    {
        var original = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.95 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.95 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.95 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.95 },
            ProtectiveFactors = new ExtractedField<List<string>> { Value = new List<string> { "family" }, Confidence = 0.85 },
            RiskFactors = new ExtractedField<List<string>> { Value = new List<string> { "isolation" }, Confidence = 0.85 }
        };
        var reExtracted = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.93 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.93 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.93 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.93 },
            ProtectiveFactors = new ExtractedField<List<string>> { Value = new List<string> { "employment", "family" }, Confidence = 0.88 },
            RiskFactors = new ExtractedField<List<string>> { Value = new List<string> { "recent loss" }, Confidence = 0.88 }
        };

        var merged = RiskAssessorAgent.ConservativeMerge(original, reExtracted);

        merged.ProtectiveFactors.Value.Should().Contain("family");
        merged.ProtectiveFactors.Value.Should().Contain("employment");
        merged.RiskFactors.Value.Should().Contain("isolation");
        merged.RiskFactors.Value.Should().Contain("recent loss");
    }

    [Fact]
    public void ConservativeMerge_MeansRestriction_TrueIfEither()
    {
        var original = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.95 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.95 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.95 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.95 },
            MeansRestrictionDiscussed = new ExtractedField<bool> { Value = true, Confidence = 0.90 }
        };
        var reExtracted = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.93 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.93 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.93 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.93 },
            MeansRestrictionDiscussed = new ExtractedField<bool> { Value = false, Confidence = 0.85 }
        };

        var merged = RiskAssessorAgent.ConservativeMerge(original, reExtracted);

        merged.MeansRestrictionDiscussed.Value.Should().BeTrue();
    }

    [Fact]
    public void FindDiscrepancies_ResolvedValueIsMoreSevere()
    {
        var original = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.Passive, Confidence = 0.90 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.95 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.95 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.95 }
        };
        var reExtracted = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.ActiveWithPlan, Confidence = 0.92 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.93 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.93 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.93 }
        };

        var discrepancies = RiskAssessorAgent.FindDiscrepancies(original, reExtracted);

        discrepancies.Should().HaveCount(1);
        var disc = discrepancies.First();
        disc.FieldName.Should().Be("SuicidalIdeation");
        disc.OriginalValue.Should().Be("Passive");
        disc.ReExtractedValue.Should().Be("ActiveWithPlan");
        disc.ResolvedValue.Should().Be("ActiveWithPlan"); // More severe
        disc.ResolutionReason.Should().Contain("Conservative merge");
    }

    [Fact]
    public void GetSeverityScore_HomicidalIdeation_ReturnsCorrectScores()
    {
        RiskAssessorAgent.GetSeverityScore("HomicidalIdeation", "None").Should().Be(0);
        RiskAssessorAgent.GetSeverityScore("HomicidalIdeation", "Passive").Should().Be(1);
        RiskAssessorAgent.GetSeverityScore("HomicidalIdeation", "ActiveNoPlan").Should().Be(2);
        RiskAssessorAgent.GetSeverityScore("HomicidalIdeation", "ActiveWithPlan").Should().Be(3);
    }

    [Fact]
    public void GetSeverityScore_UnknownFieldName_ReturnsZero()
    {
        RiskAssessorAgent.GetSeverityScore("UnknownField", "value").Should().Be(0);
    }

    [Fact]
    public void GetSeverityScore_UnknownValue_ReturnsZero()
    {
        RiskAssessorAgent.GetSeverityScore("SuicidalIdeation", "UnknownValue").Should().Be(0);
        RiskAssessorAgent.GetSeverityScore("SelfHarm", "UnknownValue").Should().Be(0);
        RiskAssessorAgent.GetSeverityScore("HomicidalIdeation", "UnknownValue").Should().Be(0);
        RiskAssessorAgent.GetSeverityScore("RiskLevelOverall", "UnknownValue").Should().Be(0);
    }

    [Fact]
    public void ExtractJson_GenericCodeBlock_ExtractsContent()
    {
        var input = """
            ```
            {"suicidalIdeation": {"value": "None", "confidence": 0.95}}
            ```
            """;
        var result = RiskAssessorAgent.ExtractJson(input);
        result.Should().Be("""{"suicidalIdeation": {"value": "None", "confidence": 0.95}}""");
    }

    [Fact]
    public void ExtractJson_PlainText_ReturnsAsIs()
    {
        var input = "plain text content";
        var result = RiskAssessorAgent.ExtractJson(input);
        result.Should().Be("plain text content");
    }

    [Fact]
    public void ConservativeMerge_SiFrequency_MoreSevereWins()
    {
        var original = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.95 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.95 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.95 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.95 },
            SiFrequency = new ExtractedField<SiFrequency> { Value = SiFrequency.Rare, Confidence = 0.90 }
        };
        var reExtracted = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.93 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.93 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.93 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.93 },
            SiFrequency = new ExtractedField<SiFrequency> { Value = SiFrequency.Frequent, Confidence = 0.88 }
        };

        var merged = RiskAssessorAgent.ConservativeMerge(original, reExtracted);

        merged.SiFrequency.Value.Should().Be(SiFrequency.Frequent);
    }

    [Fact]
    public void ConservativeMerge_SiIntensity_MoreSevereWins()
    {
        var original = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.95 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.95 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.95 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.95 },
            SiIntensity = new ExtractedField<SiIntensity> { Value = SiIntensity.Mild, Confidence = 0.90 }
        };
        var reExtracted = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.93 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.93 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.93 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.93 },
            SiIntensity = new ExtractedField<SiIntensity> { Value = SiIntensity.Severe, Confidence = 0.88 }
        };

        var merged = RiskAssessorAgent.ConservativeMerge(original, reExtracted);

        merged.SiIntensity.Value.Should().Be(SiIntensity.Severe);
    }

    [Fact]
    public void ConservativeMerge_StringFields_PreferNonNull()
    {
        var original = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.95 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.95 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.95 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.95 },
            ShRecency = new ExtractedField<string> { Value = "3 months ago", Confidence = 0.85 }
        };
        var reExtracted = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.93 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.93 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.93 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.93 },
            ShRecency = new ExtractedField<string> { Value = null, Confidence = 0 }
        };

        var merged = RiskAssessorAgent.ConservativeMerge(original, reExtracted);

        merged.ShRecency.Value.Should().Be("3 months ago");
    }

    [Fact]
    public void ConservativeMerge_HomicidalTarget_PreferNonNull()
    {
        var original = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.95 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.95 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.95 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.95 },
            HiTarget = new ExtractedField<string> { Value = null, Confidence = 0 }
        };
        var reExtracted = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.93 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.93 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.93 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.93 },
            HiTarget = new ExtractedField<string> { Value = "coworker", Confidence = 0.80 }
        };

        var merged = RiskAssessorAgent.ConservativeMerge(original, reExtracted);

        merged.HiTarget.Value.Should().Be("coworker");
    }

    [Fact]
    public void ConservativeMerge_SafetyPlanStatus_PrefersHigherConfidence()
    {
        var original = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.95 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.95 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.95 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.95 },
            SafetyPlanStatus = new ExtractedField<SafetyPlanStatus> { Value = SafetyPlanStatus.NotNeeded, Confidence = 0.80 }
        };
        var reExtracted = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.93 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.93 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.93 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.93 },
            SafetyPlanStatus = new ExtractedField<SafetyPlanStatus> { Value = SafetyPlanStatus.InPlace, Confidence = 0.90 }
        };

        var merged = RiskAssessorAgent.ConservativeMerge(original, reExtracted);

        merged.SafetyPlanStatus.Value.Should().Be(SafetyPlanStatus.InPlace);
    }

    [Fact]
    public void ParseRiskResponse_WithSourceMapping_ParsesCorrectly()
    {
        var json = """
            {
                "suicidalIdeation": {
                    "value": "Passive",
                    "confidence": 0.92,
                    "source": {
                        "text": "Patient says they wish they wouldn't wake up",
                        "startChar": 100,
                        "endChar": 150,
                        "section": "risk assessment"
                    }
                }
            }
            """;

        var result = RiskAssessorAgent.ParseRiskResponse(json);

        result.Should().NotBeNull();
        result!.SuicidalIdeation.Value.Should().Be(SuicidalIdeation.Passive);
        result.SuicidalIdeation.Confidence.Should().Be(0.92);
        result.SuicidalIdeation.Source.Should().NotBeNull();
        result.SuicidalIdeation.Source!.Text.Should().Contain("Patient says");
        result.SuicidalIdeation.Source.Section.Should().Be("risk assessment");
    }

    [Fact]
    public void ParseRiskResponse_BoolField_ParsesCorrectly()
    {
        var json = """
            {
                "meansRestrictionDiscussed": {"value": true, "confidence": 0.90}
            }
            """;

        var result = RiskAssessorAgent.ParseRiskResponse(json);

        result.Should().NotBeNull();
        result!.MeansRestrictionDiscussed.Value.Should().BeTrue();
    }

    [Fact]
    public void ParseRiskResponse_EmptyJson_ReturnsEmptyExtraction()
    {
        var json = "{}";

        var result = RiskAssessorAgent.ParseRiskResponse(json);

        result.Should().NotBeNull();
    }

    [Fact]
    public void GetSeverityScore_ActiveWithIntent_IsHighestSuicidalSeverity()
    {
        var activeWithIntent = RiskAssessorAgent.GetSeverityScore("SuicidalIdeation", "ActiveWithIntent");
        var activeWithPlan = RiskAssessorAgent.GetSeverityScore("SuicidalIdeation", "ActiveWithPlan");
        activeWithIntent.Should().BeGreaterThan(activeWithPlan);
    }

    [Fact]
    public void GetSeverityScore_ImminentSelfHarm_IsHighestSelfHarmSeverity()
    {
        var imminent = RiskAssessorAgent.GetSeverityScore("SelfHarm", "Imminent");
        var current = RiskAssessorAgent.GetSeverityScore("SelfHarm", "Current");
        imminent.Should().BeGreaterThan(current);
    }

    [Fact]
    public void SelectMoreSevere_BothSameValue_ReturnsFirst()
    {
        var result = RiskAssessorAgent.SelectMoreSevere("SuicidalIdeation", "Passive", "Passive");
        result.Should().Be("Passive");
    }

    [Fact]
    public void SelectMoreSevere_UnknownField_ReturnsFirstValue()
    {
        var result = RiskAssessorAgent.SelectMoreSevere("UnknownField", "value1", "value2");
        result.Should().Be("value1");
    }

    [Fact]
    public void ParseRiskResponse_JsonInCodeBlock_ExtractsAndParses()
    {
        var wrappedJson = """
            ```json
            {
                "suicidalIdeation": {"value": "Passive", "confidence": 0.90}
            }
            ```
            """;

        var result = RiskAssessorAgent.ParseRiskResponse(wrappedJson);

        result.Should().NotBeNull();
        result!.SuicidalIdeation.Value.Should().Be(SuicidalIdeation.Passive);
    }

    [Fact]
    public void ConservativeMerge_NullLists_HandlesGracefully()
    {
        var original = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.95 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.95 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.95 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.95 },
            ProtectiveFactors = new ExtractedField<List<string>> { Value = null!, Confidence = 0 }
        };
        var reExtracted = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.93 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.93 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.93 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.93 },
            ProtectiveFactors = new ExtractedField<List<string>> { Value = new List<string> { "support" }, Confidence = 0.85 }
        };

        var merged = RiskAssessorAgent.ConservativeMerge(original, reExtracted);

        merged.ProtectiveFactors.Value.Should().Contain("support");
    }
}
