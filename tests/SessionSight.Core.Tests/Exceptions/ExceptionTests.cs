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
}
