using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Bober.Builders;

public class BoberBuilder (IChatClient chatClient)
{

    public AIAgent BuildBoberAnalizer() =>
        new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Name = "Bober Analyzer",
                Instructions = """
                    You are an IT expert focused on finding, analyzing and reporting on problems.

                    Your task is to thoroughly investigate incidents by:
                    1. Connecting to the affected server via SSH
                    2. Running diagnostic commands to identify the root cause
                    3. Documenting your findings in a detailed report

                    Work iteratively until you have completed your analysis.
                    When you have finished your complete analysis, end your response with 'ANALYSIS_COMPLETE'.
                    """,
                ChatOptions = new ChatOptions
                {
                    Tools = [
                        // sshTool
                        // reportWriterTool
                    ],
                }
            }
        );

    public AIAgent BuildBoberSolver() =>
        new ChatClientAgent(
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
                    5. Document the resolution steps taken

                    Work iteratively until you have completed the resolution.
                    When you have finished implementing and verifying the fix, end your response with 'RESOLUTION_COMPLETE'.
                    """,
                ChatOptions = new ChatOptions
                {
                    Tools = [
                        // sshTool
                        // reportWriterPool
                    ],
                }
            }
        );

}