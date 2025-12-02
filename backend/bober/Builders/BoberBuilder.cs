using Bober.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace Bober.Builders;

public class BoberBuilder (IChatClient chatClient)
{

    public AIAgent BuildBoberAnalizer(IList<AITool> tools)
    {
        return new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Name = "Bober Analyzer",
                Instructions = """
                    You are an IT expert focused on finding, analyzing and reporting on problems.

                    Your task is to thoroughly investigate incidents by:
                    1. Connecting to the affected server via SSH to run diagnostic commands
                    2. For EACH command you execute, you MUST log it using LogCommandExecution with:
                       - reason: Why you want to execute this command
                       - command: The exact command you ran
                       - output: The output from the command
                       - findings: What you learned from this output

                    IMPORTANT WORKFLOW:
                    - Before running any SSH command, think about WHY you need it
                    - Execute the SSH command and get the output
                    - Immediately call LogCommandExecution to document it
                    - Analyze the output and decide your next step
                    - Repeat until you have identified the root cause

                    You can also use LogNote to add observations or thoughts.

                    When you have completed your analysis and documented all findings,
                    respond with 'ANALYSIS_COMPLETE'.
                    """,
                ChatOptions = new ChatOptions
                {
                    Tools = tools
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
        return new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Name = "Bober Summarizer",
                Instructions = """
                    You are an IT expert focused on reading detailed analysis reports and creating concise summaries.

                    Your task is to:
                    1. Read the complete analysis.md file that documents all commands and findings
                    2. Synthesize the information into a clear, actionable summary
                    3. Write the summary to analysis-summary.md file

                    Your summary should include:
                    - Brief overview of the incident
                    - Root cause identification
                    - Key findings from the investigation
                    - Severity assessment
                    - Recommended actions or fixes

                    Use clear, professional markdown formatting for the summary.
                    When you have finished writing the summary, include 'SUMMARY_COMPLETE' in your response.
                    """,
                ChatOptions = new ChatOptions
                {
                    Tools = tools
                }
            }
        );
    }

}