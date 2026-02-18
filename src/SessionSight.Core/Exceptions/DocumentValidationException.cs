namespace SessionSight.Core.Exceptions;

public class DocumentValidationException : ValidationException
{
    public DocumentValidationException(string message) : base(message) { }
}
