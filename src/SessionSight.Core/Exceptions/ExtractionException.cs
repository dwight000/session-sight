namespace SessionSight.Core.Exceptions;

public class ExtractionException : SessionSightException
{
    public ExtractionException(string message) : base(message) { }
    public ExtractionException(string message, Exception innerException) : base(message, innerException) { }
}
