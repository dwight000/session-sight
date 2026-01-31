using SessionSight.Core.ValueObjects;

namespace SessionSight.Core.Schema;

public class ExtractedField<T>
{
    public T? Value { get; set; }
    public double Confidence { get; set; }
    public SourceMapping? Source { get; set; }
}
