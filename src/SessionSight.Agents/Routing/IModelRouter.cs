namespace SessionSight.Agents.Routing;

public interface IModelRouter
{
    string SelectModel(ModelTask task);
}

public enum ModelTask
{
    Extraction,      // gpt-4o
    Summarization,   // gpt-4o-mini
    Embedding,       // text-embedding-3-large
    RiskAssessment   // gpt-4o
}
