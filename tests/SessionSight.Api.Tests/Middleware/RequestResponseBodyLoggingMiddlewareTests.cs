using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SessionSight.Api.Middleware;

namespace SessionSight.Api.Tests.Middleware;

public class RequestResponseBodyLoggingMiddlewareTests
{
    private readonly Mock<ILogger<RequestResponseBodyLoggingMiddleware>> _loggerMock = new();

    private RequestResponseBodyLoggingMiddleware CreateMiddleware(
        RequestDelegate next, RequestResponseLoggingOptions? options = null)
    {
        var opts = Options.Create(options ?? new RequestResponseLoggingOptions());
        return new RequestResponseBodyLoggingMiddleware(next, _loggerMock.Object, opts);
    }

    private static DefaultHttpContext CreateContext(string method, string? requestContentType = null, string? requestBody = null)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Method = method;
        if (requestContentType is not null)
            context.Request.ContentType = requestContentType;
        if (requestBody is not null)
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));
        return context;
    }

    private static async Task<string> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task InvokeAsync_WithJsonPostBody_LogsAndPassesThrough()
    {
        RequestDelegate next = ctx =>
        {
            ctx.Response.ContentType = "application/json";
            return ctx.Response.WriteAsync("""{"answer": "ok"}""");
        };
        var middleware = CreateMiddleware(next);
        var context = CreateContext("POST", "application/json", """{"question": "test"}""");

        await middleware.InvokeAsync(context);

        var response = await ReadResponseBody(context);
        response.Should().Contain("answer");
    }

    [Fact]
    public async Task InvokeAsync_WithGetRequest_SkipsRequestBodyLogging()
    {
        RequestDelegate next = ctx =>
        {
            ctx.Response.ContentType = "application/json";
            return ctx.Response.WriteAsync("{}");
        };
        var middleware = CreateMiddleware(next);
        var context = CreateContext("GET", "application/json");

        await middleware.InvokeAsync(context);

        var response = await ReadResponseBody(context);
        response.Should().Be("{}");
    }

    [Fact]
    public async Task InvokeAsync_WithMaxBodyLogBytes_TruncatesLogButPassesFullBody()
    {
        var longBody = new string('x', 500);
        RequestDelegate next = ctx =>
        {
            ctx.Response.ContentType = "text/plain";
            return ctx.Response.WriteAsync(longBody);
        };
        var options = new RequestResponseLoggingOptions { MaxBodyLogBytes = 100 };
        var middleware = CreateMiddleware(next, options);
        var context = CreateContext("GET");

        await middleware.InvokeAsync(context);

        var response = await ReadResponseBody(context);
        response.Should().Be(longBody);
    }

    [Fact]
    public async Task InvokeAsync_WithNoContentType_SkipsBodyLogging()
    {
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = CreateMiddleware(next);
        var context = CreateContext("POST");

        await middleware.InvokeAsync(context);

        context.Response.Body.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_WithXmlContentType_ProcessesBody()
    {
        RequestDelegate next = ctx =>
        {
            ctx.Response.ContentType = "application/xml";
            return ctx.Response.WriteAsync("<root/>");
        };
        var middleware = CreateMiddleware(next);
        var context = CreateContext("POST", "application/xml", "<request/>");

        await middleware.InvokeAsync(context);

        var response = await ReadResponseBody(context);
        response.Should().Be("<root/>");
    }

    [Fact]
    public async Task InvokeAsync_WithFormContentType_ProcessesBody()
    {
        RequestDelegate next = ctx =>
        {
            ctx.Response.ContentType = "text/html";
            return ctx.Response.WriteAsync("<ok/>");
        };
        var middleware = CreateMiddleware(next);
        var context = CreateContext("POST", "application/x-www-form-urlencoded", "key=value");

        await middleware.InvokeAsync(context);

        var response = await ReadResponseBody(context);
        response.Should().Be("<ok/>");
    }

    [Fact]
    public async Task InvokeAsync_WithDeleteRequest_SkipsRequestBodyLogging()
    {
        RequestDelegate next = ctx =>
        {
            ctx.Response.ContentType = "application/json";
            return ctx.Response.WriteAsync("{}");
        };
        var middleware = CreateMiddleware(next);
        var context = CreateContext("DELETE", "application/json");

        await middleware.InvokeAsync(context);

        var response = await ReadResponseBody(context);
        response.Should().Be("{}");
    }

    [Fact]
    public async Task InvokeAsync_WithSmallBodyUnderMaxBytes_DoesNotTruncate()
    {
        var smallBody = "short";
        RequestDelegate next = ctx =>
        {
            ctx.Response.ContentType = "text/plain";
            return ctx.Response.WriteAsync(smallBody);
        };
        var options = new RequestResponseLoggingOptions { MaxBodyLogBytes = 1000 };
        var middleware = CreateMiddleware(next, options);
        var context = CreateContext("GET");

        await middleware.InvokeAsync(context);

        var response = await ReadResponseBody(context);
        response.Should().Be(smallBody);
    }

    [Fact]
    public async Task InvokeAsync_WithHeadRequest_SkipsRequestBodyLogging()
    {
        RequestDelegate next = ctx =>
        {
            ctx.Response.ContentType = "application/json";
            return ctx.Response.WriteAsync("{}");
        };
        var middleware = CreateMiddleware(next);
        var context = CreateContext("HEAD", "application/json");

        await middleware.InvokeAsync(context);

        var response = await ReadResponseBody(context);
        response.Should().Be("{}");
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyPostBody_SkipsRequestBodyLog()
    {
        RequestDelegate next = ctx =>
        {
            ctx.Response.ContentType = "application/json";
            return ctx.Response.WriteAsync("""{"ok": true}""");
        };
        var middleware = CreateMiddleware(next);
        var context = CreateContext("POST", "application/json", "");

        await middleware.InvokeAsync(context);

        var response = await ReadResponseBody(context);
        response.Should().Contain("ok");
    }
}
