using Microsoft.Extensions.Configuration;

namespace SessionSight.FunctionalTests.Fixtures;

/// <summary>
/// Provides an HttpClient configured to hit the running API.
/// Base URL is configurable via:
/// 1. Environment variable: API_BASE_URL
/// 2. appsettings.Test.json: ApiBaseUrl
/// Default: http://localhost:5001
/// </summary>
public class ApiFixture : IDisposable
{
    public HttpClient Client { get; }
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
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public void Dispose()
    {
        Client.Dispose();
        _handler.Dispose();
        GC.SuppressFinalize(this);
    }
}
