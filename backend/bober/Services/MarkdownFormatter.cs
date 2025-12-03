using Bober.Models;
using System.Text;

namespace Bober.Services;

/// <summary>
/// Service for generating markdown reports from structured data
/// </summary>
public class MarkdownFormatter
{
    /// <summary>
    /// Appends an Analyzer iteration to the analysis.md file
    /// </summary>
    public string FormatAnalyzerIteration(AnalyzerIterationResponse response, int iterationNumber)
    {
        var sb = new StringBuilder();
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

        sb.AppendLine($"## Iteration {iterationNumber} - {timestamp}");
        sb.AppendLine();
        sb.AppendLine($"**Reasoning:** {response.Reasoning}");
        sb.AppendLine();

        if (response.CommandExecuted != null)
        {
            sb.AppendLine($"**Command Executed on {response.CommandExecuted.Host}:**");
            sb.AppendLine("```bash");
            sb.AppendLine(response.CommandExecuted.Command);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("**Output:**");
            sb.AppendLine("```");
            sb.AppendLine(response.CommandExecuted.Output);
            sb.AppendLine("```");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("**Command Executed:** None (thinking/planning iteration)");
            sb.AppendLine();
        }

        sb.AppendLine($"**Findings:** {response.CurrentFindings}");
        sb.AppendLine();
        sb.AppendLine($"**Progress:** {response.Progress.CompletionPercentage}% - {response.Progress.CurrentFocus}");
        sb.AppendLine($"**Severity:** {response.Progress.Severity}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Generates the complete analysis-summary.md content
    /// </summary>
    public string FormatSummary(SummaryReport summary, string incidentId, string url, string statusCode)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Incident Summary");
        sb.AppendLine();
        sb.AppendLine($"**Incident ID:** {incidentId}");
        sb.AppendLine($"**URL:** {url}");
        sb.AppendLine($"**Status Code:** {statusCode}");
        sb.AppendLine($"**Report Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine(summary.Overview);
        sb.AppendLine();

        sb.AppendLine("## Root Cause");
        sb.AppendLine();
        sb.AppendLine(summary.RootCause);
        sb.AppendLine();

        sb.AppendLine("## Severity");
        sb.AppendLine();
        sb.AppendLine($"**{summary.Severity.ToUpperInvariant()}**");
        sb.AppendLine();

        sb.AppendLine("## Key Findings");
        sb.AppendLine();
        foreach (var finding in summary.KeyFindings)
        {
            sb.AppendLine($"- {finding}");
        }
        sb.AppendLine();

        sb.AppendLine("## Timeline");
        sb.AppendLine();
        foreach (var evt in summary.Timeline)
        {
            sb.AppendLine($"- **{evt.Timestamp}:** {evt.Description}");
        }
        sb.AppendLine();

        sb.AppendLine("## Recommended Actions");
        sb.AppendLine();
        for (int i = 0; i < summary.RecommendedActions.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {summary.RecommendedActions[i]}");
        }
        sb.AppendLine();

        return sb.ToString();
    }
}
