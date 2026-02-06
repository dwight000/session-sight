using FluentAssertions;
using SessionSight.Agents.Prompts;

namespace SessionSight.Agents.Tests.Prompts;

public class RiskSchemaGeneratorTests
{
    [Fact]
    public void Generate_IncludesAllRiskFields()
    {
        var schema = RiskSchemaGenerator.Generate();

        schema.Should().Contain("\"suicidalIdeation\"");
        schema.Should().Contain("\"siFrequency\"");
        schema.Should().Contain("\"siIntensity\"");
        schema.Should().Contain("\"selfHarm\"");
        schema.Should().Contain("\"shRecency\"");
        schema.Should().Contain("\"homicidalIdeation\"");
        schema.Should().Contain("\"hiTarget\"");
        schema.Should().Contain("\"safetyPlanStatus\"");
        schema.Should().Contain("\"protectiveFactors\"");
        schema.Should().Contain("\"riskFactors\"");
        schema.Should().Contain("\"meansRestrictionDiscussed\"");
        schema.Should().Contain("\"riskLevelOverall\"");
    }

    [Fact]
    public void Generate_IncludesEnumValues()
    {
        var schema = RiskSchemaGenerator.Generate();

        // SuicidalIdeation enum values
        schema.Should().Contain("None");
        schema.Should().Contain("Passive");
        schema.Should().Contain("ActiveNoPlan");
        schema.Should().Contain("ActiveWithPlan");
        schema.Should().Contain("ActiveWithIntent");

        // SelfHarm enum values
        schema.Should().Contain("Historical");
        schema.Should().Contain("Current");
        schema.Should().Contain("Imminent");

        // RiskLevelOverall enum values
        schema.Should().Contain("Low");
        schema.Should().Contain("Moderate");
        schema.Should().Contain("High");
    }

    [Fact]
    public void Generate_IsCached()
    {
        RiskSchemaGenerator.ClearCache();

        var first = RiskSchemaGenerator.Generate();
        var second = RiskSchemaGenerator.Generate();

        ReferenceEquals(first, second).Should().BeTrue("Schema should be cached and return same instance");
    }

    [Fact]
    public void Generate_IncludesExtractedFieldStructure()
    {
        var schema = RiskSchemaGenerator.Generate();

        // Every field should have the ExtractedField structure
        schema.Should().Contain("\"confidence\"");
        schema.Should().Contain("\"source\"");
    }

    [Fact]
    public void Generate_IncludesListTypes()
    {
        var schema = RiskSchemaGenerator.Generate();

        // protectiveFactors and riskFactors are List<string>
        schema.Should().Contain("[\"string\"]");
    }

    [Fact]
    public void Generate_IncludesBoolType()
    {
        var schema = RiskSchemaGenerator.Generate();

        // meansRestrictionDiscussed is bool
        schema.Should().Contain("false");
    }
}
