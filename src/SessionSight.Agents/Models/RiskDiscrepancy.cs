namespace SessionSight.Agents.Models;

/// <summary>
/// Represents a discrepancy between original and re-extracted risk field values.
/// </summary>
public class RiskDiscrepancy
{
    /// <summary>
    /// Name of the field where the discrepancy occurred.
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Value from the original extraction.
    /// </summary>
    public string OriginalValue { get; set; } = string.Empty;

    /// <summary>
    /// Confidence of the original extraction.
    /// </summary>
    public double OriginalConfidence { get; set; }

    /// <summary>
    /// Value from the re-extraction.
    /// </summary>
    public string ReExtractedValue { get; set; } = string.Empty;

    /// <summary>
    /// Confidence of the re-extraction.
    /// </summary>
    public double ReExtractedConfidence { get; set; }

    /// <summary>
    /// The value that was chosen after conservative merge.
    /// </summary>
    public string ResolvedValue { get; set; } = string.Empty;

    /// <summary>
    /// Explanation for why this value was chosen.
    /// </summary>
    public string ResolutionReason { get; set; } = string.Empty;
}
