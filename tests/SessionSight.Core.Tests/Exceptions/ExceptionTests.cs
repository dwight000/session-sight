using FluentAssertions;
using SessionSight.Core.Exceptions;

namespace SessionSight.Core.Tests.Exceptions;

public class ExceptionTests
{
    [Fact]
    public void SessionSightException_WithMessageAndInner_StoresBoth()
    {
        var inner = new InvalidOperationException("Inner error");
        var ex = new SessionSightException("Outer error", inner);
        ex.Message.Should().Be("Outer error");
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void NotFoundException_InheritsFromSessionSightException()
    {
        var ex = new NotFoundException("Not found");
        ex.Should().BeAssignableTo<SessionSightException>();
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
    public void ValidationException_WithMessageAndInner_StoresBoth()
    {
        var inner = new ArgumentException("Bad arg");
        var ex = new ValidationException("Validation failed", inner);
        ex.Message.Should().Be("Validation failed");
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void ValidationException_InheritsFromSessionSightException()
    {
        var ex = new ValidationException("Invalid");
        ex.Should().BeAssignableTo<SessionSightException>();
    }

    [Fact]
    public void SchemaValidationException_InheritsFromValidationException()
    {
        var ex = new SchemaValidationException("Schema error");
        ex.Should().BeAssignableTo<ValidationException>();
    }

    [Fact]
    public void InputValidationException_InheritsFromValidationException()
    {
        var ex = new InputValidationException("Bad input");
        ex.Should().BeAssignableTo<ValidationException>();
    }

    [Fact]
    public void ExtractionException_WithMessageAndInner_StoresBoth()
    {
        var inner = new TimeoutException("Timeout");
        var ex = new ExtractionException("Extraction failed", inner);
        ex.Message.Should().Be("Extraction failed");
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void ExtractionException_InheritsFromSessionSightException()
    {
        var ex = new ExtractionException("Error");
        ex.Should().BeAssignableTo<SessionSightException>();
    }

    [Fact]
    public void AzureServiceException_WithMessageAndInner_StoresBoth()
    {
        var inner = new HttpRequestException("Network error");
        var ex = new AzureServiceException("Azure error", inner);
        ex.Message.Should().Be("Azure error");
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void AzureServiceException_InheritsFromSessionSightException()
    {
        var ex = new AzureServiceException("Error");
        ex.Should().BeAssignableTo<SessionSightException>();
    }
}
