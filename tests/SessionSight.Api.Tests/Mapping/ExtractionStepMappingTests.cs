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
                EstimatedCostUsd = 0.018m,
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
        dto.Steps[0].ToolCalls[0].Succeeded.Should().BeTrue();
        dto.Steps[0].ToolCalls[0].DurationMs.Should().Be(50);
        dto.Steps[0].ToolCalls[0].CalledAt.Should().Be(calledAt);
        dto.Steps[0].ToolCalls[1].LoopRound.Should().Be(1);
        dto.Steps[0].StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        dto.Steps[0].CompletedAt.Should().BeNull();
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

    [Fact]
    public void ToStepsDto_MapsToolCallInputOutputJson()
    {
        var extractionId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

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
                        CalledAt = DateTime.UtcNow,
                        InputJson = """{"schema":"clinical"}""",
                        OutputJson = """{"valid":true}"""
                    }
                }
            }
        };

        var dto = ((IReadOnlyList<ExtractionStep>)steps).ToStepsDto(extractionId);

        dto.Steps[0].ToolCalls[0].InputJson.Should().Contain("clinical");
        dto.Steps[0].ToolCalls[0].OutputJson.Should().Contain("valid");
    }

    [Fact]
    public void ToStepsDto_MapsLlmTraces()
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
                StepName = ExtractionStepName.Intake,
                Status = ExtractionStepStatus.Succeeded,
                StepOrder = 2,
                StartedAt = DateTime.UtcNow,
                DurationMs = 500,
                ModelUsed = "gpt-4.1-nano",
                ToolCalls = new List<ExtractionToolCall>(),
                LlmTraces = new List<ExtractionLlmTrace>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        StepId = stepId,
                        ModelUsed = "gpt-4.1-nano",
                        LoopRound = 0,
                        InputTokens = 200,
                        OutputTokens = 100,
                        TotalTokens = 300,
                        DurationMs = 450,
                        PromptText = "Extract metadata from this document...",
                        ResponseText = """{"isValid":true}""",
                        CalledAt = calledAt
                    }
                }
            }
        };

        var dto = ((IReadOnlyList<ExtractionStep>)steps).ToStepsDto(extractionId);

        dto.Steps[0].LlmTraces.Should().HaveCount(1);
        var trace = dto.Steps[0].LlmTraces[0];
        trace.ModelUsed.Should().Be("gpt-4.1-nano");
        trace.LoopRound.Should().Be(0);
        trace.InputTokens.Should().Be(200);
        trace.OutputTokens.Should().Be(100);
        trace.TotalTokens.Should().Be(300);
        trace.DurationMs.Should().Be(450);
        trace.PromptText.Should().Contain("Extract metadata");
        trace.ResponseText.Should().Contain("isValid");
        trace.CalledAt.Should().Be(calledAt);
    }

    [Fact]
    public void ToStepsDto_EmptyLlmTraces_ReturnsEmptyList()
    {
        var extractionId = Guid.NewGuid();
        var steps = new List<ExtractionStep>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ExtractionId = extractionId,
                StepName = ExtractionStepName.DocumentParse,
                Status = ExtractionStepStatus.Succeeded,
                StepOrder = 1,
                StartedAt = DateTime.UtcNow,
                DurationMs = 1000,
                ModelUsed = "azure-doc-intel",
                ToolCalls = new List<ExtractionToolCall>(),
                LlmTraces = new List<ExtractionLlmTrace>()
            }
        };

        var dto = ((IReadOnlyList<ExtractionStep>)steps).ToStepsDto(extractionId);

        dto.Steps[0].LlmTraces.Should().BeEmpty();
    }
}
