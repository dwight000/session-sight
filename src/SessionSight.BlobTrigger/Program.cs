using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Configure BlobServiceClient
builder.Services.AddSingleton(sp =>
{
    var connectionString = builder.Configuration["StorageConnection"] ??
        Environment.GetEnvironmentVariable("StorageConnection");
    return new BlobServiceClient(connectionString);
});

// Configure HttpClient for API calls
builder.Services.AddHttpClient("SessionSightApi", client =>
{
    var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ??
        Environment.GetEnvironmentVariable("ApiBaseUrl") ??
        "http://localhost:5001";
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

await builder.Build().RunAsync();
