using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SessionSight.Core.Exceptions;

namespace SessionSight.Api.Middleware;

public partial class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            Core.Exceptions.ValidationException => (StatusCodes.Status400BadRequest, "Validation Error"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            AzureServiceException => (StatusCodes.Status503ServiceUnavailable, "Service Unavailable"),
            ExtractionException => (StatusCodes.Status500InternalServerError, "Extraction Failed"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        LogUnhandledException(_logger, exception, title);

        string detail;
        if (exception is SessionSightException)
        {
            detail = exception.Message;
        }
        else if (_environment.IsDevelopment())
        {
            detail = exception.ToString();
        }
        else
        {
            detail = "An unexpected error occurred.";
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (httpContext.Items.TryGetValue("CorrelationId", out var correlationId))
        {
            problemDetails.Extensions["correlationId"] = correlationId;
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception: {Title}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string title);
}
