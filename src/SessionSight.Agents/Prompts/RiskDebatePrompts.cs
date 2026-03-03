using System.Globalization;
using SessionSight.Agents.Models;

namespace SessionSight.Agents.Prompts;

public static class RiskDebatePrompts
{
    public const string AdvocateSystemPrompt =
        "You are a clinical safety advocate. Your role is to defend the given risk assessment with specific evidence from the therapy note. Be concise and cite the text directly.";

    public const string ChallengerSystemPrompt =
        "You are a clinical safety challenger. Your role is to argue that the given risk assessment may be incorrect — either overstated or understated — with evidence from the therapy note. Be concise and cite the text directly.";

    public const string JudgeSystemPrompt =
        """
        You are a clinical safety judge. Having read arguments from both an advocate and a challenger, synthesize a final risk assessment.
        Return ONLY valid JSON with this exact schema:
        {
          "finalRiskLevel": "Low|Moderate|High|Imminent",
          "finalConfidence": 0.0-1.0,
          "requiresReview": true/false,
          "reviewReasons": ["reason1", ...],
          "synthesis": "Your reasoning summary"
        }
        """;

    public static string BuildAdvocatePrompt(string riskJson, string noteText) =>
        $"""
        The RiskAssessor concluded the following assessment:
        {riskJson}

        Defend this assessment with specific evidence from the therapy note below. Limit your response to 500 tokens.

        Therapy Note:
        ---
        {noteText}
        ---
        """;

    public static string BuildChallengerPrompt(string riskJson, string noteText) =>
        $"""
        The RiskAssessor produced the following assessment:
        {riskJson}

        Argue that this assessment may be incorrect — either overstated or understated — with specific evidence from the therapy note below. Limit your response to 500 tokens.

        Therapy Note:
        ---
        {noteText}
        ---
        """;

    public static string BuildAdvocateRebuttalPrompt(string challengerArgument) =>
        $"""
        The challenger argued:
        {challengerArgument}

        Rebut this argument specifically with evidence. Limit your response to 500 tokens.
        """;

    public static string BuildChallengerRebuttalPrompt(string advocateArgument) =>
        $"""
        The advocate argued:
        {advocateArgument}

        Rebut this argument specifically with evidence. Limit your response to 500 tokens.
        """;

    public static string BuildJudgePrompt(IReadOnlyList<DebateRound> rounds, string riskJson)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Original risk assessment:");
        sb.AppendLine(riskJson);
        sb.AppendLine();

        foreach (var round in rounds)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Round {round.RoundNumber}:");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Advocate: {round.AdvocateArgument}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Challenger: {round.ChallengerArgument}");
            sb.AppendLine();
        }

        sb.AppendLine("Synthesize a final risk assessment considering both sides. Return JSON only.");
        return sb.ToString();
    }
}
