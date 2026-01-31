using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SessionSight.Core.Interfaces;
using SessionSight.Infrastructure.Data;
using SessionSight.Infrastructure.Repositories;
using SessionSight.Infrastructure.Storage;

namespace SessionSight.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SessionSightDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IDocumentStorage, AzureBlobDocumentStorage>();

        return services;
    }
}
