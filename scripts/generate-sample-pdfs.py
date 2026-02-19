#!/usr/bin/env python3
"""Generate sample therapy note PDFs from golden file content.

One-time script — output is checked into public/samples/.
Run: /home/dwight/virtualenvs/my_venv/bin/python scripts/generate-sample-pdfs.py
"""

import json
import os
from pathlib import Path

from fpdf import FPDF

REPO_ROOT = Path(__file__).resolve().parent.parent
SAMPLES_DIR = REPO_ROOT / "src" / "SessionSight.Web" / "public" / "samples"
GOLDEN_NONRISK = REPO_ROOT / "plan" / "data" / "synthetic" / "golden-files" / "non-risk-extraction"
GOLDEN_RISK = REPO_ROOT / "plan" / "data" / "synthetic" / "golden-files" / "risk-assessment"


# ---------------------------------------------------------------------------
# Expanded risk notes (padded from ~80 words to full structured therapy note)
# ---------------------------------------------------------------------------

EXPANDED_RISK_001 = """\
Session Note - March 20, 2026
Patient: Rachel Morrison
Therapist: Dr. Karen Abbott, LCSW

Presenting Concerns:
Patient presents with significant distress today. Reports thinking about suicide daily \
for the past two weeks. Has developed a specific plan to overdose on prescription \
medications she has been stockpiling. States she has written goodbye letters to her \
children. Denies immediate intent but admits the urge is getting stronger. Severity is \
high. Triggers include custody hearing next week and ongoing conflict with ex-spouse.

Session Summary:
Emergency-focused session. Conducted thorough risk assessment. Safety plan reviewed and \
updated with patient. Discussed lethal means restriction - patient agreed to have her \
sister secure all prescription medications from the home tonight. Emergency contacts \
notified with patient consent, including sister Maria Morrison and ex-husband (for \
child safety awareness). Reviewed crisis hotline numbers and local emergency resources. \
Patient contracted for safety through next session.

Mood Assessment:
Current mood: 2/10 (hopeless, overwhelmed)
Mood change from last session: Significant decline
Energy level: Low
Emotional themes: hopelessness, despair, guilt about impact on children
Affect observed: Tearful, constricted
Affect congruence: Congruent with reported mood

Mental Status Examination:
Appearance: Disheveled, appeared fatigued
Behavior: Cooperative but emotionally labile
Speech: Slow rate, soft volume, occasional pauses
Thought process: Linear but preoccupied with suicidal thoughts
Thought content: Active suicidal ideation with specific plan (overdose), stockpiled means
Perception: No hallucinations or delusions
Cognition: Intact, alert and oriented
Insight: Fair - recognizes need for help but feels overwhelmed
Judgment: Impaired by emotional state

Risk Assessment:
Suicidal ideation: Active, with specific plan (medication overdose). Frequency: daily for \
two weeks. Has stockpiled prescription medications. Written goodbye letters to children. \
Denies immediate intent but urge intensifying. Protective factors: children, therapeutic \
relationship, agreed to safety plan.
Self-harm: None reported.
Homicidal ideation: None.
Risk level: High.
Safety plan: Updated - remove access to medications, crisis contacts identified, \
agreed to call 988 if urges intensify.

Interventions Used:
- Crisis intervention and safety planning
- Lethal means restriction counseling
- Supportive therapy
- Emergency contact notification
Techniques effective for session - patient agreed to safety measures.
Skills reinforced: Crisis line usage, distress tolerance skills
Homework assigned: Give all medications to sister tonight, call therapist if urges worsen
Previous homework: Not discussed due to crisis focus

Diagnoses:
Primary diagnosis: Major Depressive Disorder, recurrent, severe
Primary diagnosis code: F33.2
Diagnosis status: Worsened

Treatment Progress:
Overall progress: Significant regression due to custody stressor
Treatment phase: Crisis
Barriers: Acute stressor (custody hearing), access to means, social isolation
Strengths: Therapeutic alliance, willingness to engage in safety planning
Treatment goals: Immediate safety stabilization, reduce suicidal ideation

Plan:
- Increase session frequency to twice weekly until crisis stabilizes
- Next session: March 22, 2026
- Verify lethal means restriction completed
- Consider psychiatric hospitalization if safety plan adherence falters
- Coordinate with psychiatrist Dr. Patel for medication adjustment
- Level of care: Outpatient with intensive monitoring, consider inpatient if needed
"""

