namespace Assist.Services;

using System.Runtime.InteropServices;
using Microsoft.Win32;

/// <summary>
/// Handles Windows installation: registry (Add/Remove Programs), Start Menu and Desktop shortcuts.
/// Uses HKCU so no admin elevation is required.
/// </summary>
internal static class AppInstallerService
{
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Assist";
    private const string AppDisplayName = "Assist";
    private const string Publisher = "Oz";

    private static string ExePath => Environment.ProcessPath!;
    private static string InstallDir => Path.GetDirectoryName(ExePath)!;

    private static string StartMenuShortcut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        "Programs", "Assist.lnk");

    private static string DesktopShortcut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "Assist.lnk");

    /// <summary>
    /// Returns true if the app is registered in Add/Remove Programs.
    /// </summary>
    public static bool IsInstalled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            return key is not null;
        }
    }

    /// <summary>
    /// Registers the app in Windows and creates shortcuts.
    /// </summary>
    public static void Install()
    {
        RegisterInRegistry();
        CreateShortcut(StartMenuShortcut);
        CreateShortcut(DesktopShortcut);
    }

    /// <summary>
    /// Removes registry entry and shortcuts.
    /// </summary>
    public static void Uninstall()
    {
        DeleteFileIfExists(StartMenuShortcut);
        DeleteFileIfExists(DesktopShortcut);
        Registry.CurrentUser.DeleteSubKey(RegistryKeyPath, throwOnMissingSubKey: false);
    }

    private static void RegisterInRegistry()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
        key.SetValue("DisplayName", AppDisplayName);
        key.SetValue("DisplayVersion", AppConstants.BuildVersion);
        key.SetValue("Publisher", Publisher);
        key.SetValue("InstallLocation", InstallDir);
        key.SetValue("DisplayIcon", ExePath);
        key.SetValue("UninstallString", $"\"{ExePath}\" --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));

        var sizeKb = EstimateInstallSizeKb();
        key.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord);
    }

    private static int EstimateInstallSizeKb()
    {
        try
        {
            var dir = new DirectoryInfo(InstallDir);
            var totalBytes = dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            return (int)(totalBytes / 1024);
        }
        catch
        {
            return 0;
        }
    }

    private static void CreateShortcut(string shortcutPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(shortcutPath);
            if (dir is not null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return;

            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = ExePath;
                shortcut.WorkingDirectory = InstallDir;
                shortcut.Description = "Assist — Çok Amaçlı Masaüstü Aracı";
                shortcut.Save();
            }
            finally
            {
                Marshal.ReleaseComObject(shell);
            }
        }
        catch
        {
            // Shortcut creation is best-effort; don't block startup.
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
