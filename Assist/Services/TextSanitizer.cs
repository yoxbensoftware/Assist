namespace Assist.Services;

using System.Text;

internal static class TextSanitizer
{
    private static readonly char[] TurkishChars = new[] { 'ğ','Ğ','ü','Ü','ş','Ş','ı','İ','ö','Ö','ç','Ç' };

    public static string Normalize(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? string.Empty;

        // Quick check: if string already contains Turkish chars, assume ok
        if (s.IndexOfAny(TurkishChars) >= 0) return s;

        // If it doesn't contain mojibake indicators, return as-is
        if (!ContainsMojibakeIndicators(s)) return s;

        // Try common encodings: CP1252 (Windows-1252), CP1254 (Turkish), ISO-8859-1
        var encCandidates = new[] { 1254, 1252, 28591 };
        foreach (var cp in encCandidates)
        {
            try
            {
                var src = Encoding.GetEncoding(cp);
                var bytes = src.GetBytes(s);
                var fixedStr = Encoding.UTF8.GetString(bytes);
                if (IsLikelyTurkish(fixedStr)) return fixedStr;
            }
            catch { }
        }

        // Fallback: CP1252 -> UTF8
        try
        {
            var bytes = Encoding.GetEncoding(1252).GetBytes(s);
            var fixedStr = Encoding.UTF8.GetString(bytes);
            return fixedStr;
        }
        catch { return s; }
    }

    private static bool ContainsMojibakeIndicators(string s)
    {
        return s.Contains("Ã") || s.Contains("Ô") || s.Contains("Â") || s.Contains("├") || s.Contains("┼") || s.Contains("─") || s.Contains("�");
    }

    private static bool IsLikelyTurkish(string s)
    {
        // If contains any Turkish letters or no 'Ã' artifacts
        if (s.IndexOfAny(TurkishChars) >= 0) return true;
        if (!ContainsMojibakeIndicators(s) && s.Length > 0) return true;
        return false;
    }
}
