# SessionSight API Dockerfile
# Multi-stage build for .NET 9 API

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app
EXPOSE 8080

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy solution and project files for restore
COPY session-sight.sln ./
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY src/SessionSight.Api/SessionSight.Api.csproj src/SessionSight.Api/
COPY src/SessionSight.Core/SessionSight.Core.csproj src/SessionSight.Core/
COPY src/SessionSight.Agents/SessionSight.Agents.csproj src/SessionSight.Agents/
COPY src/SessionSight.Infrastructure/SessionSight.Infrastructure.csproj src/SessionSight.Infrastructure/
COPY src/SessionSight.ServiceDefaults/SessionSight.ServiceDefaults.csproj src/SessionSight.ServiceDefaults/

# Restore dependencies
RUN dotnet restore src/SessionSight.Api/SessionSight.Api.csproj

# Copy source code
COPY src/SessionSight.Api/ src/SessionSight.Api/
COPY src/SessionSight.Core/ src/SessionSight.Core/
COPY src/SessionSight.Agents/ src/SessionSight.Agents/
COPY src/SessionSight.Infrastructure/ src/SessionSight.Infrastructure/
COPY src/SessionSight.ServiceDefaults/ src/SessionSight.ServiceDefaults/

# Build and publish
WORKDIR /src/src/SessionSight.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

# Run as non-root user
USER $APP_UID

ENTRYPOINT ["dotnet", "SessionSight.Api.dll"]
