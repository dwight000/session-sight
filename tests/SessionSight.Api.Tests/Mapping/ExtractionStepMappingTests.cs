using FluentAssertions;
using SessionSight.Api.Mapping;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;

namespace SessionSight.Api.Tests.Mapping;

public class ExtractionStepMappingTests
{
    [Fact]
    public void ToStepsDto_MapsAllStepProperties()
    {
        var extractionId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow;
        var completedAt = startedAt.AddSeconds(2);

        var steps = new List<ExtractionStep>
        {
            new()
            {
                Id = stepId,
                ExtractionId = extractionId,
                StepName = ExtractionStepName.ClinicalExtract,
                Status = ExtractionStepStatus.Succeeded,
                StepOrder = 3,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                DurationMs = 2000,
                ModelUsed = "gpt-4.1-mini",
                InputTokens = 500,
                OutputTokens = 200,
                TotalTokens = 700,
                ResultSummaryJson = """{"fieldCount": 82}""",
                ToolCalls = new List<ExtractionToolCall>()
            }
        };

        var dto = ((IReadOnlyList<ExtractionStep>)steps).ToStepsDto(extractionId);

        dto.ExtractionId.Should().Be(extractionId);
        dto.Steps.Should().HaveCount(1);
        var step = dto.Steps[0];
        step.Id.Should().Be(stepId);
        step.StepName.Should().Be("ClinicalExtract");
        step.Status.Should().Be("Succeeded");
        step.StepOrder.Should().Be(3);
        step.DurationMs.Should().Be(2000);
        step.ModelUsed.Should().Be("gpt-4.1-mini");
        step.InputTokens.Should().Be(500);
        step.OutputTokens.Should().Be(200);
        step.TotalTokens.Should().Be(700);
        step.ResultSummaryJson.Should().Contain("fieldCount");
    }

    [Fact]
    public void ToStepsDto_MapsToolCallsWithLoopRound()
    {
        var extractionId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var calledAt = DateTime.UtcNow;

        var steps = new List<ExtractionStep>
        {
            new()
            {
                Id = stepId,
                ExtractionId = extractionId,
                StepName = ExtractionStepName.ClinicalExtract,
                Status = ExtractionStepStatus.Succeeded,
                StepOrder = 3,
                StartedAt = DateTime.UtcNow,
                DurationMs = 2000,
                ModelUsed = "gpt-4.1-mini",
                ToolCalls = new List<ExtractionToolCall>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        StepId = stepId,
                        ToolName = "ValidateSchema",
                        LoopRound = 0,
                        Succeeded = true,
                        DurationMs = 50,
                        CalledAt = calledAt
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        StepId = stepId,
                        ToolName = "ScoreConfidence",
                        LoopRound = 1,
                        Succeeded = true,
                        DurationMs = 30,
                        CalledAt = calledAt.AddMilliseconds(50)
                    }
                }
            }
        };

        var dto = ((IReadOnlyList<ExtractionStep>)steps).ToStepsDto(extractionId);

        dto.Steps[0].ToolCalls.Should().HaveCount(2);
        dto.Steps[0].ToolCalls[0].ToolName.Should().Be("ValidateSchema");
        dto.Steps[0].ToolCalls[0].LoopRound.Should().Be(0);
        dto.Steps[0].ToolCalls[1].LoopRound.Should().Be(1);
    }

    [Fact]
    public void ToStepsDto_HandlesEmptyToolCalls()
    {
        var extractionId = Guid.NewGuid();
        var steps = new List<ExtractionStep>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ExtractionId = extractionId,
                StepName = ExtractionStepName.Intake,
                Status = ExtractionStepStatus.Succeeded,
                StepOrder = 2,
                StartedAt = DateTime.UtcNow,
                DurationMs = 500,
                ModelUsed = "gpt-4.1-nano",
                ToolCalls = new List<ExtractionToolCall>()
            }
        };

        var dto = ((IReadOnlyList<ExtractionStep>)steps).ToStepsDto(extractionId);

        dto.Steps[0].ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public void ToStepsDto_FailedStep_HasErrorMessage()
    {
        var extractionId = Guid.NewGuid();
        var steps = new List<ExtractionStep>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ExtractionId = extractionId,
                StepName = ExtractionStepName.Summarize,
                Status = ExtractionStepStatus.Failed,
                StepOrder = 5,
                StartedAt = DateTime.UtcNow,
                DurationMs = 100,
                ModelUsed = "gpt-4.1-nano",
                ErrorMessage = "LLM timeout",
                ToolCalls = new List<ExtractionToolCall>()
            }
        };

        var dto = ((IReadOnlyList<ExtractionStep>)steps).ToStepsDto(extractionId);

        dto.Steps[0].Status.Should().Be("Failed");
        dto.Steps[0].ErrorMessage.Should().Be("LLM timeout");
    }

    [Fact]
    public void ToStepsDto_EmptyStepsList_ReturnsEmptyResponse()
    {
        var extractionId = Guid.NewGuid();
        var steps = new List<ExtractionStep>();

        var dto = ((IReadOnlyList<ExtractionStep>)steps).ToStepsDto(extractionId);

        dto.ExtractionId.Should().Be(extractionId);
        dto.Steps.Should().BeEmpty();
    }
}
