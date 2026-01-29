namespace SessionSight.Core.Schema;

public class DiagnosesExtracted
{
    public ExtractedField<string> PrimaryDiagnosis { get; set; } = new();
    public ExtractedField<string> PrimaryDiagnosisCode { get; set; } = new();
    public ExtractedField<List<string>> SecondaryDiagnoses { get; set; } = new();
    public ExtractedField<List<string>> SecondaryDiagnosisCodes { get; set; } = new();
    public ExtractedField<List<string>> RuleOuts { get; set; } = new();
    public ExtractedField<string> DiagnosisChanges { get; set; } = new();
}
