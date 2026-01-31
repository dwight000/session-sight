namespace SessionSight.Core.Exceptions;

public class SessionSightException : Exception
{
    public SessionSightException(string message) : base(message) { }
    public SessionSightException(string message, Exception innerException) : base(message, innerException) { }
}
