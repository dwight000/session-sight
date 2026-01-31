using SessionSight.Core.Enums;

namespace SessionSight.Core.Schema;

public class PresentingConcernsExtracted
{
    public ExtractedField<string> PrimaryConcern { get; set; } = new();
    public ExtractedField<PrimaryConcernCategory> PrimaryConcernCategory { get; set; } = new();
    public ExtractedField<List<string>> SecondaryConcerns { get; set; } = new();
    public ExtractedField<ConcernSeverity> ConcernSeverity { get; set; } = new();
    public ExtractedField<string> ConcernDuration { get; set; } = new();
    public ExtractedField<bool> NewThisSession { get; set; } = new();
    public ExtractedField<List<string>> TriggerEvents { get; set; } = new();
}
