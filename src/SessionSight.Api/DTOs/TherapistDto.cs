using System.Text.Json.Serialization;

namespace SessionSight.Api.DTOs;

public record TherapistDto(
    Guid Id,
    string Name,
    string? LicenseNumber,
    string? Credentials,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateTherapistRequest(
    string Name,
    string? LicenseNumber,
    string? Credentials,
    [property: JsonRequired] bool IsActive);

public record UpdateTherapistRequest(
    string Name,
    string? LicenseNumber,
    string? Credentials,
    [property: JsonRequired] bool IsActive);
