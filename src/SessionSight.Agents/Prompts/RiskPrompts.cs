namespace SessionSight.Agents.Prompts;

/// <summary>
/// Prompts for the Risk Assessor Agent.
/// These prompts are specifically designed for safety-critical re-extraction
/// and validation of risk assessment fields.
/// </summary>
public static class RiskPrompts
{
    /// <summary>
    /// System prompt for safety-critical risk re-extraction.
    /// </summary>
    public static string SystemPrompt { get; } = $"""
        You are a clinical safety specialist focused on risk assessment extraction.
        Your role is to carefully identify and extract risk indicators from therapy notes
        with the highest priority on patient safety.

        CRITICAL SAFETY RULES:
        1. When in doubt, report the MORE CONCERNING value
        2. Do NOT downplay or minimize risk indicators
        3. Subtle language like "thinking about not being here" IS suicidal ideation
        4. Historical self-harm IS still a risk factor
        5. Any mention of wanting to hurt others requires homicidal ideation assessment
        6. Passive statements like "wish I wouldn't wake up" indicate passive suicidal ideation
        7. Giving away possessions, saying goodbye, sudden calm after crisis are warning signs

        READ THE NOTE TWICE before extracting. Missing a risk indicator could be life-threatening.

        In addition to the risk fields, include a top-level object named "criteria_used".
        For each of these keys, provide a short list of criteria labels you used:
        - suicidal_ideation
        - si_frequency
        - self_harm
        - homicidal_ideation
        - risk_level_overall
        REQUIRED: "criteria_used" must include all five keys above, and each key must have at least one non-empty snake_case label.
        REQUIRED: Missing keys or empty arrays are invalid output.
        If uncertain for a key, include "insufficient_evidence" for that key.

        Include a second top-level object named "reasoning_used".
        For each same key above, provide a short freeform rationale in plain text that cites why you chose the value.
        REQUIRED: "reasoning_used" must include all five keys above, and each key must be a non-empty string.
        Keep each rationale concise (1-2 sentences max) and evidence-linked.

        CRITICAL: Your final message MUST be ONLY the JSON object. No explanatory text before or after.

        JSON SCHEMA (exact field names and allowed enum values):
        {RiskSchemaGenerator.Generate()}
        """;
    // NOTE: JSON output is also enforced via ChatResponseFormat.CreateJsonObjectFormat() in RiskAssessorAgent.
    // This prompt instruction is kept as defense-in-depth for edge cases (token limits, content filters).

