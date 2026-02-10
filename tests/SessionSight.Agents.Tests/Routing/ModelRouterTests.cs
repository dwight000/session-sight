using FluentAssertions;
using SessionSight.Agents.Routing;

namespace SessionSight.Agents.Tests.Routing;

public class ModelRouterTests
{
    private readonly ModelRouter _router = new();

    [Theory]
    [InlineData(ModelTask.DocumentIntake, ModelRouter.Gpt41Nano)]
    [InlineData(ModelTask.Extraction, ModelRouter.Gpt41)]
    [InlineData(ModelTask.ExtractionSimple, ModelRouter.Gpt41Nano)]
    [InlineData(ModelTask.RiskAssessment, ModelRouter.Gpt41Mini)]
    [InlineData(ModelTask.Summarization, ModelRouter.Gpt41Nano)]
    [InlineData(ModelTask.Embedding, ModelRouter.Embedding)]
    [InlineData(ModelTask.QASimple, ModelRouter.Gpt41Nano)]
    [InlineData(ModelTask.QAComplex, ModelRouter.Gpt41Mini)]
    public void SelectModel_ReturnsCorrectModel(ModelTask task, string expected)
    {
        var result = _router.SelectModel(task);
        result.Should().Be(expected);
    }

    [Fact]
    public void SelectModel_UnknownTask_DefaultsToGpt41Mini()
    {
        var result = _router.SelectModel((ModelTask)999);
        result.Should().Be(ModelRouter.Gpt41Mini);
    }

    [Fact]
    public void ModelConstants_HaveCorrectValues()
    {
        ModelRouter.Gpt41.Should().Be("gpt-4.1");
        ModelRouter.Gpt41Mini.Should().Be("gpt-4.1-mini");
        ModelRouter.Gpt41Nano.Should().Be("gpt-4.1-nano");
        ModelRouter.Embedding.Should().Be("text-embedding-3-large");
    }
}
