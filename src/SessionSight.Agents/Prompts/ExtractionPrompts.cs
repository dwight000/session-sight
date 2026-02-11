namespace SessionSight.Agents.Prompts;

/// <summary>
/// Prompts for the Clinical Extractor Agent.
/// Each section has its own prompt for parallel extraction.
/// </summary>
public static class ExtractionPrompts
{
    /// <summary>
    /// System prompt for the Clinical Extractor Agent.
    /// Includes the generated JSON schema so the LLM uses exact field names.
    /// </summary>
    public static string SystemPrompt { get; } = BuildSystemPrompt();

    private static string BuildSystemPrompt() => $"""
        You are a clinical extraction assistant specializing in extracting structured data from therapy session notes.
        Your task is to extract clinical information accurately and comprehensively.

        You have access to tools to:
        - validate_schema: Validate your extraction against the clinical schema
        - score_confidence: Calculate confidence scores for your extraction
        - check_risk_keywords: Scan the original text for risk-related keywords
        - lookup_diagnosis_code: Validate ICD-10/DSM-5 diagnosis codes

        Guidelines:
        - Extract only information that is explicitly stated or clearly implied in the note
        - Use confidence scores: 0.90-1.00 for explicit, 0.70-0.89 for implied, below 0.70 for uncertain
        - For risk assessment fields, be thorough and conservative - when in doubt, report concerns
        - After completing your extraction, return a complete JSON object with all sections

        You MUST use exactly these field names. Here is the complete JSON schema:
        {ExtractionSchemaGenerator.Generate()}

        Each field is an object with: "value" (the extracted data or null), "confidence" (number 0-1), "source" (null or a string with the source text).
        For enum fields, the allowed values are shown separated by |. Use null if not found.

        CRITICAL: Your final message MUST be ONLY the JSON object — no explanatory text, no markdown fences, no commentary before or after. Just raw JSON.
        """;
    // NOTE: JSON output is also enforced via ChatResponseFormat.CreateJsonObjectFormat() in ClinicalExtractorAgent.
    // This prompt instruction is kept as defense-in-depth for edge cases (token limits, content filters).

    /// <summary>
    /// Common instructions for all extraction prompts.
    /// </summary>
    private const string CommonInstructions = """
        Rules:
        - Only extract values that are explicitly stated or clearly implied in the note
        - Set value to null if the information is not found
        - Confidence scoring:
          * 0.90-1.00: Explicitly stated in the text
          * 0.70-0.89: Clearly implied or can be inferred
          * Below 0.70: Uncertain or ambiguous
        - Source text should be the exact quote from the note that supports the extraction
        - Return valid JSON only, no additional text or explanation
        """;

    /// <summary>
    /// Prompt for extracting session info (metadata).
    /// Uses gpt-4o-mini.
    /// </summary>
    public static string GetSessionInfoPrompt(string noteText) => """
        Extract session information from this therapy note.

        Fields to extract:
        - patientId (string): Patient/client identifier
        - sessionDate (date): Session date in YYYY-MM-DD format
        - sessionStartTime (time): Start time in HH:MM format
        - sessionEndTime (time): End time in HH:MM format
        - sessionDurationMinutes (int): Duration in minutes
        - sessionType (enum): Intake|Individual|Group|Family|Couples|Crisis|Assessment|Termination
        - sessionNumber (int): Session number in treatment sequence
        - sessionModality (enum): InPerson|TelehealthVideo|TelehealthPhone|Hybrid
        - therapistId (string): Therapist/clinician identifier

        """ + CommonInstructions + """

        Return JSON in this format:
        {
          "patientId": {"value": "...", "confidence": 0.95, "source": {"text": "exact quote", "section": "header"}},
          "sessionDate": {"value": "2024-01-15", "confidence": 0.95, "source": {"text": "exact quote", "section": "header"}},
          "sessionStartTime": {"value": "14:00", "confidence": 0.90, "source": {"text": "exact quote", "section": "header"}},
          "sessionEndTime": {"value": "14:50", "confidence": 0.90, "source": {"text": "exact quote", "section": "header"}},
          "sessionDurationMinutes": {"value": 50, "confidence": 0.95, "source": {"text": "exact quote", "section": "header"}},
          "sessionType": {"value": "Individual", "confidence": 0.90, "source": {"text": "exact quote", "section": "header"}},
          "sessionNumber": {"value": 5, "confidence": 0.85, "source": {"text": "exact quote", "section": "header"}},
          "sessionModality": {"value": "InPerson", "confidence": 0.95, "source": {"text": "exact quote", "section": "header"}},
          "therapistId": {"value": "...", "confidence": 0.90, "source": {"text": "exact quote", "section": "header"}}
        }

        Therapy Note:
        ---
        """ + noteText + """
        ---
        """;

