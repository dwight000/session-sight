using SessionSight.Agents.Tools;
using SessionSight.Core.Enums;

namespace SessionSight.Agents.Models;

public record DebateRound(int RoundNumber, string AdvocateArgument, string ChallengerArgument);

public class RiskDebateResult
{
    public List<DebateRound> Rounds { get; set; } = new();
    public string JudgeSynthesis { get; set; } = string.Empty;
    public RiskLevelOverall FinalRiskLevel { get; set; }
    public double FinalConfidence { get; set; }
    public bool RequiresReview { get; set; }
    public List<string> ReviewReasons { get; set; } = new();
    public string AdvocateModel { get; set; } = string.Empty;
    public string ChallengerModel { get; set; } = string.Empty;
    public string JudgeModel { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public IReadOnlyList<LlmCallTrace> LlmTraces { get; set; } = [];
}
