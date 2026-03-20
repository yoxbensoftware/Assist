namespace Assist;

/// <summary>
/// Application-wide constants and configuration values.
/// </summary>
public static class AppConstants
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

    // Default credentials
    public const string DefaultUsername = "admin";

    // Build version
    public const string BuildVersion = "v.0015";

    // Window titles
    public const string AppTitle = "🔒 Assist";
    public const string LoginTitle = "🔒 Assist";
    public const string AddPasswordTitle = "➕ Şifre Ekle";
    public const string EditPasswordTitle = "✏️ Şifre Düzenle";
    public const string ViewPasswordsTitle = "📄 Şifreleri Gör";
}
