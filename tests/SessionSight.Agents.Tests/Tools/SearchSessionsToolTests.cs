using System.Text.Json;
using Azure.Search.Documents.Models;
using FluentAssertions;
using NSubstitute;
using SessionSight.Agents.Services;
using SessionSight.Agents.Tools;
using SessionSight.Infrastructure.Search;

namespace SessionSight.Agents.Tests.Tools;

public class SearchSessionsToolTests
{
    private readonly ISearchIndexService _searchIndexService = Substitute.For<ISearchIndexService>();
    private readonly IEmbeddingService _embeddingService = Substitute.For<IEmbeddingService>();
    private readonly SearchSessionsTool _tool;

    public SearchSessionsToolTests()
    {
        _tool = new SearchSessionsTool(_searchIndexService, _embeddingService);
        _embeddingService.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new float[3072]);
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        _tool.Name.Should().Be("search_sessions");
    }

    [Fact]
    public void InputSchema_IsValidJson()
    {
        var schema = _tool.InputSchema.ToString();
        var parsed = JsonDocument.Parse(schema);
        parsed.RootElement.GetProperty("type").GetString().Should().Be("object");
        parsed.RootElement.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain("query");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidQuery_ReturnsResults()
    {
        var sessionId = Guid.NewGuid().ToString();
        var doc = new SessionSearchDocument
        {
            SessionId = sessionId,
            SessionDate = new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero),
            SessionType = "Individual",
            Summary = "Patient discussed anxiety.",
            RiskLevel = "Low"
        };

        SetupSearchResults(doc, 0.85);

        var input = BinaryData.FromObjectAsJson(new { query = "anxiety treatment" });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        output.RootElement.GetProperty("ResultCount").GetInt32().Should().Be(1);
        var results = output.RootElement.GetProperty("Results").EnumerateArray().ToList();
        results[0].GetProperty("SessionId").GetString().Should().Be(sessionId);
        results[0].GetProperty("RelevanceScore").GetDouble().Should().Be(0.85);
    }

    [Fact]
    public async Task ExecuteAsync_WithPatientFilter_PassesFilterToSearch()
    {
        var patientId = Guid.NewGuid();
        SetupSearchResults();

        var input = BinaryData.FromObjectAsJson(new { query = "mood changes", patientId = patientId.ToString() });
        await _tool.ExecuteAsync(input);

        await _searchIndexService.Received(1).SearchAsync(
            "mood changes",
            Arg.Any<float[]>(),
            patientId.ToString("D"),
            5,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNoResults_ReturnsEmptyList()
    {
        SetupSearchResults();

        var input = BinaryData.FromObjectAsJson(new { query = "nonexistent topic" });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        output.RootElement.GetProperty("ResultCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingQuery_ReturnsError()
    {
        var input = BinaryData.FromObjectAsJson(new { });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("query");
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
    public async Task ExecuteAsync_DefaultsMaxResultsToFive()
    {
        SetupSearchResults();

        var input = BinaryData.FromObjectAsJson(new { query = "test query" });
        await _tool.ExecuteAsync(input);

        await _searchIndexService.Received(1).SearchAsync(
            Arg.Any<string>(),
            Arg.Any<float[]>(),
            Arg.Any<string?>(),
            5,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidPatientId_ReturnsError()
    {
        var input = BinaryData.FromObjectAsJson(new { query = "test", patientId = "not-a-guid" });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("GUID");
    }

    private void SetupSearchResults(params (SessionSearchDocument Doc, double Score)[] items)
    {
        // Use the SearchModelFactory to create proper SearchResult instances
        var results = items.Select(i =>
            SearchModelFactory.SearchResult(i.Doc, i.Score, null)).ToList();

        _searchIndexService.SearchAsync(
            Arg.Any<string>(),
            Arg.Any<float[]>(),
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns(results);
    }

    private void SetupSearchResults(SessionSearchDocument doc, double score)
    {
        SetupSearchResults((doc, score));
    }

    private void SetupSearchResults()
    {
        _searchIndexService.SearchAsync(
            Arg.Any<string>(),
            Arg.Any<float[]>(),
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<SearchResult<SessionSearchDocument>>());
    }
}
