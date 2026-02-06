namespace SessionSight.Agents.Routing;

public class ModelRouter : IModelRouter
{
    public const string Gpt4o = "gpt-4o";
    public const string Gpt4oMini = "gpt-4o-mini";
    public const string Embedding = "text-embedding-3-large";

    public string SelectModel(ModelTask task) => task switch
    {
        ModelTask.DocumentIntake => Gpt4oMini,
        ModelTask.Extraction => Gpt4o,
        ModelTask.ExtractionSimple => Gpt4oMini,
        ModelTask.RiskAssessment => Gpt4o,
        ModelTask.Summarization => Gpt4oMini,
        ModelTask.Embedding => Embedding,
        ModelTask.QASimple => Gpt4oMini,
        ModelTask.QAComplex => Gpt4o,
        _ => Gpt4o
    };
}
