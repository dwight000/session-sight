using SessionSight.Api.DTOs;
using SessionSight.Core.Entities;

namespace SessionSight.Api.Mapping;

public static class TherapistMappings
{
    public static TherapistDto ToDto(this Therapist therapist) =>
        new(therapist.Id, therapist.Name, therapist.LicenseNumber, therapist.Credentials,
            therapist.IsActive, therapist.CreatedAt, therapist.UpdatedAt);

    public static Therapist ToEntity(this CreateTherapistRequest request) =>
        new()
        {
            Name = request.Name,
            LicenseNumber = request.LicenseNumber,
            Credentials = request.Credentials,
            IsActive = request.IsActive
        };
}
