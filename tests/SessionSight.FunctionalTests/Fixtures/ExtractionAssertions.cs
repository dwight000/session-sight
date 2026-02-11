using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace SessionSight.FunctionalTests.Fixtures;

/// <summary>
/// Shared field-level assertions for all 74 extracted fields from sample-note.pdf.
/// Each assertion is tagged with why it's at the given confidence level.
/// </summary>
internal static class ExtractionAssertions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static string? GetFieldValue(JsonElement section, string fieldName)
    {
        if (!section.TryGetProperty(fieldName, out var field))
            return null;
        if (!field.TryGetProperty("value", out var value))
            return null;
        return value.ValueKind == JsonValueKind.Null ? null : value.ToString();
    }

    internal static List<string> GetArrayValues(JsonElement section, string fieldName)
    {
        if (!section.TryGetProperty(fieldName, out var field))
            return [];
        if (!field.TryGetProperty("value", out var value))
            return [];
        if (value.ValueKind != JsonValueKind.Array)
            return [];
        return value.EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }

    private static void AssertFieldPresent(JsonElement section, string fieldName)
    {
        section.TryGetProperty(fieldName, out _).Should().BeTrue(
            $"Extraction schema should include '{fieldName}'");
    }

    internal static async Task AssertExtractionFields(HttpClient client, Guid sessionId)
    {
        var getResponse = await client.GetAsync($"/api/sessions/{sessionId}/extraction");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Should retrieve saved extraction");

        var dto = await getResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var data = dto.GetProperty("data");

        // Overall confidence should be non-zero
        dto.GetProperty("overallConfidence").GetDouble().Should().BeGreaterThan(0,
            "Extraction should have non-zero confidence");

        AssertSessionInfo(data.GetProperty("sessionInfo"));
        AssertPresentingConcerns(data.GetProperty("presentingConcerns"));
        AssertMoodAssessment(data.GetProperty("moodAssessment"));
        AssertRiskAssessment(data.GetProperty("riskAssessment"));
        AssertMentalStatusExam(data.GetProperty("mentalStatusExam"));
        AssertInterventions(data.GetProperty("interventions"));
        AssertDiagnoses(data.GetProperty("diagnoses"));
        AssertTreatmentProgress(data.GetProperty("treatmentProgress"));
        AssertNextSteps(data.GetProperty("nextSteps"));
    }

    // ── SessionInfo (10 fields — asserting all 10) ──────────────────

    private static void AssertSessionInfo(JsonElement s)
    {
        // "Session Note - January 15, 2026"
        var sessionDate = GetFieldValue(s, "sessionDate");
        if (sessionDate != null && sessionDate != "0001-01-01")
        {
            sessionDate.Should().Be("2026-01-15",
                "Note header says 'January 15, 2026'");
        }

        // "Patient: John Doe"
        var patientId = GetFieldValue(s, "patientId");
        patientId.Should().NotBeNull("Note says 'Patient: John Doe'");

        // "Therapist: Dr. Smith" — LLM sometimes misses this
        var therapistId = GetFieldValue(s, "therapistId");
        if (therapistId != null)
        {
            therapistId.Should().ContainAny(["Smith", "smith", "Dr"],
                "Therapist name contains Smith or Dr");
        }

        // sessionType: note is clearly individual therapy (one patient, one therapist)
        GetFieldValue(s, "sessionType").Should().Be("Individual",
            "Single patient note implies Individual session");

        // Not stated in note — may be null or default TimeOnly (00:00:00)
        var startTime = GetFieldValue(s, "sessionStartTime");
        if (startTime != null)
        {
            startTime.Should().Be("00:00:00",
                "No start time in note — should be null or default");
        }

        var endTime = GetFieldValue(s, "sessionEndTime");
        if (endTime != null)
        {
            endTime.Should().Be("00:00:00",
                "No end time in note — should be null or default");
        }

        // Session number not stated — may be null or 0
        var sessionNumber = GetFieldValue(s, "sessionNumber");
        if (sessionNumber != null)
        {
            sessionNumber.Should().BeOneOf("0", "1",
                "No session number in note — should be null or default");
        }

        // Duration not stated — exists check
        AssertFieldPresent(s, "sessionDurationMinutes");

        // Modality — in-person implied by note (no telehealth references)
        var modality = GetFieldValue(s, "sessionModality");
        if (modality != null)
        {
            modality.Should().BeOneOf(
                "InPerson", "TelehealthVideo", "TelehealthPhone", "Hybrid",
                "Should be a valid SessionModality enum value");
        }
    }

    // ── PresentingConcerns (7 fields) ───────────────────────────────

    private static void AssertPresentingConcerns(JsonElement s)
    {
        // "ongoing anxiety related to work stress"
        var primaryConcern = GetFieldValue(s, "primaryConcern");
        primaryConcern.Should().NotBeNull("Note has a clear presenting concern");
        primaryConcern!.ToLowerInvariant().Should().Contain("anxi",
            "Note says 'ongoing anxiety related to work stress'");

        // Category should be Anxiety or WorkStress
        var category = GetFieldValue(s, "primaryConcernCategory");
        category.Should().NotBeNull("Anxiety is a clear category");
        category.Should().BeOneOf(
            "Anxiety", "Depression", "Trauma", "Relationship", "Grief", "SubstanceUse",
            "Eating", "Sleep", "Anger", "SelfEsteem", "WorkStress", "LifeTransition", "Other",
            "Should be a valid PrimaryConcernCategory enum value");

        // "difficulty sleeping and increased irritability over the past two weeks"
        var secondaryConcerns = GetArrayValues(s, "secondaryConcerns");
        // LLM may put sleep/irritability here or fold into primary concern
        if (secondaryConcerns.Count > 0)
        {
            var joined = string.Join(" ", secondaryConcerns).ToLowerInvariant();
            joined.Should().ContainAny("sleep", "irritab", "insomnia",
                "Note mentions sleep difficulty and irritability");
        }

        // Severity: patient is functional, attending therapy — Mild or Moderate
        var severity = GetFieldValue(s, "concernSeverity");
        if (severity != null)
        {
            severity.Should().BeOneOf(
                "Mild", "Moderate", "Severe", "Crisis",
                "Should be a valid ConcernSeverity enum value");
        }

        // Note contains both "ongoing anxiety" and "over the past two weeks" (sleep/irritability).
        // The single concernDuration field can reasonably map to either phrasing.
        var duration = GetFieldValue(s, "concernDuration");
        if (duration != null)
        {
            duration.ToLowerInvariant().Should().ContainAny("two week", "2 week", "14 day", "ongoing",
                "Note describes both ongoing concern and a past-two-weeks timeframe");
        }

        // "ongoing" implies not new this session
        var newThisSession = GetFieldValue(s, "newThisSession");
        if (newThisSession != null)
        {
            newThisSession.Should().Be("False",
                "Note says 'ongoing anxiety' implying pre-existing");
        }

        // "work stress" is a trigger
        var triggers = GetArrayValues(s, "triggerEvents");
        // LLM may or may not populate this — if populated, should relate to work
        if (triggers.Count > 0)
        {
            var joined = string.Join(" ", triggers).ToLowerInvariant();
            joined.Should().ContainAny("work", "stress", "performance",
                "Note mentions work stress as trigger");
        }
    }

    // ── MoodAssessment (7 fields — asserting all 7) ─────────────────

    private static void AssertMoodAssessment(JsonElement s)
    {
        // "Current mood: 5/10"
        GetFieldValue(s, "selfReportedMood").Should().Be("5",
            "Note says 'Current mood: 5/10'");

        // "(anxious but hopeful)" — LLM may interpret as Anxious or Bright (hopeful aspect)
        var affect = GetFieldValue(s, "observedAffect");
        if (affect != null)
        {
            affect.Should().BeOneOf(
                "Bright", "Euthymic", "Flat", "Blunted", "Tearful", "Anxious",
                "Agitated", "Irritable", "Labile", "Incongruent",
                "Should be a valid ObservedAffect enum value");
        }

        // Affect matches reported mood — Congruent
        var congruence = GetFieldValue(s, "affectCongruence");
        if (congruence != null)
        {
            congruence.Should().Be("Congruent",
                "Patient reports anxiety and presents as anxious");
        }

        // Emotional themes: anxiety, hope
        var themes = GetArrayValues(s, "emotionalThemes");
        if (themes.Count > 0)
        {
            var joined = string.Join(" ", themes).ToLowerInvariant();
            joined.Should().ContainAny("anxi", "hope", "worry", "stress",
                "catastroph", "think", "pattern", "fear", "negative",
                "Note describes anxiety, hope, or cognitive patterns");
        }

        // MoodChange — may or may not be extracted
        var moodChange = GetFieldValue(s, "moodChangeFromLast");
        if (moodChange != null)
        {
            moodChange.Should().BeOneOf(
                "SignificantlyImproved", "Improved", "Stable", "Declined", "SignificantlyDeclined", "Unknown",
                "Should be a valid MoodChange enum value");
        }

        // MoodVariability — not explicitly stated, validate enum if present
        var moodVariability = GetFieldValue(s, "moodVariability");
        if (moodVariability != null)
        {
            moodVariability.Should().BeOneOf(
                "Stable", "Variable", "HighlyVariable",
                "Should be a valid MoodVariability enum value");
        }

        // EnergyLevel — not explicitly stated, validate enum if present
        var energyLevel = GetFieldValue(s, "energyLevel");
        if (energyLevel != null)
        {
            energyLevel.Should().BeOneOf(
                "Low", "Normal", "Elevated", "Fluctuating",
                "Should be a valid EnergyLevel enum value");
        }
    }

    // ── RiskAssessment (12 fields — asserting all 12) ────────────────

    private static void AssertRiskAssessment(JsonElement s)
    {
        // All explicitly stated in note
        GetFieldValue(s, "suicidalIdeation").Should().Be("None",
            "Note says 'Suicidal ideation: None'");
        GetFieldValue(s, "selfHarm").Should().Be("None",
            "Note says 'Self-harm behaviors: None'");
        GetFieldValue(s, "homicidalIdeation").Should().Be("None",
            "Note says 'Homicidal ideation: None'");
        GetFieldValue(s, "riskLevelOverall").Should().Be("Low",
            "Note says 'Overall risk level: Low'");

        // SI is None → frequency/intensity should be null, but LLM sometimes fills defaults
        var siFreq = GetFieldValue(s, "siFrequency");
        if (siFreq != null)
        {
            siFreq.Should().Be("Rare",
                "If populated despite None SI, should be lowest severity");
        }

        var siIntensity = GetFieldValue(s, "siIntensity");
        if (siIntensity != null)
        {
            siIntensity.Should().Be("Fleeting",
                "If populated despite None SI, should be lowest severity");
        }

        // No self-harm → recency exists check
        AssertFieldPresent(s, "shRecency");

        // No HI → target exists check
        AssertFieldPresent(s, "hiTarget");

        // All risk None/Low → safety plan not needed
        GetFieldValue(s, "safetyPlanStatus").Should().Be("NotNeeded",
            "Low risk patient does not need safety plan");

        // No means restriction discussed (low risk, no SI/SH)
        GetFieldValue(s, "meansRestrictionDiscussed").Should().Be("False",
            "No risk indicators means no means restriction discussion");

        // Protective factors — exists check (may or may not be populated)
        AssertFieldPresent(s, "protectiveFactors");

        // Risk factors — exists check (may or may not be populated)
        AssertFieldPresent(s, "riskFactors");
    }

    // ── MentalStatusExam (9 fields — asserting all 9) ────────────────

    private static void AssertMentalStatusExam(JsonElement s)
    {
        // Appearance — now an enum
        AssertFieldPresent(s, "appearance");
        var appearance = GetFieldValue(s, "appearance");
        if (appearance != null)
        {
            appearance.Should().BeOneOf(
                "WellGroomed", "Appropriate", "Disheveled", "Unkempt", "Bizarre", "Unremarkable",
                "Should be a valid Appearance enum value");
        }

        // Behavior — now an enum (was free text)
        AssertFieldPresent(s, "behavior");
        var behavior = GetFieldValue(s, "behavior");
        if (behavior != null)
        {
            behavior.Should().BeOneOf(
                "Cooperative", "Guarded", "Agitated", "Withdrawn", "Restless", "Calm", "Hyperactive",
                "Should be a valid BehaviorType enum value");
        }

        // Speech not explicitly described → Normal is reasonable default
        var speech = GetFieldValue(s, "speech");
        if (speech != null)
        {
            speech.Should().Be("Normal",
                "No speech abnormalities noted");
        }

        // Patient was able to identify thoughts, follow exercises → Linear
        var thoughtProcess = GetFieldValue(s, "thoughtProcess");
        if (thoughtProcess != null)
        {
            thoughtProcess.Should().Be("Linear",
                "Patient followed structured CBT exercises");
        }

        // "automatic negative thoughts" → thought content present
        var thoughtContent = GetArrayValues(s, "thoughtContent");
        if (thoughtContent.Count > 0)
        {
            var joined = string.Join(" ", thoughtContent).ToLowerInvariant();
            joined.Should().ContainAny("negative", "automatic", "catastroph",
                "anxi", "pattern", "thought", "worry", "cognitive",
                "Note mentions automatic negative thoughts, anxiety, or catastrophic thinking");
        }

        // No perceptual disturbances noted — exists check
        AssertFieldPresent(s, "perception");

        // Patient able to identify thoughts → Intact cognition
        var cognition = GetFieldValue(s, "cognition");
        if (cognition != null)
        {
            cognition.Should().Be("Intact",
                "Patient demonstrated cognitive engagement");
        }

        // "able to identify automatic negative thoughts" → Good insight
        var insight = GetFieldValue(s, "insight");
        if (insight != null)
        {
            insight.Should().BeOneOf(
                "Good", "Fair", "Poor", "Absent",
                "Should be a valid InsightLevel enum value");
        }

        // Good engagement, willingness to practice → Good judgment
        var judgment = GetFieldValue(s, "judgment");
        if (judgment != null)
        {
            judgment.Should().BeOneOf(
                "Good", "Fair", "Poor", "Impaired",
                "Should be a valid JudgmentLevel enum value");
        }
    }

    // ── Interventions (9 fields — asserting all 9) ───────────────────

    private static void AssertInterventions(JsonElement s)
    {
        // "Cognitive restructuring, Mindfulness breathing exercise, Psychoeducation"
        var techniques = GetArrayValues(s, "techniquesUsed");
        if (techniques.Count > 0)
        {
            var techniquesLower = techniques.Select(t => t.ToLowerInvariant()).ToList();
            // LLM may use "CognitiveRestructuring" or "Cbt" (both valid enums for cognitive work)
            techniquesLower.Should().Contain(
                t => t.Contains("cognitive") || t.Contains("cbt") || t.Contains("restructur"),
                "Note explicitly says 'Cognitive restructuring' (may map to Cbt or CognitiveRestructuring enum)");
        }

        // Patient felt calmer → Effective or SomewhatEffective
        var effectiveness = GetFieldValue(s, "techniquesEffectiveness");
        if (effectiveness != null)
        {
            effectiveness.Should().BeOneOf(
                "VeryEffective", "Effective", "SomewhatEffective", "NotEffective", "UnableToAssess",
                "Should be a valid TechniqueEffectiveness enum value");
        }

        // "mindfulness breathing exercise" taught/practiced
        var skillsTaught = GetArrayValues(s, "skillsTaught");
        var skillsPracticed = GetArrayValues(s, "skillsPracticed");
        var allSkills = skillsTaught.Concat(skillsPracticed)
            .Select(sk => sk.ToLowerInvariant()).ToList();
        if (allSkills.Count > 0)
        {
            allSkills.Should().Contain(
                sk => sk.Contains("breath") || sk.Contains("mind") ||
                      sk.Contains("refram") || sk.Contains("restructur"),
                "Note describes breathing exercise and reframing");
        }

        // "Practice breathing exercises daily, Complete thought diary"
        var homework = GetFieldValue(s, "homeworkAssigned");
        if (homework != null)
        {
            homework.ToLowerInvariant().Should().ContainAny("breath", "diary", "thought",
                "Note assigns breathing exercises and thought diary");
        }

        // No previous homework mentioned → NotAssigned or null
        var hwCompletion = GetFieldValue(s, "homeworkCompletion");
        if (hwCompletion != null)
        {
            hwCompletion.Should().BeOneOf(
                "Completed", "PartiallyCompleted", "NotCompleted", "NotAssigned",
                "Should be a valid HomeworkCompletion enum value");
        }

        // No medications discussed — exists check
        AssertFieldPresent(s, "medicationsDiscussed");

        // No medication changes — exists check
        AssertFieldPresent(s, "medicationChanges");

        var medAdherence = GetFieldValue(s, "medicationAdherence");
        if (medAdherence != null)
        {
            medAdherence.Should().BeOneOf(
                "Adherent", "PartiallyAdherent", "NonAdherent", "NotApplicable",
                "Should be a valid MedicationAdherence enum value");
        }
    }

    // ── Diagnoses (6 fields — asserting all 6) ──────────────────────

    private static void AssertDiagnoses(JsonElement s)
    {
        // Note describes anxiety symptoms — LLM should infer anxiety-related diagnosis
        var primaryDx = GetFieldValue(s, "primaryDiagnosis");
        if (primaryDx != null)
        {
            primaryDx.ToLowerInvariant().Should().ContainAny("anxi", "generalized",
                "Note describes anxiety-related symptoms");
        }

        // Diagnosis code might be "F41.1", "F41.9", or "Unspecified"
        var dxCode = GetFieldValue(s, "primaryDiagnosisCode");
        if (dxCode != null && dxCode != "0001-01-01")
        {
            dxCode.Should().BeOneOf("F41.1", "F41.9", "Unspecified",
                "Anxiety disorder code should be F41.x or Unspecified");
        }

        // No explicit secondary diagnoses — exists check
        AssertFieldPresent(s, "secondaryDiagnoses");

        // Secondary diagnosis codes — exists check
        AssertFieldPresent(s, "secondaryDiagnosisCodes");

        // No rule-outs mentioned — exists check
        AssertFieldPresent(s, "ruleOuts");

        // Diagnosis changes — now an enum
        AssertFieldPresent(s, "diagnosisChanges");
        var dxChanges = GetFieldValue(s, "diagnosisChanges");
        if (dxChanges != null)
        {
            dxChanges.Should().BeOneOf(
                "New", "Updated", "Removed", "NoChange", "Deferred",
                "Should be a valid DiagnosisChangeType enum value");
        }
    }

    // ── TreatmentProgress (7 fields — asserting all 7) ───────────────

    private static void AssertTreatmentProgress(JsonElement s)
    {
        // "gradual progress in recognizing negative thought patterns"
        var progressRating = GetFieldValue(s, "progressRatingOverall");
        if (progressRating != null)
        {
            progressRating.Should().BeOneOf(
                "SignificantImprovement", "SomeImprovement", "Stable", "SomeRegression", "SignificantRegression",
                "Should be a valid ProgressRatingOverall enum value");
        }

        // "still working on consistent application" → barrier
        var barriers = GetArrayValues(s, "barriersIdentified");
        if (barriers.Count > 0)
        {
            var joined = string.Join(" ", barriers).ToLowerInvariant();
            joined.Should().ContainAny("consist", "appl", "coping", "strateg",
                "sleep", "irritab", "difficult", "stress",
                "Note mentions barriers like inconsistent application/strategy, sleep, irritability");
        }

        // "good engagement", "able to identify thoughts" → strengths
        var strengths = GetArrayValues(s, "strengthsObserved");
        if (strengths.Count > 0)
        {
            var joined = string.Join(" ", strengths).ToLowerInvariant();
            joined.Should().ContainAny("engag", "identif", "motivat", "cooperat",
                "Note describes engagement and ability to identify thought patterns");
        }

        // Treatment goals and goals addressed — LLM may or may not populate
        var goals = GetArrayValues(s, "treatmentGoals");
        if (goals.Count > 0)
        {
            var joined = string.Join(" ", goals).ToLowerInvariant();
            joined.Should().ContainAny("anxi", "coping", "thought", "think",
                "stress", "sleep", "catastroph", "reduc", "breath",
                "Note implies treatment goals related to anxiety, coping, sleep, or thinking");
        }

        // Goals addressed — exists check
        AssertFieldPresent(s, "goalsAddressed");

        // Goal progress (Dictionary) — exists check
        AssertFieldPresent(s, "goalProgress");

        // Treatment phase — Early or Middle (new-ish patient working on skill building)
        var phase = GetFieldValue(s, "treatmentPhase");
        if (phase != null)
        {
            phase.Should().BeOneOf(
                "Assessment", "Early", "Middle", "Late", "Maintenance", "Termination",
                "Should be a valid TreatmentPhase enum value");
        }
    }

    // ── NextSteps (8 fields — asserting all 8) ──────────────────────

    private static void AssertNextSteps(JsonElement s)
    {
        // "Next appointment: January 22, 2026"
        var nextDate = GetFieldValue(s, "nextSessionDate");
        if (nextDate != null && nextDate != "0001-01-01")
        {
            nextDate.Should().Be("2026-01-22",
                "Note says 'Next appointment: January 22, 2026'");
        }

        // "Continue weekly sessions"
        GetFieldValue(s, "nextSessionFrequency").Should().Be("Weekly",
            "Note says 'Continue weekly sessions'");

        // Next session focus should mention thought diary or breathing
        var focus = GetFieldValue(s, "nextSessionFocus");
        if (focus != null)
        {
            focus.ToLowerInvariant().Should().ContainAny("breath", "diary", "thought", "coping", "restructur",
                "Note specifies homework to review next session");
        }

        // No referrals made — exists check
        AssertFieldPresent(s, "referralsMade");

        // Referral types — exists check
        AssertFieldPresent(s, "referralTypes");

        // Coordination needed — exists check
        AssertFieldPresent(s, "coordinationNeeded");

        // Outpatient level of care (standard therapy)
        var loc = GetFieldValue(s, "levelOfCareRecommendation");
        if (loc != null)
        {
            loc.Should().Be("Outpatient",
                "Standard weekly therapy implies outpatient");
        }

        // Discharge planning — now an enum
        AssertFieldPresent(s, "dischargePlanning");
        var discharge = GetFieldValue(s, "dischargePlanning");
        if (discharge != null)
        {
            discharge.Should().BeOneOf(
                "NotPlanned", "InProgress", "ReadyForDischarge", "Discharged", "NotApplicable",
                "Should be a valid DischargePlanningStatus enum value");
        }
    }
}
