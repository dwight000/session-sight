using Microsoft.Extensions.Configuration;

namespace SessionSight.FunctionalTests.Fixtures;

/// <summary>
/// Provides HttpClients configured to hit the running API.
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

        Client = new HttpClient(_handler)
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(120)
        };

        LongClient = new HttpClient(_handler)
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromMinutes(7)
        };
    }

    public void Dispose()
    {
        LongClient.Dispose();
        Client.Dispose();
        _handler.Dispose();
        GC.SuppressFinalize(this);
    }
}
