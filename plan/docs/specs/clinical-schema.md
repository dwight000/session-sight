# Clinical Schema Specification

> **Purpose**: Define the structured data fields extracted from therapy session notes.

## Overview

The Clinical Schema defines 82 fields organized into 10 categories. For each field:
- **Field Name**: Machine-readable identifier
- **Type**: Data type (string, enum, integer, array, etc.)
- **Description**: What this field represents
- **Required**: Whether the field must be present
- **Confidence Threshold**: Minimum confidence score to accept extraction

---

## Schema Categories

### 1. Session Information

Basic metadata about the therapy session.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `patient_id` | string | Yes | Unique patient identifier |
| `session_id` | string | Yes | Unique session identifier (generated) |
| `session_date` | date | Yes | Date of the session |
| `session_start_time` | time | No | Start time of session |
| `session_end_time` | time | No | End time of session |
| `session_duration_minutes` | integer | No | Duration in minutes |
| `session_type` | enum | Yes | Type of session |
| `session_number` | integer | No | Sequential session count for this patient |
| `session_modality` | enum | Yes | How session was conducted |
| `therapist_id` | string | Yes | Treating therapist identifier |

**Enums:**
- `session_type`: `intake`, `individual`, `group`, `family`, `couples`, `crisis`, `assessment`, `termination`
- `session_modality`: `in_person`, `telehealth_video`, `telehealth_phone`, `hybrid`

---

### 2. Presenting Concerns

What the patient is experiencing or seeking help for.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `primary_concern` | string | No | Main presenting problem |
| `primary_concern_category` | enum | No | Category of primary concern |
| `secondary_concerns` | array[string] | No | Additional concerns mentioned |
| `concern_severity` | enum | No | Severity of presenting concerns |
| `concern_duration` | string | No | How long concern has been present |
| `new_this_session` | boolean | No | Whether concern is newly disclosed |
| `trigger_events` | array[string] | No | Events that triggered current state |

**Enums:**
- `primary_concern_category`: `anxiety`, `depression`, `trauma`, `relationship`, `grief`, `substance_use`, `eating`, `sleep`, `anger`, `self_esteem`, `work_stress`, `life_transition`, `other`
- `concern_severity`: `mild`, `moderate`, `severe`, `crisis`

---

### 3. Mood Assessment

Patient's emotional state during the session.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `self_reported_mood` | integer (1-10) | No | Patient's self-rating |
| `observed_affect` | enum | No | Therapist's observation of affect |
| `affect_congruence` | enum | No | Whether affect matches content |
| `mood_change_from_last` | enum | No | Comparison to previous session |
| `mood_variability` | enum | No | Stability during session |
| `energy_level` | enum | No | Observed energy/activation |
| `emotional_themes` | array[string] | No | Primary emotions expressed |

**Enums:**
- `observed_affect`: `bright`, `euthymic`, `flat`, `blunted`, `tearful`, `anxious`, `agitated`, `irritable`, `labile`, `incongruent`
- `affect_congruence`: `congruent`, `incongruent`, `mixed`
- `mood_change_from_last`: `significantly_improved`, `improved`, `stable`, `declined`, `significantly_declined`, `unknown`
- `mood_variability`: `stable`, `variable`, `highly_variable`
- `energy_level`: `low`, `normal`, `elevated`, `fluctuating`

---

### 4. Risk Assessment

Safety-related concerns (CRITICAL SECTION).

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `suicidal_ideation` | enum | Yes* | Suicidal thoughts assessment |
| `si_frequency` | enum | No | Frequency of SI if present |
| `si_intensity` | enum | No | Intensity of SI if present |
| `self_harm` | enum | Yes* | Self-harm behavior assessment |
| `sh_recency` | string | No | When last self-harm occurred |
| `homicidal_ideation` | enum | Yes* | Homicidal thoughts assessment |
| `hi_target` | string | No | Target of HI if present |
| `safety_plan_status` | enum | No | Current safety plan state |
| `protective_factors` | array[string] | No | Factors reducing risk |
| `risk_factors` | array[string] | No | Factors increasing risk |
| `means_restriction_discussed` | boolean | No | Whether means restriction addressed |
| `risk_level_overall` | enum | No | Overall assessed risk level |

*Required if any risk indicators present in notes.

