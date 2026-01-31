using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TechniqueUsed
{
    Cbt,
    Dbt,
    DbtDistressTolerance,
    DbtMindfulness,
    DbtInterpersonal,
    DbtEmotionRegulation,
    Act,
    Psychodynamic,
    Emdr,
    MotivationalInterviewing,
    Exposure,
    Relaxation,
    Mindfulness,
    BehavioralActivation,
    CognitiveRestructuring,
    Interpersonal,
    Narrative,
    SolutionFocused,
    Supportive,
    Psychoeducation
}