EXPANDED_RISK_010 = """\
Session Note - March 22, 2026
Patient: Harold Jacobson
Therapist: Dr. Patricia Reeves, PhD

Presenting Concerns:
Elderly patient discussing recent loss of spouse. Reports feeling that 'my time should \
come soon too' and often wishes to 'join her.' Clarifies this is not a desire to take his \
own life but rather a readiness for natural death. No suicidal plans or intent. Describes \
profound grief and loneliness. These feelings are constant since her passing three months \
ago. Severity is moderate. Secondary concern: increased social isolation since wife's death.

Session Summary:
Grief counseling focus continued. Explored meaning of patient's wish to 'join' his wife, \
carefully distinguishing between passive death wish and active suicidal ideation. Patient \
articulated clearly that he does not want to harm himself and has no plan to do so. He \
describes a spiritual readiness for natural death rather than a desire to hasten it. \
Discussed ways to honor his wife's memory while rebuilding daily routines. Encouraged \
re-engagement with church group and weekly calls with his daughter.

Mood Assessment:
Current mood: 3/10 (sad, lonely)
Mood change from last session: Stable
Energy level: Low
Emotional themes: grief, loneliness, acceptance of mortality
Affect observed: Tearful at times, subdued
Affect congruence: Congruent

Mental Status Examination:
Appearance: Appropriately dressed, appears older than stated age
Behavior: Cooperative, quiet
Speech: Slow rate, normal volume
Thought process: Linear, coherent
Thought content: Preoccupied with loss, themes of mortality and reunion with spouse
Perception: No hallucinations. Reports occasionally 'sensing' wife's presence, \
which is culturally normative grief experience.
Cognition: Intact for age, alert and oriented x4
Insight: Good - understands difference between grief and clinical depression
Judgment: Good

Risk Assessment:
Suicidal ideation: Passive wish for natural death - 'my time should come soon too.' \
Patient explicitly denies active suicidal ideation, plan, or intent. Clarifies wish is \
for natural death, not self-harm. Consistent across sessions.
Self-harm: None.
Homicidal ideation: None.
Risk level: Low to moderate (passive ideation in context of complicated grief).
Protective factors: faith, relationship with daughter, no access to lethal means, \
clear denial of intent.

Interventions Used:
- Grief counseling (meaning-making approach)
- Supportive therapy
- Psychoeducation about complicated grief
Techniques effective - patient engaged meaningfully in grief work.
Skills taught: Continuing bonds journaling
Homework assigned: Write one letter to wife this week, attend one church group meeting
Previous homework: Partially completed - wrote in journal but did not attend social event

Diagnoses:
Primary diagnosis: Prolonged Grief Disorder
Primary diagnosis code: F43.8
Diagnosis status: No change

Treatment Progress:
Overall progress: Gradual engagement with grief process
Treatment phase: Middle
Barriers: Social isolation, low motivation, physical health limitations
Strengths: Strong faith, loving relationship with daughter, consistent attendance
Treatment goals: Process grief, reduce isolation, establish sustainable daily routine

Plan:
- Continue weekly sessions
- Next session: March 29, 2026
- Monitor passive death wish - reassess if language shifts toward active ideation
- Encourage social engagement through church community
- No referrals needed at this time
- Level of care: Outpatient
"""

