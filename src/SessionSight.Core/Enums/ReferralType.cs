using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReferralType
{
    Psychiatry,
    Medical,
    GroupTherapy,
    IntensiveOutpatient,
    PartialHospitalization,
    Inpatient,
    Specialist,
    SupportGroup,
    CommunityResources
}
