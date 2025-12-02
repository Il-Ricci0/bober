namespace Bober.Models;

public class SshCredential
{
    public string Host { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
    public string? Passphrase { get; set; }
}
