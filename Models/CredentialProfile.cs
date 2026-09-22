namespace RDPWrangler.Models;

public class CredentialProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string DisplayText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Title))
            {
                return string.IsNullOrWhiteSpace(Domain) ? Username : $"{Domain}\\{Username}";
            }

            string userPart = string.IsNullOrWhiteSpace(Domain) ? Username : $"{Domain}\\{Username}";
            return string.IsNullOrWhiteSpace(userPart) ? Title : $"{Title} ({userPart})";
        }
    }

    public CredentialProfile Clone()
    {
        return new CredentialProfile
        {
            Id = this.Id,
            Title = this.Title,
            Username = this.Username,
            Domain = this.Domain,
            Password = this.Password
        };
    }
}
