namespace SessionSight.Core.ValueObjects;

public class ConfidenceScore
{
    public double Value { get; }

    public ConfidenceScore(double value)
    {
        if (value < 0.0 || value > 1.0)
            throw new ArgumentOutOfRangeException(nameof(value), "Confidence score must be between 0.0 and 1.0");
        Value = value;
    }

    public bool MeetsThreshold(double threshold) => Value >= threshold;
}
