using System.Security.Cryptography;

namespace SessionSight.Api.Helpers;

public static class PhiRedaction
{
    public static string HashPatientId(Guid patientId) =>
        Convert.ToBase64String(SHA256.HashData(patientId.ToByteArray()))[..8];
}
