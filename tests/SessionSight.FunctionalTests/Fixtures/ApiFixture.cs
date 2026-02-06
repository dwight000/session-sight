using System.Net;
using Microsoft.Extensions.Configuration;

namespace SessionSight.FunctionalTests.Fixtures;

/// <summary>
/// Provides HttpClients configured to hit the running API with transient fault retry.
/// Base URL is configurable via:
/// 1. Environment variable: API_BASE_URL
/// 2. appsettings.Test.json: ApiBaseUrl
/// Default: http://localhost:5001
/// </summary>
public class ApiFixture : IDisposable
{
    /// <summary>Standard client with 120s timeout (CRUD, health, Q&amp;A calls).</summary>
    public HttpClient Client { get; }

    /// <summary>Long-timeout client for extraction calls (Doc Intelligence + 3 LLM agents + embedding + indexing).</summary>
    public HttpClient LongClient { get; }

    public string BaseUrl { get; }
    private readonly HttpClientHandler _handler;
    private readonly RetryHandler _retryHandler;

    public ApiFixture()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        BaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL")
            ?? configuration["ApiBaseUrl"]
            ?? "http://localhost:5001";

        // Allow self-signed certificates for local development
        _handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        // Single retry on transient socket/TLS errors
        _retryHandler = new RetryHandler(_handler);

        Client = new HttpClient(_retryHandler)
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(120)
        };

        LongClient = new HttpClient(_retryHandler)
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    public void Dispose()
    {
        LongClient.Dispose();
        Client.Dispose();
        _retryHandler.Dispose();
        _handler.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Delegating handler that retries once on transient HTTP errors
/// (socket resets, TLS failures, 502/503/504).
/// </summary>
internal sealed class RetryHandler : DelegatingHandler
{
    private const int RetryDelayMs = 1000;

    public RetryHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (IsTransientStatusCode(response.StatusCode))
            {
                await Task.Delay(RetryDelayMs, cancellationToken);
                response.Dispose();

                // Clone the request for retry (original is already sent)
                using var retryRequest = await CloneRequestAsync(request);
                return await base.SendAsync(retryRequest, cancellationToken);
            }

            return response;
        }
        catch (HttpRequestException)
        {
            // Socket reset, TLS failure, connection refused — retry once
            await Task.Delay(RetryDelayMs, cancellationToken);
            using var retryRequest = await CloneRequestAsync(request);
            return await base.SendAsync(retryRequest, cancellationToken);
        }
        catch (IOException)
        {
            await Task.Delay(RetryDelayMs, cancellationToken);
            using var retryRequest = await CloneRequestAsync(request);
            return await base.SendAsync(retryRequest, cancellationToken);
        }
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        if (request.Content is not null)
        {
            var content = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(content);

            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
