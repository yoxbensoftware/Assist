namespace Assist.Services;

using System.Text.Json;

internal enum AppTheme
{
    Matrix,
    Amber,
    Ocean,
    Violet,
}

internal sealed record ThemePalette(
    Color Back,
    Color Surface,
    Color Surface2,
    Color Accent,
    Color Text,
    Color Muted,
    Color Grid,
    Color Positive,
    Color Negative,
    Color Selection,
    Color SelectionText,
    Color MenuBack);

internal static class ThemeService
{
    private static readonly string ThemeFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Assist",
        "theme.json");

    public static event EventHandler? ThemeChanged;

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Matrix;

    /// <summary>
    /// Loads the persisted theme selection from the settings file.
    /// </summary>
    public static void Load()
    {
        try
        {
            if (!File.Exists(ThemeFilePath))
                return;

            var text = File.ReadAllText(ThemeFilePath);
            var saved = JsonSerializer.Deserialize<ThemeState>(text);
            if (saved is null)
                return;

            CurrentTheme = Enum.IsDefined(typeof(AppTheme), saved.Theme) ? saved.Theme : AppTheme.Matrix;
        }
        catch
        {
            CurrentTheme = AppTheme.Matrix;
        }
    }

    /// <summary>
    /// Changes the active theme, optionally persisting the selection, and raises the <see cref="ThemeChanged"/> event.
    /// </summary>
    public static void SetTheme(AppTheme theme, bool persist = true)
    {
        if (CurrentTheme == theme)
            return;

        CurrentTheme = theme;

        if (persist)
            Save();

        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Returns the color palette associated with the specified theme.
    /// </summary>
    public static ThemePalette GetPalette(AppTheme theme) => theme switch
    {
        AppTheme.Amber => new ThemePalette(
            Color.FromArgb(14, 10, 0),
            Color.FromArgb(28, 18, 0),
            Color.FromArgb(44, 28, 0),
            Color.FromArgb(255, 170, 0),
            Color.FromArgb(255, 235, 190),
            Color.FromArgb(185, 145, 90),
            Color.FromArgb(70, 44, 0),
            Color.FromArgb(255, 204, 0),
            Color.FromArgb(255, 110, 90),
            Color.FromArgb(255, 204, 0),
            Color.Black,
            Color.FromArgb(16, 12, 0)),
        AppTheme.Ocean => new ThemePalette(
            Color.FromArgb(3, 10, 16),
            Color.FromArgb(10, 24, 38),
            Color.FromArgb(16, 42, 64),
            Color.FromArgb(64, 176, 255),
            Color.FromArgb(226, 244, 255),
            Color.FromArgb(141, 181, 204),
            Color.FromArgb(26, 56, 84),
            Color.FromArgb(80, 220, 160),
            Color.FromArgb(255, 110, 110),
            Color.FromArgb(64, 176, 255),
            Color.White,
            Color.FromArgb(8, 18, 28)),
        AppTheme.Violet => new ThemePalette(
            Color.FromArgb(11, 0, 18),
            Color.FromArgb(24, 10, 40),
            Color.FromArgb(38, 18, 58),
            Color.FromArgb(170, 110, 255),
            Color.FromArgb(245, 238, 255),
            Color.FromArgb(186, 164, 214),
            Color.FromArgb(60, 34, 88),
            Color.FromArgb(106, 255, 170),
            Color.FromArgb(255, 110, 145),
            Color.FromArgb(170, 110, 255),
            Color.White,
            Color.FromArgb(18, 8, 28)),
        _ => new ThemePalette(
            Color.FromArgb(0, 0, 0),
            Color.FromArgb(10, 10, 10),
            Color.FromArgb(20, 20, 20),
            Color.FromArgb(0, 255, 0),
            Color.FromArgb(0, 255, 0),
            Color.FromArgb(140, 140, 140),
            Color.FromArgb(0, 60, 0),
            Color.FromArgb(0, 255, 0),
            Color.FromArgb(255, 86, 86),
            Color.FromArgb(0, 255, 0),
            Color.Black,
            Color.FromArgb(0, 0, 0)),
    };

    /// <summary>
    /// Returns the list of available themes with their display names.
    /// </summary>
    public static IReadOnlyList<(AppTheme Theme, string Name)> GetThemeOptions() =>
    [
        (AppTheme.Matrix, "Matrix Green"),
        (AppTheme.Amber, "Amber Terminal"),
        (AppTheme.Ocean, "Ocean Blue"),
        (AppTheme.Violet, "Violet Neon"),
    ];

    /// <summary>
    /// Persists the current theme selection to the settings file.
    /// </summary>
    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ThemeFilePath)!);
            var json = JsonSerializer.Serialize(new ThemeState(CurrentTheme), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ThemeFilePath, json);
        }
        catch
        {
            // ignore persistence errors
        }
    }

    private sealed record ThemeState(AppTheme Theme);
}
