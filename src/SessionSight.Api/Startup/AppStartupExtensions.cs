using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using SessionSight.Api.Middleware;

namespace SessionSight.Api.Startup;

public static class AppStartupExtensions
{
    public static void EnsureConfiguredSerilogDirectories(this WebApplicationBuilder builder)
    {
        var writeToSinks = builder.Configuration.GetSection("Serilog:WriteTo").GetChildren();
        foreach (var sink in writeToSinks)
        {
            if (!string.Equals(sink["Name"], "File", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sinkPath = sink.GetSection("Args")["path"];
            if (string.IsNullOrWhiteSpace(sinkPath))
            {
                continue;
            }

            var localApiLogDirectory = Path.GetDirectoryName(sinkPath);
            if (string.IsNullOrWhiteSpace(localApiLogDirectory))
            {
                continue;
            }

            Directory.CreateDirectory(localApiLogDirectory);
        }
    }

    public static void ConfigureRequestResponseLogging(
        this WebApplication app,
        RequestResponseLoggingOptions options)
    {
        if (!options.Enabled)
        {
            return;
        }

        app.UseSerilogRequestLogging(requestLoggingOptions =>
        {
            requestLoggingOptions.GetLevel = DetermineRequestLogLevel;
            requestLoggingOptions.EnrichDiagnosticContext = EnrichRequestDiagnosticContext;
        });

        if (options.LogBodies)
        {
            app.UseMiddleware<RequestResponseBodyLoggingMiddleware>();
        }
    }

    public static void ConfigureDevelopmentEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        app.UseCors("DevCors");
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    private static LogEventLevel DetermineRequestLogLevel(HttpContext httpContext, double _, Exception? ex)
    {
        if (ex is not null || httpContext.Response.StatusCode >= 500)
        {
            return LogEventLevel.Error;
        }

        if (httpContext.Response.StatusCode >= 400)
        {
            return LogEventLevel.Warning;
        }

        return LogEventLevel.Information;
    }

    private static void EnrichRequestDiagnosticContext(IDiagnosticContext diagnosticContext, HttpContext httpContext)
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("CorrelationId",
            httpContext.Items["CorrelationId"]?.ToString() ?? httpContext.TraceIdentifier);
    }
}