**Enums:**
- `suicidal_ideation`: `none`, `passive`, `active_no_plan`, `active_with_plan`, `active_with_intent`
- `si_frequency`: `rare`, `occasional`, `frequent`, `constant`
- `si_intensity`: `fleeting`, `mild`, `moderate`, `severe`
- `self_harm`: `none`, `historical`, `recent`, `current`, `imminent`
- `homicidal_ideation`: `none`, `passive`, `active_no_plan`, `active_with_plan`
- `safety_plan_status`: `not_needed`, `in_place`, `needs_update`, `needs_creation`, `declined`
- `risk_level_overall`: `low`, `moderate`, `high`, `imminent`

---

### 5. Mental Status Exam

Clinical observations from the session.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `appearance` | string | No | General appearance description |
| `behavior` | string | No | Behavioral observations |
| `speech` | enum | No | Speech characteristics |
| `thought_process` | enum | No | Organization of thinking |
| `thought_content` | array[string] | No | Notable thought content |
| `perception` | array[string] | No | Perceptual disturbances if any |
| `cognition` | enum | No | Cognitive functioning |
| `insight` | enum | No | Patient insight level |
| `judgment` | enum | No | Patient judgment assessment |

**Enums:**
- `speech`: `normal`, `pressured`, `slowed`, `soft`, `loud`, `monotone`
- `thought_process`: `linear`, `circumstantial`, `tangential`, `loose`, `flight_of_ideas`, `blocking`
- `cognition`: `intact`, `impaired`, `fluctuating`
- `insight`: `good`, `fair`, `poor`, `absent`
- `judgment`: `good`, `fair`, `poor`, `impaired`

---

### 6. Interventions & Treatment

What was done during the session.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `techniques_used` | array[enum] | No | Therapeutic techniques applied |
| `techniques_effectiveness` | enum | No | Patient response to interventions |
| `skills_taught` | array[string] | No | New skills introduced |
| `skills_practiced` | array[string] | No | Previously taught skills practiced |
| `homework_assigned` | string | No | Between-session tasks given |
| `homework_completion` | enum | No | Completion of prior homework |
| `medications_discussed` | array[string] | No | Medications mentioned |
| `medication_changes` | string | No | Any medication adjustments |
| `medication_adherence` | enum | No | Patient medication compliance |

**Enums:**
- `techniques_used`: `cbt`, `dbt`, `dbt_distress_tolerance`, `dbt_mindfulness`, `dbt_interpersonal`, `dbt_emotion_regulation`, `act`, `psychodynamic`, `emdr`, `motivational_interviewing`, `exposure`, `relaxation`, `mindfulness`, `behavioral_activation`, `cognitive_restructuring`, `interpersonal`, `narrative`, `solution_focused`, `supportive`, `psychoeducation`
- `techniques_effectiveness`: `very_effective`, `effective`, `somewhat_effective`, `not_effective`, `unable_to_assess`
- `homework_completion`: `completed`, `partially_completed`, `not_completed`, `not_assigned`
- `medication_adherence`: `adherent`, `partially_adherent`, `non_adherent`, `not_applicable`

---

### 7. Diagnoses

Clinical diagnostic information.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `primary_diagnosis` | string | No | Primary diagnosis (ICD-10/DSM-5) |
| `primary_diagnosis_code` | string | No | ICD-10 code |
| `secondary_diagnoses` | array[string] | No | Additional diagnoses |
| `secondary_diagnosis_codes` | array[string] | No | ICD-10 codes |
| `rule_outs` | array[string] | No | Diagnoses being considered |
| `diagnosis_changes` | string | No | Any diagnostic changes this session |

---

### 8. Treatment Progress

How the patient is progressing toward goals.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `treatment_goals` | array[string] | No | Current treatment goals |
| `goals_addressed` | array[string] | No | Goals worked on this session |
| `goal_progress` | object | No | Progress rating per goal |
| `progress_rating_overall` | enum | No | Overall progress assessment |
| `barriers_identified` | array[string] | No | Obstacles to progress |
| `strengths_observed` | array[string] | No | Patient strengths noted |
| `treatment_phase` | enum | No | Current phase of treatment |

**Enums:**
- `progress_rating_overall`: `significant_improvement`, `some_improvement`, `stable`, `some_regression`, `significant_regression`
- `treatment_phase`: `assessment`, `early`, `middle`, `late`, `maintenance`, `termination`

