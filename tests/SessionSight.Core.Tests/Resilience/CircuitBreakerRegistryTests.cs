using FluentAssertions;
using SessionSight.Core.Resilience;

namespace SessionSight.Core.Tests.Resilience;

public class CircuitBreakerRegistryTests
{
    [Fact]
    public void Get_ReturnsSameInstanceForSameName()
    {
        var registry = new CircuitBreakerRegistry();

        var first = registry.Get("openai");
        var second = registry.Get("openai");

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void Get_ReturnsDifferentInstancesForDifferentNames()
    {
        var registry = new CircuitBreakerRegistry();

        var openai = registry.Get("openai");
        var search = registry.Get("search");

        openai.Should().NotBeSameAs(search);
    }

    [Fact]
    public void Get_CreatedBreaker_StartsClosed()
    {
        var registry = new CircuitBreakerRegistry();

        var breaker = registry.Get("test");

        breaker.State.Should().Be(CircuitState.Closed);
    }
}