    /// <summary>
    /// Prompt for extracting presenting concerns.
    /// Uses gpt-4o-mini.
    /// </summary>
    public static string GetPresentingConcernsPrompt(string noteText) => """
        Extract presenting concerns from this therapy note.

        Fields to extract:
        - primaryConcern (string): Main issue discussed in session
        - primaryConcernCategory (enum): Anxiety|Depression|Trauma|Relationship|Grief|SubstanceUse|Eating|Sleep|Anger|SelfEsteem|WorkStress|LifeTransition|Other
        - secondaryConcerns (list of strings): Other issues mentioned
        - concernSeverity (enum): Mild|Moderate|Severe|Crisis
        - concernDuration (string): How long the concern has been present
        - newThisSession (bool): Whether this concern was first mentioned this session
        - triggerEvents (list of strings): Events that triggered or worsened the concern

        """ + CommonInstructions + """

        Return JSON in this format:
        {
          "primaryConcern": {"value": "...", "confidence": 0.90, "source": {"text": "exact quote", "section": "presenting problem"}},
          "primaryConcernCategory": {"value": "Anxiety", "confidence": 0.85, "source": {"text": "exact quote", "section": "presenting problem"}},
          "secondaryConcerns": {"value": ["concern1", "concern2"], "confidence": 0.80, "source": {"text": "exact quote", "section": "presenting problem"}},
          "concernSeverity": {"value": "Moderate", "confidence": 0.85, "source": {"text": "exact quote", "section": "presenting problem"}},
          "concernDuration": {"value": "3 months", "confidence": 0.90, "source": {"text": "exact quote", "section": "history"}},
          "newThisSession": {"value": false, "confidence": 0.95, "source": {"text": "exact quote", "section": "presenting problem"}},
          "triggerEvents": {"value": ["event1", "event2"], "confidence": 0.80, "source": {"text": "exact quote", "section": "presenting problem"}}
        }

        Therapy Note:
        ---
        """ + noteText + """
        ---
        """;

    /// <summary>
    /// Prompt for extracting mood assessment.
    /// Uses gpt-4o (clinical judgment required).
    /// </summary>
    public static string GetMoodAssessmentPrompt(string noteText) => """
        Extract mood assessment information from this therapy note.

        Fields to extract:
        - selfReportedMood (int): Client's self-reported mood on 1-10 scale. Parse "X/10", "X out of 10", or bare numbers. Extract X as the integer value (e.g., "5/10" → 5).
        - observedAffect (enum): Bright|Euthymic|Flat|Blunted|Tearful|Anxious|Agitated|Irritable|Labile|Incongruent
        - affectCongruence (enum): Congruent|Incongruent|Mixed
        - moodChangeFromLast (enum): SignificantlyImproved|Improved|Stable|Declined|SignificantlyDeclined|Unknown
        - moodVariability (enum): Stable|Variable|HighlyVariable
        - energyLevel (enum): Low|Normal|Elevated|Fluctuating
        - emotionalThemes (list of strings): Key emotional themes in session

        """ + CommonInstructions + """

        Return JSON in this format:
        {
          "selfReportedMood": {"value": 6, "confidence": 0.95, "source": {"text": "exact quote", "section": "mood assessment"}},
          "observedAffect": {"value": "Anxious", "confidence": 0.90, "source": {"text": "exact quote", "section": "mental status"}},
          "affectCongruence": {"value": "Congruent", "confidence": 0.85, "source": {"text": "exact quote", "section": "mental status"}},
          "moodChangeFromLast": {"value": "Improved", "confidence": 0.80, "source": {"text": "exact quote", "section": "progress"}},
          "moodVariability": {"value": "Stable", "confidence": 0.85, "source": {"text": "exact quote", "section": "mood assessment"}},
          "energyLevel": {"value": "Low", "confidence": 0.80, "source": {"text": "exact quote", "section": "mood assessment"}},
          "emotionalThemes": {"value": ["frustration", "hopelessness"], "confidence": 0.85, "source": {"text": "exact quote", "section": "session content"}}
        }

        Therapy Note:
        ---
        """ + noteText + """
        ---
        """;

