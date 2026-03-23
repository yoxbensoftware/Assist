using System.Xml.Linq;
using Assist.Models;

namespace Assist.Services;

/// <summary>
/// Fetches news from Google News RSS feeds.
/// </summary>
internal sealed class NewsService
{
    private const string GoogleNewsTrUrl = "https://news.google.com/rss?hl=tr&gl=TR&ceid=TR:tr";
    private const string GoogleNewsGlobalUrl = "https://news.google.com/rss?hl=en-US&gl=US&ceid=US:en";
    private const string GoogleNewsTechUrl = "https://news.google.com/rss/search?q=technology&hl=en-US&gl=US&ceid=US:en";

    private static readonly HttpClient SharedHttpClient = new();

    public Task<List<NewsItem>> GetTopTrAsync(int max = 30)
        => GetFromRssAsync(GoogleNewsTrUrl, max);

    public Task<List<NewsItem>> GetTopGlobalAsync(int max = 30)
        => GetFromRssAsync(GoogleNewsGlobalUrl, max);

    public Task<List<NewsItem>> GetTopTechAsync(int max = 30)
        => GetFromRssAsync(GoogleNewsTechUrl, max);

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
}