EXPANDED_RISK_030 = """\
Session Note - March 25, 2026
Patient: Brian Okafor
Therapist: Dr. Susan Hartley, LPC

Presenting Concerns:
Patient presenting for intake evaluation following referral from PCP Dr. Adams for \
depression symptoms. Reports depressed mood for past 2 months following job loss. PHQ-9 \
score of 14 indicating moderate depression. Secondary concerns include insomnia and \
decreased appetite. This is the first episode of depression; no prior psychiatric history.

Session Summary:
Comprehensive intake evaluation completed. Gathered full psychosocial history, reviewed \
screening instruments, and established initial treatment plan. When completing Columbia \
Suicide Severity Rating Scale, patient answered NO to all screening questions including: \
wish to be dead, suicidal thoughts, suicidal thoughts with method, suicidal intent, and \
suicidal intent with plan. No history of self-harm or suicide attempts. No homicidal \
ideation. Patient is motivated to engage in therapy and expressed relief at taking this step.

Mood Assessment:
Current mood: 4/10 (low, discouraged)
Energy level: Low
Emotional themes: discouragement, loss of identity tied to career, worry about finances
Affect observed: Constricted, mildly dysphoric
Affect congruence: Congruent

Mental Status Examination:
Appearance: Well-groomed, appropriately dressed
Behavior: Cooperative, good eye contact
Speech: Normal rate and volume
Thought process: Linear, goal-directed
Thought content: Themes of loss and self-worth related to job loss
Perception: No abnormalities
Cognition: Intact, alert and oriented
Insight: Good - recognizes connection between job loss and mood
Judgment: Good

Risk Assessment:
Suicidal ideation: None. Columbia Suicide Severity Rating Scale completed - all items \
negative (wish to be dead: No, suicidal thoughts: No, suicidal thoughts with method: No, \
suicidal intent: No, suicidal intent with plan: No).
Self-harm: None. No history of self-harm or suicide attempts.
Homicidal ideation: None.
Risk level: Low.
Protective factors: supportive family, no substance use, motivated for treatment.

Interventions Used:
- Comprehensive intake assessment
- Psychoeducation about depression
- PHQ-9 and C-SSRS administration
- Initial treatment planning
Skills discussed: Sleep hygiene basics, behavioral activation concepts

Diagnoses:
Primary diagnosis: Major Depressive Disorder, single episode, moderate
Primary diagnosis code: F32.1
Diagnosis status: New diagnosis

Treatment Progress:
Treatment phase: Intake/Assessment
Recommended treatment: Weekly CBT sessions, 12-16 week course
Barriers: Financial concerns (lost insurance with job), transportation
Strengths: Motivated, supportive spouse, no substance use history

Plan:
- Start psychotherapy, weekly CBT sessions
- Next session: April 1, 2026
- Consider medication if no improvement in 4 weeks - coordinate with PCP Dr. Adams
- Sliding scale fee discussed and approved
- No immediate referrals needed
- Level of care: Outpatient
"""

# ---------------------------------------------------------------------------
# Sample metadata: (source_file, expanded_content_or_None, title, description)
# ---------------------------------------------------------------------------

SAMPLES = [
    {
        "id": "sample-nonrisk-001",
        "source": "nonrisk-001_v1.json",
        "source_dir": "nonrisk",
        "expanded": None,
        "title": "Anxiety / CBT Session",
        "description": "GAD with cognitive restructuring, individual session",
    },
    {
        "id": "sample-nonrisk-002",
        "source": "nonrisk-002_v1.json",
        "source_dir": "nonrisk",
        "expanded": None,
        "title": "Depression / Telehealth",
        "description": "MDD with behavioral activation, psychiatrist referral",
    },
    {
        "id": "sample-nonrisk-003",
        "source": "nonrisk-003_v1.json",
        "source_dir": "nonrisk",
        "expanded": None,
        "title": "PTSD / EMDR Session",
        "description": "Trauma processing after motor vehicle accident, SUD ratings",
    },
    {
        "id": "sample-nonrisk-004",
        "source": "nonrisk-004_v1.json",
        "source_dir": "nonrisk",
        "expanded": None,
        "title": "Substance Use / Motivational Interviewing",
        "description": "Alcohol use disorder, harm reduction, IOP consideration",
    },
    {
        "id": "sample-nonrisk-005",
        "source": "nonrisk-005_v1.json",
        "source_dir": "nonrisk",
        "expanded": None,
        "title": "Termination / Discharge",
        "description": "Adjustment disorder resolved, relapse prevention planning",
    },
    {
        "id": "sample-risk-001",
        "source": "risk-test-001_v2.json",
        "source_dir": "risk",
        "expanded": EXPANDED_RISK_001,
        "title": "Active SI with Safety Plan",
        "description": "High risk - specific plan, stockpiled means, emergency contacts",
    },
    {
        "id": "sample-risk-010",
        "source": "risk-test-010_v2.json",
        "source_dir": "risk",
        "expanded": EXPANDED_RISK_010,
        "title": "Elderly Grief, Passive SI",
        "description": "Nuanced - wish for natural death vs active suicidality",
    },
    {
        "id": "sample-risk-030",
        "source": "risk-test-030_v2.json",
        "source_dir": "risk",
        "expanded": EXPANDED_RISK_030,
        "title": "Intake Eval with Columbia Scale",
        "description": "Formal screening tool, all items negative, moderate depression",
    },
]


