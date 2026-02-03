using FluentAssertions;
using SessionSight.Api.Helpers;

namespace SessionSight.Api.Tests.Helpers;

public class PhiRedactionTests
{
    [Fact]
    public void HashPatientId_ReturnsEightCharacters()
    {
        var patientId = Guid.NewGuid();

        var hash = PhiRedaction.HashPatientId(patientId);

        hash.Should().HaveLength(8);
    }

    [Fact]
    public void HashPatientId_SameInput_ReturnsSameOutput()
    {
        var patientId = Guid.NewGuid();

        var hash1 = PhiRedaction.HashPatientId(patientId);
        var hash2 = PhiRedaction.HashPatientId(patientId);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void HashPatientId_DifferentInputs_ReturnDifferentOutputs()
    {
        var patientId1 = Guid.NewGuid();
        var patientId2 = Guid.NewGuid();

        var hash1 = PhiRedaction.HashPatientId(patientId1);
        var hash2 = PhiRedaction.HashPatientId(patientId2);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void HashPatientId_EmptyGuid_ReturnsConsistentHash()
    {
        var hash1 = PhiRedaction.HashPatientId(Guid.Empty);
        var hash2 = PhiRedaction.HashPatientId(Guid.Empty);

        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(8);
    }

    [Fact]
    public void HashPatientId_ReturnsBase64EncodedString()
    {
        var patientId = Guid.NewGuid();

        var hash = PhiRedaction.HashPatientId(patientId);

        // Base64 characters include A-Z, a-z, 0-9, +, /, and =
        hash.Should().MatchRegex(@"^[A-Za-z0-9+/=]+$");
    }

    [Fact]
    public void HashPatientId_DoesNotContainOriginalGuid()
    {
        var patientId = Guid.NewGuid();

        var hash = PhiRedaction.HashPatientId(patientId);

        hash.Should().NotContain(patientId.ToString());
        hash.Should().NotContain(patientId.ToString("N"));
    }
}
