namespace SessionSight.Core.Schema;

public class ClinicalExtraction
{
    public SessionInfoExtracted SessionInfo { get; set; } = new();
    public PresentingConcernsExtracted PresentingConcerns { get; set; } = new();
    public MoodAssessmentExtracted MoodAssessment { get; set; } = new();
    public RiskAssessmentExtracted RiskAssessment { get; set; } = new();
    public MentalStatusExamExtracted MentalStatusExam { get; set; } = new();
    public InterventionsExtracted Interventions { get; set; } = new();
    public DiagnosesExtracted Diagnoses { get; set; } = new();
    public TreatmentProgressExtracted TreatmentProgress { get; set; } = new();
    public NextStepsExtracted NextSteps { get; set; } = new();
    public ExtractionMetadata Metadata { get; set; } = new();
}
