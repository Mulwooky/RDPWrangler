using System.Text;
using RDPWrangler.Models;

namespace RDPWrangler.Services;

public class IniManager
{
    private readonly string _filePath;

    public IniManager(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "servers.ini");
    }

    public string FilePath => _filePath;

    public (AppSettings Settings, List<RdpServerConnection> Servers, List<CredentialProfile> CredentialProfiles) Load()
    {
        if (!File.Exists(_filePath))
        {
            var defaultSettings = new AppSettings();
            var defaultServers = CreateDefaultSampleServers();
            var defaultProfiles = CreateDefaultSampleProfiles();
            Save(defaultSettings, defaultServers, defaultProfiles);
            return (defaultSettings, defaultServers, defaultProfiles);
        }

        var lines = File.ReadAllLines(_filePath, Encoding.UTF8);
        var sections = ParseSections(lines);

        var settings = new AppSettings();
        if (sections.TryGetValue("Settings", out var settingsDict))
        {
            if (settingsDict.TryGetValue("SplitterDistance", out var sd) && int.TryParse(sd, out var sVal))
                settings.SplitterDistance = sVal;
            if (settingsDict.TryGetValue("SidebarCollapsed", out var sc) && bool.TryParse(sc, out var scVal))
                settings.SidebarCollapsed = scVal;
            if (settingsDict.TryGetValue("LastConnectedServerId", out var lcs))
                settings.LastConnectedServerId = lcs;
            if (settingsDict.TryGetValue("WindowWidth", out var ww) && int.TryParse(ww, out var wwVal))
                settings.WindowWidth = wwVal;
            if (settingsDict.TryGetValue("WindowHeight", out var wh) && int.TryParse(wh, out var whVal))
                settings.WindowHeight = whVal;
            if (settingsDict.TryGetValue("WindowMaximized", out var wm) && bool.TryParse(wm, out var wmVal))
                settings.WindowMaximized = wmVal;
        }

        var credentialProfiles = new List<CredentialProfile>();
        var servers = new List<RdpServerConnection>();

        foreach (var (sectionName, values) in sections)
        {
            // Parse Credential Profiles
            if (sectionName.StartsWith("Credential.", StringComparison.OrdinalIgnoreCase) ||
                sectionName.StartsWith("Credential_", StringComparison.OrdinalIgnoreCase))
            {
                var id = sectionName.Substring(11);
                var profile = new CredentialProfile
                {
                    Id = id,
                    Title = values.GetValueOrDefault("Title", string.Empty),
                    Username = values.GetValueOrDefault("Username", string.Empty),
                    Domain = values.GetValueOrDefault("Domain", string.Empty),
                    Password = ResolvePassword(values)
                };
                credentialProfiles.Add(profile);
            }
            // Parse Servers
            else if (sectionName.StartsWith("Server.", StringComparison.OrdinalIgnoreCase) ||
                     sectionName.StartsWith("Server_", StringComparison.OrdinalIgnoreCase))
            {
                var id = sectionName.Substring(7);
                var server = new RdpServerConnection
                {
                    Id = id,
                    DisplayName = values.GetValueOrDefault("DisplayName", string.Empty),
                    Group = values.GetValueOrDefault("Group", "General"),
                    Host = values.GetValueOrDefault("Host", string.Empty),
                    Port = int.TryParse(values.GetValueOrDefault("Port", "3389"), out var p) ? p : 3389,
                    Username = values.GetValueOrDefault("Username", string.Empty),
                    Domain = values.GetValueOrDefault("Domain", string.Empty),
                    Password = ResolvePassword(values),
                    CredentialProfileId = values.TryGetValue("CredentialProfileId", out var cpid) ? cpid : null,
                    SmartSizing = !bool.TryParse(values.GetValueOrDefault("SmartSizing", "true"), out var ss) || ss,
                    Notes = values.GetValueOrDefault("Notes", string.Empty)
                };

                if (string.IsNullOrWhiteSpace(server.Group))
                {
                    server.Group = "General";
                }

                servers.Add(server);
            }
        }

        return (settings, servers, credentialProfiles);
    }

    public void Save(AppSettings settings, IEnumerable<RdpServerConnection> servers, IEnumerable<CredentialProfile>? credentialProfiles = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("; ===================================================================");
        sb.AppendLine("; RDPWrangler Server Connections & Settings INI File");
        sb.AppendLine("; Passwords are securely encrypted via Windows DPAPI (PasswordEncrypted).");
        sb.AppendLine("; ===================================================================");
        sb.AppendLine();

        sb.AppendLine("[Settings]");
        sb.AppendLine($"SplitterDistance={settings.SplitterDistance}");
        sb.AppendLine($"SidebarCollapsed={settings.SidebarCollapsed}");
        sb.AppendLine($"LastConnectedServerId={settings.LastConnectedServerId}");
        sb.AppendLine($"WindowWidth={settings.WindowWidth}");
        sb.AppendLine($"WindowHeight={settings.WindowHeight}");
        sb.AppendLine($"WindowMaximized={settings.WindowMaximized}");
        sb.AppendLine();

        if (credentialProfiles != null)
        {
            foreach (var profile in credentialProfiles)
            {
                sb.AppendLine($"[Credential.{profile.Id}]");
                sb.AppendLine($"Title={profile.Title}");
                sb.AppendLine($"Username={profile.Username}");
                sb.AppendLine($"Domain={profile.Domain}");
                string encPass = CredentialVault.EncryptSecret(profile.Password);
                sb.AppendLine($"PasswordEncrypted={encPass}");
                sb.AppendLine();
            }
        }

        foreach (var server in servers)
        {
            sb.AppendLine($"[Server.{server.Id}]");
            sb.AppendLine($"DisplayName={server.DisplayName}");
            sb.AppendLine($"Group={server.Group}");
            sb.AppendLine($"Host={server.Host}");
            sb.AppendLine($"Port={server.Port}");
            sb.AppendLine($"Username={server.Username}");
            sb.AppendLine($"Domain={server.Domain}");
            if (!string.IsNullOrWhiteSpace(server.CredentialProfileId))
            {
                sb.AppendLine($"CredentialProfileId={server.CredentialProfileId}");
            }
            string encPass = CredentialVault.EncryptSecret(server.Password);
            sb.AppendLine($"PasswordEncrypted={encPass}");
            sb.AppendLine($"SmartSizing={server.SmartSizing}");
            sb.AppendLine($"Notes={server.Notes}");
            sb.AppendLine();
        }

        File.WriteAllText(_filePath, sb.ToString(), Encoding.UTF8);
    }

    private static string ResolvePassword(Dictionary<string, string> values)
    {
        // 1. Prefer DPAPI encrypted password
        if (values.TryGetValue("PasswordEncrypted", out var enc) && !string.IsNullOrWhiteSpace(enc))
        {
            return CredentialVault.DecryptSecret(enc);
        }

        // 2. Legacy fallback to plain password (will be migrated to encrypted upon next save)
        if (values.TryGetValue("Password", out var plain))
        {
            return plain;
        }

        return string.Empty;
    }

    private static Dictionary<string, Dictionary<string, string>> ParseSections(string[] lines)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string currentSection = string.Empty;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line.Substring(1, line.Length - 2).Trim();
                if (!result.ContainsKey(currentSection))
                {
                    result[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                continue;
            }

            if (!string.IsNullOrEmpty(currentSection))
            {
                var eqIdx = line.IndexOf('=');
                if (eqIdx > 0)
                {
                    var key = line.Substring(0, eqIdx).Trim();
                    var val = line.Substring(eqIdx + 1).Trim();
                    result[currentSection][key] = val;
                }
            }
        }

        return result;
    }

    private static List<CredentialProfile> CreateDefaultSampleProfiles()
    {
        return new List<CredentialProfile>
        {
            new CredentialProfile
            {
                Id = "cred_domain_admin",
                Title = "Domain Administrator",
                Username = "Administrator",
                Domain = "CORP",
                Password = ""
            },
            new CredentialProfile
            {
                Id = "cred_local_admin",
                Title = "Local Admin",
                Username = "admin",
                Domain = "",
                Password = ""
            }
        };
    }

    private static List<RdpServerConnection> CreateDefaultSampleServers()
    {
        return new List<RdpServerConnection>
        {
            new RdpServerConnection
            {
                Id = "sample_local",
                DisplayName = "Localhost Test Server",
                Group = "Local Lab",
                Host = "127.0.0.1",
                Port = 3389,
                Username = "Administrator",
                Domain = "",
                Password = "",
                SmartSizing = true,
                Notes = "Sample local RDP server for testing"
            },
            new RdpServerConnection
            {
                Id = "sample_dev",
                DisplayName = "Dev Database Node",
                Group = "Development",
                Host = "192.168.1.150",
                Port = 3389,
                Username = "dbadmin",
                Domain = "CORP",
                Password = "",
                SmartSizing = true,
                Notes = "Development environment database server"
            },
            new RdpServerConnection
            {
                Id = "sample_prod",
                DisplayName = "Production Web 01",
                Group = "Production",
                Host = "10.0.0.21",
                Port = 3389,
                Username = "deploy",
                Domain = "PROD",
                Password = "",
                SmartSizing = true,
                Notes = "Primary production web frontend"
            }
        };
    }
}
