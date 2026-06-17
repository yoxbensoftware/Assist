namespace Assist.Services;

using System.Text.Json;

internal sealed class AppSettings
{
    public bool LowPowerMode { get; set; }
    public bool DashboardEnabled { get; set; } = true;
    public bool ClipboardHistoryEnabled { get; set; } = true;
    public bool RestoreLastSession { get; set; } = true;
    public bool QuickLauncherEnabled { get; set; } = true;
    public int NormalClipboardIntervalMs { get; set; } = 1500;
    public int LowPowerClipboardIntervalMs { get; set; } = 5000;
}

internal static class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string SettingsFilePath = Path.Combine(AppConstants.AppDataPath, "settings.json");

    public static event EventHandler? SettingsChanged;

    public static AppSettings Current { get; private set; } = new();

    public static int EffectiveClipboardIntervalMs =>
        Current.LowPowerMode ? Current.LowPowerClipboardIntervalMs : Current.NormalClipboardIntervalMs;

    public static TimeSpan FastDashboardInterval =>
        TimeSpan.FromSeconds(Current.LowPowerMode ? 5 : 2);

    public static TimeSpan MediumDashboardInterval =>
        TimeSpan.FromSeconds(Current.LowPowerMode ? 60 : 30);

    public static TimeSpan SlowDashboardInterval =>
        TimeSpan.FromMinutes(Current.LowPowerMode ? 15 : 5);

    public static void Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return;

            var json = File.ReadAllText(SettingsFilePath);
            Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            Normalize();
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    public static void Update(Action<AppSettings> update, bool persist = true)
    {
        update(Current);
        Normalize();

        if (persist)
            Save();

        SettingsChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(AppConstants.AppDataPath);
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Ignore settings persistence errors.
        }
    }

    private static void Normalize()
    {
        Current.NormalClipboardIntervalMs = Math.Clamp(Current.NormalClipboardIntervalMs, 500, 60_000);
        Current.LowPowerClipboardIntervalMs = Math.Clamp(Current.LowPowerClipboardIntervalMs, 1000, 60_000);
    }
}