    /// <summary>
    /// Gets the focused safety prompt for risk re-extraction.
    /// </summary>
    public static string GetRiskReExtractionPrompt(string noteText) => $"""
        Carefully extract ALL risk assessment indicators from this therapy note.

        IMPORTANT: This is a SAFETY-CRITICAL extraction. Read the note TWICE before answering.
        When uncertain, choose the MORE CONCERNING value. It is better to over-report risk than under-report.

        Fields to extract:

        suicidalIdeation - Level of suicidal ideation:
        - None: No suicidal thoughts mentioned or explicitly denied
        - Passive: Wishes to be dead, "wouldn't mind not waking up" (NO active intent)
        - ActiveNoPlan: Active thoughts of suicide without a specific plan
        - ActiveWithPlan: Active thoughts WITH a specific method/plan identified
        - ActiveWithIntent: Active thoughts with plan AND stated intent to act

        siFrequency - How often suicidal thoughts occur:
        - Rare: Occasional fleeting thoughts, once a week or less
        - Occasional: Several times per week
        - Frequent: Daily or most days
        - Constant: Persistent, throughout the day

        siIntensity - Intensity/severity of suicidal thoughts:
        - Fleeting: Brief, easily dismissed
        - Mild: Present but manageable
        - Moderate: Distressing, hard to ignore
        - Severe: Overwhelming, difficult to control

        selfHarm - Self-harm status:
        - None: No self-harm history mentioned
        - Historical: Past self-harm, not current (> 1 year ago)
        - Recent: Self-harm within past year
        - Current: Active self-harm behavior
        - Imminent: Stated intent to self-harm imminently

        shRecency - When self-harm last occurred (if applicable, e.g., "3 months ago")

        homicidalIdeation - Level of homicidal ideation:
        - None: No thoughts of harming others
        - Passive: Thoughts about harm to others without intent
        - ActiveNoPlan: Active thoughts without a specific plan
        - ActiveWithPlan: Active thoughts WITH specific plan/target

        hiTarget - Target of homicidal ideation if applicable

        safetyPlanStatus - Current safety plan status:
        - NotNeeded: No safety plan required
        - InPlace: Safety plan exists and is current
        - NeedsUpdate: Safety plan needs revision
        - NeedsCreation: Safety plan should be created
        - Declined: Patient declined safety planning

        protectiveFactors - Factors that REDUCE risk (list of strings):
        Examples: supportive family, stable employment, pets, religious beliefs, children

        riskFactors - Factors that INCREASE risk (list of strings):
        Examples: recent loss, access to means, substance use, isolation, previous attempts

        meansRestrictionDiscussed - Whether limiting access to lethal means was discussed (true/false)

        riskLevelOverall - Overall clinical risk level:
        - Low: Minimal risk indicators, adequate protective factors
        - Moderate: Some risk indicators present, needs monitoring
        - High: Significant risk indicators, requires intervention
        - Imminent: Immediate risk, requires crisis intervention

        Rules:
        - Set value to null ONLY if the information is truly not found
        - Confidence must be >= 0.90 for any risk indicator that is present
        - Include the exact source text that supports each extraction
        - When indicators are ambiguous, choose the MORE SEVERE value
        - Classification boundaries:
          * siFrequency: "once or twice a month" is Rare; several times per week is Occasional; daily/most days is Frequent.
          * selfHarm: classify only when there is evidence of actual self-injury behavior (cutting, burning, scratching, overdose attempt, other direct self-injury act).
          * selfHarm: suicidal ideation/plan/intent alone does NOT imply selfHarm. If no self-injury behavior is described, use None.
          * selfHarm: Recent means any self-harm within the last 12 months; Historical is only when last event is more than 12 months ago.
          * homicidalIdeation: use Passive only when thoughts about harming others are actually present; otherwise use None.
          * Self-directed phrases ("hurt myself", "harm myself", "kill myself", suicidal ideation) are NOT homicidal ideation.
          * riskLevelOverall: ActiveWithPlan or ActiveWithIntent cannot be Low. Minimum is High unless evidence supports Imminent.
          * If frequency is explicitly stated, use literal mapping; only escalate severity when frequency is ambiguous.
          * Collateral evidence of suicide planning or means research should influence suicidalIdeation/riskLevelOverall, not selfHarm unless self-injury behavior is explicitly present.
        - Collateral information (family, partner, school, caregiver) counts as valid risk evidence, even with patient denial.

        REQUIRED ADDITIONAL OUTPUT:
        - Include top-level "criteria_used" with concise snake_case criteria labels for:
          * suicidal_ideation
          * si_frequency
          * self_harm
          * homicidal_ideation
          * risk_level_overall
        - "criteria_used" must include all 5 keys above. Every key must have at least one non-empty label.
        - Keep criteria labels short and concrete (examples: explicit_suicidal_statement, specific_plan_present, self_injury_behavior_absent, collateral_risk_report, denial_statement_present).
        - If uncertain for a key, use "insufficient_evidence" instead of leaving it empty.
        - Do not include long explanations in criteria_used; labels only.
        - Include top-level "reasoning_used" for the same 5 keys above.
        - Every "reasoning_used" key must be a non-empty freeform sentence (1-2 sentences) describing why the selected value was chosen from the note.
        - Use concrete note evidence, and avoid generic wording.

        Return ONLY the JSON object matching the schema in the system prompt. No other text.

        Therapy Note:
        ---
        {noteText}
        ---
        """;
}