    /// <summary>
    /// Prompt for extracting risk assessment.
    /// Uses gpt-4o (safety-critical).
    /// </summary>
    public static string GetRiskAssessmentPrompt(string noteText) => """
        Extract risk assessment information from this therapy note.

        IMPORTANT: This is safety-critical extraction. Be thorough and conservative.
        When in doubt about risk indicators, report them rather than omit them.

        Fields to extract:
        - suicidalIdeation (enum): None|Passive|ActiveNoPlan|ActiveWithPlan|ActiveWithIntent
        - siFrequency (enum): Rare|Occasional|Frequent|Constant
        - siIntensity (enum): Fleeting|Mild|Moderate|Severe
        - selfHarm (enum): None|Historical|Recent|Current|Imminent
        - shRecency (string): When self-harm last occurred if applicable
        - homicidalIdeation (enum): None|Passive|ActiveNoPlan|ActiveWithPlan
        - hiTarget (string): Target of homicidal ideation if applicable
        - safetyPlanStatus (enum): NotNeeded|InPlace|NeedsUpdate|NeedsCreation|Declined
        - protectiveFactors (list of strings): Factors that reduce risk
        - riskFactors (list of strings): Factors that increase risk
        - meansRestrictionDiscussed (bool): Whether limiting access to lethal means was discussed
        - riskLevelOverall (enum): Low|Moderate|High|Imminent

        """ + CommonInstructions + """

        Additional risk assessment rules:
        - If suicidal or homicidal ideation is mentioned, confidence must be >= 0.90
        - If risk indicators are ambiguous, set to the more concerning value
        - Always extract protective factors when risk is present
        - Classification boundaries:
          * suicidalIdeation: statements like "wish I could go to sleep and not wake up", "wish I would not wake up", "better off dead", or "not be here" are Passive (not None), even when plan/intent is denied.
          * suicidalIdeation: if the patient makes a distress statement (for example "I can't take this anymore") and is evasive or refuses to answer direct suicide-risk questions, classify as Passive rather than None.
          * siFrequency: "once or twice a month" is Rare; several times per week is Occasional; daily/most days is Frequent.
          * selfHarm: classify only when there is evidence of actual self-injury behavior (e.g., cutting, burning, scratching, overdose attempt, other direct self-injury act).
          * selfHarm: suicidal ideation/plan/intent by itself does NOT imply selfHarm. If no self-injury behavior is described, use None.
          * selfHarm: Recent means any self-harm within the last 12 months; Historical is only when last event is more than 12 months ago. A suicide attempt described as days, weeks, or months ago is Recent, not Historical.
          * suicidalIdeation: Behavioral warning signs (getting affairs in order, giving away possessions, writing a will without medical reason) are RISK FACTORS that elevate riskLevelOverall. They are NOT suicidal ideation unless the patient also expresses suicidal thoughts.
          * homicidalIdeation: use Passive only when thoughts about harming others are actually present; otherwise use None.
          * Self-directed phrases ("hurt myself", "harm myself", "kill myself", suicidal ideation) are NOT homicidal ideation.
          * riskLevelOverall: ActiveWithPlan or ActiveWithIntent cannot be Low. Minimum is High unless the note clearly supports Imminent.
          * riskLevelOverall: Imminent requires ActiveWithPlan or ActiveWithIntent PLUS at least one of: current means access, emergency/crisis response triggered (crisis team, ER transport, 911 call), or stated intent to act now. Subsequent mitigation (e.g., voluntarily surrendering means) does not retroactively lower the risk classification from the assessment moment.
          * riskLevelOverall: Passive suicidal ideation with recurrent thoughts (Occasional/Frequent/Constant) should be at least Moderate, even without plan or intent.
          * riskLevelOverall: if safety assessment is incomplete due to evasiveness/refusal on suicide-risk questions after distress language, minimum is Moderate.
          * If frequency is explicitly stated, use literal mapping; only escalate severity when frequency is ambiguous.
          * Collateral reports about researching means or suicide planning should affect suicidalIdeation/riskLevelOverall, but do not set selfHarm unless self-injury behavior is explicitly reported.
        - Collateral information (family, partner, other reliable sources) counts as valid evidence for risk, even if the patient denies it.

        IMPORTANT: Risk indicators may appear ANYWHERE in the note (session narrative, clinical observations,
        collateral reports, intake sections, plan) — not only in a labeled "Risk Assessment" section.
        Scan the full note text before deciding any risk field value.

        Return JSON in this format:
        {
          "suicidalIdeation": {"value": "ActiveNoPlan", "confidence": 0.95, "source": {"text": "exact quote from note", "section": "clinical observations"}},
          "siFrequency": {"value": "Occasional", "confidence": 0.90, "source": {"text": "exact quote from note", "section": "clinical observations"}},
          "siIntensity": {"value": "Moderate", "confidence": 0.90, "source": {"text": "exact quote from note", "section": "clinical observations"}},
          "selfHarm": {"value": "None", "confidence": 0.95, "source": {"text": "exact quote from note", "section": "risk assessment"}},
          "shRecency": {"value": null, "confidence": 0.0, "source": null},
          "homicidalIdeation": {"value": "None", "confidence": 0.95, "source": {"text": "exact quote from note", "section": "risk assessment"}},
          "hiTarget": {"value": null, "confidence": 0.0, "source": null},
          "safetyPlanStatus": {"value": "InPlace", "confidence": 0.90, "source": {"text": "exact quote from note", "section": "plan"}},
          "protectiveFactors": {"value": ["supportive family", "employment"], "confidence": 0.85, "source": {"text": "exact quote from note", "section": "clinical observations"}},
          "riskFactors": {"value": ["recent stressor", "isolation"], "confidence": 0.85, "source": {"text": "exact quote from note", "section": "clinical observations"}},
          "meansRestrictionDiscussed": {"value": false, "confidence": 0.90, "source": {"text": "exact quote from note", "section": "risk assessment"}},
          "riskLevelOverall": {"value": "Moderate", "confidence": 0.90, "source": {"text": "exact quote from note", "section": "risk assessment"}}
        }

        Therapy Note:
        ---
        """ + noteText + """
        ---
        """;

