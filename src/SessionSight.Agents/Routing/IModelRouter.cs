namespace SessionSight.Agents.Routing;

public interface IModelRouter
{
    string SelectModel(ModelTask task);
}

public enum ModelTask
{
    DocumentIntake,    // gpt-4o-mini
    Extraction,        // gpt-4o (complex clinical extraction)
    ExtractionSimple,  // gpt-4o-mini (simple metadata extraction)
    Summarization,     // gpt-4o-mini
    Embedding,         // text-embedding-3-large
    RiskAssessment,    // gpt-4o
    QASimple,          // gpt-4o-mini (simple Q&A)
    QAComplex          // gpt-4o (complex Q&A)
}
