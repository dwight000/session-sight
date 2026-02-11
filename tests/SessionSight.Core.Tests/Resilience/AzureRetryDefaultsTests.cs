using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Search.Documents;
using FluentAssertions;
using SessionSight.Core.Resilience;
using System.ClientModel.Primitives;

namespace SessionSight.Core.Tests.Resilience;

public class AzureRetryDefaultsTests
{
    [Fact]
    public void Configure_SetsMaxRetries_ToFive()
    {
        var options = new SearchClientOptions();

        AzureRetryDefaults.Configure(options);

        options.Retry.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void Configure_SetsDelay_ToThreeSeconds()
    {
        var options = new SearchClientOptions();

        AzureRetryDefaults.Configure(options);

        options.Retry.Delay.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Configure_SetsMaxDelay_ToSixtySeconds()
    {
        var options = new SearchClientOptions();

        AzureRetryDefaults.Configure(options);

        options.Retry.MaxDelay.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void Configure_SetsExponentialMode()
    {
        var options = new SearchClientOptions();

        AzureRetryDefaults.Configure(options);

        options.Retry.Mode.Should().Be(RetryMode.Exponential);
    }

    [Fact]
    public void Configure_SetsNetworkTimeout()
    {
        var options = new SearchClientOptions();

        AzureRetryDefaults.Configure(options);

        options.Retry.NetworkTimeout.Should().Be(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void Configure_ReturnsTheSameInstance()
    {
        var options = new SearchClientOptions();

        var result = AzureRetryDefaults.Configure(options);

        result.Should().BeSameAs(options);
    }

    [Fact]
    public void ConfigureRetryPolicy_SetsRetryPolicy()
    {
        var options = new AzureOpenAIClientOptions();

        AzureRetryDefaults.ConfigureRetryPolicy(options);

        options.RetryPolicy.Should().NotBeNull();
        options.RetryPolicy.Should().BeOfType<SpacedRetryPolicy>();
    }

    [Fact]
    public void ConfigureRetryPolicy_ReturnsTheSameInstance()
    {
        var options = new AzureOpenAIClientOptions();

        var result = AzureRetryDefaults.ConfigureRetryPolicy(options);

        result.Should().BeSameAs(options);
    }
}
