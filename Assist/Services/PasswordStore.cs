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
    private static readonly object SyncRoot = new();
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

        if (TryLoadEntries(AppConstants.PasswordsFilePath, out var entries) ||
            TryLoadEntries(AppConstants.PasswordsBackupFilePath, out entries))
        {
            _entries = entries;
            if (!File.Exists(AppConstants.PasswordsFilePath) && File.Exists(AppConstants.PasswordsBackupFilePath))
                SaveToFile();
        }
    }

    /// <summary>
    /// Encrypts and saves all password entries to disk using DPAPI.
    /// </summary>
    public static void SaveToFile()
    {
        SaveProtectedBlob(AppConstants.PasswordsFilePath, AppConstants.PasswordsBackupFilePath, SerializeEntries());
    }

    /// <summary>
    /// Saves login credentials (username and password) encrypted to disk.
    /// </summary>
    public static void SaveLogin(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            throw new ArgumentException("Username ve password boş olamaz.");

        var login = $"{username}:{password}";
        var bytes = Encoding.UTF8.GetBytes(login);
        var encrypted = ProtectedData.Protect(
            bytes,
            null,
            DataProtectionScope.CurrentUser);
        SaveProtectedBlob(AppConstants.LoginFilePath, AppConstants.LoginBackupFilePath, encrypted);
    }

    /// <summary>
    /// Loads and decrypts saved login credentials from disk.
    /// Returns <c>null</c> if no credentials are stored or decryption fails.
    /// </summary>
    public static (string username, string password)? LoadLogin()
    {
        if (TryLoadProtectedBytes(AppConstants.LoginFilePath, out var bytes) ||
            TryLoadProtectedBytes(AppConstants.LoginBackupFilePath, out bytes))
        {
            var decrypted = ProtectedData.Unprotect(
                bytes,
                null,
                DataProtectionScope.CurrentUser);
            var login = Encoding.UTF8.GetString(decrypted);
            var separatorIndex = login.IndexOf(':');

            return separatorIndex > 0
                ? (login[..separatorIndex], login[(separatorIndex + 1)..])
                : null;
        }

        return null;
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

    private static byte[] SerializeEntries()
    {
        var json = JsonSerializer.Serialize(_entries);
        return Encoding.UTF8.GetBytes(json);
    }

    private static bool TryLoadEntries(string filePath, out List<PasswordEntry> entries)
    {
        entries = [];

        if (!TryLoadProtectedBytes(filePath, out var encrypted))
            return false;

        try
        {
            var decrypted = ProtectedData.Unprotect(
                encrypted,
                null,
                DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(decrypted);
            entries = JsonSerializer.Deserialize<List<PasswordEntry>>(json) ?? [];
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or IOException)
        {
            return false;
        }
    }

    private static bool TryLoadProtectedBytes(string filePath, out byte[] bytes)
    {
        bytes = [];
        if (!File.Exists(filePath)) return false;

        try
        {
            bytes = File.ReadAllBytes(filePath);
            return bytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void SaveProtectedBlob(string primaryPath, string backupPath, byte[] data)
    {
        lock (SyncRoot)
        {
            EnsureAppDataDirectory();

            var tempPath = primaryPath + ".tmp";
            try
            {
                File.WriteAllBytes(tempPath, data);

                if (File.Exists(primaryPath))
                {
                    File.Replace(tempPath, primaryPath, backupPath, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tempPath, primaryPath, overwrite: true);
                    File.Copy(primaryPath, backupPath, overwrite: true);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
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
