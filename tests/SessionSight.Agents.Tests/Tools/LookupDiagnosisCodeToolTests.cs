using System.Text.Json;
using FluentAssertions;
using SessionSight.Agents.Tools;

namespace SessionSight.Agents.Tests.Tools;

public class LookupDiagnosisCodeToolTests
{
    private readonly LookupDiagnosisCodeTool _tool = new();

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        _tool.Name.Should().Be("lookup_diagnosis_code");
    }

    [Fact]
    public void Description_IsNotEmpty()
    {
        _tool.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InputSchema_IsValidJson()
    {
        var schema = _tool.InputSchema.ToString();
        var parsed = JsonDocument.Parse(schema);
        parsed.RootElement.GetProperty("type").GetString().Should().Be("object");
        parsed.RootElement.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain("code");
    }

    [Theory]
    [InlineData("F32.1", true, "Major depressive disorder, single episode, moderate")]
    [InlineData("F41.1", true, "Generalized anxiety disorder")]
    [InlineData("F43.10", true, "Post-traumatic stress disorder, unspecified")]
    [InlineData("F90.2", true, "Attention-deficit hyperactivity disorder, combined type")]
    public async Task ExecuteAsync_WithKnownCode_ReturnsDescription(string code, bool expectedValid, string expectedDesc)
    {
        var input = BinaryData.FromObjectAsJson(new { code });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonSerializer.Deserialize<TestOutput>(result.Data.ToStream());
        output!.IsValid.Should().Be(expectedValid);
        output.Description.Should().Be(expectedDesc);
        output.CodeSystem.Should().Be("ICD-10");
    }

    [Theory]
    [InlineData("F99", true, "Code not in local database")]
    [InlineData("F99.99", true, "Code not in local database")]
    public async Task ExecuteAsync_WithUnknownButValidFormatCode_ReturnsValidWithPlaceholder(string code, bool expectedValid, string expectedDescContains)
    {
        var input = BinaryData.FromObjectAsJson(new { code });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonSerializer.Deserialize<TestOutput>(result.Data.ToStream());
        output!.IsValid.Should().Be(expectedValid);
        output.Description.Should().Contain(expectedDescContains);
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("ABC123")]
    [InlineData("Z99.99")]  // Not an F code
    public async Task ExecuteAsync_WithInvalidCode_ReturnsInvalid(string code)
    {
        var input = BinaryData.FromObjectAsJson(new { code });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue(); // Tool execution succeeded, but code is invalid
        var output = JsonSerializer.Deserialize<TestOutput>(result.Data.ToStream());
        output!.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyCode_ReturnsError()
    {
        var input = BinaryData.FromObjectAsJson(new { code = "" });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("code");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingCode_ReturnsError()
    {
        var input = BinaryData.FromObjectAsJson(new { });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("code");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidJson_ReturnsError()
    {
        var input = BinaryData.FromString("not valid json");

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid JSON");
    }

    [Fact]
    public async Task ExecuteAsync_NormalizesCodeToUppercase()
    {
        var input = BinaryData.FromObjectAsJson(new { code = "f32.1" });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonSerializer.Deserialize<TestOutput>(result.Data.ToStream());
        output!.Code.Should().Be("F32.1");
        output.IsValid.Should().BeTrue();
    }

    private class TestOutput
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string CodeSystem { get; set; } = string.Empty;
    }
}
