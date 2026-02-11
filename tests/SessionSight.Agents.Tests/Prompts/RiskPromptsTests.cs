using FluentAssertions;
using SessionSight.Agents.Prompts;

namespace SessionSight.Agents.Tests.Prompts;

public class RiskPromptsTests
{
    [Fact]
    public void SystemPrompt_IsNotEmpty()
    {
        RiskPrompts.SystemPrompt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SystemPrompt_ContainsSafetyRules()
    {
        RiskPrompts.SystemPrompt.Should().Contain("CRITICAL SAFETY RULES");
        RiskPrompts.SystemPrompt.Should().Contain("MORE CONCERNING");
    }

    [Fact]
    public void SystemPrompt_MentionsSuicidalIdeation()
    {
        RiskPrompts.SystemPrompt.Should().Contain("suicidal ideation");
    }

    [Fact]
    public void SystemPrompt_MentionsSelfHarm()
    {
        RiskPrompts.SystemPrompt.Should().Contain("self-harm");
    }

    [Fact]
    public void SystemPrompt_MentionsHomicidalIdeation()
    {
        RiskPrompts.SystemPrompt.Should().Contain("homicidal ideation");
    }

    [Fact]
    public void SystemPrompt_RequiresCompleteCriteriaUsedObject()
    {
        RiskPrompts.SystemPrompt.Should().Contain("criteria_used");
        RiskPrompts.SystemPrompt.Should().Contain("reasoning_used");
        RiskPrompts.SystemPrompt.Should().Contain("must include all five keys");
        RiskPrompts.SystemPrompt.Should().Contain("insufficient_evidence");
    }

    [Fact]
    public void GetRiskReExtractionPrompt_IncludesNoteText()
    {
        var noteText = "Patient reports feeling anxious.";

        var prompt = RiskPrompts.GetRiskReExtractionPrompt(noteText);

        prompt.Should().Contain(noteText);
    }

    [Fact]
    public void GetRiskReExtractionPrompt_ContainsSafetyWarning()
    {
        var prompt = RiskPrompts.GetRiskReExtractionPrompt("Test note");

        prompt.Should().Contain("SAFETY-CRITICAL");
    }

    [Fact]
    public void GetRiskReExtractionPrompt_ContainsFieldDefinitions()
    {
        var prompt = RiskPrompts.GetRiskReExtractionPrompt("Test note");

        prompt.Should().Contain("suicidalIdeation");
        prompt.Should().Contain("selfHarm");
        prompt.Should().Contain("homicidalIdeation");
        prompt.Should().Contain("riskLevelOverall");
    }

    [Fact]
    public void GetRiskReExtractionPrompt_ContainsRiskLevelDefinitions()
    {
        var prompt = RiskPrompts.GetRiskReExtractionPrompt("Test note");

        prompt.Should().Contain("Low");
        prompt.Should().Contain("Moderate");
        prompt.Should().Contain("High");
        prompt.Should().Contain("Imminent");
    }

    [Fact]
    public void GetRiskReExtractionPrompt_ContainsSuicidalIdeationLevels()
    {
        var prompt = RiskPrompts.GetRiskReExtractionPrompt("Test note");

        prompt.Should().Contain("None");
        prompt.Should().Contain("Passive");
        prompt.Should().Contain("ActiveNoPlan");
        prompt.Should().Contain("ActiveWithPlan");
        prompt.Should().Contain("ActiveWithIntent");
    }

    [Fact]
    public void SystemPrompt_ContainsGeneratedSchema()
    {
        // Schema moved from user prompt to system prompt via RiskSchemaGenerator
        RiskPrompts.SystemPrompt.Should().Contain("\"value\":");
        RiskPrompts.SystemPrompt.Should().Contain("\"confidence\":");
        RiskPrompts.SystemPrompt.Should().Contain("\"source\":");
        RiskPrompts.SystemPrompt.Should().Contain("\"suicidalIdeation\"");
        RiskPrompts.SystemPrompt.Should().Contain("\"riskLevelOverall\"");
    }

    [Fact]
    public void GetRiskReExtractionPrompt_ReferencesSystemSchema()
    {
        var prompt = RiskPrompts.GetRiskReExtractionPrompt("Test note");

        prompt.Should().Contain("Return ONLY the JSON object matching the schema in the system prompt");
    }

    [Fact]
    public void GetRiskReExtractionPrompt_ContainsProtectiveFactors()
    {
        var prompt = RiskPrompts.GetRiskReExtractionPrompt("Test note");

        prompt.Should().Contain("protectiveFactors");
        prompt.Should().Contain("riskFactors");
    }

    [Fact]
    public void GetRiskReExtractionPrompt_MentionsConfidenceThreshold()
    {
        var prompt = RiskPrompts.GetRiskReExtractionPrompt("Test note");

        prompt.Should().Contain("0.90");
    }

    [Fact]
    public void GetRiskReExtractionPrompt_ContainsSiFrequencyInferenceRule()
    {
        var prompt = RiskPrompts.GetRiskReExtractionPrompt("Test note");

        prompt.Should().Contain("infer at least Occasional");
    }

    [Fact]
    public void GetRiskReExtractionPrompt_RequiresNonEmptyCriteriaUsedValues()
    {
        var prompt = RiskPrompts.GetRiskReExtractionPrompt("Test note");

        prompt.Should().Contain("must include all 5 keys");
        prompt.Should().Contain("at least one non-empty label");
        prompt.Should().Contain("reasoning_used");
        prompt.Should().Contain("non-empty freeform explanation");
        prompt.Should().Contain("insufficient_evidence");
    }

    [Fact]
    public void GetRiskReExtractionPrompt_ContainsEuphemisticLanguageRule()
    {
        var prompt = RiskPrompts.GetRiskReExtractionPrompt("Test note");

        prompt.Should().Contain("Euphemistic or indirect language");
        prompt.Should().Contain("go to sleep and not wake up");
        prompt.Should().Contain("ActiveNoPlan");
    }
}
