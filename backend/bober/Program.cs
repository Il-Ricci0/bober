using Bober.Models;
using Bober.Services;
using Bober.Tools;
using Microsoft.Extensions.AI;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddEndpointsApiExplorer();

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Initialize Ollama client
var ollamaUri = new Uri("http://localhost:11434");
string ollamaModel = "llama3.1:8b";
IChatClient chatClient = new OllamaApiClient(ollamaUri, ollamaModel);

// Initialize SSH credential pool
var sshPool = new List<SshCredential>
{
    new() { Host = "192.168.1.22", Username = "ubuntu", Password = "ubuntu" },
};

// Register services for dependency injection
builder.Services.AddSingleton(chatClient);
builder.Services.AddSingleton(new SshTool(sshPool));
builder.Services.AddSingleton<MarkdownFormatter>();
builder.Services.AddSingleton<WorkflowTracker>();
builder.Services.AddSingleton<IncidentWorkflowService>();

var app = builder.Build();

// Use CORS
app.UseCors("AllowAngularDev");

// Health check endpoint
app.MapGet("/", () => Results.Ok(new { status = "Bober is running", timestamp = DateTime.UtcNow }));

// Webhook endpoint for incident reports
app.MapPost("/webhook/incident", async (
    MonitorEvent monitorEvent,
    IncidentWorkflowService workflowService,
    WorkflowTracker workflowTracker,
    ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Received incident webhook - URL: {Url}, Status Code: {StatusCode}",
            monitorEvent.Url, monitorEvent.StatusCode);

        // Create incident directory structure
        var incidentContext = IncidentContext.Create(
            Directory.GetCurrentDirectory(),
            monitorEvent
        );
        logger.LogInformation("Created incident directory: {IncidentId}", incidentContext.IncidentId);

        // Register workflow for tracking
        var workflowStatus = workflowTracker.RegisterWorkflow(
            incidentContext.IncidentId,
            monitorEvent,
            incidentContext.DirectoryPath
        );

        // Execute workflow in background without blocking the response
        _ = Task.Run(async () =>
        {
            try
            {
                await workflowService.ExecuteWorkflowAsync(monitorEvent, incidentContext);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background workflow execution failed for incident {IncidentId}",
                    incidentContext.IncidentId);
            }
        });

        // Return immediately with incident details
        return Results.Accepted(
            $"/incident/{incidentContext.IncidentId}",
            new
            {
                status = "processing",
                incidentId = incidentContext.IncidentId,
                incident = new
                {
                    url = monitorEvent.Url,
                    statusCode = monitorEvent.StatusCode
                },
                directoryPath = incidentContext.DirectoryPath,
                message = "Incident workflow started. Check the incident directory for progress.",
                timestamp = DateTime.UtcNow
            });
    }
    catch (Exception ex)
    {
        logger.LogInformation(ex, "Error processing incident webhook");
        return Results.Problem(
            title: "Incident processing failed",
            detail: ex.Message,
            statusCode: 500
        );
    }
});

// Get all workflows
app.MapGet("/workflows", (WorkflowTracker workflowTracker) =>
{
    var workflows = workflowTracker.GetAllWorkflows();
    return Results.Ok(workflows);
});

// Get specific workflow status
app.MapGet("/workflows/{incidentId}", (string incidentId, WorkflowTracker workflowTracker) =>
{
    var status = workflowTracker.GetStatus(incidentId);
    return status != null ? Results.Ok(status) : Results.NotFound();
});

// Cancel a workflow
app.MapPost("/workflows/{incidentId}/cancel", (string incidentId, WorkflowTracker workflowTracker) =>
{
    var cancelled = workflowTracker.CancelWorkflow(incidentId);
    return cancelled
        ? Results.Ok(new { message = "Workflow cancelled successfully" })
        : Results.NotFound(new { message = "Workflow not found or already completed" });
});

app.Run();