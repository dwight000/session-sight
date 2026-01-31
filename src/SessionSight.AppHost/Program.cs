var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure — Azure SQL (RunAsContainer for local dev)
var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent);

var db = sql.AddDatabase("sessionsight");

// Infrastructure — Azure Blob Storage (RunAsEmulator for local dev)
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator.WithLifetime(ContainerLifetime.Persistent));

var blobs = storage.AddBlobs("documents");

// Azure-only resources (require Azure subscription — uncomment when deploying)
// var insights = builder.AddAzureApplicationInsights("insights");
// var keyVault = builder.AddAzureKeyVault("secrets");

// API project
builder.AddProject<Projects.SessionSight_Api>("api")
    .WithReference(db).WaitFor(db)
    .WithReference(blobs);
// .WithReference(insights)
// .WithReference(keyVault);

builder.Build().Run();
