using Bober.Builders;
using Bober.Models;
using Bober.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace Bober.Services;

public class IncidentWorkflowService
{
    private readonly IChatClient _chatClient;
    private readonly SshTool _sshTool;
    private readonly MarkdownFormatter _markdownFormatter;
    private readonly ILogger<IncidentWorkflowService> _logger;
    private const int MaxAnalyzerIterations = 20;
    private const int MaxSummarizerIterations = 5;

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

    public async Task ExecuteWorkflowAsync(MonitorEvent monitorEvent, IncidentContext incidentContext)
    {
        try
        {
            _logger.LogInformation("Starting incident response for {IncidentId}", incidentContext.IncidentId);

            // Initialize tools
            var markdownReportTool = new MarkdownReportTool(incidentContext.DirectoryPath);

            // Create SSH functions with command allowlists
            var sshFunctionAnalyzer = _sshTool.ExecuteDynamic(
                CommandAllowlist.AnalyzerCommands,
                "Analyzer"
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

            // Create agents
            var boberBuilder = new BoberBuilder(_chatClient);
            AIAgent boberAnalyzer = boberBuilder.BuildBoberAnalizer([sshFunctionAnalyzer]);
            AIAgent boberSummarizer = boberBuilder.BuildBoberSummarizer([readAnalysisFunction]);

            // Phase 1: Run Analyzer loop
            _logger.LogInformation("Starting analysis phase...");
            await RunAnalyzerLoopAsync(boberAnalyzer, monitorEvent, incidentContext);

            // Phase 2: Run Summarizer loop
            _logger.LogInformation("Starting summarization phase...");
            await RunSummarizerLoopAsync(boberSummarizer, monitorEvent, incidentContext);

            _logger.LogInformation("Incident response completed for {IncidentId}", incidentContext.IncidentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing incident response for {IncidentId}", incidentContext.IncidentId);
            throw;
        }
    }

    private async Task RunAnalyzerLoopAsync(
        AIAgent analyzer,
        MonitorEvent monitorEvent,
        IncidentContext incidentContext)
    {
        // Create a new thread for this conversation
        AgentThread thread = analyzer.GetNewThread();

        int iterationCount = 0;
        bool isComplete = false;

        // Initial prompt
        string userPrompt = $"Investigate this incident and provide a detailed analysis:\n\nURL: {monitorEvent.Url}\nStatus Code: {monitorEvent.StatusCode}";

        while (!isComplete && iterationCount < MaxAnalyzerIterations)
        {
            iterationCount++;
            _logger.LogInformation("Analyzer iteration {Iteration}", iterationCount);

            // Run the agent with the thread
            var response = await analyzer.RunAsync(userPrompt, thread);

            // Extract the response content
            string responseContent = string.Empty;
            foreach (var message in response.Messages)
            {
                if (message.Role == ChatRole.Assistant)
                {
                    responseContent = message.Text ?? string.Empty;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                _logger.LogWarning("Analyzer iteration {Iteration} returned empty response", iterationCount);
                break;
            }

            // Parse and process the response
            try
            {
                var analyzerResponse = JsonSerializer.Deserialize<AnalyzerIterationResponse>(
                    responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (analyzerResponse == null)
                {
                    _logger.LogError("Failed to deserialize Analyzer response at iteration {Iteration}", iterationCount);
                    break;
                }

                // Generate markdown and append to analysis.md
                string markdownEntry = _markdownFormatter.FormatAnalyzerIteration(analyzerResponse, iterationCount);
                await File.AppendAllTextAsync(incidentContext.AnalysisFilePath, markdownEntry);

                _logger.LogInformation(
                    "Analyzer iteration {Iteration}: {Progress}% complete - {Focus} (IsComplete: {IsComplete})",
                    iterationCount,
                    analyzerResponse.Progress.CompletionPercentage,
                    analyzerResponse.Progress.CurrentFocus,
                    analyzerResponse.IsComplete
                );

                isComplete = analyzerResponse.IsComplete;

                // If not complete, set up prompt for next iteration
                if (!isComplete)
                {
                    userPrompt = "Continue your investigation based on the previous findings.";
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error at iteration {Iteration}: {Response}",
                    iterationCount, responseContent);
                break;
            }
        }

        if (iterationCount >= MaxAnalyzerIterations)
        {
            _logger.LogWarning("Analyzer reached maximum iterations ({Max})", MaxAnalyzerIterations);
        }
        else
        {
            _logger.LogInformation("Analysis phase complete after {Count} iterations", iterationCount);
        }
    }

    private async Task RunSummarizerLoopAsync(
        AIAgent summarizer,
        MonitorEvent monitorEvent,
        IncidentContext incidentContext)
    {
        // Create a new thread for this conversation
        AgentThread thread = summarizer.GetNewThread();

        int iterationCount = 0;
        bool isComplete = false;

        // Initial prompt
        string userPrompt = "Read the analysis report and create a comprehensive summary of the incident investigation.";

        while (!isComplete && iterationCount < MaxSummarizerIterations)
        {
            iterationCount++;
            _logger.LogInformation("Summarizer iteration {Iteration}", iterationCount);

            // Run the agent with the thread
            var response = await summarizer.RunAsync(userPrompt, thread);

            // Extract the response content
            string responseContent = string.Empty;
            foreach (var message in response.Messages)
            {
                if (message.Role == ChatRole.Assistant)
                {
                    responseContent = message.Text ?? string.Empty;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                _logger.LogWarning("Summarizer iteration {Iteration} returned empty response", iterationCount);
                break;
            }

            // Parse and process the response
            try
            {
                var summaryResponse = JsonSerializer.Deserialize<SummaryReport>(
                    responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (summaryResponse == null)
                {
                    _logger.LogError("Failed to deserialize Summarizer response at iteration {Iteration}", iterationCount);
                    break;
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

                    isComplete = true;
                }
                else
                {
                    // Set up prompt for next iteration
                    userPrompt = "Please complete the summary based on the analysis.";
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error at iteration {Iteration}: {Response}",
                    iterationCount, responseContent);
                break;
            }
        }

        if (iterationCount >= MaxSummarizerIterations)
        {
            _logger.LogWarning("Summarizer reached maximum iterations ({Max})", MaxSummarizerIterations);
        }
        else
        {
            _logger.LogInformation("Summarization phase complete after {Count} iterations", iterationCount);
        }
    }

}
