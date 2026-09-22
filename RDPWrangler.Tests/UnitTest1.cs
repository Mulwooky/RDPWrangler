using RDPWrangler.Models;
using RDPWrangler.Services;
using Xunit;

namespace RDPWrangler.Tests;

public class CredentialVaultTests
{
    [Fact]
    public void EncryptAndDecrypt_RoundTrip_ReturnsOriginalSecret()
    {
        string original = "P@ssw0rd_Super_Secret!123";
        string encrypted = CredentialVault.EncryptSecret(original);

        Assert.NotEmpty(encrypted);
        Assert.NotEqual(original, encrypted);

        string decrypted = CredentialVault.DecryptSecret(encrypted);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void DecryptSecret_WithInvalidString_ReturnsEmpty()
    {
        string decrypted = CredentialVault.DecryptSecret("not-a-valid-base64-or-dpapi-token");
        Assert.Empty(decrypted);
    }
}

public class IniManagerTests : IDisposable
{
    private readonly string _tempFile;

    public IniManagerTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"rdp_test_{Guid.NewGuid():N}.ini");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_CreatesDefaultsAndFile()
    {
        var manager = new IniManager(_tempFile);
        var (settings, servers, profiles) = manager.Load();

        Assert.True(File.Exists(_tempFile));
        Assert.NotNull(settings);
        Assert.NotEmpty(servers);
        Assert.NotEmpty(profiles);
        Assert.Contains(servers, s => s.Group == "Production");
        Assert.Contains(servers, s => s.Group == "Development");
        Assert.Contains(profiles, p => p.Title == "Domain Administrator");
    }

    [Fact]
    public void SaveAndLoad_PreservesAllServerFieldsAndEncryptsPasswords()
    {
        var manager = new IniManager(_tempFile);

        var settings = new AppSettings
        {
            SplitterDistance = 320,
            SidebarCollapsed = true,
            LastConnectedServerId = "srv-123",
            WindowWidth = 1400,
            WindowHeight = 900,
            WindowMaximized = true
        };

        var profiles = new List<CredentialProfile>
        {
            new CredentialProfile
            {
                Id = "prof-1",
                Title = "Enterprise Admin",
                Username = "corpadmin",
                Domain = "CORP",
                Password = "SuperAdminPassword#99"
            }
        };

        var servers = new List<RdpServerConnection>
        {
            new RdpServerConnection
            {
                Id = "srv-123",
                DisplayName = "Core Database",
                Group = "Database Cluster",
                Host = "10.0.1.5",
                Port = 3390,
                Username = "dbadmin",
                Domain = "CORP",
                Password = "SecretPassword123!",
                CredentialProfileId = "prof-1",
                SmartSizing = false,
                Notes = "Primary active node"
            },
            new RdpServerConnection
            {
                Id = "srv-456",
                DisplayName = "Jumpbox",
                Group = "Infrastructure",
                Host = "bastion.example.com",
                Port = 3389,
                Username = "ops",
                Domain = "",
                Password = "",
                SmartSizing = true,
                Notes = "SSH & RDP Bastion"
            }
        };

        manager.Save(settings, servers, profiles);

        // Verify the raw INI content does NOT leak plain passwords
        string rawContent = File.ReadAllText(_tempFile);
        Assert.DoesNotContain("SecretPassword123!", rawContent);
        Assert.DoesNotContain("SuperAdminPassword#99", rawContent);
        Assert.Contains("PasswordEncrypted=", rawContent);

        // Verify Load decrypts the passwords properly
        var (loadedSettings, loadedServers, loadedProfiles) = manager.Load();

        Assert.Equal(320, loadedSettings.SplitterDistance);
        Assert.True(loadedSettings.SidebarCollapsed);
        Assert.Equal("srv-123", loadedSettings.LastConnectedServerId);

        var s1 = loadedServers.First(s => s.Id == "srv-123");
        Assert.Equal("Core Database", s1.DisplayName);
        Assert.Equal("Database Cluster", s1.Group);
        Assert.Equal("10.0.1.5", s1.Host);
        Assert.Equal(3390, s1.Port);
        Assert.Equal("dbadmin", s1.Username);
        Assert.Equal("CORP", s1.Domain);
        Assert.Equal("SecretPassword123!", s1.Password);
        Assert.Equal("prof-1", s1.CredentialProfileId);
        Assert.False(s1.SmartSizing);

        var p1 = loadedProfiles.First(p => p.Id == "prof-1");
        Assert.Equal("Enterprise Admin", p1.Title);
        Assert.Equal("SuperAdminPassword#99", p1.Password);
    }

    [Fact]
    public void Load_LegacyPlainTextPassword_ReadsAndMigratesToEncrypted()
    {
        string legacyIni = @"
[Settings]
SplitterDistance=250

[Server.legacy1]
DisplayName=Legacy Server
Group=General
Host=192.168.1.50
Port=3389
Username=admin
Password=LegacyPlainTextPassword!
SmartSizing=true
";
        File.WriteAllText(_tempFile, legacyIni);

        var manager = new IniManager(_tempFile);
        var (settings, servers, _) = manager.Load();

        var s = servers.First(x => x.Id == "legacy1");
        Assert.Equal("LegacyPlainTextPassword!", s.Password);

        // Save again and verify it is now encrypted
        manager.Save(settings, servers);
        string newContent = File.ReadAllText(_tempFile);
        Assert.DoesNotContain("LegacyPlainTextPassword!", newContent);
        Assert.Contains("PasswordEncrypted=", newContent);
    }

    [Fact]
    public void DeleteServer_RemovesFromCollectionAndFile()
    {
        var manager = new IniManager(_tempFile);
        var (settings, servers, profiles) = manager.Load();

        var serverToDelete = servers.First();
        servers.RemoveAll(s => s.Id == serverToDelete.Id);
        manager.Save(settings, servers, profiles);

        var (_, reloaded, _) = manager.Load();
        Assert.DoesNotContain(reloaded, s => s.Id == serverToDelete.Id);
    }
}