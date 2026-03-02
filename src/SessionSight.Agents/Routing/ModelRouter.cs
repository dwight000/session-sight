namespace SessionSight.Agents.Routing;

public class ModelRouter : IModelRouter
{
    // Current tiering:
    // - gpt-4.1-mini for extraction, risk assessment, and complex Q&A
    // - gpt-4.1-nano for lowest-cost tasks (intake, summarization, simple Q&A)
    // - Mistral-Large-3 on AIServices for debate challenger (cross-model diversity)
    public const string Gpt41 = "gpt-4.1";
    public const string Gpt41Mini = "gpt-4.1-mini";
    public const string Gpt41Nano = "gpt-4.1-nano";
    public const string Embedding = "text-embedding-3-large";
    public const string MistralLarge3 = "Mistral-Large-3";

    public ModelSelection SelectModel(ModelTask task) => task switch
    {
        ModelTask.DocumentIntake    => new(Gpt41Nano,    ModelProvider.AzureOpenAI),
        ModelTask.Extraction        => new(Gpt41Mini,    ModelProvider.AzureOpenAI),
        ModelTask.ExtractionSimple  => new(Gpt41Nano,    ModelProvider.AzureOpenAI),
        ModelTask.RiskAssessment    => new(Gpt41Mini,    ModelProvider.AzureOpenAI),
        ModelTask.Summarization     => new(Gpt41Nano,    ModelProvider.AzureOpenAI),
        ModelTask.Embedding         => new(Embedding,    ModelProvider.AzureOpenAI),
        ModelTask.QASimple          => new(Gpt41Nano,    ModelProvider.AzureOpenAI),
        ModelTask.QAComplex         => new(Gpt41Mini,    ModelProvider.AzureOpenAI),
        ModelTask.RiskDebateAdvocate   => new(Gpt41Nano,    ModelProvider.AzureOpenAI),
        ModelTask.RiskDebateChallenger => new(MistralLarge3, ModelProvider.AzureAIServices),
        ModelTask.RiskDebateJudge      => new(Gpt41Mini,    ModelProvider.AzureOpenAI),
        _ => new(Gpt41Mini, ModelProvider.AzureOpenAI)
    };
}