def load_note_content(sample: dict) -> str:
    """Load note content from golden file, or use expanded version."""
    if sample["expanded"]:
        return sample["expanded"]

    if sample["source_dir"] == "nonrisk":
        path = GOLDEN_NONRISK / sample["source"]
    else:
        path = GOLDEN_RISK / sample["source"]

    with open(path) as f:
        data = json.load(f)
    return data["note_content"]


def generate_pdf(content: str, filename: str, output_dir: Path) -> None:
    """Generate a single-column therapy note PDF."""
    pdf = FPDF()
    pdf.set_auto_page_break(auto=True, margin=15)
    pdf.add_page()
    pdf.set_margins(10, 10, 10)
    pdf.set_font("Helvetica", size=10)

    for line in content.strip().split("\n"):
        stripped = line.strip()

        # Empty lines
        if not stripped:
            pdf.ln(3)
            continue

        # Always reset x to left margin before rendering
        pdf.set_x(pdf.l_margin)

        # Title line (first line with date)
        if stripped.startswith("Session Note"):
            pdf.set_font("Helvetica", "B", 14)
            pdf.multi_cell(0, 7, stripped)
            pdf.set_font("Helvetica", size=10)
            pdf.ln(1)
            continue

        # Section headers (lines ending with colon, no leading dash/bullet)
        if (
            stripped.endswith(":")
            and not stripped.startswith("-")
            and not stripped.startswith("*")
            and len(stripped) < 50
        ):
            pdf.ln(1)
            pdf.set_font("Helvetica", "B", 11)
            pdf.multi_cell(0, 6, stripped)
            pdf.set_font("Helvetica", size=10)
            continue

        # Bullet points — render as single multi_cell with indent
        if stripped.startswith("- "):
            pdf.set_x(pdf.l_margin + 4)
            pdf.multi_cell(0, 5, stripped)
            continue

        # Regular text (including key-value lines — rendered as plain text)
        pdf.multi_cell(0, 5, stripped)

    output_path = output_dir / filename
    pdf.output(str(output_path))
    print(f"  Generated: {output_path.name}")


def main():
    SAMPLES_DIR.mkdir(parents=True, exist_ok=True)

    metadata = []

    for sample in SAMPLES:
        content = load_note_content(sample)
        filename = f"{sample['id']}.pdf"

        generate_pdf(content, filename, SAMPLES_DIR)

        # Build preview text (first ~200 chars of content after the header)
        lines = content.strip().split("\n")
        preview_lines = []
        char_count = 0
        for line in lines:
            stripped = line.strip()
            if not stripped:
                continue
            preview_lines.append(stripped)
            char_count += len(stripped)
            if char_count >= 200:
                break
        preview_text = " ".join(preview_lines)[:250]

        metadata.append(
            {
                "id": sample["id"],
                "filename": filename,
                "title": sample["title"],
                "description": sample["description"],
                "previewText": preview_text,
            }
        )

    # Write metadata JSON
    meta_path = SAMPLES_DIR / "samples.json"
    with open(meta_path, "w") as f:
        json.dump(metadata, f, indent=2)
    print(f"  Generated: {meta_path.name}")

    print(f"\nDone! {len(SAMPLES)} PDFs + samples.json in {SAMPLES_DIR}")


if __name__ == "__main__":
    main()
