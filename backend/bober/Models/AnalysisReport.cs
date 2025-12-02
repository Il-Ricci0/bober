namespace Bober.Models;

/// <summary>
/// Structured output from Bober Analyzer containing incident analysis details
/// </summary>
public class AnalysisReport
{
    /// <summary>
    /// Brief summary of the incident (1-2 sentences)
    /// </summary>
    public required string Summary { get; set; }

    /// <summary>
    /// Identified root cause of the incident
    /// </summary>
    public required string RootCause { get; set; }

    /// <summary>
    /// List of diagnostic commands executed during investigation
    /// </summary>
    public required List<DiagnosticCommand> CommandsExecuted { get; set; }

    /// <summary>
    /// Detailed findings from the investigation
    /// </summary>
    public required List<string> Findings { get; set; }

    /// <summary>
    /// Severity level of the incident
    /// </summary>
    public required string Severity { get; set; }

    /// <summary>
    /// Recommended next steps or fixes
    /// </summary>
    public required List<string> Recommendations { get; set; }

    /// <summary>
    /// Indicates if analysis is complete
    /// </summary>
    public required bool IsComplete { get; set; }
}

/// <summary>
/// Represents a diagnostic command executed during analysis
/// </summary>
public class DiagnosticCommand
{
    /// <summary>
    /// The command that was executed
    /// </summary>
    public required string Command { get; set; }

    /// <summary>
    /// Output from the command execution
    /// </summary>
    public required string Output { get; set; }

    /// <summary>
    /// Purpose or reason for running this command
    /// </summary>
    public required string Purpose { get; set; }
}
