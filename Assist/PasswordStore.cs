using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Assist.Models;

namespace Assist;

/// <summary>
/// Manages password storage with DPAPI encryption.
/// </summary>
public static class PasswordStore
{
    private static List<PasswordEntry> _entries = [];

    public static IReadOnlyList<PasswordEntry> Entries => _entries.AsReadOnly();

    public static void Add(PasswordEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
        SaveToFile();
    }

    public static void LoadFromFile()
    {
        EnsureAppDataDirectory();

        if (!File.Exists(AppConstants.PasswordsFilePath))
        {
            _entries = [];
            return;
        }

        try
        {
            var encrypted = File.ReadAllBytes(AppConstants.PasswordsFilePath);
            var decrypted = ProtectedData.Unprotect(
                encrypted,
                null,
                DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(decrypted);
            _entries = JsonSerializer.Deserialize<List<PasswordEntry>>(json) ?? [];
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or IOException)
        {
            _entries = [];
        }
    }

    public static void SaveToFile()
    {
        EnsureAppDataDirectory();

        var json = JsonSerializer.Serialize(_entries);
        var bytes = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(
            bytes,
            null,
            DataProtectionScope.CurrentUser);
        File.WriteAllBytes(AppConstants.PasswordsFilePath, encrypted);
    }

    public static void SaveLogin(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            throw new ArgumentException("Username ve password boş olamaz.");

        EnsureAppDataDirectory();

        var login = $"{username}:{password}";
        var bytes = Encoding.UTF8.GetBytes(login);
        var encrypted = ProtectedData.Protect(
            bytes,
            null,
            DataProtectionScope.CurrentUser);
        File.WriteAllBytes(AppConstants.LoginFilePath, encrypted);
    }

    public static (string username, string password)? LoadLogin()
    {
        if (!File.Exists(AppConstants.LoginFilePath))
            return null;

        try
        {
            var encrypted = File.ReadAllBytes(AppConstants.LoginFilePath);
            var decrypted = ProtectedData.Unprotect(
                encrypted,
                null,
                DataProtectionScope.CurrentUser);
            var login = Encoding.UTF8.GetString(decrypted);
            var separatorIndex = login.IndexOf(':');

            return separatorIndex > 0
                ? (login[..separatorIndex], login[(separatorIndex + 1)..])
                : null;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException)
        {
            return null;
        }
    }

    public static void DeleteEntry(string? title)
    {
        if (string.IsNullOrEmpty(title)) return;

        var entry = _entries.FirstOrDefault(x => x.Title == title);
        if (entry is not null)
        {
            _entries.Remove(entry);
            SaveToFile();
        }
    }

    private static void EnsureAppDataDirectory()
    {
        if (!Directory.Exists(AppConstants.AppDataPath))
        {
            Directory.CreateDirectory(AppConstants.AppDataPath);
        }
    }
}
