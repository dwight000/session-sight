namespace SessionSight.Core.Exceptions;

public class AzureServiceException : SessionSightException
{
    public AzureServiceException(string message) : base(message) { }
    public AzureServiceException(string message, Exception innerException) : base(message, innerException) { }
}
