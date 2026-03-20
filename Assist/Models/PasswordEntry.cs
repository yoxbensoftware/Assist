using System.Security.Cryptography;
using System.Text;

namespace Assist.Models;

/// <summary>
/// Represents a password entry with encrypted password storage.
/// </summary>
internal sealed class PasswordEntry
{
    public string Title { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public string GetDecryptedPassword()
    {
        if (string.IsNullOrEmpty(EncryptedPassword))
            return string.Empty;

        try
        {
            var encryptedBytes = Convert.FromBase64String(EncryptedPassword);
            var decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                null,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
    }

    public void SetPassword(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password ?? string.Empty);
        var encryptedBytes = ProtectedData.Protect(
            bytes,
            null,
            DataProtectionScope.CurrentUser);
        EncryptedPassword = Convert.ToBase64String(encryptedBytes);
    }
}
