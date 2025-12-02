using Bober.Builders;
using Bober.Models;
using Bober.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OllamaSharp;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Initialize Ollama client and tools
var ollamaUri = new Uri("http://localhost:11434");
string ollamaModel = "codellama:7b";
IChatClient chatClient = new OllamaApiClient(ollamaUri, ollamaModel);

var sshPool = new List<SshCredential>
{
    new() { Host = "server1.com", Username = "user1", Password = "pass1" },
    new() { Host = "server2.com", Username = "user2", PrivateKeyPath = "/keys/key2.pem" }
};

var sshTool = new SshTool(sshPool);

// Health check endpoint
app.MapGet("/", () => Results.Ok(new { status = "Bober is running", timestamp = DateTime.UtcNow }));

// Webhook endpoint for incident reports
app.MapPost("/webhook/incident", async (MonitorEvent monitorEvent, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Received incident webhook - URL: {Url}, Status Code: {StatusCode}",
            monitorEvent.Url, monitorEvent.StatusCode);

        // Create agents with iteration instructions
        var boberBuilder = new BoberBuilder(chatClient);
        AIAgent boberAnalyzer = boberBuilder.BuildBoberAnalizer();
        AIAgent boberSolver = boberBuilder.BuildBoberSolver();

        // Build workflow: Analyzer loops until complete, then Solver loops until complete
        var workflow = new WorkflowBuilder(boberAnalyzer)
            .AddEdge<ExecutorCompletedEvent>(boberAnalyzer, boberAnalyzer, condition: ShouldContinueAnalysis)
            .AddEdge<ExecutorCompletedEvent>(boberAnalyzer, boberSolver, condition: IsAnalysisComplete)
            .AddEdge<ExecutorCompletedEvent>(boberSolver, boberSolver, condition: ShouldContinueSolving)
            .WithOutputFrom(boberSolver)
            .Build();

        AnalysisReport? analysisReport = null;
        ResolutionReport? resolutionReport = null;

        // Execute the workflow
        logger.LogInformation("Starting workflow execution...");
        await using Run run = await InProcessExecution.RunAsync(
            workflow,
            $"Investigate this incident and provide a detailed analysis:\n\nURL: {monitorEvent.Url}\nStatus Code: {monitorEvent.StatusCode}"
        );

        // Collect results from workflow events
        foreach (WorkflowEvent evt in run.NewEvents)
        {
            switch (evt)
            {
                case ExecutorCompletedEvent executorComplete:
                    var agentResponse = executorComplete.Data?.ToString() ?? string.Empty;
                    logger.LogInformation("{AgentId}: {Response}", executorComplete.ExecutorId, agentResponse);

                    if (executorComplete.ExecutorId == boberAnalyzer.Name)
                    {
                        // Deserialize structured analysis output
                        try
                        {
                            analysisReport = JsonSerializer.Deserialize<AnalysisReport>(
                                agentResponse,
                                JsonSerializerOptions.Web
                            );

                            if (analysisReport?.IsComplete == true)
                            {
                                logger.LogInformation("Analysis phase complete, transitioning to Solver");
                            }
                        }
                        catch (JsonException ex)
                        {
                            logger.LogWarning(ex, "Failed to deserialize analysis report, using raw response");
                        }
                    }
                    else if (executorComplete.ExecutorId == boberSolver.Name)
                    {
                        // Deserialize structured resolution output
                        try
                        {
                            resolutionReport = JsonSerializer.Deserialize<ResolutionReport>(
                                agentResponse,
                                JsonSerializerOptions.Web
                            );
                        }
                        catch (JsonException ex)
                        {
                            logger.LogWarning(ex, "Failed to deserialize resolution report, using raw response");
                        }
                    }
                    break;
            }
        }

        logger.LogInformation("Workflow completed successfully");

        return Results.Ok(new
        {
            status = "completed",
            incident = new
            {
                url = monitorEvent.Url,
                statusCode = monitorEvent.StatusCode
            },
            analysis = analysisReport,
            resolution = resolutionReport,
            timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing incident webhook");
        return Results.Problem(
            title: "Incident processing failed",
            detail: ex.Message,
            statusCode: 500
        );
    }
});

app.Run();

// Workflow condition: Should Analyzer continue iterating?
static bool ShouldContinueAnalysis(ExecutorCompletedEvent? evt)
{
    if (evt == null)
        return false;

    var response = evt.Data?.ToString() ?? string.Empty;

    // Try to deserialize and check IsComplete flag
    try
    {
        var report = JsonSerializer.Deserialize<AnalysisReport>(response, JsonSerializerOptions.Web);
        return report?.IsComplete != true;
    }
    catch
    {
        // Fallback: continue if we can't parse
        return true;
    }
}

// Workflow condition: Is Analyzer complete and ready to move to Solver?
static bool IsAnalysisComplete(ExecutorCompletedEvent? evt)
{
    if (evt == null)
        return false;

    var response = evt.Data?.ToString() ?? string.Empty;

    // Try to deserialize and check IsComplete flag
    try
    {
        var report = JsonSerializer.Deserialize<AnalysisReport>(response, JsonSerializerOptions.Web);
        return report?.IsComplete == true;
    }
    catch
    {
        // Fallback: don't transition if we can't parse
        return false;
    }
}

// Workflow condition: Should Solver continue iterating?
static bool ShouldContinueSolving(ExecutorCompletedEvent? evt)
{
    if (evt == null)
        return false;

    var response = evt.Data?.ToString() ?? string.Empty;

    // Try to deserialize and check IsComplete flag
    try
    {
        var report = JsonSerializer.Deserialize<ResolutionReport>(response, JsonSerializerOptions.Web);
        return report?.IsComplete != true;
    }
    catch
    {
        // Fallback: continue if we can't parse
        return true;
    }
}