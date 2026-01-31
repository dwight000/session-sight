using SessionSight.Core.Enums;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Validation;

/// <summary>
/// Validates clinical extraction data against schema rules and business constraints.
/// </summary>
public class SchemaValidator : ISchemaValidator
{
    /// <summary>
    /// Minimum confidence threshold for risk assessment fields.
    /// Per ADR-004, risk fields require 0.9 confidence.
    /// </summary>
    private const double RiskConfidenceThreshold = 0.9;

    /// <summary>
    /// Minimum valid mood score (1-10 scale).
    /// </summary>
    private const int MinMoodScore = 1;

    /// <summary>
    /// Maximum valid mood score (1-10 scale).
    /// </summary>
    private const int MaxMoodScore = 10;

    public ValidationResult Validate(ClinicalExtraction extraction)
    {
        var errors = new List<ValidationError>();

        ValidateRequiredFields(extraction, errors);
        ValidateRiskConfidence(extraction.RiskAssessment, errors);
        ValidateRanges(extraction, errors);
        ValidateConsistency(extraction, errors);

        return new ValidationResult(errors.Count == 0, errors);
    }

    private static void ValidateRequiredFields(ClinicalExtraction extraction, List<ValidationError> errors)
    {
        // SessionDate is required
        if (extraction.SessionInfo.SessionDate.Value == default)
        {
            errors.Add(new ValidationError(
                "SessionInfo.SessionDate",
                "Session date is required"));
        }

        // RiskLevelOverall is required if any risk indicators are present
        var hasRiskIndicators = HasSignificantRisk(extraction.RiskAssessment);

        if (hasRiskIndicators && extraction.RiskAssessment.RiskLevelOverall.Value == default)
        {
            errors.Add(new ValidationError(
                "RiskAssessment.RiskLevelOverall",
                "Overall risk level is required when risk indicators are present"));
        }
    }

    private static bool HasSignificantRisk(RiskAssessmentExtracted risk)
    {
        var si = risk.SuicidalIdeation.Value;
        var sh = risk.SelfHarm.Value;
        var hi = risk.HomicidalIdeation.Value;

        return (si != default && si != SuicidalIdeation.None)
            || (sh != default && sh != SelfHarm.None)
            || (hi != default && hi != HomicidalIdeation.None);
    }

    private static void ValidateRiskConfidence(RiskAssessmentExtracted risk, List<ValidationError> errors)
    {
        // Suicidal ideation confidence check
        var si = risk.SuicidalIdeation.Value;
        if (si != default && si != SuicidalIdeation.None
            && risk.SuicidalIdeation.Confidence < RiskConfidenceThreshold)
        {
            errors.Add(new ValidationError(
                "RiskAssessment.SuicidalIdeation",
                $"Risk field confidence {risk.SuicidalIdeation.Confidence:F2} is below threshold {RiskConfidenceThreshold}",
                ValidationSeverity.Warning));
        }

        // Self-harm confidence check
        var sh = risk.SelfHarm.Value;
        if (sh != default && sh != SelfHarm.None
            && risk.SelfHarm.Confidence < RiskConfidenceThreshold)
        {
            errors.Add(new ValidationError(
                "RiskAssessment.SelfHarm",
                $"Risk field confidence {risk.SelfHarm.Confidence:F2} is below threshold {RiskConfidenceThreshold}",
                ValidationSeverity.Warning));
        }

        // Homicidal ideation confidence check
        var hi = risk.HomicidalIdeation.Value;
        if (hi != default && hi != HomicidalIdeation.None
            && risk.HomicidalIdeation.Confidence < RiskConfidenceThreshold)
        {
            errors.Add(new ValidationError(
                "RiskAssessment.HomicidalIdeation",
                $"Risk field confidence {risk.HomicidalIdeation.Confidence:F2} is below threshold {RiskConfidenceThreshold}",
                ValidationSeverity.Warning));
        }

        // Overall risk level confidence check when high or imminent
        var riskLevel = risk.RiskLevelOverall.Value;
        if ((riskLevel == RiskLevelOverall.High || riskLevel == RiskLevelOverall.Imminent)
            && risk.RiskLevelOverall.Confidence < RiskConfidenceThreshold)
        {
            errors.Add(new ValidationError(
                "RiskAssessment.RiskLevelOverall",
                $"High/imminent risk confidence {risk.RiskLevelOverall.Confidence:F2} is below threshold {RiskConfidenceThreshold}",
                ValidationSeverity.Warning));
        }
    }

    private static void ValidateRanges(ClinicalExtraction extraction, List<ValidationError> errors)
    {
        // Self-reported mood must be 1-10
        var mood = extraction.MoodAssessment.SelfReportedMood;
        if (mood.Value != default && (mood.Value < MinMoodScore || mood.Value > MaxMoodScore))
        {
            errors.Add(new ValidationError(
                "MoodAssessment.SelfReportedMood",
                $"Self-reported mood {mood.Value} is outside valid range {MinMoodScore}-{MaxMoodScore}"));
        }

        // Session duration must be positive
        var duration = extraction.SessionInfo.SessionDurationMinutes;
        if (duration.Value != default && duration.Value <= 0)
        {
            errors.Add(new ValidationError(
                "SessionInfo.SessionDurationMinutes",
                "Session duration must be positive"));
        }

        // Session number must be positive
        var sessionNumber = extraction.SessionInfo.SessionNumber;
        if (sessionNumber.Value != default && sessionNumber.Value <= 0)
        {
            errors.Add(new ValidationError(
                "SessionInfo.SessionNumber",
                "Session number must be positive"));
        }
    }

    private static void ValidateConsistency(ClinicalExtraction extraction, List<ValidationError> errors)
    {
        // If session end time is before start time, that's invalid
        var startTime = extraction.SessionInfo.SessionStartTime.Value;
        var endTime = extraction.SessionInfo.SessionEndTime.Value;
        if (startTime != default && endTime != default && endTime < startTime)
        {
            errors.Add(new ValidationError(
                "SessionInfo.SessionEndTime",
                "Session end time cannot be before start time"));
        }

        // If SI is ActiveWithPlan or ActiveWithIntent, safety plan should not be NotNeeded
        var si = extraction.RiskAssessment.SuicidalIdeation.Value;
        var safetyPlan = extraction.RiskAssessment.SafetyPlanStatus.Value;
        if ((si == SuicidalIdeation.ActiveWithPlan || si == SuicidalIdeation.ActiveWithIntent)
            && safetyPlan == SafetyPlanStatus.NotNeeded)
        {
            errors.Add(new ValidationError(
                "RiskAssessment.SafetyPlanStatus",
                "Safety plan should not be 'NotNeeded' when active SI with plan/intent is present",
                ValidationSeverity.Warning));
        }

        // If homework completion is NotAssigned, homeworkAssigned should be null
        var homeworkCompletion = extraction.Interventions.HomeworkCompletion.Value;
        var homeworkAssigned = extraction.Interventions.HomeworkAssigned.Value;
        if (homeworkCompletion == HomeworkCompletion.NotAssigned && !string.IsNullOrEmpty(homeworkAssigned))
        {
            errors.Add(new ValidationError(
                "Interventions.HomeworkAssigned",
                "Homework cannot be assigned when completion status is 'NotAssigned'",
                ValidationSeverity.Warning));
        }
    }
}
