namespace Bober.Models;

/// <summary>
/// Structured output from Bober Analyzer for a single iteration
/// </summary>
public class AnalyzerIterationResponse
{
    /// <summary>
    /// Current thought process or reasoning for this iteration
    /// </summary>
    public required string Reasoning { get; set; }

    /// <summary>
    /// Command details if a command was executed this iteration (null if just thinking)
    /// </summary>
    public CommandExecution? CommandExecuted { get; set; }

    /// <summary>
    /// Current findings or observations from this iteration
    /// </summary>
    public required string CurrentFindings { get; set; }

    /// <summary>
    /// Indicates if the analysis is complete and ready to transition to Summarizer
    /// </summary>
    public required bool IsComplete { get; set; }

    /// <summary>
    /// Progress metadata for tracking investigation state
    /// </summary>
    public required ProgressMetadata Progress { get; set; }
}

/// <summary>
/// Details of a command execution in this iteration
/// </summary>
public class CommandExecution
{
    /// <summary>
    /// The SSH command that was executed
    /// </summary>
    public required string Command { get; set; }

    /// <summary>
    /// Output from the command execution
    /// </summary>
    public required string Output { get; set; }

    /// <summary>
    /// Host where command was executed
    /// </summary>
    public required string Host { get; set; }
}

/// <summary>
/// Progress tracking metadata
/// </summary>
public class ProgressMetadata
{
    /// <summary>
    /// Current severity assessment (low/medium/high/critical)
    /// </summary>
    public required string Severity { get; set; }

    /// <summary>
    /// Current focus area of investigation
    /// </summary>
    public required string CurrentFocus { get; set; }

    /// <summary>
    /// Estimated completion percentage (0-100)
    /// </summary>
    public required int CompletionPercentage { get; set; }
}
