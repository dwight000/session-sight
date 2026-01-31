using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SessionSight.Core.Interfaces;
using SessionSight.Infrastructure.Data;

namespace SessionSight.Api.Tests.Integration;

/// <summary>
/// Base class for integration tests using WebApplicationFactory with in-memory database.
/// Each test class gets a fresh database instance for isolation.
/// </summary>
public class IntegrationTestBase : IAsyncLifetime
{
    protected HttpClient Client { get; private set; } = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private readonly string _databaseName = $"TestDb_{Guid.NewGuid()}";

    public virtual Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove all EF Core related services (from Aspire SqlServer integration)
                    var efDescriptors = services.Where(d =>
                        d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true ||
                        d.ServiceType.FullName?.Contains("SessionSightDbContext") == true ||
                        d.ImplementationType?.FullName?.Contains("EntityFrameworkCore") == true ||
                        d.ImplementationType?.FullName?.Contains("SessionSightDbContext") == true)
                        .ToList();

                    foreach (var descriptor in efDescriptors)
                    {
                        services.Remove(descriptor);
                    }

                    // Remove BlobServiceClient (from Aspire Azure Blob integration)
                    var blobDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(BlobServiceClient));
                    if (blobDescriptor != null)
                    {
                        services.Remove(blobDescriptor);
                    }

                    // Remove real document storage and add stub
                    var storageDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IDocumentStorage));
                    if (storageDescriptor != null)
                    {
                        services.Remove(storageDescriptor);
                    }
                    services.AddScoped<IDocumentStorage, StubDocumentStorage>();

                    // Add in-memory database with unique name per test class
                    services.AddDbContext<SessionSightDbContext>(options =>
                        options.UseInMemoryDatabase(_databaseName));
                });
            });

        Client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public virtual async Task DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
    }
}

/// <summary>
/// Stub implementation of IDocumentStorage for integration tests.
/// Does not actually store files - just returns fake URIs.
/// </summary>
internal class StubDocumentStorage : IDocumentStorage
{
    public Task<string> UploadAsync(string fileName, Stream content, string contentType)
        => Task.FromResult($"https://fake-storage.blob.core.windows.net/documents/{Guid.NewGuid()}/{fileName}");

    public Task<Stream> DownloadAsync(string blobUri)
        => Task.FromResult<Stream>(new MemoryStream());

    public Task DeleteAsync(string blobUri)
        => Task.CompletedTask;
}
