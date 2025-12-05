using Bober.Builders;
using Bober.Models;
using Bober.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace Bober.Services;

public class IncidentWorkflowService
{
    private readonly IChatClient _chatClient;
    private readonly SshTool _sshTool;
    private readonly MarkdownFormatter _markdownFormatter;
    private readonly ILogger<IncidentWorkflowService> _logger;

    public IncidentWorkflowService(
        IChatClient chatClient,
        SshTool sshTool,
        MarkdownFormatter markdownFormatter,
        ILogger<IncidentWorkflowService> logger)
    {
        _chatClient = chatClient;
        _sshTool = sshTool;
        _markdownFormatter = markdownFormatter;
        _logger = logger;
    }

    private class WorkflowExecutionContext
    {
        public int AnalyzerIterationCount { get; set; }
    }

    public async Task ExecuteWorkflowAsync(MonitorEvent monitorEvent, IncidentContext incidentContext)
    {
        try
        {
            _logger.LogInformation("Starting workflow for incident {IncidentId}", incidentContext.IncidentId);

            // Initialize tools
            var markdownReportTool = new MarkdownReportTool(incidentContext.DirectoryPath);

            // Create SSH functions with command allowlists
            var sshFunctionAnalyzer = _sshTool.ExecuteDynamic(
                CommandAllowlist.AnalyzerCommands,
                "Analyzer"
            );
            var sshFunctionSolver = _sshTool.ExecuteDynamic(
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
            var boberBuilder = new BoberBuilder(_chatClient);
            AIAgent boberAnalyzer = boberBuilder.BuildBoberAnalizer([sshFunctionAnalyzer]);
            AIAgent boberSummarizer = boberBuilder.BuildBoberSummarizer([readAnalysisFunction]);
            AIAgent boberSolver = boberBuilder.BuildBoberSolver([sshFunctionSolver, readAnalysisFunction]); // Future use

            // Build workflow: Analyzer → Summarizer → optionally Solver
            var workflow = new WorkflowBuilder(boberAnalyzer)
                .AddEdge<ExecutorCompletedEvent>(boberAnalyzer, boberAnalyzer, condition: ShouldContinueAnalysis)
                .AddEdge<ExecutorCompletedEvent>(boberAnalyzer, boberSummarizer, condition: IsAnalysisComplete)
                .AddEdge<ExecutorCompletedEvent>(boberSummarizer, boberSummarizer, condition: ShouldContinueSummarizing)
                .WithOutputFrom(boberSummarizer)
                .Build();

            // Execute the workflow
            _logger.LogInformation("Starting workflow execution...");
            await using Run run = await InProcessExecution.RunAsync(
                workflow,
                $"Investigate this incident and provide a detailed analysis:\n\nURL: {monitorEvent.Url}\nStatus Code: {monitorEvent.StatusCode}"
            );

            // Track execution context
            var executionContext = new WorkflowExecutionContext();
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
                                await ProcessAnalyzerResponseAsync(
                                    agentResponse,
                                    incidentContext,
                                    executionContext
                                );
                            }
                            else if (executorComplete.ExecutorId?.Contains("Summarizer") == true)
                            {
                                await ProcessSummarizerResponseAsync(
                                    agentResponse,
                                    incidentContext,
                                    monitorEvent
                                );
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(ex, "JSON deserialization error for {AgentId}: {Response}",
                                executorComplete.ExecutorId, agentResponse);
                            // Continue workflow - don't crash on deserialization errors
                        }

                        currentAgentResponse = string.Empty;
                        break;
                }
            }

            _logger.LogInformation("Workflow completed successfully for incident {IncidentId}", incidentContext.IncidentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing workflow for incident {IncidentId}", incidentContext.IncidentId);
            throw;
        }
    }

    private async Task ProcessAnalyzerResponseAsync(
        string agentResponse,
        IncidentContext incidentContext,
        WorkflowExecutionContext executionContext)
    {
        // Deserialize Analyzer response
        var analyzerResponse = JsonSerializer.Deserialize<AnalyzerIterationResponse>(
            agentResponse,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        if (analyzerResponse == null)
        {
            _logger.LogError("Failed to deserialize Analyzer response");
            return;
        }

        executionContext.AnalyzerIterationCount++;

        // Generate markdown and append to analysis.md
        string markdownEntry = _markdownFormatter.FormatAnalyzerIteration(
            analyzerResponse,
            executionContext.AnalyzerIterationCount
        );
        await File.AppendAllTextAsync(incidentContext.AnalysisFilePath, markdownEntry);

        _logger.LogInformation(
            "Analyzer iteration {Iteration}: {Progress}% complete - {Focus} (IsComplete: {IsComplete})",
            executionContext.AnalyzerIterationCount,
            analyzerResponse.Progress.CompletionPercentage,
            analyzerResponse.Progress.CurrentFocus,
            analyzerResponse.IsComplete
        );

        if (analyzerResponse.IsComplete)
        {
            _logger.LogInformation("Analysis phase complete, transitioning to Summarizer");
        }
    }

    private async Task ProcessSummarizerResponseAsync(
        string agentResponse,
        IncidentContext incidentContext,
        MonitorEvent monitorEvent)
    {
        // Deserialize Summarizer response
        var summaryResponse = JsonSerializer.Deserialize<SummaryReport>(
            agentResponse,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        if (summaryResponse == null)
        {
            _logger.LogError("Failed to deserialize Summarizer response");
            return;
        }

        if (summaryResponse.IsComplete)
        {
            // Generate and write summary markdown
            string summaryMarkdown = _markdownFormatter.FormatSummary(
                summaryResponse,
                incidentContext.IncidentId,
                monitorEvent.Url,
                monitorEvent.StatusCode
            );
            await File.WriteAllTextAsync(incidentContext.SummaryFilePath, summaryMarkdown);

            _logger.LogInformation(
                "Summary complete - Severity: {Severity}, Root Cause: {RootCause}",
                summaryResponse.Severity,
                summaryResponse.RootCause
            );
        }
    }

    // Workflow condition: Should Analyzer continue iterating?
    private static bool ShouldContinueAnalysis(ExecutorCompletedEvent? evt)
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
    private static bool IsAnalysisComplete(ExecutorCompletedEvent? evt)
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
    private static bool ShouldContinueSummarizing(ExecutorCompletedEvent? evt)
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
}