---

### 9. Next Steps & Plan

Planning for continued care.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `next_session_date` | date | No | Scheduled follow-up |
| `next_session_frequency` | enum | No | Recommended session frequency |
| `next_session_focus` | string | No | Planned focus for next session |
| `referrals_made` | array[string] | No | Referrals to other providers |
| `referral_types` | array[enum] | No | Types of referrals |
| `coordination_needed` | array[string] | No | Care coordination needs |
| `level_of_care_recommendation` | enum | No | Recommended level of care |
| `discharge_planning` | string | No | Discharge considerations |

**Enums:**
- `next_session_frequency`: `twice_weekly`, `weekly`, `biweekly`, `monthly`, `as_needed`, `discharge`
- `referral_types`: `psychiatry`, `medical`, `group_therapy`, `intensive_outpatient`, `partial_hospitalization`, `inpatient`, `specialist`, `support_group`, `community_resources`
- `level_of_care_recommendation`: `outpatient`, `intensive_outpatient`, `partial_hospitalization`, `inpatient`, `residential`

---

### 10. Extraction Metadata

Information about the extraction process itself.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `extraction_timestamp` | datetime | Yes | When extraction was performed |
| `extraction_model` | string | Yes | Model used for extraction |
| `extraction_version` | string | Yes | Schema version used |
| `overall_confidence` | float (0-1) | Yes | Overall extraction confidence |
| `low_confidence_fields` | array[string] | No | Fields with confidence < threshold |
| `extraction_notes` | string | No | Notes about extraction quality |
| `requires_review` | boolean | Yes | Whether human review needed |

---

## Field Count Summary

| Category | Field Count |
|----------|-------------|
| Session Information | 10 |
| Presenting Concerns | 7 |
| Mood Assessment | 7 |
| Risk Assessment | 12 |
| Mental Status Exam | 9 |
| Interventions & Treatment | 9 |
| Diagnoses | 6 |
| Treatment Progress | 7 |
| Next Steps & Plan | 8 |
| Extraction Metadata | 7 |
| **Total** | **82** |

---

## JSON Schema Example

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "ClinicalSessionExtraction",
  "type": "object",
  "properties": {
    "session_info": {
      "type": "object",
      "properties": {
        "patient_id": { "type": "string" },
        "session_id": { "type": "string" },
        "session_date": { "type": "string", "format": "date" },
        "session_type": {
          "type": "string",
          "enum": ["intake", "individual", "group", "family", "couples", "crisis", "assessment", "termination"]
        }
      },
      "required": ["patient_id", "session_id", "session_date", "session_type"]
    },
    "risk_assessment": {
      "type": "object",
      "properties": {
        "suicidal_ideation": {
          "type": "string",
          "enum": ["none", "passive", "active_no_plan", "active_with_plan", "active_with_intent"]
        },
        "risk_level_overall": {
          "type": "string",
          "enum": ["low", "moderate", "high", "imminent"]
        }
      }
    }
  }
}
```

---

## Confidence Scoring

Each extracted field includes a confidence score (0.0 - 1.0):

| Score Range | Interpretation | Action |
|-------------|----------------|--------|
| 0.9 - 1.0 | High confidence | Accept as-is |
| 0.7 - 0.89 | Medium confidence | Accept with flag |
| 0.5 - 0.69 | Low confidence | Requires review |
| < 0.5 | Very low | Mark as uncertain |

**Threshold by Category:**
- Risk Assessment: 0.9 minimum (safety-critical, flags for review if below) - see ADR-004
- Session Info: 0.7 minimum
- All others: 0.6 minimum

---

## Source Mapping

Each extracted value should include source mapping:

```json
{
  "field": "self_reported_mood",
  "value": 6,
  "confidence": 0.95,
  "source": {
    "text": "Patient reports mood as 6/10",
    "start_char": 234,
    "end_char": 262,
    "section": "assessment"
  }
}
```

This enables:
- Verification of extraction accuracy
- UI highlighting of source text
- Audit trail for clinical review

---

## Versioning

Schema versioning follows semver: `MAJOR.MINOR.PATCH`

- **MAJOR**: Breaking changes to required fields or types
- **MINOR**: New optional fields added
- **PATCH**: Description or enum value additions

Current version: `1.0.0`
