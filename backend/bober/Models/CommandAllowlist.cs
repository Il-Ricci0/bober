namespace Bober.Models;

/// <summary>
/// Defines allowlists of SSH commands for different agent types
/// </summary>
public static class CommandAllowlist
{
    /// <summary>
    /// Commands allowed for the Analyzer agent (diagnostic/read-only operations)
    /// </summary>
    public static readonly HashSet<string> AnalyzerCommands = new()
    {
        // System information
        "uname",
        "hostname",
        "uptime",
        "whoami",
        "date",

        // Disk usage
        "df",
        "du",
        "lsblk",

        // Memory and CPU
        "free",
        "top",
        "htop",
        "ps",
        "vmstat",
        "iostat",

        // Network diagnostics
        "netstat",
        "ss",
        "ip",
        "ifconfig",
        "ping",
        "traceroute",
        "nslookup",
        "dig",
        "curl",
        "wget",

        // Log viewing
        "tail",
        "head",
        "cat",
        "less",
        "more",
        "grep",
        "journalctl",
        "dmesg",

        // Service status
        "systemctl",
        "service",
        "docker",

        // File system (read-only)
        "ls",
        "find",
        "stat",
        "file",
        "wc",
        "which",
        "whereis",

        // Environment
        "env",
        "printenv",
        "echo"
    };

    /// <summary>
    /// Commands allowed for the Solver agent (includes remediation operations)
    /// </summary>
    public static readonly HashSet<string> SolverCommands = new()
    {
        // Include all analyzer commands (Solver can diagnose too)
        "uname",
        "hostname",
        "uptime",
        "whoami",
        "date",
        "df",
        "du",
        "lsblk",
        "free",
        "top",
        "htop",
        "ps",
        "vmstat",
        "iostat",
        "netstat",
        "ss",
        "ip",
        "ifconfig",
        "ping",
        "traceroute",
        "nslookup",
        "dig",
        "curl",
        "wget",
        "tail",
        "head",
        "cat",
        "less",
        "more",
        "grep",
        "journalctl",
        "dmesg",
        "systemctl",
        "service",
        "docker",
        "ls",
        "find",
        "stat",
        "file",
        "wc",
        "which",
        "whereis",
        "env",
        "printenv",
        "echo",

        // Service management (Solver-specific)
        "supervisorctl",
        "pm2",

        // Process management
        "kill",
        "pkill",
        "killall",

        // File operations
        "rm",
        "mv",
        "cp",
        "mkdir",
        "rmdir",
        "touch",
        "chmod",
        "chown",
        "chgrp",

        // Text editing/manipulation
        "sed",
        "awk",
        "tee",

        // Cleanup operations
        "truncate",

        // Package management (for fixes)
        "apt",
        "apt-get",
        "yum",
        "dnf",
        "npm",
        "pip",
        "pip3"
    };

    /// <summary>
    /// Validates if a command is allowed based on the provided allowlist
    /// </summary>
    public static bool IsCommandAllowed(string command, HashSet<string> allowlist)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        // Extract the base command (first word)
        var baseCommand = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

        if (string.IsNullOrWhiteSpace(baseCommand))
            return false;

        // Check if the base command is in the allowlist
        return allowlist.Contains(baseCommand);
    }

    /// <summary>
    /// Gets a user-friendly error message for rejected commands
    /// </summary>
    public static string GetRejectionMessage(string command, string agentType)
    {
        var baseCommand = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return $"Command '{baseCommand}' is not allowed for {agentType} agent. Command rejected for security reasons.";
    }
}
