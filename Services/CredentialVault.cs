using System.Security.Cryptography;
using System.Text;

namespace RDPWrangler.Services;

public static class CredentialVault
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RDPWrangler.Vault.v1");

    /// <summary>
    /// Encrypts plaintext secret using Windows DPAPI (tied to current Windows user).
    /// </summary>
    public static string EncryptSecret(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(cipherBytes);
        }
        catch
        {
            // If DPAPI is unavailable, do not leak plain text
            return string.Empty;
        }
    }

    /// <summary>
    /// Decrypts a Base64 DPAPI token back to plaintext.
    /// Returns empty string if invalid or created on another machine/user account.
    /// </summary>
    public static string DecryptSecret(string? cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
            return string.Empty;

        try
        {
            byte[] cipherBytes = Convert.FromBase64String(cipherText.Trim());
            byte[] plainBytes = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // Return empty if decryption fails (e.g. copied to another Windows profile)
            return string.Empty;
        }
    }
}
