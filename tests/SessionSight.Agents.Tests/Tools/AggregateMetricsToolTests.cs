using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using SessionSight.Agents.Tools;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Tests.Tools;

public class AggregateMetricsToolTests
{
    private readonly ISessionRepository _repository = Substitute.For<ISessionRepository>();
    private readonly AggregateMetricsTool _tool;

    public AggregateMetricsToolTests()
    {
        _tool = new AggregateMetricsTool(_repository);
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        _tool.Name.Should().Be("aggregate_metrics");
    }

    [Fact]
    public void InputSchema_IsValidJson()
    {
        var schema = _tool.InputSchema.ToString();
        var parsed = JsonDocument.Parse(schema);
        parsed.RootElement.GetProperty("type").GetString().Should().Be("object");
        var required = parsed.RootElement.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        required.Should().Contain("patientId");
        required.Should().Contain("metricType");
    }

    [Fact]
    public async Task ExecuteAsync_MoodTrend_ReturnsAverageMinMaxTrend()
    {
        var patientId = Guid.NewGuid();
        var sessions = new List<Session>
        {
            CreateSessionWithMood(patientId, DateOnly.Parse("2024-01-01"), 1, 3),
            CreateSessionWithMood(patientId, DateOnly.Parse("2024-01-08"), 2, 5),
            CreateSessionWithMood(patientId, DateOnly.Parse("2024-01-15"), 3, 7)
        };
        _repository.GetByPatientIdAsync(patientId).Returns(sessions);

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString(), metricType = "mood_trend" });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        output.RootElement.GetProperty("average").GetDouble().Should().Be(5.0);
        output.RootElement.GetProperty("min").GetDouble().Should().Be(3);
        output.RootElement.GetProperty("max").GetDouble().Should().Be(7);
        output.RootElement.GetProperty("trend").GetString().Should().Be("improving");
    }

    [Fact]
    public async Task ExecuteAsync_SessionCount_ReturnsByTypeAndModality()
    {
        var patientId = Guid.NewGuid();
        var sessions = new List<Session>
        {
            CreateTestSession(patientId, DateOnly.Parse("2024-01-01"), 1, SessionType.Individual),
            CreateTestSession(patientId, DateOnly.Parse("2024-01-08"), 2, SessionType.Individual),
            CreateTestSession(patientId, DateOnly.Parse("2024-01-15"), 3, SessionType.Group)
        };
        _repository.GetByPatientIdAsync(patientId).Returns(sessions);

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString(), metricType = "session_count" });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        output.RootElement.GetProperty("total").GetInt32().Should().Be(3);
        output.RootElement.GetProperty("byType").GetProperty("Individual").GetInt32().Should().Be(2);
        output.RootElement.GetProperty("byType").GetProperty("Group").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_InterventionFrequency_ReturnsSortedCounts()
    {
        var patientId = Guid.NewGuid();
        var sessions = new List<Session>
        {
            CreateSessionWithInterventions(patientId, DateOnly.Parse("2024-01-01"), 1, [TechniqueUsed.Cbt, TechniqueUsed.Act]),
            CreateSessionWithInterventions(patientId, DateOnly.Parse("2024-01-08"), 2, [TechniqueUsed.Cbt]),
            CreateSessionWithInterventions(patientId, DateOnly.Parse("2024-01-15"), 3, [TechniqueUsed.Cbt, TechniqueUsed.Dbt])
        };
        _repository.GetByPatientIdAsync(patientId).Returns(sessions);

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString(), metricType = "intervention_frequency" });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        var interventions = output.RootElement.GetProperty("interventions").EnumerateArray().ToList();
        interventions[0].GetProperty("intervention").GetString().Should().Be("Cbt");
        interventions[0].GetProperty("count").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_RiskDistribution_ReturnsCountsByLevel()
    {
        var patientId = Guid.NewGuid();
        var sessions = new List<Session>
        {
            CreateSessionWithRisk(patientId, DateOnly.Parse("2024-01-01"), 1, RiskLevelOverall.Low),
            CreateSessionWithRisk(patientId, DateOnly.Parse("2024-01-08"), 2, RiskLevelOverall.Low),
            CreateSessionWithRisk(patientId, DateOnly.Parse("2024-01-15"), 3, RiskLevelOverall.Moderate)
        };
        _repository.GetByPatientIdAsync(patientId).Returns(sessions);

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString(), metricType = "risk_distribution" });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        output.RootElement.GetProperty("totalAssessed").GetInt32().Should().Be(3);
        output.RootElement.GetProperty("distribution").GetProperty("Low").GetInt32().Should().Be(2);
        output.RootElement.GetProperty("distribution").GetProperty("Moderate").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_DiagnosisHistory_ReturnsFirstAndLastSeen()
    {
        var patientId = Guid.NewGuid();
        var sessions = new List<Session>
        {
            CreateSessionWithDiagnosis(patientId, DateOnly.Parse("2024-01-01"), 1, "Generalized Anxiety"),
            CreateSessionWithDiagnosis(patientId, DateOnly.Parse("2024-02-01"), 2, "MDD"),
            CreateSessionWithDiagnosis(patientId, DateOnly.Parse("2024-03-01"), 3, "Generalized Anxiety")
        };
        _repository.GetByPatientIdAsync(patientId).Returns(sessions);

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString(), metricType = "diagnosis_history" });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        output.RootElement.GetProperty("totalDiagnoses").GetInt32().Should().Be(2);
        var diagnoses = output.RootElement.GetProperty("diagnoses").EnumerateArray().ToList();
        var anxiety = diagnoses.First(d => d.GetProperty("diagnosis").GetString() == "Generalized Anxiety");
        anxiety.GetProperty("firstSeen").GetString().Should().Be("2024-01-01");
        anxiety.GetProperty("lastSeen").GetString().Should().Be("2024-03-01");
    }

    [Fact]
    public async Task ExecuteAsync_NoSessions_ReturnsEmptyMetrics()
    {
        var patientId = Guid.NewGuid();
        _repository.GetByPatientIdAsync(patientId).Returns(new List<Session>());

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString(), metricType = "session_count" });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        output.RootElement.GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidMetricType_ReturnsError()
    {
        var patientId = Guid.NewGuid();
        _repository.GetByPatientIdAsync(patientId).Returns(new List<Session>());

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString(), metricType = "invalid_metric" });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("invalid_metric");
    }

    [Fact]
    public async Task ExecuteAsync_MissingPatientId_ReturnsError()
    {
        var input = BinaryData.FromObjectAsJson(new { metricType = "mood_trend" });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("patientId");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidGuid_ReturnsError()
    {
        var input = BinaryData.FromObjectAsJson(new { patientId = "not-a-guid", metricType = "mood_trend" });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("GUID");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidJson_ReturnsError()
    {
        var input = BinaryData.FromString("not valid json");

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid JSON");
    }

    [Fact]
    public async Task ExecuteAsync_MoodTrendDeclining_ReportsDeclining()
    {
        var patientId = Guid.NewGuid();
        var sessions = new List<Session>
        {
            CreateSessionWithMood(patientId, DateOnly.Parse("2024-01-01"), 1, 8),
            CreateSessionWithMood(patientId, DateOnly.Parse("2024-01-08"), 2, 4)
        };
        _repository.GetByPatientIdAsync(patientId).Returns(sessions);

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString(), metricType = "mood_trend" });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        output.RootElement.GetProperty("trend").GetString().Should().Be("declining");
    }

    private static Session CreateTestSession(Guid patientId, DateOnly date, int sessionNumber, SessionType type = SessionType.Individual)
    {
        return new Session
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            SessionDate = date,
            SessionNumber = sessionNumber,
            SessionType = type,
            Modality = SessionModality.InPerson
        };
    }

    private static Session CreateSessionWithMood(Guid patientId, DateOnly date, int sessionNumber, int moodScore)
    {
        var session = CreateTestSession(patientId, date, sessionNumber);
        session.Extraction = new ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Data = new ClinicalExtraction
            {
                MoodAssessment = new MoodAssessmentExtracted
                {
                    SelfReportedMood = new ExtractedField<int> { Value = moodScore }
                }
            }
        };
        return session;
    }

    private static Session CreateSessionWithInterventions(Guid patientId, DateOnly date, int sessionNumber, List<TechniqueUsed> techniques)
    {
        var session = CreateTestSession(patientId, date, sessionNumber);
        session.Extraction = new ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Data = new ClinicalExtraction
            {
                Interventions = new InterventionsExtracted
                {
                    TechniquesUsed = new ExtractedField<List<TechniqueUsed>> { Value = techniques }
                }
            }
        };
        return session;
    }

    private static Session CreateSessionWithRisk(Guid patientId, DateOnly date, int sessionNumber, RiskLevelOverall riskLevel)
    {
        var session = CreateTestSession(patientId, date, sessionNumber);
        session.Extraction = new ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Data = new ClinicalExtraction
            {
                RiskAssessment = new RiskAssessmentExtracted
                {
                    RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = riskLevel }
                }
            }
        };
        return session;
    }

    private static Session CreateSessionWithDiagnosis(Guid patientId, DateOnly date, int sessionNumber, string diagnosis)
    {
        var session = CreateTestSession(patientId, date, sessionNumber);
        session.Extraction = new ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Data = new ClinicalExtraction
            {
                Diagnoses = new DiagnosesExtracted
                {
                    PrimaryDiagnosis = new ExtractedField<string> { Value = diagnosis }
                }
            }
        };
        return session;
    }
}
