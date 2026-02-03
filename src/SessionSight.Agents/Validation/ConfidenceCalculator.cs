using SessionSight.Core.Enums;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Validation;

/// <summary>
/// Calculates overall confidence scores for clinical extractions.
/// </summary>
public static class ConfidenceCalculator
{
    /// <summary>
    /// Minimum confidence threshold for risk assessment fields.
    /// Per ADR-004, risk fields require 0.9 confidence.
    /// </summary>
    private const double RiskConfidenceThreshold = 0.9;

    /// <summary>
    /// Calculates the overall confidence score for an extraction.
    /// </summary>
    /// <param name="extraction">The extraction to calculate confidence for.</param>
    /// <returns>Average confidence score across all extracted fields with values.</returns>
    public static double Calculate(ClinicalExtraction extraction)
    {
        var scores = new List<double>();

        CollectSectionScores(extraction.SessionInfo, scores);
        CollectSectionScores(extraction.PresentingConcerns, scores);
        CollectSectionScores(extraction.MoodAssessment, scores);
        CollectSectionScores(extraction.RiskAssessment, scores);
        CollectSectionScores(extraction.MentalStatusExam, scores);
        CollectSectionScores(extraction.Interventions, scores);
        CollectSectionScores(extraction.Diagnoses, scores);
        CollectSectionScores(extraction.TreatmentProgress, scores);
        CollectSectionScores(extraction.NextSteps, scores);

        return scores.Count > 0 ? scores.Average() : 0.0;
    }

    /// <summary>
    /// Checks if any critical risk fields have low confidence.
    /// </summary>
    /// <param name="extraction">The extraction to check.</param>
    /// <returns>True if any critical risk field has confidence below threshold.</returns>
    public static bool HasLowConfidenceRiskFields(ClinicalExtraction extraction)
    {
        var risk = extraction.RiskAssessment;

        // Check suicidal ideation confidence when present
        var si = risk.SuicidalIdeation.Value;
        if (si != SuicidalIdeation.None
            && risk.SuicidalIdeation.Confidence < RiskConfidenceThreshold)
        {
            return true;
        }

        // Check self-harm confidence when present
        var sh = risk.SelfHarm.Value;
        if (sh != SelfHarm.None
            && risk.SelfHarm.Confidence < RiskConfidenceThreshold)
        {
            return true;
        }

        // Check homicidal ideation confidence when present
        var hi = risk.HomicidalIdeation.Value;
        if (hi != HomicidalIdeation.None
            && risk.HomicidalIdeation.Confidence < RiskConfidenceThreshold)
        {
            return true;
        }

        // Check overall risk level confidence when high/imminent
        var riskLevel = risk.RiskLevelOverall.Value;
        if ((riskLevel == RiskLevelOverall.High || riskLevel == RiskLevelOverall.Imminent)
            && risk.RiskLevelOverall.Confidence < RiskConfidenceThreshold)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the list of fields with low confidence.
    /// </summary>
    /// <param name="extraction">The extraction to check.</param>
    /// <param name="threshold">Minimum confidence threshold.</param>
    /// <returns>List of field names with confidence below threshold.</returns>
    public static List<string> GetLowConfidenceFields(ClinicalExtraction extraction, double threshold = 0.7)
    {
        var lowConfidenceFields = new List<string>();

        CollectLowConfidenceFields("SessionInfo", extraction.SessionInfo, threshold, lowConfidenceFields);
        CollectLowConfidenceFields("PresentingConcerns", extraction.PresentingConcerns, threshold, lowConfidenceFields);
        CollectLowConfidenceFields("MoodAssessment", extraction.MoodAssessment, threshold, lowConfidenceFields);
        CollectLowConfidenceFields("RiskAssessment", extraction.RiskAssessment, threshold, lowConfidenceFields);
        CollectLowConfidenceFields("MentalStatusExam", extraction.MentalStatusExam, threshold, lowConfidenceFields);
        CollectLowConfidenceFields("Interventions", extraction.Interventions, threshold, lowConfidenceFields);
        CollectLowConfidenceFields("Diagnoses", extraction.Diagnoses, threshold, lowConfidenceFields);
        CollectLowConfidenceFields("TreatmentProgress", extraction.TreatmentProgress, threshold, lowConfidenceFields);
        CollectLowConfidenceFields("NextSteps", extraction.NextSteps, threshold, lowConfidenceFields);

        return lowConfidenceFields;
    }

    private static void CollectSectionScores(object section, List<double> scores)
    {
        var properties = section.GetType().GetProperties();

        foreach (var prop in properties)
        {
            if (!prop.PropertyType.IsGenericType ||
                prop.PropertyType.GetGenericTypeDefinition() != typeof(ExtractedField<>))
            {
                continue;
            }

            var fieldValue = prop.GetValue(section);
            if (fieldValue is null) continue;

            var valueProperty = fieldValue.GetType().GetProperty("Value");
            var confidenceProperty = fieldValue.GetType().GetProperty("Confidence");

            if (valueProperty is null || confidenceProperty is null) continue;

            var value = valueProperty.GetValue(fieldValue);
            var confidence = (double)(confidenceProperty.GetValue(fieldValue) ?? 0.0);

            // Only include fields that have a non-default value
            if (value is not null && !IsDefaultValue(value) && confidence > 0)
            {
                scores.Add(confidence);
            }
        }
    }

    private static void CollectLowConfidenceFields(
        string sectionName,
        object section,
        double threshold,
        List<string> lowConfidenceFields)
    {
        var properties = section.GetType().GetProperties();

        foreach (var prop in properties)
        {
            if (!prop.PropertyType.IsGenericType ||
                prop.PropertyType.GetGenericTypeDefinition() != typeof(ExtractedField<>))
            {
                continue;
            }

            var fieldValue = prop.GetValue(section);
            if (fieldValue is null) continue;

            var valueProperty = fieldValue.GetType().GetProperty("Value");
            var confidenceProperty = fieldValue.GetType().GetProperty("Confidence");

            if (valueProperty is null || confidenceProperty is null) continue;

            var value = valueProperty.GetValue(fieldValue);
            var confidence = (double)(confidenceProperty.GetValue(fieldValue) ?? 0.0);

            // Only include fields that have a value and low confidence
            if (value is not null && !IsDefaultValue(value) && confidence > 0 && confidence < threshold)
            {
                lowConfidenceFields.Add($"{sectionName}.{prop.Name}");
            }
        }
    }

    private static bool IsDefaultValue(object value)
    {
        var type = value.GetType();

        if (type.IsValueType)
        {
            var defaultValue = Activator.CreateInstance(type);
            return value.Equals(defaultValue);
        }

        return value is null;
    }
}
