namespace SessionSight.Agents.Routing;

public class ModelRouter : IModelRouter
{
    // Stepped down one tier for cost savings (Feb 2026 migration from deprecated gpt-4o)
    // gpt-4o tasks → gpt-4.1-mini, gpt-4o-mini tasks → gpt-4.1-nano
    public const string Gpt41Mini = "gpt-4.1-mini";
    public const string Gpt41Nano = "gpt-4.1-nano";
    public const string Embedding = "text-embedding-3-large";

    // Legacy constants for test compatibility
    public const string Gpt4o = Gpt41Mini;
    public const string Gpt4oMini = Gpt41Nano;

    public string SelectModel(ModelTask task) => task switch
    {
        ModelTask.DocumentIntake => Gpt41Nano,
        ModelTask.Extraction => Gpt41Mini,
        ModelTask.ExtractionSimple => Gpt41Nano,
        ModelTask.RiskAssessment => Gpt41Mini,
        ModelTask.Summarization => Gpt41Nano,
        ModelTask.Embedding => Embedding,
        ModelTask.QASimple => Gpt41Nano,
        ModelTask.QAComplex => Gpt41Mini,
        _ => Gpt41Mini
    };
}
