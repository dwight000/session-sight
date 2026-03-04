using FluentAssertions;
using SessionSight.Agents.Routing;

namespace SessionSight.Agents.Tests.Routing;

public class ModelRouterTests
{
    private readonly ModelRouter _router = new();

    [Theory]
    [InlineData(ModelTask.DocumentIntake, ModelRouter.Gpt41Nano, ModelProvider.AzureOpenAI)]
    [InlineData(ModelTask.Extraction, ModelRouter.Gpt41Mini, ModelProvider.AzureOpenAI)]
    [InlineData(ModelTask.ExtractionSimple, ModelRouter.Gpt41Nano, ModelProvider.AzureOpenAI)]
    [InlineData(ModelTask.RiskAssessment, ModelRouter.Gpt41Mini, ModelProvider.AzureOpenAI)]
    [InlineData(ModelTask.Summarization, ModelRouter.Gpt41Nano, ModelProvider.AzureOpenAI)]
    [InlineData(ModelTask.Embedding, ModelRouter.Embedding, ModelProvider.AzureOpenAI)]
    [InlineData(ModelTask.QASimple, ModelRouter.Gpt41Nano, ModelProvider.AzureOpenAI)]
    [InlineData(ModelTask.QAComplex, ModelRouter.Gpt41Mini, ModelProvider.AzureOpenAI)]
    [InlineData(ModelTask.RiskDebateAdvocate, ModelRouter.Gpt41Nano, ModelProvider.AzureOpenAI)]
    [InlineData(ModelTask.RiskDebateChallenger, ModelRouter.MistralLarge3, ModelProvider.AzureAIServices)]
    [InlineData(ModelTask.RiskDebateJudge, ModelRouter.Gpt41Mini, ModelProvider.AzureOpenAI)]
    public void SelectModel_ReturnsCorrectModelAndProvider(ModelTask task, string expectedDeployment, ModelProvider expectedProvider)
    {
        var result = _router.SelectModel(task);
        result.DeploymentName.Should().Be(expectedDeployment);
        result.Provider.Should().Be(expectedProvider);
    }

    [Fact]
    public void SelectModel_UnknownTask_DefaultsToGpt41Mini()
    {
        var result = _router.SelectModel((ModelTask)999);
        result.DeploymentName.Should().Be(ModelRouter.Gpt41Mini);
        result.Provider.Should().Be(ModelProvider.AzureOpenAI);
    }

    [Fact]
    public void ModelConstants_HaveCorrectValues()
    {
        ModelRouter.Gpt41.Should().Be("gpt-4.1");
        ModelRouter.Gpt41Mini.Should().Be("gpt-4.1-mini");
        ModelRouter.Gpt41Nano.Should().Be("gpt-4.1-nano");
        ModelRouter.Embedding.Should().Be("text-embedding-3-large");
        ModelRouter.MistralLarge3.Should().Be("Mistral-Large-3");
    }

    [Fact]
    public void SelectModel_RiskDebateChallenger_UsesAIServicesProvider()
    {
        var result = _router.SelectModel(ModelTask.RiskDebateChallenger);
        result.Provider.Should().Be(ModelProvider.AzureAIServices);
        result.DeploymentName.Should().Be(ModelRouter.MistralLarge3);
    }
}
