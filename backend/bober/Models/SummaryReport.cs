namespace Bober.Models;

/// <summary>
/// Structured output from Bober Summarizer containing incident summary
/// </summary>
public class SummaryReport
{
    /// <summary>
    /// Brief overview of the incident (2-3 sentences)
    /// </summary>
    public required string Overview { get; set; }

    /// <summary>
    /// Identified root cause
    /// </summary>
    public required string RootCause { get; set; }

    /// <summary>
    /// Key findings from the analysis
    /// </summary>
    public required List<string> KeyFindings { get; set; }

    /// <summary>
    /// Severity assessment
    /// </summary>
    public required string Severity { get; set; }

    /// <summary>
    /// Recommended next steps or actions
    /// </summary>
    public required List<string> RecommendedActions { get; set; }

    /// <summary>
    /// Timeline of critical events during investigation
    /// </summary>
    public required List<TimelineEvent> Timeline { get; set; }

    /// <summary>
    /// Indicates if summary is complete
    /// </summary>
    public required bool IsComplete { get; set; }
}

/// <summary>
/// A significant event in the investigation timeline
/// </summary>
public class TimelineEvent
{
    /// <summary>
    /// Timestamp of the event
    /// </summary>
    public required string Timestamp { get; set; }

    /// <summary>
    /// Description of what happened
    /// </summary>
    public required string Description { get; set; }
}
