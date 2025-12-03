using Bober.Models;
using Microsoft.Extensions.AI;
using Renci.SshNet;

namespace Bober.Tools;

public class SshTool : AITool
{
    private readonly List<SshCredential> _credentialPool;

    public SshTool(List<SshCredential> credentialPool)
    {
        _credentialPool = credentialPool;
    }

    // Tool metadata
    public override string Name => "SSH Tool";
    public override string Description => "Executes commands on remote servers via SSH using credentials from a pool";

    // Expose dynamic SSH execution as an AIFunction with command validation
    public AIFunction ExecuteDynamic(HashSet<string> allowedCommands, string agentType)
    {
        return AIFunctionFactory.Create(
            (string host, string command) =>
            {
                // Validate command against allowlist
                if (!CommandAllowlist.IsCommandAllowed(command, allowedCommands))
                {
                    var errorMessage = CommandAllowlist.GetRejectionMessage(command, agentType);
                    throw new UnauthorizedAccessException(errorMessage);
                }

                var cred = _credentialPool.FirstOrDefault(c => c.Host == host);
                if (cred == null) throw new Exception($"No credentials found for host {host}");

                Renci.SshNet.ConnectionInfo connection;

                if (!string.IsNullOrEmpty(cred.Password))
                {
                    connection = new PasswordConnectionInfo(cred.Host, cred.Username, cred.Password);
                }
                else if (!string.IsNullOrEmpty(cred.PrivateKeyPath))
                {
                    var keyFile = cred.Passphrase == null
                        ? new PrivateKeyFile(cred.PrivateKeyPath)
                        : new PrivateKeyFile(cred.PrivateKeyPath, cred.Passphrase);

                    connection = new PrivateKeyConnectionInfo(cred.Host, cred.Username, keyFile);
                }
                else
                {
                    throw new Exception("Invalid credentials: no password or private key provided.");
                }

                using var client = new SshClient(connection);
                client.Connect();
                var result = client.RunCommand(command);
                client.Disconnect();

                return result.Result;
            },
            new AIFunctionFactoryOptions
            {
                Name = "ssh_dynamic",
                Description = $"Executes a command on any host from the credential pool. Only commands in the {agentType} allowlist are permitted."
            }
        );
    }
}

