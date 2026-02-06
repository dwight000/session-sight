using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;
using SessionSight.Api.Controllers;
using SessionSight.Core.Entities;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Tests.Controllers;

public class QAControllerTests
{
    private readonly Mock<IQAAgent> _mockQAAgent;
    private readonly Mock<IPatientRepository> _mockPatientRepo;
    private readonly QAController _controller;

    public QAControllerTests()
    {
        _mockQAAgent = new Mock<IQAAgent>();
        _mockPatientRepo = new Mock<IPatientRepository>();
        _controller = new QAController(
            _mockQAAgent.Object,
            _mockPatientRepo.Object);
    }

    [Fact]
    public async Task AskAboutPatient_PatientNotFound_ReturnsNotFound()
    {
        var patientId = Guid.NewGuid();
        var request = new QARequest { Question = "How is the patient doing?" };
        _mockPatientRepo.Setup(r => r.GetByIdAsync(patientId)).ReturnsAsync((Patient?)null);

        var result = await _controller.AskAboutPatient(patientId, request);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AskAboutPatient_EmptyQuestion_ReturnsBadRequest()
    {
        var patientId = Guid.NewGuid();
        var request = new QARequest { Question = "" };

        var result = await _controller.AskAboutPatient(patientId, request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AskAboutPatient_WhitespaceQuestion_ReturnsBadRequest()
    {
        var patientId = Guid.NewGuid();
        var request = new QARequest { Question = "   " };

        var result = await _controller.AskAboutPatient(patientId, request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AskAboutPatient_ValidRequest_ReturnsQAResponse()
    {
        var patientId = Guid.NewGuid();
        var patient = new Patient { Id = patientId, ExternalId = "P001" };
        var request = new QARequest { Question = "What interventions have been used?" };
        var expectedResponse = new QAResponse
        {
            Question = "What interventions have been used?",
            Answer = "CBT and mindfulness techniques have been used.",
            Confidence = 0.85,
            ModelUsed = "gpt-4o-mini",
            GeneratedAt = DateTime.UtcNow
        };

        _mockPatientRepo.Setup(r => r.GetByIdAsync(patientId)).ReturnsAsync(patient);
        _mockQAAgent.Setup(a => a.AnswerAsync(request.Question, patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.AskAboutPatient(patientId, request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<QAResponse>().Subject;
        response.Answer.Should().Contain("CBT");
        response.Confidence.Should().Be(0.85);
    }

    [Fact]
    public async Task AskAboutPatient_ValidRequest_CallsAgentWithCorrectParams()
    {
        var patientId = Guid.NewGuid();
        var patient = new Patient { Id = patientId, ExternalId = "P001" };
        var request = new QARequest { Question = "How is anxiety trending?" };
        var expectedResponse = new QAResponse { Answer = "Improving" };

        _mockPatientRepo.Setup(r => r.GetByIdAsync(patientId)).ReturnsAsync(patient);
        _mockQAAgent.Setup(a => a.AnswerAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        await _controller.AskAboutPatient(patientId, request);

        _mockQAAgent.Verify(a => a.AnswerAsync(
            "How is anxiety trending?",
            patientId,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
