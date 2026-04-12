namespace Assist.Services;

using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Assist.Models;

/// <summary>
/// Fetches news from Google News RSS feeds.
/// </summary>
internal sealed class NewsService
{
    private const string GoogleNewsTrUrl = "https://news.google.com/rss?hl=tr&gl=TR&ceid=TR:tr";
    private const string GoogleNewsGlobalUrl = "https://news.google.com/rss?hl=en-US&gl=US&ceid=US:en";
    private const string GoogleNewsTechUrl = "https://news.google.com/rss/search?q=technology&hl=en-US&gl=US&ceid=US:en";

    private static readonly HttpClient SharedHttpClient = new();

    /// <summary>
    /// Fetches the top Turkish news headlines from Google News RSS.
    /// </summary>
    public Task<List<NewsItem>> GetTopTrAsync(int max = 30)
        => GetFromRssAsync(GoogleNewsTrUrl, max);

    /// <summary>
    /// Fetches the top global news headlines from Google News RSS.
    /// </summary>
    public Task<List<NewsItem>> GetTopGlobalAsync(int max = 30)
        => GetFromRssAsync(GoogleNewsGlobalUrl, max);

    /// <summary>
    /// Fetches the top technology news headlines from Google News RSS.
    /// </summary>
    public Task<List<NewsItem>> GetTopTechAsync(int max = 30)
        => GetFromRssAsync(GoogleNewsTechUrl, max);

    /// <summary>
    /// Parses an RSS feed URL and returns a list of news items up to the specified maximum.
    /// </summary>
    private static async Task<List<NewsItem>> GetFromRssAsync(string rssUrl, int max)
    {
        try
        {
            var response = await SharedHttpClient.GetStringAsync(rssUrl).ConfigureAwait(false);
            var doc = XDocument.Parse(response);
            var items = new List<NewsItem>();

            foreach (var element in doc.Descendants("item"))
            {
                if (items.Count >= max) break;

                var pubDateStr = (string?)element.Element("pubDate");
                DateTime? publishDate = DateTime.TryParse(pubDateStr, out var dt) ? dt : null;

                items.Add(new NewsItem
                {
                    Title = (string?)element.Element("title") ?? string.Empty,
                    Link = (string?)element.Element("link"),
                    Summary = StripHtml((string?)element.Element("description")),
                    PublishDate = publishDate,
                    Source = (string?)element.Element("source")
                });
            }

            return items;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Strips HTML tags from the input string and returns plain text.
    /// </summary>
    private static string? StripHtml(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        try
        {
            var doc = XDocument.Parse($"<root>{input}</root>");
            return doc.Root?.Nodes() is not null
                ? string.Concat(doc.Root.Nodes())
                : input;
        }
        catch
        {
            return input;
        }
    }

    /// <summary>
    /// Fetches article content from a URL and returns a brief text summary extracted from paragraphs.
    /// </summary>
    public static async Task<string> FetchArticleSummaryAsync(string url)
    {
        try
        {
            var client = AppConstants.SharedHttpClient;
            var html = await client.GetStringAsync(url).ConfigureAwait(false);

            var paragraphs = Regex.Matches(
                html, @"<p[^>]*>(.*?)</p>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            var sb = new StringBuilder();
            foreach (Match match in paragraphs)
            {
                var text = WebUtility.HtmlDecode(
                    StripHtmlTags(match.Groups[1].Value)).Trim();

                if (text.Length > 30)
                {
                    sb.AppendLine(text);
                    sb.AppendLine();
                    if (sb.Length > 800) break;
                }
            }

            var result = sb.ToString().Trim();
            return string.IsNullOrEmpty(result) ? "Makale i\u00e7eri\u011fi \u00e7\u0131kar\u0131lamad\u0131." : result;
        }
        catch (Exception ex)
        {
            return $"Makale al\u0131n\u0131rken hata olu\u015ftu: {ex.Message}";
        }
    }

    /// <summary>
    /// Strips all HTML tags from a string using regex.
    /// </summary>
    private static string StripHtmlTags(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return Regex.Replace(input, @"<[^>]+>", string.Empty).Trim();
    }
}
