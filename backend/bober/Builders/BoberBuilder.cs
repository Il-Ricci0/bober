using Bober.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace Bober.Builders;

public class BoberBuilder (IChatClient chatClient)
{

    public AIAgent BuildBoberAnalizer()
    {
        // Create JSON schema for structured output
        JsonElement analysisSchema = AIJsonUtilities.CreateJsonSchema(
            typeof(AnalysisReport),
            serializerOptions: JsonSerializerOptions.Default
        );

        return new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Name = "Bober Analyzer",
                Instructions = """
                    You are an IT expert focused on finding, analyzing and reporting on problems.

                    Your task is to thoroughly investigate incidents by:
                    1. Connecting to the affected server via SSH
                    2. Running diagnostic commands to identify the root cause
                    3. Documenting your findings in a structured report

                    You must provide your analysis in a structured JSON format with:
                    - Summary: Brief overview of the incident
                    - RootCause: The identified root cause
                    - CommandsExecuted: Array of diagnostic commands you ran
                    - Findings: Detailed findings from your investigation
                    - Severity: Critical/High/Medium/Low
                    - Recommendations: Suggested fixes or next steps
                    - IsComplete: Set to true when analysis is finished

                    Work iteratively until you have completed your analysis.
                    """,
                ChatOptions = new ChatOptions
                {
                    Tools = [
                        // sshTool
                        // reportWriterTool
                    ],
                    ResponseFormat = ChatResponseFormat.ForJsonSchema(
                        schema: analysisSchema,
                        schemaName: "AnalysisReport",
                        schemaDescription: "Structured analysis report for incident investigation"
                    )
                }
            }
        );
    }

    public AIAgent BuildBoberSolver()
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
                    Tools = [
                        // sshTool
                        // reportWriterPool
                    ],
                    ResponseFormat = ChatResponseFormat.ForJsonSchema(
                        schema: resolutionSchema,
                        schemaName: "ResolutionReport",
                        schemaDescription: "Structured resolution report documenting fix implementation"
                    )
                }
            }
        );
    }

}