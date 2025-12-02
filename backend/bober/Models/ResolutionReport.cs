namespace Bober.Models;

/// <summary>
/// Structured output from Bober Solver containing resolution details
/// </summary>
public class ResolutionReport
{
    /// <summary>
    /// Brief summary of the resolution approach
    /// </summary>
    public required string Summary { get; set; }

    /// <summary>
    /// List of resolution steps executed to fix the incident
    /// </summary>
    public required List<ResolutionStep> StepsExecuted { get; set; }

    /// <summary>
    /// Result of the fix attempt (success/partial/failed)
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Verification details confirming the fix worked
    /// </summary>
    public required List<string> VerificationResults { get; set; }

    /// <summary>
    /// Any follow-up actions or monitoring recommendations
    /// </summary>
    public required List<string> FollowUpActions { get; set; }

    /// <summary>
    /// Indicates if resolution is complete
    /// </summary>
    public required bool IsComplete { get; set; }
}

/// <summary>
/// Represents a resolution step executed during fixing
/// </summary>
public class ResolutionStep
{
    /// <summary>
    /// Description of the action taken
    /// </summary>
    public required string Action { get; set; }

    /// <summary>
    /// The command(s) executed for this step
    /// </summary>
    public required string Command { get; set; }

    /// <summary>
    /// Output from the command execution
    /// </summary>
    public required string Output { get; set; }

    /// <summary>
    /// Whether this step was successful
    /// </summary>
    public required bool Success { get; set; }
}
