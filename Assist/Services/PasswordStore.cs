namespace Assist.Services;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Assist.Models;

/// <summary>
/// Manages password storage with DPAPI encryption.
/// </summary>
internal static class PasswordStore
{
    private static List<PasswordEntry> _entries = [];

    public static IReadOnlyList<PasswordEntry> Entries => _entries.AsReadOnly();

    /// <summary>
    /// Adds a new password entry and persists the store to disk.
    /// </summary>
    public static void Add(PasswordEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
        SaveToFile();
    }

    /// <summary>
    /// Loads all password entries from the encrypted file on disk.
    /// </summary>
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

    /// <summary>
    /// Encrypts and saves all password entries to disk using DPAPI.
    /// </summary>
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

    /// <summary>
    /// Saves login credentials (username and password) encrypted to disk.
    /// </summary>
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

    /// <summary>
    /// Loads and decrypts saved login credentials from disk.
    /// Returns <c>null</c> if no credentials are stored or decryption fails.
    /// </summary>
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

    /// <summary>
    /// Deletes a password entry by title and persists the change.
    /// </summary>
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

    /// <summary>
    /// Ensures the application data directory exists, creating it if necessary.
    /// </summary>
    private static void EnsureAppDataDirectory()
    {
        if (!Directory.Exists(AppConstants.AppDataPath))
        {
            Directory.CreateDirectory(AppConstants.AppDataPath);
        }
    }
}
