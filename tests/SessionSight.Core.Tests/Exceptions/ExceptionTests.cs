using FluentAssertions;
using SessionSight.Core.Exceptions;

namespace SessionSight.Core.Tests.Exceptions;

public class ExceptionTests
{
    [Theory]
    [InlineData(typeof(NotFoundException))]
    [InlineData(typeof(ValidationException))]
    [InlineData(typeof(ExtractionException))]
    [InlineData(typeof(AzureServiceException))]
    public void Exception_InheritsFromSessionSightException(Type exceptionType)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType, "Test message")!;
        ex.Should().BeAssignableTo<SessionSightException>();
    }

    [Theory]
    [InlineData(typeof(SchemaValidationException))]
    [InlineData(typeof(InputValidationException))]
    [InlineData(typeof(DocumentValidationException))]
    public void Exception_InheritsFromValidationException(Type exceptionType)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType, "Test message")!;
        ex.Should().BeAssignableTo<ValidationException>();
    }

    [Theory]
    [InlineData(typeof(SessionSightException))]
    [InlineData(typeof(ValidationException))]
    [InlineData(typeof(ExtractionException))]
    [InlineData(typeof(AzureServiceException))]
    public void Exception_WithMessageAndInner_StoresBoth(Type exceptionType)
    {
        var inner = new InvalidOperationException("Inner error");
        var ex = (Exception)Activator.CreateInstance(exceptionType, "Outer error", inner)!;
        ex.Message.Should().Be("Outer error");
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void PatientNotFoundException_WithId_FormatsMessage()
    {
        var id = Guid.NewGuid();
        var ex = new PatientNotFoundException(id);
        ex.Message.Should().Contain(id.ToString());
        ex.Message.Should().Contain("Patient");
    }

    [Fact]
    public void SessionNotFoundException_WithId_FormatsMessage()
    {
        var id = Guid.NewGuid();
        var ex = new SessionNotFoundException(id);
        ex.Message.Should().Contain(id.ToString());
        ex.Message.Should().Contain("Session");
    }

    [Fact]
    public void CircuitBreakerOpenException_InheritsFromAzureServiceException()
    {
        var ex = new CircuitBreakerOpenException("openai", TimeSpan.FromSeconds(30));
        ex.Should().BeAssignableTo<AzureServiceException>();
    }

    [Fact]
    public void CircuitBreakerOpenException_StoresRetryAfter()
    {
        var retryAfter = TimeSpan.FromSeconds(45);
        var ex = new CircuitBreakerOpenException("search", retryAfter);
        ex.RetryAfter.Should().Be(retryAfter);
        ex.Message.Should().Contain("search");
        ex.Message.Should().Contain("45");
    }
}
