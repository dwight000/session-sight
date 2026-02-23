using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using SessionSight.Api.DTOs;
using SessionSight.Api.Tests.Integration;
using SessionSight.Core.Enums;
using SessionSight.Core.Schema;

namespace SessionSight.Api.Tests.Contract;

/// <summary>
/// Contract tests that verify JSON serialization shape of API DTOs.
/// These catch frontend/backend field-name drift that mocked unit tests miss.
/// Two patterns: HTTP round-trip (CRUD DTOs) and manual serialization (Azure-dependent DTOs).
/// </summary>
public class ContractTests : IntegrationTestBase
{
    /// <summary>
    /// Mirrors the JSON options configured in Program.cs (lines 134-139).
    /// Used for manual-serialization contract tests where HTTP round-trip isn't possible.
    /// </summary>
    private static readonly JsonSerializerOptions s_apiOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ──────────────────────────────────────────────
    //  HTTP round-trip contract tests
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PatientDto_JsonShape_MatchesContract()
    {
        var create = new CreatePatientRequest("CTR-P01", "Ada", "Lovelace", new DateOnly(1815, 12, 10));
        var post = await Client.PostAsJsonAsync("/api/patients", create);
        post.EnsureSuccessStatusCode();
        var created = await post.Content.ReadFromJsonAsync<PatientDto>();

        var get = await Client.GetAsync($"/api/patients/{created!.Id}");
        get.EnsureSuccessStatusCode();

        var json = await get.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var keys = root.EnumerateObject().Select(p => p.Name).ToList();

        keys.Should().BeEquivalentTo([
            "id", "externalId", "firstName", "lastName",
            "dateOfBirth", "createdAt", "updatedAt",
        ]);

        // dateOfBirth serializes as a string, not an object
        root.GetProperty("dateOfBirth").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task SessionDto_JsonShape_MatchesContract()
    {
        // Create prerequisite patient + therapist
        var patient = await CreatePatient();
        var therapist = await CreateTherapist();

        var create = new CreateSessionRequest(
            patient.Id, therapist.Id,
            new DateOnly(2025, 6, 1), SessionType.Individual,
            SessionModality.InPerson, 50, 1);
        var post = await Client.PostAsJsonAsync("/api/sessions", create);
        post.EnsureSuccessStatusCode();
        var created = await post.Content.ReadFromJsonAsync<SessionDto>();

        var get = await Client.GetAsync($"/api/sessions/{created!.Id}");
        get.EnsureSuccessStatusCode();

        var json = await get.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var keys = root.EnumerateObject().Select(p => p.Name).ToList();

        // documentStatus is null for a new session → omitted by WhenWritingNull
        keys.Should().BeEquivalentTo([
            "id", "patientId", "therapistId", "sessionDate",
            "sessionType", "modality", "durationMinutes",
            "sessionNumber", "hasDocument", "createdAt", "updatedAt",
        ]);

        // Enum values serialize as strings, not ints
        root.GetProperty("sessionType").ValueKind.Should().Be(JsonValueKind.String);
        root.GetProperty("modality").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task TherapistDto_JsonShape_MatchesContract()
    {
        // Create therapist without nullable fields to verify WhenWritingNull
        var create = new CreateTherapistRequest("Dr. Test", null, null, true);
        var post = await Client.PostAsJsonAsync("/api/therapists", create);
        post.EnsureSuccessStatusCode();
        var created = await post.Content.ReadFromJsonAsync<TherapistDto>();

        var get = await Client.GetAsync($"/api/therapists/{created!.Id}");
        get.EnsureSuccessStatusCode();

        var json = await get.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var keys = root.EnumerateObject().Select(p => p.Name).ToList();

        // licenseNumber and credentials are null → omitted; updatedAt may be null too
        keys.Should().Contain("id");
        keys.Should().Contain("name");
        keys.Should().Contain("isActive");
        keys.Should().Contain("createdAt");
        keys.Should().NotContain("licenseNumber");
        keys.Should().NotContain("credentials");
    }

    [Fact]
    public async Task TherapistDto_WithOptionalFields_IncludesThem()
    {
        var create = new CreateTherapistRequest("Dr. Full", "LIC-123", "LCSW", true);
        var post = await Client.PostAsJsonAsync("/api/therapists", create);
        post.EnsureSuccessStatusCode();
        var created = await post.Content.ReadFromJsonAsync<TherapistDto>();

        var get = await Client.GetAsync($"/api/therapists/{created!.Id}");
        get.EnsureSuccessStatusCode();

        var json = await get.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        keys.Should().Contain("licenseNumber");
        keys.Should().Contain("credentials");
    }

    // ──────────────────────────────────────────────
    //  Manual-serialization contract tests
    // ──────────────────────────────────────────────

    [Fact]
    public void ExtractionResultDto_JsonShape_MatchesContract()
    {
        var dto = new ExtractionResultDto(
            Guid.NewGuid(), Guid.NewGuid(), "1.0", "gpt-4.1-mini",
            0.85, true, DateTime.UtcNow,
            new ClinicalExtraction(),
            new RiskDiagnosticsDto(
                true,
                new GuardrailDetailDto(true, "elevated risk"),
                null,
                2, 1,
                false,
                [
                    new RiskFieldDecisionDto(
                        "suicidalIdeation", "low", "moderate", "moderate",
                        "escalate", ["presence", "frequency"], "criteria match"),
                ]));

        var json = JsonSerializer.Serialize(dto, s_apiOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var topKeys = root.EnumerateObject().Select(p => p.Name).ToList();
        topKeys.Should().BeEquivalentTo([
            "id", "sessionId", "schemaVersion", "modelUsed",
            "overallConfidence", "requiresReview", "extractedAt",
            "data", "riskDiagnostics",
        ]);

        // Nested riskDiagnostics shape
        var risk = root.GetProperty("riskDiagnostics");
        var riskKeys = risk.EnumerateObject().Select(p => p.Name).ToList();
        riskKeys.Should().BeEquivalentTo([
            "guardrailApplied", "homicidalGuardrail",
            "criteriaValidationAttempts", "discrepancyCount",
            "contentFilterBlocked", "fieldDecisions",
        ]);
        // selfHarmGuardrail is null → omitted
        riskKeys.Should().NotContain("selfHarmGuardrail");

        // fieldDecisions[0] shape
        var fd = risk.GetProperty("fieldDecisions")[0];
        var fdKeys = fd.EnumerateObject().Select(p => p.Name).ToList();
        fdKeys.Should().BeEquivalentTo([
            "field", "originalValue", "reExtractedValue", "finalValue",
            "ruleApplied", "criteriaUsed", "reasoningUsed",
        ]);
        fd.GetProperty("criteriaUsed").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public void ExtractionResultDto_NullRiskDiagnostics_IsOmitted()
    {
        var dto = new ExtractionResultDto(
            Guid.NewGuid(), Guid.NewGuid(), "1.0", "gpt-4.1-mini",
            0.90, false, DateTime.UtcNow,
            new ClinicalExtraction(), null);

        var json = JsonSerializer.Serialize(dto, s_apiOptions);
        using var doc = JsonDocument.Parse(json);
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        keys.Should().NotContain("riskDiagnostics");
    }

    [Fact]
    public void ExtractionStepsResponseDto_JsonShape_MatchesContract()
    {
        var dto = new ExtractionStepsResponseDto(
            Guid.NewGuid(), "Completed",
            [
                new ExtractionStepDto(
                    Guid.NewGuid(), "Intake", "Completed", 1,
                    DateTime.UtcNow, DateTime.UtcNow, 1200,
                    "gpt-4.1-nano", 100, 50, 150, null, null,
                    [], []),
            ]);

        var json = JsonSerializer.Serialize(dto, s_apiOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("steps").ValueKind.Should().Be(JsonValueKind.Array);
        var step = root.GetProperty("steps")[0];
        var stepKeys = step.EnumerateObject().Select(p => p.Name).ToList();
        stepKeys.Should().Contain("toolCalls");
        stepKeys.Should().Contain("llmTraces");

        // Empty arrays are still present (not omitted by WhenWritingNull)
        step.GetProperty("toolCalls").ValueKind.Should().Be(JsonValueKind.Array);
        step.GetProperty("llmTraces").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public void ReviewQueueItemDto_JsonShape_MatchesContract()
    {
        var dto = new ReviewQueueItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "Jane Doe",
            new DateOnly(2025, 5, 1), ReviewStatus.Pending,
            0.65, ["Low confidence"], DateTime.UtcNow);

        var json = JsonSerializer.Serialize(dto, s_apiOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // reviewStatus serializes as string "Pending", not int
        root.GetProperty("reviewStatus").GetString().Should().Be("Pending");
        root.GetProperty("reviewReasons").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public void ReviewStatsDto_JsonShape_MatchesContract()
    {
        var dto = new ReviewStatsDto(5, 3, 1);

        var json = JsonSerializer.Serialize(dto, s_apiOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var keys = root.EnumerateObject().Select(p => p.Name).ToList();
        keys.Should().BeEquivalentTo(["pendingCount", "approvedToday", "dismissedToday"]);

        root.GetProperty("pendingCount").GetInt32().Should().Be(5);
        root.GetProperty("approvedToday").GetInt32().Should().Be(3);
        root.GetProperty("dismissedToday").GetInt32().Should().Be(1);
    }

    [Fact]
    public void UploadDocumentResponse_JsonShape_MatchesContract()
    {
        var dto = new UploadDocumentResponse(
            Guid.NewGuid(), Guid.NewGuid(),
            "therapy-note.pdf", "https://blob.example.com/doc.pdf", "Uploaded");

        var json = JsonSerializer.Serialize(dto, s_apiOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var keys = root.EnumerateObject().Select(p => p.Name).ToList();
        keys.Should().BeEquivalentTo([
            "documentId", "sessionId", "originalFileName", "blobUri", "status",
        ]);

        // Document known drift: frontend uses "fileName" but backend sends "originalFileName"
        keys.Should().Contain("originalFileName");
        keys.Should().NotContain("fileName");
    }

    // ──────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────

    private async Task<PatientDto> CreatePatient()
    {
        var req = new CreatePatientRequest($"CTR-{Guid.NewGuid():N}", "Test", "Patient", new DateOnly(1990, 1, 1));
        var resp = await Client.PostAsJsonAsync("/api/patients", req);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PatientDto>())!;
    }

    private async Task<TherapistDto> CreateTherapist()
    {
        var req = new CreateTherapistRequest("Dr. Contract", null, null, true);
        var resp = await Client.PostAsJsonAsync("/api/therapists", req);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<TherapistDto>())!;
    }
}
