using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SessionSight.Api.DTOs;

namespace SessionSight.Api.Tests.Integration;

public class ProcessingJobsIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task GetAll_ReturnsOkWithArray()
    {
        var response = await Client.GetAsync("/api/processing-jobs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jobs = await response.Content.ReadFromJsonAsync<ProcessingJobDto[]>();
        jobs.Should().NotBeNull();
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        var response = await Client.GetAsync($"/api/processing-jobs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
