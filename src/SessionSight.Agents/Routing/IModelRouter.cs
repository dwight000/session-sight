namespace SessionSight.Agents.Routing;

public interface IModelRouter
{
    ModelSelection SelectModel(ModelTask task);
}

public enum ModelProvider { AzureOpenAI, AzureAIServices }

public record ModelSelection(
    string DeploymentName,
    ModelProvider Provider,
    string? EndpointOverride = null);

public enum ModelTask
{
    DocumentIntake,        // gpt-4.1-nano
    Extraction,            // gpt-4.1-mini (complex clinical extraction)
    ExtractionSimple,      // gpt-4.1-nano (simple metadata extraction)
    Summarization,         // gpt-4.1-nano
    Embedding,             // text-embedding-3-large
    RiskAssessment,        // gpt-4.1-mini
    QASimple,              // gpt-4.1-nano (simple Q&A)
    QAComplex,             // gpt-4.1-mini (complex Q&A)
    RiskDebateAdvocate,    // gpt-4.1-nano  (OpenAI)
    RiskDebateChallenger,  // Mistral-Large-3 (AIServices)
    RiskDebateJudge        // gpt-4.1-mini  (OpenAI)
}
