using System.Text;
using Microsoft.Extensions.Options;

namespace SessionSight.Api.Middleware;

public sealed partial class RequestResponseBodyLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseBodyLoggingMiddleware> _logger;
    private readonly RequestResponseLoggingOptions _options;

    public RequestResponseBodyLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestResponseBodyLoggingMiddleware> logger,
        IOptions<RequestResponseLoggingOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (CanLogRequestBody(context.Request))
        {
            context.Request.EnableBuffering();
            var requestBody = await ReadBodyAsync(context.Request.Body);
            context.Request.Body.Position = 0;

            if (!string.IsNullOrWhiteSpace(requestBody))
            {
                LogRequestBody(_logger, context.Request.Method, context.Request.Path, ApplyMaxBytes(requestBody));
            }
        }

        var originalBody = context.Response.Body;
        await using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await _next(context);

            if (CanLogResponseBody(responseBuffer, context.Response))
            {
                responseBuffer.Position = 0;
                var responseBody = await ReadBodyAsync(responseBuffer);

                if (!string.IsNullOrWhiteSpace(responseBody))
                {
                    LogResponseBody(_logger, context.Response.StatusCode, context.Request.Path, ApplyMaxBytes(responseBody));
                }
            }

            responseBuffer.Position = 0;
            await responseBuffer.CopyToAsync(originalBody);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static bool CanLogRequestBody(HttpRequest request) =>
        IsSupportedContentType(request.ContentType)
        && !HttpMethods.IsGet(request.Method)
        && !HttpMethods.IsHead(request.Method)
        && !HttpMethods.IsDelete(request.Method);

    private static bool CanLogResponseBody(Stream responseBody, HttpResponse response) =>
        responseBody.Length > 0 && IsSupportedContentType(response.ContentType);

    private static bool IsSupportedContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("application/xml", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ReadBodyAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private string ApplyMaxBytes(string body)
    {
        if (_options.MaxBodyLogBytes is not > 0)
        {
            return body;
        }

        var bytes = Encoding.UTF8.GetBytes(body);
        if (bytes.Length <= _options.MaxBodyLogBytes.Value)
        {
            return body;
        }

        var truncated = Encoding.UTF8.GetString(bytes, 0, _options.MaxBodyLogBytes.Value);
        return $"{truncated}... [truncated at {_options.MaxBodyLogBytes.Value} bytes]";
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "HTTP request body {Method} {Path}: {Body}")]
    private static partial void LogRequestBody(ILogger logger, string method, string path, string body);

    [LoggerMessage(Level = LogLevel.Information, Message = "HTTP response body {StatusCode} {Path}: {Body}")]
    private static partial void LogResponseBody(ILogger logger, int statusCode, string path, string body);
}
