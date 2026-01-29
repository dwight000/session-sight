using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using Scalar.AspNetCore;
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
