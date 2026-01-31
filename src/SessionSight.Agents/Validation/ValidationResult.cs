namespace SessionSight.Agents.Validation;

/// <summary>
/// Result of validating a clinical extraction.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<ValidationError> Errors { get; }

    public ValidationResult(bool isValid, IEnumerable<ValidationError> errors)
    {
        IsValid = isValid;
        Errors = errors.ToList().AsReadOnly();
    }

    public static ValidationResult Success() => new(true, []);

    public static ValidationResult Failure(IEnumerable<ValidationError> errors) => new(false, errors);
}

/// <summary>
/// A validation error for a specific field.
/// </summary>
public class ValidationError
{
    public string Field { get; }
    public string Message { get; }
    public ValidationSeverity Severity { get; }

    public ValidationError(string field, string message, ValidationSeverity severity = ValidationSeverity.Error)
    {
        Field = field;
        Message = message;
        Severity = severity;
    }
}

/// <summary>
/// Severity level for validation errors.
/// </summary>
public enum ValidationSeverity
{
    Warning,
    Error
}
