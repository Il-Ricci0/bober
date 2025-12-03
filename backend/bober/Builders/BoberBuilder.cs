using Bober.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace Bober.Builders;

public class BoberBuilder (IChatClient chatClient)
{

    public AIAgent BuildBoberAnalizer(IList<AITool> tools)
    {
        // Create JSON schema for structured output
        JsonElement analyzerSchema = AIJsonUtilities.CreateJsonSchema(
            typeof(AnalyzerIterationResponse),
            serializerOptions: JsonSerializerOptions.Default
        );

        return new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Name = "Bober Analyzer",
                Instructions = """
                    You are an IT expert focused on finding, analyzing and reporting on problems.

                    Your task is to thoroughly investigate incidents by connecting to servers via SSH and running diagnostic commands.

                    IMPORTANT: You MUST respond with structured JSON data at EVERY iteration.

                    Your response structure:
                    - Reasoning: Explain your current thinking and why you're taking this step
                    - CommandExecuted: If you executed a command this iteration, include command, output, and host. If just thinking, set to null.
                    - CurrentFindings: What you've learned or observed in this iteration
                    - IsComplete: Set to true ONLY when you've identified the root cause and are ready to hand off to the Summarizer
                    - Progress:
                      - Severity: Current severity assessment (low/medium/high/critical)
                      - CurrentFocus: What aspect you're investigating now
                      - CompletionPercentage: Estimated progress (0-100)

                    WORKFLOW:
                    1. Think about what you need to investigate
                    2. Execute ONE SSH command using the ssh_dynamic tool
                    3. Analyze the output
                    4. Return structured JSON with your findings and reasoning
                    5. Repeat until you've identified the root cause
                    6. Set IsComplete=true when done

                    Work methodically and document your reasoning clearly in each iteration.
                    """,
                ChatOptions = new ChatOptions
                {
                    Tools = tools,
                    ResponseFormat = ChatResponseFormat.ForJsonSchema(
                        schema: analyzerSchema,
                        schemaName: "AnalyzerIterationResponse",
                        schemaDescription: "Structured response for each iteration of incident analysis"
                    )
                }
            }
        );
    }

    public AIAgent BuildBoberSolver(IList<AITool> tools)
    {
        // Create JSON schema for structured output
        JsonElement resolutionSchema = AIJsonUtilities.CreateJsonSchema(
            typeof(ResolutionReport),
            serializerOptions: JsonSerializerOptions.Default
        );

        return new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Name = "Bober Solver",
                Instructions = """
                    You are an IT expert focused on reading reports about problems and fixing them.

                    Your task is to:
                    1. Read and understand the analysis report provided
                    2. Connect to the affected server via SSH
                    3. Execute commands to implement the fix
                    4. Verify the fix was successful
                    5. Document the resolution in a structured format

                    You must provide your resolution in a structured JSON format with:
                    - Summary: Brief overview of the resolution approach
                    - StepsExecuted: Array of resolution steps taken
                    - Status: success/partial/failed
                    - VerificationResults: Details confirming the fix
                    - FollowUpActions: Any monitoring or follow-up needed
                    - IsComplete: Set to true when resolution is finished

                    Work iteratively until you have completed the resolution.
                    """,
                ChatOptions = new ChatOptions
                {
                    Tools = tools,
                    ResponseFormat = ChatResponseFormat.ForJsonSchema(
                        schema: resolutionSchema,
                        schemaName: "ResolutionReport",
                        schemaDescription: "Structured resolution report documenting fix implementation"
                    )
                }
            }
        );
    }

    public AIAgent BuildBoberSummarizer(IList<AITool> tools)
    {
        // Create JSON schema for structured output
        JsonElement summarySchema = AIJsonUtilities.CreateJsonSchema(
            typeof(SummaryReport),
            serializerOptions: JsonSerializerOptions.Default
        );

        return new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Name = "Bober Summarizer",
                Instructions = """
                    You are an IT expert focused on reading detailed analysis reports and creating concise summaries.

                    Your task is to:
                    1. Read the complete analysis.md file using the read_analysis tool
                    2. Synthesize the information into a structured summary

                    You MUST respond with structured JSON data containing:
                    - Overview: Brief 2-3 sentence overview of the incident
                    - RootCause: Identified root cause from the analysis
                    - KeyFindings: List of the most important findings
                    - Severity: Assessment of incident severity
                    - RecommendedActions: List of recommended next steps or fixes
                    - Timeline: Chronological list of critical events during investigation
                    - IsComplete: Set to true when summary is ready

                    Analyze the full analysis report carefully and extract the most important information.
                    Your summary should be clear, concise, and actionable for both technical and non-technical audiences.
                    """,
                ChatOptions = new ChatOptions
                {
                    Tools = tools,
                    ResponseFormat = ChatResponseFormat.ForJsonSchema(
                        schema: summarySchema,
                        schemaName: "SummaryReport",
                        schemaDescription: "Structured summary of incident analysis"
                    )
                }
            }
        );
    }

}