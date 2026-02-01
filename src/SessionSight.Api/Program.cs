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
using SessionSight.Agents.Validation;
using SessionSight.Api.Middleware;
using SessionSight.Api.Validators;
using SessionSight.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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

// AI Foundry + Model Router + Agents
builder.Services.AddSingleton<IAIFoundryClientFactory, AIFoundryClientFactory>();
builder.Services.AddSingleton<IModelRouter, ModelRouter>();
builder.Services.AddScoped<IIntakeAgent, IntakeAgent>();
builder.Services.AddScoped<IClinicalExtractorAgent, ClinicalExtractorAgent>();
builder.Services.AddScoped<IRiskAssessorAgent, RiskAssessorAgent>();
builder.Services.AddSingleton<ISchemaValidator, SchemaValidator>();
builder.Services.AddSingleton<ConfidenceCalculator>();

// RiskAssessor configuration
builder.Services.Configure<RiskAssessorOptions>(
    builder.Configuration.GetSection(RiskAssessorOptions.SectionName));

// Document Intelligence configuration
builder.Services.Configure<DocumentIntelligenceOptions>(
    builder.Configuration.GetSection(DocumentIntelligenceOptions.SectionName));

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<DocumentIntelligenceOptions>>().Value;
    if (string.IsNullOrEmpty(options.Endpoint))
    {
        // Return null client for local dev without Document Intelligence
        return null!;
    }
    return new DocumentIntelligenceClient(
        new Uri(options.Endpoint),
        new DefaultAzureCredential());
});

builder.Services.AddScoped<IDocumentParser, DocumentIntelligenceParser>();

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

// ProblemDetails + global exception handler
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// OpenAPI (.NET 9 native)
builder.Services.AddOpenApi();

var app = builder.Build();

// Middleware pipeline
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();

// Make the implicit Program class public for WebApplicationFactory integration tests
public partial class Program { }
