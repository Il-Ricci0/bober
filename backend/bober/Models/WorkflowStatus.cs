namespace Bober.Models;

public enum WorkflowState
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

public class WorkflowStatus
{
    public required string IncidentId { get; set; }
    public required WorkflowState State { get; set; }
    public required string CurrentPhase { get; set; }
    public int AnalyzerIterations { get; set; }
    public int SummarizerIterations { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? ErrorMessage { get; set; }
    public required MonitorEvent MonitorEvent { get; set; }
    public required string DirectoryPath { get; set; }
}
