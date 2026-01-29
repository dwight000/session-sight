namespace SessionSight.Core.Exceptions;

public class ValidationException : SessionSightException
{
    public ValidationException(string message) : base(message) { }
    public ValidationException(string message, Exception innerException) : base(message, innerException) { }
}

public class SchemaValidationException : ValidationException
{
    public SchemaValidationException(string message) : base(message) { }
}

public class InputValidationException : ValidationException
{
    public InputValidationException(string message) : base(message) { }
}
