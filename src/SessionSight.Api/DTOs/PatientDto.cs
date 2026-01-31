namespace SessionSight.Api.DTOs;

public record PatientDto(
    Guid Id,
    string ExternalId,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreatePatientRequest(
    string ExternalId,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth);

public record UpdatePatientRequest(
    string ExternalId,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth);