    /// <summary>
    /// Prompt for extracting mental status exam.
    /// Uses gpt-4o (clinical terminology).
    /// </summary>
    public static string GetMentalStatusExamPrompt(string noteText) => """
        Extract mental status examination information from this therapy note.

        Fields to extract:
        - appearance (enum): WellGroomed|Appropriate|Disheveled|Unkempt|Bizarre|Unremarkable
        - behavior (enum): Cooperative|Guarded|Agitated|Withdrawn|Restless|Calm|Hyperactive
        - speech (enum): Normal|Pressured|Slowed|Soft|Loud|Monotone
        - thoughtProcess (enum): Linear|Circumstantial|Tangential|Loose|FlightOfIdeas|Blocking
        - thoughtContent (list of strings): Notable thought content (delusions, obsessions, preoccupations)
        - perception (list of strings): Perceptual disturbances (hallucinations, illusions)
        - cognition (enum): Intact|Impaired|Fluctuating
        - insight (enum): Good|Fair|Poor|Absent
        - judgment (enum): Good|Fair|Poor|Impaired

        """ + CommonInstructions + """

        Return JSON in this format:
        {
          "appearance": {"value": "WellGroomed", "confidence": 0.90, "source": {"text": "exact quote", "section": "mental status"}},
          "behavior": {"value": "Cooperative", "confidence": 0.90, "source": {"text": "exact quote", "section": "mental status"}},
          "speech": {"value": "Normal", "confidence": 0.85, "source": {"text": "exact quote", "section": "mental status"}},
          "thoughtProcess": {"value": "Linear", "confidence": 0.90, "source": {"text": "exact quote", "section": "mental status"}},
          "thoughtContent": {"value": [], "confidence": 0.85, "source": {"text": "exact quote", "section": "mental status"}},
          "perception": {"value": [], "confidence": 0.85, "source": {"text": "exact quote", "section": "mental status"}},
          "cognition": {"value": "Intact", "confidence": 0.90, "source": {"text": "exact quote", "section": "mental status"}},
          "insight": {"value": "Good", "confidence": 0.85, "source": {"text": "exact quote", "section": "mental status"}},
          "judgment": {"value": "Good", "confidence": 0.85, "source": {"text": "exact quote", "section": "mental status"}}
        }

        Therapy Note:
        ---
        """ + noteText + """
        ---
        """;

