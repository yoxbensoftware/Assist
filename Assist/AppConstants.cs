namespace Assist;

/// <summary>
/// Application-wide constants and configuration values.
/// </summary>
internal static class AppConstants
{
    // Data grid column names
    public const string ColumnTitle = "Title";
    public const string ColumnUsername = "Username";
    public const string ColumnPassword = "Password";
    public const string ColumnNotes = "Notes";
    public const string ColumnEye = "Eye";
    public const string ColumnEdit = "Edit";
    public const string ColumnDelete = "Delete";

    // File paths
    public static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AssistPasswordStore");

    public static readonly string PasswordsFilePath = Path.Combine(AppDataPath, "passwords.dat");
    public static readonly string LoginFilePath = Path.Combine(AppDataPath, "login.dat");

    // UI Icons (emoji-based)
    public const string IconEye = "👁";
    public const string IconEdit = "✏️";
    public const string IconDelete = "🗑️";

    // Build version
    public const string BuildVersion = "v1.40";

    // Window titles
    public const string AppTitle = "🔒 Assist";
    public const string LoginTitle = "🔒 Assist";
    public const string AddPasswordTitle = "➕ Şifre Ekle";
    public const string EditPasswordTitle = "✏️ Şifre Düzenle";
    public const string ViewPasswordsTitle = "📄 Şifreleri Gör";

    // Shared theme color — used by all forms before theme is applied
    public static readonly Color AccentText = Color.FromArgb(0, 255, 0);

    // Shared HttpClient — reuse a single instance across the entire app
    public static HttpClient SharedHttpClient { get; } = CreateSharedHttpClient();

    /// <summary>
    /// Creates and configures the shared <see cref="HttpClient"/> instance with default timeout and user-agent.
    /// </summary>
    private static HttpClient CreateSharedHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Assist/1.0");
        return client;
    }
}
