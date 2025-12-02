using Bober.Builders;
using Bober.Models;
using Bober.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();

// Create the ollama chat client.
var uri = new Uri("http://localhost:11434");
string model = "codellama:7b";
IChatClient chatClient = new OllamaApiClient(uri, model);

var sshPool = new List<SshCredential>
{
    new() { Host = "server1.com", Username = "user1", Password = "pass1" },
    new() { Host = "server2.com", Username = "user2", PrivateKeyPath = "/keys/key2.pem" }
};

var sshTool = new SshTool(sshPool);

var boberBuilder = new BoberBuilder(chatClient);

// Create the agent Bober agents.
AIAgent boberAnalyzer = boberBuilder.BuildBoberAnalizer();
AIAgent boberSolver = boberBuilder.BuildBoberSolver();

var workflow = new WorkflowBuilder(boberAnalyzer)
    .AddEdge(boberAnalyzer, boberSolver)
    .WithOutputFrom(boberSolver)
    .Build();

// Execute the workflow with input data
await using Run run = await InProcessExecution.RunAsync(workflow, "how many files are int the home directory in server1.com?");
foreach (WorkflowEvent evt in run.NewEvents)
{
    switch (evt)
    {
        case ExecutorCompletedEvent executorComplete:
            Console.WriteLine($"{executorComplete.ExecutorId}: {executorComplete.Data}");
            break;
    }
}