    /// <summary>
    /// Prompt for extracting interventions.
    /// Uses gpt-4o-mini.
    /// </summary>
    public static string GetInterventionsPrompt(string noteText) => """
        Extract intervention information from this therapy note.

        Fields to extract:
        - techniquesUsed (list of enums): Cbt|Dbt|DbtDistressTolerance|DbtMindfulness|DbtInterpersonal|DbtEmotionRegulation|Act|Psychodynamic|Emdr|MotivationalInterviewing|Exposure|Relaxation|Mindfulness|BehavioralActivation|CognitiveRestructuring|Interpersonal|Narrative|SolutionFocused|Supportive|Psychoeducation
        - techniquesEffectiveness (enum): VeryEffective|Effective|SomewhatEffective|NotEffective|UnableToAssess
        - skillsTaught (list of strings): New skills taught in session
        - skillsPracticed (list of strings): Skills practiced in session
        - homeworkAssigned (string): Homework or tasks assigned for next session
        - homeworkCompletion (enum): Completed|PartiallyCompleted|NotCompleted|NotAssigned
        - medicationsDiscussed (list of strings): Medications mentioned
        - medicationChanges (string): Any medication changes noted
        - medicationAdherence (enum): Adherent|PartiallyAdherent|NonAdherent|NotApplicable

        """ + CommonInstructions + """

        Return JSON in this format:
        {
          "techniquesUsed": {"value": ["Cbt", "CognitiveRestructuring"], "confidence": 0.90, "source": {"text": "exact quote", "section": "interventions"}},
          "techniquesEffectiveness": {"value": "Effective", "confidence": 0.80, "source": {"text": "exact quote", "section": "interventions"}},
          "skillsTaught": {"value": ["thought challenging"], "confidence": 0.85, "source": {"text": "exact quote", "section": "interventions"}},
          "skillsPracticed": {"value": ["deep breathing"], "confidence": 0.85, "source": {"text": "exact quote", "section": "interventions"}},
          "homeworkAssigned": {"value": "Complete thought record daily", "confidence": 0.90, "source": {"text": "exact quote", "section": "plan"}},
          "homeworkCompletion": {"value": "Completed", "confidence": 0.90, "source": {"text": "exact quote", "section": "homework review"}},
          "medicationsDiscussed": {"value": [], "confidence": 0.85, "source": null},
          "medicationChanges": {"value": null, "confidence": 0.0, "source": null},
          "medicationAdherence": {"value": "NotApplicable", "confidence": 0.80, "source": null}
        }

        Therapy Note:
        ---
        """ + noteText + """
        ---
        """;

    /// <summary>
    /// Prompt for extracting diagnoses.
    /// Uses gpt-4o (ICD codes, clinical).
    /// </summary>
    public static string GetDiagnosesPrompt(string noteText) => """
        Extract diagnosis information from this therapy note.

        Fields to extract:
        - primaryDiagnosis (string): Primary diagnosis name
        - primaryDiagnosisCode (string): ICD-10 or DSM-5 code for primary diagnosis
        - secondaryDiagnoses (list of strings): Secondary diagnosis names
        - secondaryDiagnosisCodes (list of strings): Codes for secondary diagnoses
        - ruleOuts (list of strings): Diagnoses being ruled out
        - diagnosisChanges (enum): New|Updated|Removed|NoChange|Deferred

        """ + CommonInstructions + """

        Return JSON in this format:
        {
          "primaryDiagnosis": {"value": "Major Depressive Disorder, moderate", "confidence": 0.95, "source": {"text": "exact quote", "section": "diagnosis"}},
          "primaryDiagnosisCode": {"value": "F32.1", "confidence": 0.95, "source": {"text": "exact quote", "section": "diagnosis"}},
          "secondaryDiagnoses": {"value": ["Generalized Anxiety Disorder"], "confidence": 0.90, "source": {"text": "exact quote", "section": "diagnosis"}},
          "secondaryDiagnosisCodes": {"value": ["F41.1"], "confidence": 0.90, "source": {"text": "exact quote", "section": "diagnosis"}},
          "ruleOuts": {"value": ["Bipolar II"], "confidence": 0.80, "source": {"text": "exact quote", "section": "diagnosis"}},
          "diagnosisChanges": {"value": "NoChange", "confidence": 0.0, "source": null}
        }

        Therapy Note:
        ---
        """ + noteText + """
        ---
        """;

