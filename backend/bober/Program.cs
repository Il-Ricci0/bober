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

        // Create incident directory structure
        var incidentContext = IncidentContext.Create(
            Directory.GetCurrentDirectory(),
            monitorEvent
        );
        logger.LogInformation("Created incident directory: {IncidentId}", incidentContext.IncidentId);

        // Initialize tools
        var markdownReportTool = new MarkdownReportTool(incidentContext.DirectoryPath);

        // Create AIFunction instances from tools
        var sshFunction = sshTool.ExecuteDynamic();
        var logCommandFunction = AIFunctionFactory.Create(
            markdownReportTool.LogCommandExecution,
            new AIFunctionFactoryOptions
            {
                Name = "log_command_execution",
                Description = "Logs an SSH command execution with reasoning and findings to the analysis report"
            }
        );
        var logNoteFunction = AIFunctionFactory.Create(
            markdownReportTool.LogNote,
            new AIFunctionFactoryOptions
            {
                Name = "log_note",
                Description = "Adds a general note or observation to the analysis report"
            }
        );
        var readAnalysisFunction = AIFunctionFactory.Create(
            markdownReportTool.ReadAnalysis,
            new AIFunctionFactoryOptions
            {
                Name = "read_analysis",
                Description = "Reads the complete analysis report"
            }
        );
        var writeSummaryFunction = AIFunctionFactory.Create(
            markdownReportTool.WriteSummary,
            new AIFunctionFactoryOptions
            {
                Name = "write_summary",
                Description = "Writes the final summary to analysis-summary.md"
            }
        );

        // Create agents with tools
        var boberBuilder = new BoberBuilder(chatClient);
        AIAgent boberAnalyzer = boberBuilder.BuildBoberAnalizer([sshFunction, logCommandFunction, logNoteFunction]);
        AIAgent boberSummarizer = boberBuilder.BuildBoberSummarizer([readAnalysisFunction, writeSummaryFunction]);
        AIAgent boberSolver = boberBuilder.BuildBoberSolver([sshFunction, readAnalysisFunction]);

        // Build workflow: Analyzer → Summarizer → optionally Solver
        var workflow = new WorkflowBuilder(boberAnalyzer)
            .AddEdge<ExecutorCompletedEvent>(boberAnalyzer, boberAnalyzer, condition: ShouldContinueAnalysis)
            .AddEdge<ExecutorCompletedEvent>(boberAnalyzer, boberSummarizer, condition: IsAnalysisComplete)
            .AddEdge<ExecutorCompletedEvent>(boberSummarizer, boberSummarizer, condition: ShouldContinueSummarizing)
            .WithOutputFrom(boberSummarizer)
            .Build();

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
                        if (agentResponse.Contains("ANALYSIS_COMPLETE", StringComparison.OrdinalIgnoreCase))
                        {
                            logger.LogInformation("Analysis phase complete, transitioning to Summarizer");
                        }
                    }
                    else if (executorComplete.ExecutorId == boberSummarizer.Name)
                    {
                        if (agentResponse.Contains("SUMMARY_COMPLETE", StringComparison.OrdinalIgnoreCase))
                        {
                            logger.LogInformation("Summary phase complete");
                        }
                    }
                    break;
            }
        }

        logger.LogInformation("Workflow completed successfully");

        // Read the generated files
        string? analysisContent = null;
        string? summaryContent = null;

        if (File.Exists(incidentContext.AnalysisFilePath))
        {
            analysisContent = await File.ReadAllTextAsync(incidentContext.AnalysisFilePath);
        }

        if (File.Exists(incidentContext.SummaryFilePath))
        {
            summaryContent = await File.ReadAllTextAsync(incidentContext.SummaryFilePath);
        }

        return Results.Ok(new
        {
            status = "completed",
            incidentId = incidentContext.IncidentId,
            incident = new
            {
                url = monitorEvent.Url,
                statusCode = monitorEvent.StatusCode
            },
            analysisFile = incidentContext.AnalysisFilePath,
            summaryFile = incidentContext.SummaryFilePath,
            summary = summaryContent,
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

// Workflow condition: Is Analyzer complete and ready to move to Summarizer?
static bool IsAnalysisComplete(ExecutorCompletedEvent? evt)
{
    if (evt == null)
        return false;

    var response = evt.Data?.ToString() ?? string.Empty;

    // Move to Summarizer when analysis is complete
    return response.Contains("ANALYSIS_COMPLETE", StringComparison.OrdinalIgnoreCase);
}

// Workflow condition: Should Summarizer continue iterating?
static bool ShouldContinueSummarizing(ExecutorCompletedEvent? evt)
{
    if (evt == null)
        return false;

    var response = evt.Data?.ToString() ?? string.Empty;

    // Continue if summary is not complete
    return !response.Contains("SUMMARY_COMPLETE", StringComparison.OrdinalIgnoreCase);
}