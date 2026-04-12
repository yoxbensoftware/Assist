namespace Assist.Services;

using System.Text;
using System.Text.Json;

/// <summary>
/// Translation service using Azure Translator API with MyMemory fallback.
/// Configure via ASSIST_TRANSLATOR_KEY and ASSIST_TRANSLATOR_REGION environment variables.
/// </summary>
internal static class TranslationService
{
    private const string DefaultEndpoint = "https://api.cognitive.microsofttranslator.com";
    private const string MyMemoryEndpoint = "https://api.mymemory.translated.net/get";

    private static readonly HttpClient HttpClient = new();

    private static string? Key => Environment.GetEnvironmentVariable("ASSIST_TRANSLATOR_KEY");
    private static string? Region => Environment.GetEnvironmentVariable("ASSIST_TRANSLATOR_REGION");
    private static string? Endpoint => Environment.GetEnvironmentVariable("ASSIST_TRANSLATOR_ENDPOINT");

    public static bool IsAvailable =>
        !string.IsNullOrEmpty(Key) &&
        (!string.IsNullOrEmpty(Region) || !string.IsNullOrEmpty(Endpoint));

    /// <summary>
    /// Translates text to the target language. Falls back to MyMemory if Azure is unavailable.
    /// </summary>
    public static async Task<string> TranslateAsync(string text, string toLanguage = "tr")
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Try Azure Translator first
        if (!string.IsNullOrEmpty(Key))
        {
            var result = await TryAzureTranslateAsync(text, toLanguage).ConfigureAwait(false);
            if (result is not null) return result;
        }

        // Fallback to MyMemory
        var fallback = await TryMyMemoryTranslateAsync(text, toLanguage).ConfigureAwait(false);
        return fallback ?? text;
    }

    /// <summary>
    /// Attempts to translate text using the Azure Cognitive Translator API.
    /// </summary>
    private static async Task<string?> TryAzureTranslateAsync(string text, string toLanguage)
    {
        try
        {
            var endpoint = string.IsNullOrEmpty(Endpoint) ? DefaultEndpoint : Endpoint.TrimEnd('/');
            var url = $"{endpoint}/translate?api-version=3.0&to={toLanguage}";

            var requestBody = JsonSerializer.Serialize(new[] { new { Text = text } });
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("Ocp-Apim-Subscription-Key", Key);
            if (!string.IsNullOrEmpty(Region))
            {
                request.Headers.Add("Ocp-Apim-Subscription-Region", Region);
            }

            var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var translations = doc.RootElement[0].GetProperty("translations");
            if (translations.GetArrayLength() > 0)
            {
                return translations[0].GetProperty("text").GetString();
            }
        }
        catch
        {
            // Fall through to return null
        }

        return null;
    }

    /// <summary>
    /// Attempts to translate text using the MyMemory free translation API as a fallback.
    /// </summary>
    private static string? MyMemoryEmail => Environment.GetEnvironmentVariable("ASSIST_MYMEMORY_EMAIL");

    private static async Task<string?> TryMyMemoryTranslateAsync(string text, string toLanguage)
    {
        try
        {
            var encodedText = Uri.EscapeDataString(text);
            var email = string.IsNullOrEmpty(MyMemoryEmail) ? "" : $"&de={Uri.EscapeDataString(MyMemoryEmail)}";
            var url = $"{MyMemoryEndpoint}?q={encodedText}&langpair=en|{toLanguage}{email}";

            var response = await HttpClient.GetStringAsync(url).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(response);

            if (doc.RootElement.TryGetProperty("responseData", out var responseData) &&
                responseData.TryGetProperty("translatedText", out var translatedText))
            {
                return translatedText.GetString();
            }
        }
        catch
        {
            // Fall through to return null
        }

        return null;
    }
}
