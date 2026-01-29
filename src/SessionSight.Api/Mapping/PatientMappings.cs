using SessionSight.Api.DTOs;
using SessionSight.Core.Entities;

namespace SessionSight.Api.Mapping;

public static class PatientMappings
{
    public static PatientDto ToDto(this Patient patient) =>
        new(patient.Id, patient.ExternalId, patient.FirstName, patient.LastName,
            patient.DateOfBirth, patient.CreatedAt, patient.UpdatedAt);

    public static Patient ToEntity(this CreatePatientRequest request) =>
        new()
        {
            ExternalId = request.ExternalId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth
        };
}
