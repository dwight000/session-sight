using FluentAssertions;
using SessionSight.Agents.Routing;

namespace SessionSight.Agents.Tests.Routing;

public class ModelRouterTests
{
    private readonly ModelRouter _router = new();

    [Theory]
    [InlineData(ModelTask.DocumentIntake, ModelRouter.Gpt4oMini)]
    [InlineData(ModelTask.Extraction, ModelRouter.Gpt4o)]
    [InlineData(ModelTask.ExtractionSimple, ModelRouter.Gpt4oMini)]
    [InlineData(ModelTask.RiskAssessment, ModelRouter.Gpt4o)]
    [InlineData(ModelTask.Summarization, ModelRouter.Gpt4oMini)]
    [InlineData(ModelTask.Embedding, ModelRouter.Embedding)]
    [InlineData(ModelTask.QASimple, ModelRouter.Gpt4oMini)]
    [InlineData(ModelTask.QAComplex, ModelRouter.Gpt4o)]
    public void SelectModel_ReturnsCorrectModel(ModelTask task, string expected)
    {
        var result = _router.SelectModel(task);
        result.Should().Be(expected);
    }

    [Fact]
    public void SelectModel_UnknownTask_DefaultsToGpt4o()
    {
        var result = _router.SelectModel((ModelTask)999);
        result.Should().Be(ModelRouter.Gpt4o);
    }

    [Fact]
    public void ModelConstants_HaveCorrectValues()
    {
        ModelRouter.Gpt4o.Should().Be("gpt-4o");
        ModelRouter.Gpt4oMini.Should().Be("gpt-4o-mini");
        ModelRouter.Embedding.Should().Be("text-embedding-3-large");
    }
}
