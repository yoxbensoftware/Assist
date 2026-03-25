namespace Assist.Services;

using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

/// <summary>
/// Checks GitHub Releases for new versions and applies self-updates without reinstallation.
/// </summary>
internal static class AutoUpdateService
{
    private const string GitHubOwner = "yoxbensoftware";
    private const string GitHubRepo = "Assist";
    private const string ReleasesApiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

    private static readonly string UpdateDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Assist", "Updates");

    /// <summary>
    /// Result of an update check operation.
    /// </summary>
    internal sealed record UpdateInfo(string TagName, string DownloadUrl, string Body)
    {
        /// <summary>
        /// Normalized version string without the 'v.' or 'v' prefix.
        /// </summary>
        public string VersionNumber => TagName
            .Replace("v.", "", StringComparison.OrdinalIgnoreCase)
            .Replace("v", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    /// <summary>
    /// Checks GitHub for a newer release compared to the current <see cref="AppConstants.BuildVersion"/>.
    /// Returns the update info if available, or null if already up to date.
    /// </summary>
    public static async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApiUrl);
            request.Headers.Add("Accept", "application/vnd.github.v3+json");

            var response = await AppConstants.SharedHttpClient.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString();
            var body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";

            if (string.IsNullOrEmpty(tagName))
                return null;

            // Compare versions
            var remoteVersion = NormalizeVersion(tagName);
            var localVersion = NormalizeVersion(AppConstants.BuildVersion);

            if (remoteVersion <= localVersion)
                return null;

            // Find the zip asset download URL
            var downloadUrl = FindZipAssetUrl(root);
            if (downloadUrl is null)
                return null;

            return new UpdateInfo(tagName, downloadUrl, body);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Downloads the update zip, extracts it, and launches the updater batch script that replaces files after exit.
    /// </summary>
    public static async Task<bool> DownloadAndApplyAsync(UpdateInfo update, Action<string>? onProgress = null)
    {
        try
        {
            Directory.CreateDirectory(UpdateDir);
            var zipPath = Path.Combine(UpdateDir, "update.zip");
            var extractDir = Path.Combine(UpdateDir, "extract");

            // Clean previous update artifacts
            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, true);
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            // Download
            onProgress?.Invoke("Güncelleme indiriliyor...");
            var response = await AppConstants.SharedHttpClient.GetAsync(update.DownloadUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return false;

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using var fileStream = File.Create(zipPath);
            await stream.CopyToAsync(fileStream).ConfigureAwait(false);
            fileStream.Close();

            // Extract
            onProgress?.Invoke("Dosyalar çıkarılıyor...");
            ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

            // Find the folder that contains the exe (may be nested inside the zip)
            var updateSourceDir = FindExeDirectory(extractDir) ?? extractDir;

            // Create updater batch script
            onProgress?.Invoke("Güncelleme hazırlanıyor...");
            var installDir = Path.GetDirectoryName(Environment.ProcessPath)!;
            var batPath = Path.Combine(UpdateDir, "update.bat");
            var processId = Environment.ProcessId;

            var batContent = $"""
                @echo off
                echo Assist guncelleniyor, lutfen bekleyin...
                timeout /t 2 /nobreak >nul
                
                :waitloop
                tasklist /fi "PID eq {processId}" 2>nul | find "{processId}" >nul
                if not errorlevel 1 (
                    timeout /t 1 /nobreak >nul
                    goto waitloop
                )
                
                xcopy /E /Y /Q "{updateSourceDir}\*" "{installDir}\"
                
                start "" "{installDir}\Assist.exe"
                
                rd /S /Q "{UpdateDir}"
                del "%~f0"
                """;

            await File.WriteAllTextAsync(batPath, batContent).ConfigureAwait(false);

            // Launch the updater and exit
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parses a version tag string (e.g. "v.0017") into an integer for comparison.
    /// </summary>
    private static int NormalizeVersion(string tag)
    {
        var cleaned = tag
            .Replace("v.", "", StringComparison.OrdinalIgnoreCase)
            .Replace("v", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        return int.TryParse(cleaned, out var version) ? version : 0;
    }

    /// <summary>
    /// Finds the first .zip asset download URL from the GitHub release assets array.
    /// </summary>
    private static string? FindZipAssetUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets))
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return asset.GetProperty("browser_download_url").GetString();
            }
        }

        return null;
    }

    /// <summary>
    /// Recursively searches for the directory containing Assist.exe within the extracted update folder.
    /// </summary>
    private static string? FindExeDirectory(string root)
    {
        if (File.Exists(Path.Combine(root, "Assist.exe")))
            return root;

        foreach (var dir in Directory.GetDirectories(root))
        {
            var found = FindExeDirectory(dir);
            if (found is not null)
                return found;
        }

        return null;
    }
}
