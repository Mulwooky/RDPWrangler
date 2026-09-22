namespace RDPWrangler.Models;

public class RdpServerConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = string.Empty;
    public string Group { get; set; } = "General";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 3389;
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool SmartSizing { get; set; } = true;
    public string Notes { get; set; } = string.Empty;
    public string? CredentialProfileId { get; set; }

    public string Title => !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName : Host;

    public string FullAddress => Port == 3389 ? Host : $"{Host}:{Port}";

    public RdpServerConnection Clone()
    {
        return new RdpServerConnection
        {
            Id = this.Id,
            DisplayName = this.DisplayName,
            Group = this.Group,
            Host = this.Host,
            Port = this.Port,
            Username = this.Username,
            Domain = this.Domain,
            Password = this.Password,
            SmartSizing = this.SmartSizing,
            Notes = this.Notes,
            CredentialProfileId = this.CredentialProfileId
        };
    }
}
