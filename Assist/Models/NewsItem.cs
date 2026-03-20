namespace Assist.Models;

/// <summary>
/// Represents a news article from RSS feed.
/// </summary>
internal sealed class NewsItem
{
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Link { get; set; }
    public DateTime? PublishDate { get; set; }
    public string? Source { get; set; }
}
