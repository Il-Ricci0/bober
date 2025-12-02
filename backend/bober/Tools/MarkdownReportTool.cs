using System.ComponentModel;

namespace Bober.Tools;

/// <summary>
/// Tool for writing analysis and summary reports to markdown files
/// </summary>
public class MarkdownReportTool
{
    private readonly string _incidentDirectory;

    public MarkdownReportTool(string incidentDirectory)
    {
        _incidentDirectory = incidentDirectory;
    }

    /// <summary>
    /// Appends a command execution log entry to the analysis.md file
    /// </summary>
    [Description("Logs an SSH command execution with reasoning and findings to the analysis report")]
    public async Task<string> LogCommandExecution(
        [Description("The reason why you want to execute this command")] string reason,
        [Description("The SSH command that was executed")] string command,
        [Description("The output from the command execution")] string output,
        [Description("What you learned or discovered from this command's output")] string findings)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
        var analysisFile = Path.Combine(_incidentDirectory, "analysis.md");

        var entry = $"""
            ## {timestamp}

            **Reason:** {reason}

            **Command:**
            ```bash
            {command}
            ```

            **Output:**
            ```
            {output}
            ```

            **Findings:** {findings}

            ---

            """;

        await File.AppendAllTextAsync(analysisFile, entry);
        return $"Logged command execution to analysis.md";
    }

    /// <summary>
    /// Writes a note or observation to the analysis.md file
    /// </summary>
    [Description("Adds a general note or observation to the analysis report")]
    public async Task<string> LogNote(
        [Description("The note or observation to log")] string note)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
        var analysisFile = Path.Combine(_incidentDirectory, "analysis.md");

        var entry = $"""
            ## {timestamp}

            **Note:** {note}

            ---

            """;

        await File.AppendAllTextAsync(analysisFile, entry);
        return $"Logged note to analysis.md";
    }

    /// <summary>
    /// Reads the entire analysis.md file
    /// </summary>
    [Description("Reads the complete analysis report from analysis.md")]
    public async Task<string> ReadAnalysis()
    {
        var analysisFile = Path.Combine(_incidentDirectory, "analysis.md");

        if (!File.Exists(analysisFile))
        {
            return "No analysis file exists yet.";
        }

        return await File.ReadAllTextAsync(analysisFile);
    }

    /// <summary>
    /// Writes the summary to analysis-summary.md file
    /// </summary>
    [Description("Writes the final summary of the analysis to analysis-summary.md")]
    public async Task<string> WriteSummary(
        [Description("The complete summary in markdown format")] string summaryContent)
    {
        var summaryFile = Path.Combine(_incidentDirectory, "analysis-summary.md");

        await File.WriteAllTextAsync(summaryFile, summaryContent);
        return $"Summary written to analysis-summary.md";
    }
}
