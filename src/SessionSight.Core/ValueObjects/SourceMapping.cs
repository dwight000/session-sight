namespace SessionSight.Core.ValueObjects;

public class SourceMapping
{
    public string Text { get; set; } = string.Empty;
    public int StartChar { get; set; }
    public int EndChar { get; set; }
    public string? Section { get; set; }
}