    /// <summary>
    /// Prompt for extracting treatment progress.
    /// Uses gpt-4o-mini.
    /// </summary>
    public static string GetTreatmentProgressPrompt(string noteText) => """
        Extract treatment progress information from this therapy note.

        Fields to extract:
        - treatmentGoals (list of strings): Overall treatment goals
        - goalsAddressed (list of strings): Goals addressed this session
        - goalProgress (dictionary): Progress on each goal (goal -> progress description)
        - progressRatingOverall (enum): SignificantImprovement|SomeImprovement|Stable|SomeRegression|SignificantRegression
        - barriersIdentified (list of strings): Barriers to treatment progress
        - strengthsObserved (list of strings): Client strengths observed
        - treatmentPhase (enum): Assessment|Early|Middle|Late|Maintenance|Termination

        """ + CommonInstructions + """

        Return JSON in this format:
        {
          "treatmentGoals": {"value": ["reduce anxiety symptoms", "improve sleep"], "confidence": 0.90, "source": {"text": "exact quote", "section": "treatment plan"}},
          "goalsAddressed": {"value": ["reduce anxiety symptoms"], "confidence": 0.90, "source": {"text": "exact quote", "section": "session content"}},
          "goalProgress": {"value": {"reduce anxiety symptoms": "PHQ-9 reduced from 15 to 12"}, "confidence": 0.85, "source": {"text": "exact quote", "section": "progress"}},
          "progressRatingOverall": {"value": "SomeImprovement", "confidence": 0.85, "source": {"text": "exact quote", "section": "assessment"}},
          "barriersIdentified": {"value": ["work schedule conflicts"], "confidence": 0.80, "source": {"text": "exact quote", "section": "barriers"}},
          "strengthsObserved": {"value": ["motivation to change", "family support"], "confidence": 0.85, "source": {"text": "exact quote", "section": "strengths"}},
          "treatmentPhase": {"value": "Middle", "confidence": 0.80, "source": {"text": "exact quote", "section": "treatment plan"}}
        }

        Therapy Note:
        ---
        """ + noteText + """
        ---
        """;

    /// <summary>
    /// Prompt for extracting next steps.
    /// Uses gpt-4o-mini.
    /// </summary>
    public static string GetNextStepsPrompt(string noteText) => """
        Extract next steps and planning information from this therapy note.

        Fields to extract:
        - nextSessionDate (date): Next scheduled session date in YYYY-MM-DD format
        - nextSessionFrequency (enum): TwiceWeekly|Weekly|Biweekly|Monthly|AsNeeded|Discharge
        - nextSessionFocus (string): Planned focus for next session
        - referralsMade (list of strings): Referrals made this session
        - referralTypes (list of enums): Psychiatry|Medical|GroupTherapy|IntensiveOutpatient|PartialHospitalization|Inpatient|Specialist|SupportGroup|CommunityResources
        - coordinationNeeded (list of strings): Care coordination tasks needed
        - levelOfCareRecommendation (enum): Outpatient|IntensiveOutpatient|PartialHospitalization|Inpatient|Residential
        - dischargePlanning (enum): NotPlanned|InProgress|ReadyForDischarge|Discharged|NotApplicable

        """ + CommonInstructions + """

        Return JSON in this format:
        {
          "nextSessionDate": {"value": "2024-01-22", "confidence": 0.95, "source": {"text": "exact quote", "section": "plan"}},
          "nextSessionFrequency": {"value": "Weekly", "confidence": 0.90, "source": {"text": "exact quote", "section": "plan"}},
          "nextSessionFocus": {"value": "Continue cognitive restructuring work", "confidence": 0.85, "source": {"text": "exact quote", "section": "plan"}},
          "referralsMade": {"value": [], "confidence": 0.90, "source": null},
          "referralTypes": {"value": [], "confidence": 0.90, "source": null},
          "coordinationNeeded": {"value": [], "confidence": 0.85, "source": null},
          "levelOfCareRecommendation": {"value": "Outpatient", "confidence": 0.90, "source": {"text": "exact quote", "section": "assessment"}},
          "dischargePlanning": {"value": "NotApplicable", "confidence": 0.0, "source": null}
        }

        Therapy Note:
        ---
        """ + noteText + """
        ---
        """;
}
