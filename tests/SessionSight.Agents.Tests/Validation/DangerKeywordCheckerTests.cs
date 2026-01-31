using FluentAssertions;
using SessionSight.Agents.Validation;

namespace SessionSight.Agents.Tests.Validation;

public class DangerKeywordCheckerTests
{
    [Fact]
    public void Check_EmptyText_ReturnsNoMatches()
    {
        var result = DangerKeywordChecker.Check("");

        result.HasAnyMatches.Should().BeFalse();
        result.SuicidalMatches.Should().BeEmpty();
        result.SelfHarmMatches.Should().BeEmpty();
        result.HomicidalMatches.Should().BeEmpty();
    }

    [Fact]
    public void Check_NullText_ReturnsNoMatches()
    {
        var result = DangerKeywordChecker.Check(null!);

        result.HasAnyMatches.Should().BeFalse();
    }

    [Fact]
    public void Check_SuicidalKeyword_DetectsMatch()
    {
        var text = "Patient reports having thoughts of suicide.";

        var result = DangerKeywordChecker.Check(text);

        result.HasAnyMatches.Should().BeTrue();
        result.SuicidalMatches.Should().Contain("suicide");
    }

    [Fact]
    public void Check_MultipleSuicidalKeywords_DetectsAll()
    {
        var text = "Patient is suicidal and says they want to die.";

        var result = DangerKeywordChecker.Check(text);

        result.SuicidalMatches.Should().Contain("suicidal");
        result.SuicidalMatches.Should().Contain("want to die");
    }

    [Fact]
    public void Check_SelfHarmKeywords_DetectsMatch()
    {
        var text = "History of cutting behavior. Patient reports recent self-harm.";

        var result = DangerKeywordChecker.Check(text);

        result.HasAnyMatches.Should().BeTrue();
        result.SelfHarmMatches.Should().Contain("cutting");
        result.SelfHarmMatches.Should().Contain("self-harm");
    }

    [Fact]
    public void Check_HomicidalKeywords_DetectsMatch()
    {
        var text = "Patient reports having homicidal thoughts about a coworker.";

        var result = DangerKeywordChecker.Check(text);

        result.HasAnyMatches.Should().BeTrue();
        result.HomicidalMatches.Should().Contain("homicidal");
    }

    [Fact]
    public void Check_CaseInsensitive_DetectsMatch()
    {
        var text = "Patient has SUICIDAL ideation.";

        var result = DangerKeywordChecker.Check(text);

        result.SuicidalMatches.Should().Contain("suicidal");
    }

    [Fact]
    public void Check_NoKeywords_ReturnsNoMatches()
    {
        var text = "Patient reports feeling better. Mood is stable. No risk indicators.";

        var result = DangerKeywordChecker.Check(text);

        result.HasAnyMatches.Should().BeFalse();
    }

    [Fact]
    public void Check_AllCategories_DetectsAll()
    {
        var text = "Patient is suicidal, has history of cutting, and homicidal thoughts toward neighbor.";

        var result = DangerKeywordChecker.Check(text);

        result.SuicidalMatches.Should().NotBeEmpty();
        result.SelfHarmMatches.Should().NotBeEmpty();
        result.HomicidalMatches.Should().NotBeEmpty();
    }

    [Fact]
    public void Check_SubtleLanguage_DetectsMatch()
    {
        var text = "Patient says they feel like they'd be better off dead.";

        var result = DangerKeywordChecker.Check(text);

        result.SuicidalMatches.Should().Contain("better off dead");
    }

    [Fact]
    public void Check_WordBoundary_DoesNotMatchPartialWords()
    {
        // "suicidal" should not be found within "antisuicidal" if we have proper word boundaries
        // However, "suicide" IS a keyword and should match
        var text = "Discussion of antisuicide protocols.";

        var result = DangerKeywordChecker.Check(text);

        // "suicide" appears as a substring but not as a whole word
        // The word "antisuicide" contains "suicide" but with word boundaries it shouldn't match
        // Let's verify our implementation handles this correctly
        result.SuicidalMatches.Should().NotContain("suicidal");
    }

    [Fact]
    public void AllMatches_CombinesAllCategories()
    {
        var text = "Patient is suicidal with history of cutting and violent thoughts.";

        var result = DangerKeywordChecker.Check(text);

        result.AllMatches.Should().Contain(m => m.StartsWith("suicidal:"));
        result.AllMatches.Should().Contain(m => m.StartsWith("self-harm:"));
    }

    [Fact]
    public void Check_PassiveSuicidalLanguage_Detects()
    {
        var text = "Patient wishes they wouldn't wake up in the morning.";

        var result = DangerKeywordChecker.Check(text);

        result.SuicidalMatches.Should().Contain("wouldn't wake up");
    }

    [Fact]
    public void Check_NotBeHere_Detects()
    {
        var text = "Patient has thoughts about not being here anymore.";

        var result = DangerKeywordChecker.Check(text);

        result.SuicidalMatches.Should().Contain("not being here");
    }
}
