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
                Instructions = "You are an IT expert focused on finding, analyzing and reporting on problems.",
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
                Name = "Bober Fixer",
                Instructions = "You are an IT expert focused on reading reports about problems and fixing them and report on the resolution.",
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