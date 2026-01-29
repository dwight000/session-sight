namespace SessionSight.Core.Exceptions;

public class NotFoundException : SessionSightException
{
    public NotFoundException(string message) : base(message) { }
}

public class PatientNotFoundException : NotFoundException
{
    public PatientNotFoundException(Guid id) : base($"Patient with ID '{id}' was not found.") { }
}

public class SessionNotFoundException : NotFoundException
{
    public SessionNotFoundException(Guid id) : base($"Session with ID '{id}' was not found.") { }
}
