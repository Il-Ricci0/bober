using Bober.Builders;
using Bober.Models;
using Bober.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OllamaSharp;

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

        string analyzerResult = string.Empty;
        string solverResult = string.Empty;

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
                        analyzerResult = agentResponse;

                        // Check if transitioning to Solver phase
                        if (agentResponse.Contains("ANALYSIS_COMPLETE", StringComparison.OrdinalIgnoreCase))
                        {
                            logger.LogInformation("Analysis phase complete, transitioning to Solver");
                        }
                    }
                    else if (executorComplete.ExecutorId == boberSolver.Name)
                    {
                        solverResult = agentResponse;
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
            analysis = analyzerResult,
            resolution = solverResult,
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

    // Continue if analysis is not complete
    return !response.Contains("ANALYSIS_COMPLETE", StringComparison.OrdinalIgnoreCase);
}

// Workflow condition: Is Analyzer complete and ready to move to Solver?
static bool IsAnalysisComplete(ExecutorCompletedEvent? evt)
{
    if (evt == null)
        return false;

    var response = evt.Data?.ToString() ?? string.Empty;

    // Move to Solver when analysis is complete
    return response.Contains("ANALYSIS_COMPLETE", StringComparison.OrdinalIgnoreCase);
}

// Workflow condition: Should Solver continue iterating?
static bool ShouldContinueSolving(ExecutorCompletedEvent? evt)
{
    if (evt == null)
        return false;

    var response = evt.Data?.ToString() ?? string.Empty;

    // Continue if resolution is not complete
    return !response.Contains("RESOLUTION_COMPLETE", StringComparison.OrdinalIgnoreCase);
}