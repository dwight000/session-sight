using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SessionSight.Api.DTOs;

namespace SessionSight.Api.Tests.Integration;

public class PatientsIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task CreatePatient_ValidRequest_ReturnsCreated()
    {
        var request = new CreatePatientRequest("P001", "John", "Doe", new DateOnly(1990, 1, 15));

        var response = await Client.PostAsJsonAsync("/api/patients", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var patient = await response.Content.ReadFromJsonAsync<PatientDto>();
        patient.Should().NotBeNull();
        patient!.ExternalId.Should().Be("P001");
        patient.FirstName.Should().Be("John");
        patient.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task GetPatient_AfterCreate_ReturnsOk()
    {
        // Arrange - Create patient first
        var createRequest = new CreatePatientRequest("P002", "Jane", "Smith", new DateOnly(1985, 6, 20));
        var createResponse = await Client.PostAsJsonAsync("/api/patients", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PatientDto>();

        // Act
        var response = await Client.GetAsync($"/api/patients/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var patient = await response.Content.ReadFromJsonAsync<PatientDto>();
        patient.Should().NotBeNull();
        patient!.Id.Should().Be(created.Id);
        patient.ExternalId.Should().Be("P002");
    }

    [Fact]
    public async Task GetPatient_NotFound_Returns404()
    {
        var response = await Client.GetAsync($"/api/patients/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllPatients_ReturnsOkWithArray()
    {
        // Arrange - Create two patients
        await Client.PostAsJsonAsync("/api/patients",
            new CreatePatientRequest("P003", "Alice", "Wonder", new DateOnly(1992, 3, 10)));
        await Client.PostAsJsonAsync("/api/patients",
            new CreatePatientRequest("P004", "Bob", "Builder", new DateOnly(1988, 8, 25)));

        // Act
        var response = await Client.GetAsync("/api/patients");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var patients = await response.Content.ReadFromJsonAsync<PatientDto[]>();
        patients.Should().NotBeNull();
        patients!.Length.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task UpdatePatient_ValidRequest_ReturnsOk()
    {
        // Arrange - Create patient first
        var createRequest = new CreatePatientRequest("P005", "Original", "Name", new DateOnly(1990, 5, 1));
        var createResponse = await Client.PostAsJsonAsync("/api/patients", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PatientDto>();

        // Act - Update the patient
        var updateRequest = new UpdatePatientRequest("P005-Updated", "Updated", "Name", new DateOnly(1990, 5, 1));
        var response = await Client.PutAsJsonAsync($"/api/patients/{created!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<PatientDto>();
        updated.Should().NotBeNull();
        updated!.ExternalId.Should().Be("P005-Updated");
        updated.FirstName.Should().Be("Updated");
    }

    [Fact]
    public async Task UpdatePatient_NotFound_Returns404()
    {
        var updateRequest = new UpdatePatientRequest("P999", "No", "One", new DateOnly(1990, 1, 1));

        var response = await Client.PutAsJsonAsync($"/api/patients/{Guid.NewGuid()}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePatient_ValidId_ReturnsNoContent()
    {
        // Arrange - Create patient first
        var createRequest = new CreatePatientRequest("P006", "ToDelete", "Patient", new DateOnly(1995, 12, 15));
        var createResponse = await Client.PostAsJsonAsync("/api/patients", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PatientDto>();

        // Act
        var response = await Client.DeleteAsync($"/api/patients/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify patient is gone
        var getResponse = await Client.GetAsync($"/api/patients/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePatient_NotFound_Returns404()
    {
        var response = await Client.DeleteAsync($"/api/patients/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreatePatient_MissingRequiredFields_Returns400()
    {
        var request = new { firstName = "", lastName = "Doe" }; // Missing externalId and invalid firstName

        var response = await Client.PostAsJsonAsync("/api/patients", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
