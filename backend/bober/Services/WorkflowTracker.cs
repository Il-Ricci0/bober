using Bober.Models;
using System.Collections.Concurrent;

namespace Bober.Services;

public class WorkflowTracker
{
    private readonly ConcurrentDictionary<string, WorkflowStatus> _workflows = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();

    public WorkflowStatus RegisterWorkflow(string incidentId, MonitorEvent monitorEvent, string directoryPath)
    {
        var status = new WorkflowStatus
        {
            IncidentId = incidentId,
            State = WorkflowState.Pending,
            CurrentPhase = "Initializing",
            AnalyzerIterations = 0,
            SummarizerIterations = 0,
            StartTime = DateTime.UtcNow,
            MonitorEvent = monitorEvent,
            DirectoryPath = directoryPath
        };

        _workflows[incidentId] = status;
        _cancellationTokens[incidentId] = new CancellationTokenSource();

        return status;
    }

    public void UpdatePhase(string incidentId, string phase)
    {
        if (_workflows.TryGetValue(incidentId, out var status))
        {
            status.CurrentPhase = phase;
            status.State = WorkflowState.Running;
        }
    }

    public void UpdateAnalyzerIteration(string incidentId, int iteration)
    {
        if (_workflows.TryGetValue(incidentId, out var status))
        {
            status.AnalyzerIterations = iteration;
            status.CurrentPhase = $"Analysis (Iteration {iteration})";
        }
    }

    public void UpdateSummarizerIteration(string incidentId, int iteration)
    {
        if (_workflows.TryGetValue(incidentId, out var status))
        {
            status.SummarizerIterations = iteration;
            status.CurrentPhase = $"Summarization (Iteration {iteration})";
        }
    }

    public void MarkCompleted(string incidentId)
    {
        if (_workflows.TryGetValue(incidentId, out var status))
        {
            status.State = WorkflowState.Completed;
            status.CurrentPhase = "Completed";
            status.EndTime = DateTime.UtcNow;
        }

        if (_cancellationTokens.TryRemove(incidentId, out var cts))
        {
            cts.Dispose();
        }
    }

    public void MarkFailed(string incidentId, string errorMessage)
    {
        if (_workflows.TryGetValue(incidentId, out var status))
        {
            status.State = WorkflowState.Failed;
            status.CurrentPhase = "Failed";
            status.EndTime = DateTime.UtcNow;
            status.ErrorMessage = errorMessage;
        }

        if (_cancellationTokens.TryRemove(incidentId, out var cts))
        {
            cts.Dispose();
        }
    }

    public void MarkCancelled(string incidentId)
    {
        if (_workflows.TryGetValue(incidentId, out var status))
        {
            status.State = WorkflowState.Cancelled;
            status.CurrentPhase = "Cancelled";
            status.EndTime = DateTime.UtcNow;
        }
    }

    public bool CancelWorkflow(string incidentId)
    {
        if (_cancellationTokens.TryGetValue(incidentId, out var cts))
        {
            cts.Cancel();
            MarkCancelled(incidentId);
            return true;
        }
        return false;
    }

    public CancellationToken GetCancellationToken(string incidentId)
    {
        return _cancellationTokens.TryGetValue(incidentId, out var cts)
            ? cts.Token
            : CancellationToken.None;
    }

    public WorkflowStatus? GetStatus(string incidentId)
    {
        return _workflows.TryGetValue(incidentId, out var status) ? status : null;
    }

    public IEnumerable<WorkflowStatus> GetAllWorkflows()
    {
        return _workflows.Values.OrderByDescending(w => w.StartTime);
    }
}
