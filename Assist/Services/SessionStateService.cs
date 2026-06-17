namespace Assist.Services;

using System.Text.Json;

internal static class SessionStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string SessionFilePath = Path.Combine(AppConstants.AppDataPath, "session.json");

    public static IReadOnlyList<string> LoadOpenForms()
    {
        try
        {
            if (!File.Exists(SessionFilePath))
                return [];

            var json = File.ReadAllText(SessionFilePath);
            var state = JsonSerializer.Deserialize<SessionState>(json);
            return state?.OpenForms ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void SaveOpenForms(IEnumerable<string> formKeys)
    {
        try
        {
            Directory.CreateDirectory(AppConstants.AppDataPath);
            var state = new SessionState([.. formKeys.Distinct(StringComparer.Ordinal)]);
            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(SessionFilePath, json);
        }
        catch
        {
            // Ignore session persistence errors.
        }
    }

    private sealed record SessionState(List<string> OpenForms);
}
