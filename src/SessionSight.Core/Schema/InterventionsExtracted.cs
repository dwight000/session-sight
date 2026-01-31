using SessionSight.Core.Enums;

namespace SessionSight.Core.Schema;

public class InterventionsExtracted
{
    public ExtractedField<List<TechniqueUsed>> TechniquesUsed { get; set; } = new();
    public ExtractedField<TechniqueEffectiveness> TechniquesEffectiveness { get; set; } = new();
    public ExtractedField<List<string>> SkillsTaught { get; set; } = new();
    public ExtractedField<List<string>> SkillsPracticed { get; set; } = new();
    public ExtractedField<string> HomeworkAssigned { get; set; } = new();
    public ExtractedField<HomeworkCompletion> HomeworkCompletion { get; set; } = new();
    public ExtractedField<List<string>> MedicationsDiscussed { get; set; } = new();
    public ExtractedField<string> MedicationChanges { get; set; } = new();
    public ExtractedField<MedicationAdherence> MedicationAdherence { get; set; } = new();
}
