using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using SessionSight.Api.Middleware;
using SessionSight.Core.Exceptions;

namespace SessionSight.Api.Tests.Middleware;

public class GlobalExceptionHandlerTests
{
    private readonly Mock<ILogger<GlobalExceptionHandler>> _loggerMock;
    private readonly Mock<IHostEnvironment> _environmentMock;
    private readonly GlobalExceptionHandler _handler;

    public GlobalExceptionHandlerTests()
    {
        _loggerMock = new Mock<ILogger<GlobalExceptionHandler>>();
        _environmentMock = new Mock<IHostEnvironment>();
        _handler = new GlobalExceptionHandler(_loggerMock.Object, _environmentMock.Object);
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_Returns400()
    {
        var context = CreateHttpContext();
        var exception = new ValidationException("Invalid input");

        var result = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task TryHandleAsync_NotFoundException_Returns404()
    {
        var context = CreateHttpContext();
        var exception = new NotFoundException("Resource not found");

        var result = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task TryHandleAsync_AzureServiceException_Returns503()
    {
        var context = CreateHttpContext();
        var exception = new AzureServiceException("Service unavailable");

        var result = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task TryHandleAsync_ExtractionException_Returns500()
    {
        var context = CreateHttpContext();
        var exception = new ExtractionException("Extraction failed");

        var result = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task TryHandleAsync_GenericException_Returns500()
    {
        var context = CreateHttpContext();
        var exception = new InvalidOperationException("Something went wrong");

        var result = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task TryHandleAsync_SessionSightException_IncludesMessageInDetail()
    {
        var context = CreateHttpContext();
        var exception = new ValidationException("Field X is required");

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        var body = await ReadResponseBody(context);
        body.Should().Contain("Field X is required");
    }

    [Fact]
    public async Task TryHandleAsync_InDevelopment_IncludesStackTraceForGenericException()
    {
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Development");
        var context = CreateHttpContext();
        var exception = new InvalidOperationException("Something went wrong");

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        var body = await ReadResponseBody(context);
        body.Should().Contain("InvalidOperationException");
    }

    [Fact]
    public async Task TryHandleAsync_InProduction_HidesStackTraceForGenericException()
    {
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Production");
        var context = CreateHttpContext();
        var exception = new InvalidOperationException("Internal details");

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        var body = await ReadResponseBody(context);
        body.Should().Contain("unexpected error");
        body.Should().NotContain("Internal details");
    }

    [Fact]
    public async Task TryHandleAsync_IncludesTraceId()
    {
        var context = CreateHttpContext();
        context.TraceIdentifier = "trace-123";
        var exception = new ValidationException("Error");

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        var body = await ReadResponseBody(context);
        body.Should().Contain("trace-123");
    }

    [Fact]
    public async Task TryHandleAsync_WithCorrelationId_IncludesInResponse()
    {
        var context = CreateHttpContext();
        context.Items["CorrelationId"] = "corr-456";
        var exception = new ValidationException("Error");

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        var body = await ReadResponseBody(context);
        body.Should().Contain("corr-456");
    }

    [Fact]
    public async Task TryHandleAsync_PatientNotFoundException_Returns404()
    {
        var context = CreateHttpContext();
        var exception = new PatientNotFoundException(Guid.NewGuid());

        var result = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task TryHandleAsync_SessionNotFoundException_Returns404()
    {
        var context = CreateHttpContext();
        var exception = new SessionNotFoundException(Guid.NewGuid());

        var result = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task TryHandleAsync_SchemaValidationException_Returns400()
    {
        var context = CreateHttpContext();
        var exception = new SchemaValidationException("Schema invalid");

        var result = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task TryHandleAsync_AlwaysReturnsTrue()
    {
        var context = CreateHttpContext();
        var exception = new Exception("Any exception");

        var result = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        result.Should().BeTrue();
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/test";
        return context;
    }

    private static async Task<string> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
}
