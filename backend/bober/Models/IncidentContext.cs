namespace Bober.Models;

/// <summary>
/// Represents the context and file system structure for an incident investigation
/// </summary>
public class IncidentContext
{
    /// <summary>
    /// Unique identifier for this incident
    /// </summary>
    public required string IncidentId { get; set; }

    /// <summary>
    /// Directory path where incident files are stored
    /// </summary>
    public required string DirectoryPath { get; set; }

    /// <summary>
    /// Path to the analysis.md file
    /// </summary>
    public string AnalysisFilePath => Path.Combine(DirectoryPath, "analysis.md");

    /// <summary>
    /// Path to the analysis-summary.md file
    /// </summary>
    public string SummaryFilePath => Path.Combine(DirectoryPath, "analysis-summary.md");

    /// <summary>
    /// Creates a new incident context with directory structure
    /// </summary>
    public static IncidentContext Create(string baseDirectory, MonitorEvent monitorEvent)
    {
        // Generate incident ID from timestamp and URL hash
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var urlHash = Math.Abs(monitorEvent.Url.GetHashCode()).ToString("X6");
        var incidentId = $"{timestamp}-{urlHash}";

        var incidentDir = Path.Combine(baseDirectory, "incidents", incidentId);

        // Create directory if it doesn't exist
        Directory.CreateDirectory(incidentDir);

        // Create initial analysis.md header
        var analysisPath = Path.Combine(incidentDir, "analysis.md");
        var header = $"""
            # Incident Analysis Report

            **Incident ID:** {incidentId}
            **URL:** {monitorEvent.Url}
            **Status Code:** {monitorEvent.StatusCode}
            **Reported At:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}

            ---

            """;

        File.WriteAllText(analysisPath, header);

        return new IncidentContext
        {
            IncidentId = incidentId,
            DirectoryPath = incidentDir
        };
    }
}
