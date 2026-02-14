using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SessionSight.Api.DTOs;
using SessionSight.Infrastructure.Data;

namespace SessionSight.Api.Tests.Integration;

public class TherapistsIntegrationTests : IntegrationTestBase
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // EnsureCreated applies HasData seed (required for in-memory provider)
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SessionSightDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task FullRoundTrip_CreateGetUpdateDeleteTherapist()
    {
        // Create
        var createRequest = new CreateTherapistRequest("Dr. Integration", "LIC-INT", "LCSW", true);
        var createResponse = await Client.PostAsJsonAsync("/api/therapists", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();

        var created = await createResponse.Content.ReadFromJsonAsync<TherapistDto>();
        created.Should().NotBeNull();
        created!.Name.Should().Be("Dr. Integration");
        created.LicenseNumber.Should().Be("LIC-INT");
        created.Credentials.Should().Be("LCSW");
        created.IsActive.Should().BeTrue();

        // Get by ID
        var getResponse = await Client.GetAsync($"/api/therapists/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<TherapistDto>();
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(created.Id);
        fetched.Name.Should().Be("Dr. Integration");

        // Update
        var updateRequest = new UpdateTherapistRequest("Dr. Updated", "LIC-UPD", "PhD", false);
        var updateResponse = await Client.PutAsJsonAsync($"/api/therapists/{created.Id}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<TherapistDto>();
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Dr. Updated");
        updated.LicenseNumber.Should().Be("LIC-UPD");
        updated.Credentials.Should().Be("PhD");
        updated.IsActive.Should().BeFalse();

        // Verify update via GET
        var verifyResponse = await Client.GetAsync($"/api/therapists/{created.Id}");
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var verified = await verifyResponse.Content.ReadFromJsonAsync<TherapistDto>();
        verified.Should().NotBeNull();
        verified!.Name.Should().Be("Dr. Updated");

        // Delete
        var deleteResponse = await Client.DeleteAsync($"/api/therapists/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted
        var deletedResponse = await Client.GetAsync($"/api/therapists/{created.Id}");
        deletedResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SeedTherapist_ExistsInGetAll()
    {
        var response = await Client.GetAsync("/api/therapists");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var therapists = await response.Content.ReadFromJsonAsync<TherapistDto[]>();
        therapists.Should().NotBeNull();

        var seedId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        therapists.Should().Contain(t => t.Id == seedId);
    }
}
