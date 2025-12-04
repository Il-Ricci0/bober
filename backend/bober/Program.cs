using Bober.Builders;
using Bober.Models;
using Bober.Services;
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
string ollamaModel = "llama3.1:8b";
IChatClient chatClient = new OllamaApiClient(ollamaUri, ollamaModel);

var sshPool = new List<SshCredential>
{
    new() { Host = "192.168.1.22", Username = "ubuntu", Password = "ubuntu" },
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

        // Initialize services and tools
        var markdownFormatter = new MarkdownFormatter();
        var markdownReportTool = new MarkdownReportTool(incidentContext.DirectoryPath);

        // Create SSH functions with command allowlists
        var sshFunctionAnalyzer = sshTool.ExecuteDynamic(
            CommandAllowlist.AnalyzerCommands,
            "Analyzer"
        );
        var sshFunctionSolver = sshTool.ExecuteDynamic(
            CommandAllowlist.SolverCommands,
            "Solver"
        );

        // Create ReadAnalysis function
        var readAnalysisFunction = AIFunctionFactory.Create(
            markdownReportTool.ReadAnalysis,
            new AIFunctionFactoryOptions
            {
                Name = "read_analysis",
                Description = "Reads the complete analysis report from analysis.md"
            }
        );

        // Create agents with updated tool lists
        var boberBuilder = new BoberBuilder(chatClient);
        AIAgent boberAnalyzer = boberBuilder.BuildBoberAnalizer([sshFunctionAnalyzer]);
        AIAgent boberSummarizer = boberBuilder.BuildBoberSummarizer([readAnalysisFunction]);
        AIAgent boberSolver = boberBuilder.BuildBoberSolver([sshFunctionSolver, readAnalysisFunction]); // Future use

        // Build workflow: Analyzer → Summarizer → optionally Solver
        var workflow = new WorkflowBuilder(boberAnalyzer)
            // Consider using a custom agent executor: https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/using-agents?pivots=programming-language-csharp
            // And agent threads: https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/multi-turn-conversation?pivots=programming-language-csharp.
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

        // Track iteration count for markdown formatting
        int analyzerIterationCount = 0;
        string currentAgentResponse = string.Empty;

        // Collect results from workflow events
        foreach (WorkflowEvent evt in run.NewEvents)
        {
            switch (evt)
            {
                case AgentRunUpdateEvent agentUpdate:
                    if (agentUpdate.Update != null)
                    {
                        var updateContent = agentUpdate.Update.ToString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(updateContent))
                        {
                            currentAgentResponse = updateContent;
                        }
                    }
                    break;

                case ExecutorCompletedEvent executorComplete:
                    var agentResponse = currentAgentResponse;

                    try
                    {
                        if (executorComplete.ExecutorId?.Contains("Analyzer") == true)
                        {
                            // Deserialize Analyzer response
                            var analyzerResponse = JsonSerializer.Deserialize<AnalyzerIterationResponse>(
                                agentResponse,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                            );

                            if (analyzerResponse == null)
                            {
                                logger.LogError("Failed to deserialize Analyzer response");
                                break;
                            }

                            analyzerIterationCount++;

                            // Generate markdown and append to analysis.md
                            string markdownEntry = markdownFormatter.FormatAnalyzerIteration(
                                analyzerResponse,
                                analyzerIterationCount
                            );
                            await File.AppendAllTextAsync(incidentContext.AnalysisFilePath, markdownEntry);

                            logger.LogInformation(
                                "Analyzer iteration {Iteration}: {Progress}% complete - {Focus} (IsComplete: {IsComplete})",
                                analyzerIterationCount,
                                analyzerResponse.Progress.CompletionPercentage,
                                analyzerResponse.Progress.CurrentFocus,
                                analyzerResponse.IsComplete
                            );

                            if (analyzerResponse.IsComplete)
                            {
                                logger.LogInformation("Analysis phase complete, transitioning to Summarizer");
                            }
                        }
                        else if (executorComplete.ExecutorId?.Contains("Summarizer") == true)
                        {
                            // Deserialize Summarizer response
                            var summaryResponse = JsonSerializer.Deserialize<SummaryReport>(
                                agentResponse,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                            );

                            if (summaryResponse == null)
                            {
                                logger.LogError("Failed to deserialize Summarizer response");
                                break;
                            }

                            if (summaryResponse.IsComplete)
                            {
                                // Generate and write summary markdown
                                string summaryMarkdown = markdownFormatter.FormatSummary(
                                    summaryResponse,
                                    incidentContext.IncidentId,
                                    monitorEvent.Url,
                                    monitorEvent.StatusCode
                                );
                                await File.WriteAllTextAsync(incidentContext.SummaryFilePath, summaryMarkdown);

                                logger.LogInformation(
                                    "Summary complete - Severity: {Severity}, Root Cause: {RootCause}",
                                    summaryResponse.Severity,
                                    summaryResponse.RootCause
                                );
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        logger.LogError(ex, "JSON deserialization error for {AgentId}: {Response}",
                            executorComplete.ExecutorId, agentResponse);
                        // Continue workflow - don't crash on deserialization errors
                    }

                    currentAgentResponse = string.Empty;
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

    try
    {
        var analyzerResponse = JsonSerializer.Deserialize<AnalyzerIterationResponse>(
            response,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        // Continue if analysis is not complete
        return analyzerResponse?.IsComplete == false;
    }
    catch
    {
        // Safe default: continue if we can't parse
        return true;
    }
}

// Workflow condition: Is Analyzer complete and ready to move to Summarizer?
static bool IsAnalysisComplete(ExecutorCompletedEvent? evt)
{
    if (evt == null)
        return false;

    var response = evt.Data?.ToString() ?? string.Empty;

    try
    {
        var analyzerResponse = JsonSerializer.Deserialize<AnalyzerIterationResponse>(
            response,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        // Move to Summarizer when analysis is complete
        return analyzerResponse?.IsComplete == true;
    }
    catch
    {
        // Safe default: don't transition if we can't parse
        return false;
    }
}

// Workflow condition: Should Summarizer continue iterating?
static bool ShouldContinueSummarizing(ExecutorCompletedEvent? evt)
{
    if (evt == null)
        return false;

    var response = evt.Data?.ToString() ?? string.Empty;

    try
    {
        var summaryResponse = JsonSerializer.Deserialize<SummaryReport>(
            response,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        // Continue if summary is not complete
        return summaryResponse?.IsComplete == false;
    }
    catch
    {
        // Safe default: continue if we can't parse
        return true;
    }
}