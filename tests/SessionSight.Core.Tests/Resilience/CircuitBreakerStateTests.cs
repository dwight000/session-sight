using FluentAssertions;
using SessionSight.Core.Resilience;

namespace SessionSight.Core.Tests.Resilience;

public class CircuitBreakerStateTests
{
    [Fact]
    public void NewBreaker_IsClosed()
    {
        var breaker = new CircuitBreakerState("test");

        breaker.State.Should().Be(CircuitState.Closed);
        breaker.IsOpen(out _).Should().BeFalse();
    }

    [Fact]
    public void BelowThreshold_StaysClosed()
    {
        var breaker = new CircuitBreakerState("test", failureThreshold: 5);

        for (var i = 0; i < 4; i++)
            breaker.RecordFailure();

        breaker.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void AtThreshold_Opens()
    {
        var breaker = new CircuitBreakerState("test", failureThreshold: 5);

        for (var i = 0; i < 5; i++)
            breaker.RecordFailure();

        breaker.State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public void Open_RejectsImmediately()
    {
        var breaker = new CircuitBreakerState("test", failureThreshold: 1);
        breaker.RecordFailure();

        breaker.IsOpen(out var remaining).Should().BeTrue();
        remaining.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void Open_AfterBreakDuration_TransitionsToHalfOpen()
    {
        var breaker = new CircuitBreakerState("test",
            failureThreshold: 1,
            breakDuration: TimeSpan.FromMilliseconds(1));

        breaker.RecordFailure();
        breaker.State.Should().Be(CircuitState.Open);

        Thread.Sleep(10); // Wait past break duration

        breaker.IsOpen(out _).Should().BeFalse();
        breaker.State.Should().Be(CircuitState.HalfOpen);
    }

    [Fact]
    public void HalfOpen_SuccessCloses()
    {
        var breaker = new CircuitBreakerState("test",
            failureThreshold: 1,
            breakDuration: TimeSpan.FromMilliseconds(1));

        breaker.RecordFailure();
        Thread.Sleep(10);
        breaker.IsOpen(out _); // Triggers HalfOpen transition

        breaker.RecordSuccess();

        breaker.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void HalfOpen_FailureReopens()
    {
        var breaker = new CircuitBreakerState("test",
            failureThreshold: 1,
            breakDuration: TimeSpan.FromMilliseconds(1));

        breaker.RecordFailure();
        Thread.Sleep(10);
        breaker.IsOpen(out _); // Triggers HalfOpen transition

        breaker.RecordFailure();

        breaker.State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public void SlidingWindow_OldFailuresExpire()
    {
        var breaker = new CircuitBreakerState("test",
            failureThreshold: 3,
            failureWindow: TimeSpan.FromMilliseconds(50));

        // Record 2 failures
        breaker.RecordFailure();
        breaker.RecordFailure();

        // Wait for them to expire
        Thread.Sleep(60);

        // Record 2 more (below threshold since old ones expired)
        breaker.RecordFailure();
        breaker.RecordFailure();

        breaker.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void Success_InClosed_DoesNothing()
    {
        var breaker = new CircuitBreakerState("test");

        breaker.RecordSuccess();

        breaker.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void ClosedAfterReset_AccumulatesNewFailures()
    {
        var breaker = new CircuitBreakerState("test",
            failureThreshold: 2,
            breakDuration: TimeSpan.FromMilliseconds(1));

        // Open and close via HalfOpen
        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.State.Should().Be(CircuitState.Open);

        Thread.Sleep(10);
        breaker.IsOpen(out _);
        breaker.RecordSuccess();
        breaker.State.Should().Be(CircuitState.Closed);

        // Now accumulate new failures — should open again
        breaker.RecordFailure();
        breaker.State.Should().Be(CircuitState.Closed);
        breaker.RecordFailure();
        breaker.State.Should().Be(CircuitState.Open);
    }
}
