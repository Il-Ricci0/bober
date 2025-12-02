using Renci.SshNet;

namespace Bober.Tools;

public class SshTool
{
    private readonly Renci.SshNet.ConnectionInfo _connection;

    public SshTool(string host, string username, string password)
    {
        _connection = new PasswordConnectionInfo(
            host,
            username,
            password
        );
    }

    public SshTool(string host, string username, string privateKeyPath, string? passphrase = null)
    {
        var keyFile = passphrase == null
            ? new PrivateKeyFile(privateKeyPath)
            : new PrivateKeyFile(privateKeyPath, passphrase);

        _connection = new PrivateKeyConnectionInfo(
            host,
            username,
            keyFile
        );
    }

    public string ExecuteCommand(string command)
    {
        using var client = new SshClient(_connection);
        client.Connect();
        var result = client.RunCommand(command);
        client.Disconnect();
        return result.Result;
    }
}