using System.ComponentModel;

namespace Bober.Tools;

/// <summary>
/// Tool for reading analysis reports
///
/// Note: LogCommandExecution, LogNote, and WriteSummary methods have been removed.
/// Markdown generation is now handled programmatically in Program.cs via MarkdownFormatter service.
/// </summary>
public class MarkdownReportTool
{
    private readonly string _incidentDirectory;

    public MarkdownReportTool(string incidentDirectory)
    {
        _incidentDirectory = incidentDirectory;
    }

    /// <summary>
    /// Reads the entire analysis.md file
    /// </summary>
    [Description("Reads the complete analysis report from analysis.md")]
    public async Task<string> ReadAnalysis()
    {
        var analysisFile = Path.Combine(_incidentDirectory, "analysis.md");

        if (!File.Exists(analysisFile))
        {
            return "No analysis file exists yet.";
        }

        return await File.ReadAllTextAsync(analysisFile);
    }
}
