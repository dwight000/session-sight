using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using SessionSight.Agents.Tools;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;

namespace SessionSight.Agents.Tests.Tools;

public class QueryPatientHistoryToolTests
{
    private readonly ISessionRepository _repository = Substitute.For<ISessionRepository>();
    private readonly QueryPatientHistoryTool _tool;

    public QueryPatientHistoryToolTests()
    {
        _tool = new QueryPatientHistoryTool(_repository);
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        _tool.Name.Should().Be("query_patient_history");
    }

    [Fact]
    public void InputSchema_IsValidJson()
    {
        var schema = _tool.InputSchema.ToString();
        var parsed = JsonDocument.Parse(schema);
        parsed.RootElement.GetProperty("type").GetString().Should().Be("object");
        parsed.RootElement.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain("patientId");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidPatientId_ReturnsSessions()
    {
        var patientId = Guid.NewGuid();
        var sessions = new List<Session>
        {
            CreateTestSession(patientId, DateOnly.FromDateTime(DateTime.Today), 1),
            CreateTestSession(patientId, DateOnly.FromDateTime(DateTime.Today.AddDays(-7)), 2)
        };
        _repository.GetByPatientIdAsync(patientId).Returns(sessions);

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString() });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        output.RootElement.GetProperty("SessionCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoSessions_ReturnsEmptyList()
    {
        var patientId = Guid.NewGuid();
        _repository.GetByPatientIdAsync(patientId).Returns(new List<Session>());

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString() });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        output.RootElement.GetProperty("SessionCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithMaxSessions_LimitsResults()
    {
        var patientId = Guid.NewGuid();
        var sessions = Enumerable.Range(1, 10)
            .Select(i => CreateTestSession(patientId, DateOnly.FromDateTime(DateTime.Today.AddDays(-i)), i))
            .ToList();
        _repository.GetByPatientIdAsync(patientId).Returns(sessions);

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString(), maxSessions = 3 });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        output.RootElement.GetProperty("SessionCount").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingPatientId_ReturnsError()
    {
        var input = BinaryData.FromObjectAsJson(new { });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("patientId");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyPatientId_ReturnsError()
    {
        var input = BinaryData.FromObjectAsJson(new { patientId = "" });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("patientId");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidGuid_ReturnsError()
    {
        var input = BinaryData.FromObjectAsJson(new { patientId = "not-a-guid" });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("GUID");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidJson_ReturnsError()
    {
        var input = BinaryData.FromString("not valid json");

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid JSON");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSessionsSortedByDateDescending()
    {
        var patientId = Guid.NewGuid();
        var sessions = new List<Session>
        {
            CreateTestSession(patientId, DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), 1),
            CreateTestSession(patientId, DateOnly.FromDateTime(DateTime.Today), 3),
            CreateTestSession(patientId, DateOnly.FromDateTime(DateTime.Today.AddDays(-15)), 2)
        };
        _repository.GetByPatientIdAsync(patientId).Returns(sessions);

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString() });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        var sessionsArray = output.RootElement.GetProperty("Sessions").EnumerateArray().ToList();
        sessionsArray[0].GetProperty("SessionNumber").GetInt32().Should().Be(3);
        sessionsArray[1].GetProperty("SessionNumber").GetInt32().Should().Be(2);
        sessionsArray[2].GetProperty("SessionNumber").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultsMaxSessionsToFive()
    {
        var patientId = Guid.NewGuid();
        var sessions = Enumerable.Range(1, 10)
            .Select(i => CreateTestSession(patientId, DateOnly.FromDateTime(DateTime.Today.AddDays(-i)), i))
            .ToList();
        _repository.GetByPatientIdAsync(patientId).Returns(sessions);

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString() });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        output.RootElement.GetProperty("SessionCount").GetInt32().Should().Be(5);
    }

    private static Session CreateTestSession(Guid patientId, DateOnly date, int sessionNumber)
    {
        return new Session
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            SessionDate = date,
            SessionNumber = sessionNumber,
            SessionType = SessionType.Individual,
            Modality = SessionModality.InPerson
        };
    }
}
