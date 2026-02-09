using System.Text.Json.Serialization;
using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;
using SessionSight.Agents.Orchestration;
using SessionSight.Agents.Routing;
using SessionSight.Agents.Services;
using SessionSight.Agents.Tools;
using SessionSight.Agents.Validation;
using SessionSight.Api.Middleware;
using SessionSight.Api.Startup;
using SessionSight.Api.Validators;
using SessionSight.Core.Resilience;
using SessionSight.Infrastructure;
using SessionSight.Infrastructure.Search;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.EnsureConfiguredSerilogDirectories();

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

// Aspire ServiceDefaults (OpenTelemetry, health checks, resilience)
builder.AddServiceDefaults();

// Aspire-managed EF Core (SQL Server)
builder.AddSqlServerDbContext<SessionSight.Infrastructure.Data.SessionSightDbContext>("sessionsight");

// Aspire-managed Azure Blob Storage
builder.AddAzureBlobClient("documents");

// Infrastructure DI (repositories, blob storage)
builder.Services.AddScoped<SessionSight.Core.Interfaces.IPatientRepository, SessionSight.Infrastructure.Repositories.PatientRepository>();
builder.Services.AddScoped<SessionSight.Core.Interfaces.ISessionRepository, SessionSight.Infrastructure.Repositories.SessionRepository>();
builder.Services.AddScoped<SessionSight.Core.Interfaces.IDocumentStorage, SessionSight.Infrastructure.Storage.AzureBlobDocumentStorage>();
builder.Services.AddScoped<SessionSight.Core.Interfaces.IReviewRepository, SessionSight.Infrastructure.Repositories.ReviewRepository>();

// AI Foundry + Model Router + Agents
builder.Services.AddSingleton<IAIFoundryClientFactory, AIFoundryClientFactory>();
builder.Services.AddSingleton<IModelRouter, ModelRouter>();
builder.Services.AddScoped<IIntakeAgent, IntakeAgent>();
builder.Services.AddScoped<IClinicalExtractorAgent, ClinicalExtractorAgent>();
builder.Services.AddScoped<IRiskAssessorAgent, RiskAssessorAgent>();
builder.Services.AddScoped<ISummarizerAgent, SummarizerAgent>();
builder.Services.AddScoped<IQAAgent, QAAgent>();
builder.Services.AddScoped<ExtractionAgents>(sp => new ExtractionAgents(
    sp.GetRequiredService<IIntakeAgent>(),
    sp.GetRequiredService<IClinicalExtractorAgent>(),
    sp.GetRequiredService<IRiskAssessorAgent>(),
    sp.GetRequiredService<ISummarizerAgent>()));
builder.Services.AddSingleton<ISchemaValidator, SchemaValidator>();

// Agent tools (existing from P2-006a)
builder.Services.AddSingleton<IAgentTool, CheckRiskKeywordsTool>();
builder.Services.AddSingleton<IAgentTool, ValidateSchemaTool>();

// Agent tools (new in P2-006b)
builder.Services.AddSingleton<IAgentTool, ScoreConfidenceTool>();
builder.Services.AddScoped<IAgentTool, QueryPatientHistoryTool>();  // Scoped - needs repository
builder.Services.AddSingleton<IAgentTool, LookupDiagnosisCodeTool>();

// Agent loop runner
builder.Services.AddScoped<AgentLoopRunner>();

// Q&A Agent tools (concrete types — NOT as IAgentTool to keep separate from extraction tools)
builder.Services.AddScoped<SearchSessionsTool>();
builder.Services.AddScoped<GetSessionDetailTool>();
builder.Services.AddScoped<GetPatientTimelineTool>();
builder.Services.AddScoped<AggregateMetricsTool>();

// RiskAssessor configuration
builder.Services.Configure<RiskAssessorOptions>(
    builder.Configuration.GetSection(RiskAssessorOptions.SectionName));

// Document Intelligence configuration
builder.Services.Configure<DocumentIntelligenceOptions>(
    builder.Configuration.GetSection(DocumentIntelligenceOptions.SectionName));

builder.Services.Configure<RequestResponseLoggingOptions>(
    builder.Configuration.GetSection(RequestResponseLoggingOptions.SectionName));

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<DocumentIntelligenceOptions>>().Value;
    if (string.IsNullOrEmpty(options.Endpoint))
    {
        throw new InvalidOperationException(
            "DocumentIntelligence:Endpoint is not configured. " +
            "Set it via user-secrets: dotnet user-secrets set \"DocumentIntelligence:Endpoint\" \"https://...\"");
    }
    return new DocumentIntelligenceClient(
        new Uri(options.Endpoint),
        new DefaultAzureCredential(),
        AzureRetryDefaults.Configure(new DocumentIntelligenceClientOptions()));
});

builder.Services.AddScoped<IDocumentParser, DocumentIntelligenceParser>();

// Azure AI Search
builder.Services.Configure<SearchOptions>(
    builder.Configuration.GetSection(SearchOptions.SectionName));
builder.Services.AddSingleton<ISearchIndexService, SearchIndexService>();
builder.Services.AddHostedService<SearchIndexInitializer>();

// Embedding and Session Indexing
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<ISessionIndexingService, SessionIndexingService>();

// Extraction Orchestrator
builder.Services.AddScoped<IExtractionOrchestrator, ExtractionOrchestrator>();

// Controllers + JSON serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreatePatientValidator>();
builder.Services.AddFluentValidationAutoValidation();

// CORS for React dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// ProblemDetails + global exception handler
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// OpenAPI (.NET 9 native)
builder.Services.AddOpenApi();

var app = builder.Build();
var requestResponseLoggingOptions = app.Services
    .GetRequiredService<IOptions<RequestResponseLoggingOptions>>()
    .Value;

// Middleware pipeline
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();

app.ConfigureRequestResponseLogging(requestResponseLoggingOptions);
app.ConfigureDevelopmentEndpoints();

app.UseHttpsRedirection();
app.MapControllers();
app.MapDefaultEndpoints();

await app.RunAsync();

// Make the implicit Program class public for WebApplicationFactory integration tests
#pragma warning disable S1118 // Utility classes should not have public constructors
public partial class Program { }
#pragma warning restore S1118
