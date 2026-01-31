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
    public const string SystemPrompt = """
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
        """;

    /// <summary>
    /// Gets the focused safety prompt for risk re-extraction.
    /// </summary>
    public static string GetRiskReExtractionPrompt(string noteText) => """
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

        Return JSON in this format:
        {
          "suicidalIdeation": {"value": "None", "confidence": 0.95, "source": {"text": "exact quote", "section": "risk assessment"}},
          "siFrequency": {"value": null, "confidence": 0.0, "source": null},
          "siIntensity": {"value": null, "confidence": 0.0, "source": null},
          "selfHarm": {"value": "None", "confidence": 0.95, "source": {"text": "exact quote", "section": "risk assessment"}},
          "shRecency": {"value": null, "confidence": 0.0, "source": null},
          "homicidalIdeation": {"value": "None", "confidence": 0.95, "source": {"text": "exact quote", "section": "risk assessment"}},
          "hiTarget": {"value": null, "confidence": 0.0, "source": null},
          "safetyPlanStatus": {"value": "NotNeeded", "confidence": 0.90, "source": {"text": "exact quote", "section": "risk assessment"}},
          "protectiveFactors": {"value": [], "confidence": 0.85, "source": null},
          "riskFactors": {"value": [], "confidence": 0.85, "source": null},
          "meansRestrictionDiscussed": {"value": false, "confidence": 0.90, "source": null},
          "riskLevelOverall": {"value": "Low", "confidence": 0.90, "source": {"text": "exact quote", "section": "risk assessment"}}
        }

        Therapy Note:
        ---
        """ + noteText + """
        ---
        """;
}
