var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure — Azure SQL (RunAsContainer for local dev)
// Password from user-secrets or Parameters config. Set with:
//   cd src/SessionSight.AppHost && dotnet user-secrets set Parameters:sql-password "LocalDev#2026!"
// To connect manually: Server=localhost,{port};Database=sessionsight;User Id=sa;Password={password};TrustServerCertificate=true
var sqlPassword = builder.AddParameter("sql-password", secret: true);
var sql = builder.AddSqlServer("sql", password: sqlPassword)
    .WithLifetime(ContainerLifetime.Persistent);

var db = sql.AddDatabase("sessionsight");

// Infrastructure — Azure Blob Storage (RunAsEmulator for local dev)
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator.WithLifetime(ContainerLifetime.Persistent));

var blobs = storage.AddBlobs("documents");

// Blob containers for ingestion pipeline
_ = storage.AddBlobs("incoming");
_ = storage.AddBlobs("processing");
_ = storage.AddBlobs("processed");
_ = storage.AddBlobs("failed");

// Azure AI Search endpoint (for embedding/RAG). Set with:
//   cd src/SessionSight.AppHost && dotnet user-secrets set Parameters:search-endpoint "https://sessionsight-search-dev.search.windows.net"
var searchEndpoint = builder.AddParameter("search-endpoint");

// API project
var api = builder.AddProject<Projects.SessionSight_Api>("api")
    .WithReference(db).WaitFor(db)
    .WithReference(blobs)
    .WithEnvironment("AzureSearch__Endpoint", searchEndpoint);

// Web frontend
builder.AddNpmApp("web", "../SessionSight.Web", "dev")
    .WithReference(api)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
