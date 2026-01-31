using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SessionSight.Api.DTOs;
using SessionSight.Core.Enums;

namespace SessionSight.Api.Tests.Integration;

public class SessionsIntegrationTests : IntegrationTestBase
{
    private async Task<PatientDto> CreateTestPatientAsync()
    {
        var request = new CreatePatientRequest(
            $"P-{Guid.NewGuid():N}",
            "Test",
            "Patient",
            new DateOnly(1990, 1, 1));
        var response = await Client.PostAsJsonAsync("/api/patients", request);
        return (await response.Content.ReadFromJsonAsync<PatientDto>())!;
    }

    [Fact]
    public async Task CreateSession_ValidRequest_ReturnsCreated()
    {
        // Arrange - Create patient first
        var patient = await CreateTestPatientAsync();
        var therapistId = Guid.NewGuid();

        var request = new CreateSessionRequest(
            patient.Id,
            therapistId,
            new DateOnly(2026, 1, 20),
            SessionType.Individual,
            SessionModality.InPerson,
            50,
            1);

        // Act
        var response = await Client.PostAsJsonAsync("/api/sessions", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var session = await response.Content.ReadFromJsonAsync<SessionDto>();
        session.Should().NotBeNull();
        session!.PatientId.Should().Be(patient.Id);
        session.TherapistId.Should().Be(therapistId);
        session.SessionType.Should().Be(SessionType.Individual);
        session.Modality.Should().Be(SessionModality.InPerson);
        session.DurationMinutes.Should().Be(50);
        session.SessionNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetSession_AfterCreate_ReturnsOk()
    {
        // Arrange - Create patient and session
        var patient = await CreateTestPatientAsync();
        var createRequest = new CreateSessionRequest(
            patient.Id,
            Guid.NewGuid(),
            new DateOnly(2026, 1, 21),
            SessionType.Intake,
            SessionModality.TelehealthVideo,
            60,
            1);
        var createResponse = await Client.PostAsJsonAsync("/api/sessions", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<SessionDto>();

        // Act
        var response = await Client.GetAsync($"/api/sessions/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<SessionDto>();
        session.Should().NotBeNull();
        session!.Id.Should().Be(created.Id);
        session.SessionType.Should().Be(SessionType.Intake);
    }

    [Fact]
    public async Task GetSession_NotFound_Returns404()
    {
        var response = await Client.GetAsync($"/api/sessions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSessionsByPatient_ReturnsOkWithArray()
    {
        // Arrange - Create patient with multiple sessions
        var patient = await CreateTestPatientAsync();
        var therapistId = Guid.NewGuid();

        await Client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(
            patient.Id, therapistId, new DateOnly(2026, 1, 15),
            SessionType.Intake, SessionModality.InPerson, 60, 1));
        await Client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(
            patient.Id, therapistId, new DateOnly(2026, 1, 22),
            SessionType.Individual, SessionModality.InPerson, 50, 2));

        // Act
        var response = await Client.GetAsync($"/api/patients/{patient.Id}/sessions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessions = await response.Content.ReadFromJsonAsync<SessionDto[]>();
        sessions.Should().NotBeNull();
        sessions!.Length.Should().Be(2);
        sessions.Should().AllSatisfy(s => s.PatientId.Should().Be(patient.Id));
    }

    [Fact]
    public async Task GetSessionsByPatient_NoSessions_ReturnsEmptyArray()
    {
        // Arrange - Create patient with no sessions
        var patient = await CreateTestPatientAsync();

        // Act
        var response = await Client.GetAsync($"/api/patients/{patient.Id}/sessions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessions = await response.Content.ReadFromJsonAsync<SessionDto[]>();
        sessions.Should().NotBeNull();
        sessions!.Length.Should().Be(0);
    }

    [Fact]
    public async Task UpdateSession_ValidRequest_ReturnsOk()
    {
        // Arrange - Create patient and session
        var patient = await CreateTestPatientAsync();
        var originalTherapist = Guid.NewGuid();
        var createRequest = new CreateSessionRequest(
            patient.Id,
            originalTherapist,
            new DateOnly(2026, 1, 25),
            SessionType.Individual,
            SessionModality.InPerson,
            50,
            1);
        var createResponse = await Client.PostAsJsonAsync("/api/sessions", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<SessionDto>();

        // Act - Update session
        var newTherapist = Guid.NewGuid();
        var updateRequest = new UpdateSessionRequest(
            newTherapist,
            new DateOnly(2026, 1, 26), // Different date
            SessionType.Family, // Different type
            SessionModality.TelehealthVideo, // Different modality
            75, // Different duration
            2); // Different session number

        var response = await Client.PutAsJsonAsync($"/api/sessions/{created!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<SessionDto>();
        updated.Should().NotBeNull();
        updated!.TherapistId.Should().Be(newTherapist);
        updated.SessionDate.Should().Be(new DateOnly(2026, 1, 26));
        updated.SessionType.Should().Be(SessionType.Family);
        updated.Modality.Should().Be(SessionModality.TelehealthVideo);
        updated.DurationMinutes.Should().Be(75);
        updated.SessionNumber.Should().Be(2);
    }

    [Fact]
    public async Task UpdateSession_NotFound_Returns404()
    {
        var updateRequest = new UpdateSessionRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 1, 1),
            SessionType.Individual,
            SessionModality.InPerson,
            50,
            1);

        var response = await Client.PutAsJsonAsync($"/api/sessions/{Guid.NewGuid()}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateSession_MissingPatientId_Returns400()
    {
        // Create request with empty patient ID (invalid)
        var request = new { sessionDate = "2026-01-20", sessionType = "Individual" };

        var response = await Client.PostAsJsonAsync("/api/sessions", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
