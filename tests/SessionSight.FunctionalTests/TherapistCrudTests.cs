using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SessionSight.FunctionalTests.Fixtures;

namespace SessionSight.FunctionalTests;

[Trait("Category", "Functional")]
public class TherapistCrudTests : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public TherapistCrudTests(ApiFixture fixture)
    {
        _client = fixture.Client;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    [Fact]
    public async Task SeedTherapist_ExistsAfterMigration()
    {
        var response = await _client.GetAsync("/api/therapists");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var therapists = await response.Content.ReadFromJsonAsync<JsonElement[]>(_jsonOptions);
        therapists.Should().NotBeNull();

        var seedId = "00000000-0000-0000-0000-000000000001";
        therapists.Should().Contain(t => t.GetProperty("id").GetString() == seedId);
    }

    [Fact]
    public async Task CreateTherapist_ReturnsCreated()
    {
        var request = new { name = "Func Test Therapist", licenseNumber = "LIC-FUNC", credentials = "LCSW", isActive = true };
        var response = await _client.PostAsJsonAsync("/api/therapists", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("name").GetString().Should().Be("Func Test Therapist");
        body.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetTherapist_AfterCreate_ReturnsOk()
    {
        var request = new { name = "GetTest Therapist", licenseNumber = "LIC-GET", credentials = "PhD", isActive = true };
        var createResponse = await _client.PostAsJsonAsync("/api/therapists", request);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var id = created.GetProperty("id").GetString();

        var getResponse = await _client.GetAsync($"/api/therapists/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("name").GetString().Should().Be("GetTest Therapist");
    }

    [Fact]
    public async Task UpdateTherapist_ModifiesFields()
    {
        var request = new { name = "Before Update", licenseNumber = "LIC-UPD", credentials = "MA", isActive = true };
        var createResponse = await _client.PostAsJsonAsync("/api/therapists", request);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var id = created.GetProperty("id").GetString();

        var updateRequest = new { name = "After Update", licenseNumber = "LIC-UPD2", credentials = "PhD", isActive = false };
        var updateResponse = await _client.PutAsJsonAsync($"/api/therapists/{id}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/therapists/{id}");
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("name").GetString().Should().Be("After Update");
        body.GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTherapist_ReturnsNoContent()
    {
        var request = new { name = "Delete Me", licenseNumber = (string?)null, credentials = (string?)null, isActive = true }; // NOSONAR - CodeQL cs/useless-upcast: explicit null for anonymous type
        var createResponse = await _client.PostAsJsonAsync("/api/therapists", request);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var id = created.GetProperty("id").GetString();

        var deleteResponse = await _client.DeleteAsync($"/api/therapists/{id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/therapists/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateSession_WithNewTherapist_Succeeds()
    {
        // Create therapist
        var therapistRequest = new { name = "Session FK Test", licenseNumber = "LIC-FK", credentials = "LCSW", isActive = true };
        var therapistResponse = await _client.PostAsJsonAsync("/api/therapists", therapistRequest);
        var therapist = await therapistResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var therapistId = therapist.GetProperty("id").GetString();

        // Create patient
        var patientRequest = new
        {
            externalId = $"FK-{Guid.NewGuid():N}",
            firstName = "FK",
            lastName = "Test",
            dateOfBirth = "1990-01-01"
        };
        var patientResponse = await _client.PostAsJsonAsync("/api/patients", patientRequest);
        patientResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var patient = await patientResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var patientId = patient.GetProperty("id").GetString();

        // Create session with new therapist
        var sessionRequest = new
        {
            patientId,
            therapistId,
            sessionDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            sessionType = "Individual",
            modality = "InPerson",
            sessionNumber = 1
        };
        var sessionResponse = await _client.PostAsJsonAsync("/api/sessions", sessionRequest);
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "Session creation should succeed with new therapist FK");
    }

    [Fact]
    public async Task GetProcessingJobs_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/processing-jobs");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().StartWith("["); // Verify it's an array
    }
}
