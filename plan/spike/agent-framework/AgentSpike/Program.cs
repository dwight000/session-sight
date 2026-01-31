using System.Text.Json;
using Azure;
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;

// ── Configuration ──────────────────────────────────────────────────
// Option A: Foundry project endpoint (set via env var or appsettings)
var projectEndpoint = Environment.GetEnvironmentVariable("PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException(
        "Set PROJECT_ENDPOINT env var to your AI Foundry project endpoint. " +
        "Format: https://<ai-services>.services.ai.azure.com/api/projects/<project-name>");

var modelDeployment = Environment.GetEnvironmentVariable("MODEL_DEPLOYMENT") ?? "gpt-4o";

Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("  B-001 Agent Framework Spike — SessionSight");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine($"Endpoint: {projectEndpoint}");
Console.WriteLine($"Model:    {modelDeployment}");
Console.WriteLine();

// ── 1. Connect to Foundry project ──────────────────────────────────
Console.WriteLine("[1/7] Connecting to AI Foundry project...");
PersistentAgentsClient client = new(projectEndpoint, new DefaultAzureCredential());
Console.WriteLine("      ✓ Client created");

// ── 2. Define validate_schema tool ─────────────────────────────────
Console.WriteLine("[2/7] Defining validate_schema tool...");
FunctionToolDefinition validateSchemaTool = new(
    name: "validate_schema",
    description: "Validates extracted clinical data against the SessionSight schema. " +
                 "Returns validation result with any errors or warnings.",
    parameters: BinaryData.FromObjectAsJson(
        new
        {
            Type = "object",
            Properties = new
            {
                ExtractedData = new
                {
                    Type = "object",
                    Description = "The extracted clinical data object to validate against the schema",
                },
            },
            Required = new[] { "extractedData" },
        },
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
Console.WriteLine("      ✓ Tool defined");

// ── 3. Create agent with clinical extraction prompt ────────────────
Console.WriteLine("[3/7] Creating clinical extraction agent...");
const string systemPrompt = """
    You are a clinical data extraction agent for SessionSight, a therapy session analysis tool.

    Your job is to extract structured clinical data from therapy session notes.

    For each note, extract the following fields into a JSON object:
    - clientId: string (anonymized identifier, use "CLIENT-001" if not specified)
    - sessionDate: string (ISO 8601 date)
    - sessionType: string (e.g., "Individual", "Group", "Family")
    - presentingIssues: string[] (list of issues discussed)
    - interventionsUsed: string[] (therapeutic interventions applied)
    - moodRating: number (1-10 scale, assess from context)
    - riskLevel: string ("none", "low", "moderate", "high", "critical")
    - riskIndicators: string[] (any risk factors identified)
    - progressNotes: string (brief summary of progress)
    - nextSessionPlan: string (plan for next session)

    After extracting the data, ALWAYS call the validate_schema tool with the extracted data
    to verify it meets the schema requirements before returning your final answer.

    Return your final answer as a JSON object with the extracted fields.
    Do NOT wrap the JSON in markdown code fences.
    """;

PersistentAgent agent = await client.Administration.CreateAgentAsync(
    model: modelDeployment,
    name: "SessionSight Clinical Extractor",
    instructions: systemPrompt,
    tools: [validateSchemaTool]);
Console.WriteLine($"      ✓ Agent created: {agent.Id}");

// ── 4. Create thread and add therapy note ──────────────────────────
Console.WriteLine("[4/7] Creating thread with therapy note...");
PersistentAgentThread thread = await client.Threads.CreateThreadAsync();

const string therapyNote = """
    Session Date: January 15, 2026
    Client: A.M. (returning client, 6th session)
    Session Type: Individual - CBT
    Duration: 50 minutes

    Presenting Issues:
    Client reports continued difficulty with anxiety in social situations,
    particularly at work meetings. Sleep has improved slightly since starting
    the relaxation techniques discussed last session (reports 6-7 hours vs
    previous 4-5 hours). Client mentions feeling "overwhelmed" by upcoming
    performance review but denies any thoughts of self-harm.

    Interventions:
    - Cognitive restructuring: Identified and challenged catastrophic thinking
      patterns related to performance review ("I'll definitely get fired" →
      "Reviews are stressful but I've received positive feedback before")
    - Exposure hierarchy: Developed graduated exposure plan for work meetings,
      starting with small team standups
    - Mindfulness breathing exercise practiced in session (4-7-8 technique)

    Progress:
    Client showing good engagement with CBT techniques. Sleep improvement
    suggests relaxation skills are being practiced between sessions. Anxiety
    remains moderate but client is developing better coping strategies. GAD-7
    score decreased from 14 to 11 since intake.

    Risk Assessment:
    No suicidal ideation, no self-harm. Client has supportive partner and
    social network. Low risk.

    Plan:
    Continue CBT focus on social anxiety. Practice exposure hierarchy items
    1-3 before next session. Continue sleep hygiene and relaxation exercises.
    Next session: January 22, 2026.
    """;

await client.Messages.CreateMessageAsync(
    thread.Id,
    MessageRole.User,
    $"Extract structured clinical data from this therapy session note:\n\n{therapyNote}");
Console.WriteLine($"      ✓ Thread created: {thread.Id}");

// ── 5. Run agent ───────────────────────────────────────────────────
Console.WriteLine("[5/7] Running agent...");
ThreadRun run = await client.Runs.CreateRunAsync(thread.Id, agent.Id);
Console.WriteLine($"      Run started: {run.Id}");

bool toolWasCalled = false;

do
{
    await Task.Delay(TimeSpan.FromMilliseconds(1000));
    run = await client.Runs.GetRunAsync(thread.Id, run.Id);
    Console.WriteLine($"      Status: {run.Status}");

    // ── 6. Handle tool calls ───────────────────────────────────────
    if (run.Status == RunStatus.RequiresAction
        && run.RequiredAction is SubmitToolOutputsAction submitAction)
    {
        Console.WriteLine("[6/7] Agent requested tool call!");
        List<ToolOutput> toolOutputs = [];

        foreach (RequiredToolCall toolCall in submitAction.ToolCalls)
        {
            if (toolCall is RequiredFunctionToolCall functionCall)
            {
                Console.WriteLine($"      Tool: {functionCall.Name}");
                Console.WriteLine($"      Args: {functionCall.Arguments[..Math.Min(200, functionCall.Arguments.Length)]}...");

                if (functionCall.Name == "validate_schema")
                {
                    toolWasCalled = true;

                    // Mock validation — in production this would check against
                    // the actual SessionSight clinical schema
                    var validationResult = new
                    {
                        valid = true,
                        errors = Array.Empty<string>(),
                        warnings = new[]
                        {
                            "moodRating is subjective — consider using standardized scale"
                        },
                        schemaVersion = "1.0.0",
                        validatedAt = DateTime.UtcNow.ToString("o")
                    };

                    string resultJson = JsonSerializer.Serialize(validationResult,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    Console.WriteLine($"      Validation result: {resultJson}");

                    toolOutputs.Add(new ToolOutput(toolCall, resultJson));
                }
                else
                {
                    Console.WriteLine($"      WARNING: Unknown tool called: {functionCall.Name}");
                    toolOutputs.Add(new ToolOutput(toolCall,
                        JsonSerializer.Serialize(new { error = $"Unknown tool: {functionCall.Name}" })));
                }
            }
        }

        run = await client.Runs.SubmitToolOutputsToRunAsync(run, toolOutputs, cancellationToken: default);
        Console.WriteLine("      ✓ Tool outputs submitted");
    }
}
while (run.Status == RunStatus.Queued
    || run.Status == RunStatus.InProgress);

// ── 7. Print final output ──────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("[7/7] Reading agent response...");
Console.WriteLine("───────────────────────────────────────────────────────");

string? finalOutput = null;

AsyncPageable<PersistentThreadMessage> messages = client.Messages.GetMessagesAsync(
    threadId: thread.Id,
    order: ListSortOrder.Ascending);

await foreach (PersistentThreadMessage message in messages)
{
    if (message.Role == MessageRole.Agent)
    {
        foreach (MessageContent content in message.ContentItems)
        {
            if (content is MessageTextContent textContent)
            {
                finalOutput = textContent.Text;
                Console.WriteLine(textContent.Text);
            }
        }
    }
}

Console.WriteLine("───────────────────────────────────────────────────────");
Console.WriteLine();

// ── Verification ───────────────────────────────────────────────────
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("  VERIFICATION CHECKLIST");
Console.WriteLine("═══════════════════════════════════════════════════════");

bool toolDefWorks = agent.Tools.Any(t => t is FunctionToolDefinition);
Console.WriteLine($"  [{(toolDefWorks ? "✓" : "✗")}] Tool definition compiles and is accepted");
Console.WriteLine($"  [{(toolWasCalled ? "✓" : "✗")}] Agent calls tool autonomously");
Console.WriteLine($"  [{(run.Status == RunStatus.Completed ? "✓" : "✗")}] Tool result processed correctly (run completed)");

bool isValidJson = false;
if (finalOutput != null)
{
    // Try to parse as JSON — strip markdown fences if present
    string jsonCandidate = finalOutput.Trim();
    if (jsonCandidate.StartsWith("```"))
    {
        int firstNewline = jsonCandidate.IndexOf('\n');
        int lastFence = jsonCandidate.LastIndexOf("```");
        if (firstNewline >= 0 && lastFence > firstNewline)
        {
            jsonCandidate = jsonCandidate[(firstNewline + 1)..lastFence].Trim();
        }
    }

    try
    {
        using var doc = JsonDocument.Parse(jsonCandidate);
        var root = doc.RootElement;
        isValidJson = root.TryGetProperty("presentingIssues", out _)
                   || root.TryGetProperty("interventionsUsed", out _)
                   || root.TryGetProperty("riskLevel", out _)
                   || root.TryGetProperty("sessionDate", out _);
    }
    catch (JsonException)
    {
        isValidJson = false;
    }
}
Console.WriteLine($"  [{(isValidJson ? "✓" : "✗")}] Final output is valid structured JSON");

Console.WriteLine();
bool allPassed = toolDefWorks && toolWasCalled && run.Status == RunStatus.Completed && isValidJson;
Console.WriteLine($"  RESULT: {(allPassed ? "ALL PASS ✓" : "SOME FAILED ✗")}");
Console.WriteLine("═══════════════════════════════════════════════════════");

// ── Cleanup ────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("Cleaning up...");
await client.Threads.DeleteThreadAsync(thread.Id);
await client.Administration.DeleteAgentAsync(agent.Id);
Console.WriteLine("Done.");

// Exit with appropriate code
Environment.Exit(allPassed ? 0 : 1);
