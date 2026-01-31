using SessionSight.Core.Schema;

namespace SessionSight.Agents.Validation;

/// <summary>
/// Validates clinical extraction data against schema rules and business constraints.
/// </summary>
public interface ISchemaValidator
{
    /// <summary>
    /// Validates a clinical extraction for required fields, valid ranges, and confidence thresholds.
    /// </summary>
    /// <param name="extraction">The extraction to validate.</param>
    /// <returns>Validation result with any errors found.</returns>
    ValidationResult Validate(ClinicalExtraction extraction);
}
