using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Assist.Services;

namespace Assist.Forms.Online;

internal sealed partial class WikipediaSearchForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);
    private static readonly HttpClient Http = new();
    private static readonly Dictionary<string, string> LangMap = new()
    {
        ["TR"] = "tr", ["EN"] = "en", ["DE"] = "de", ["FR"] = "fr", ["ES"] = "es"
    };

    private readonly TextBox _txtSearch;
    private readonly ComboBox _cmbLang;
    private readonly Button _btnSearch;
    private readonly FlowLayoutPanel _resultsPanel;

    public WikipediaSearchForm()
    {
        Text = "Wikipedia Arama";
        ClientSize = new Size(900, 620);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.Black };

        _cmbLang = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 60,
            Location = new Point(6, 8),
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat
        };
        _cmbLang.Items.AddRange(["TR", "EN", "DE", "FR", "ES"]);
        _cmbLang.SelectedIndex = 0;

        _txtSearch = new TextBox
        {
            Location = new Point(74, 8),
            Width = 640,
            Height = 28,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = GreenText,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 11),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _btnSearch = new Button
        {
            Text = "Ara",
            Location = new Point(722, 7),
            Width = 160,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 10, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _btnSearch.FlatAppearance.BorderColor = GreenText;

        topPanel.Controls.AddRange([_cmbLang, _txtSearch, _btnSearch]);

        _resultsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.Black,
            WrapContents = false,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(6)
        };

        Controls.Add(_resultsPanel);
        Controls.Add(topPanel);

        _btnSearch.Click += async (_, _) => await SearchAsync();
        _txtSearch.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SearchAsync();
            }
        };
    }

    static WikipediaSearchForm()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Assist/1.0 (educational tool)");
    }

    [GeneratedRegex("<.*?>")]
    private static partial Regex HtmlTagRegex();

    private async Task SearchAsync()
    {
        var query = _txtSearch.Text.Trim();
        if (string.IsNullOrEmpty(query)) return;

        await Loading.RunAsync(this, async () =>
        {
            var lang = LangMap.GetValueOrDefault(_cmbLang.SelectedItem?.ToString() ?? "TR", "tr");
            var url = $"https://{lang}.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(query)}&srlimit=10&format=json&origin=*";

            var json = await Http.GetFromJsonAsync<JsonElement>(url);
            var results = json.GetProperty("query").GetProperty("search");

            _resultsPanel.Controls.Clear();

            if (results.GetArrayLength() == 0)
            {
                _resultsPanel.Controls.Add(new Label
                {
                    Text = "Sonuç bulunamadı.",
                    ForeColor = Color.Yellow,
                    AutoSize = true,
                    Padding = new Padding(10)
                });
                return;
            }

            var cardWidth = _resultsPanel.ClientSize.Width - 30;
            foreach (var item in results.EnumerateArray())
            {
                var title = item.GetProperty("title").GetString() ?? "";
                var snippet = HtmlTagRegex().Replace(item.GetProperty("snippet").GetString() ?? "", "");
                var wikiUrl = $"https://{lang}.wikipedia.org/wiki/{Uri.EscapeDataString(title.Replace(' ', '_'))}";

                _resultsPanel.Controls.Add(CreateResultCard(cardWidth, title, snippet, wikiUrl));
            }
        }, "Wikipedia aranıyor...");
    }

    private static Panel CreateResultCard(int width, string title, string snippet, string url)
    {
        var card = new Panel
        {
            Width = width,
            Height = 100,
            BackColor = Color.FromArgb(20, 20, 20),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(4),
            Padding = new Padding(8)
        };

        var lblTitle = new Label
        {
            Text = $"📖  {title}",
            ForeColor = GreenText,
            Font = new Font("Consolas", 11, FontStyle.Bold),
            AutoSize = false,
            Width = width - 180,
            Height = 22,
            Location = new Point(8, 6)
        };

        var lblSnippet = new Label
        {
            Text = snippet,
            ForeColor = Color.LightGray,
            AutoSize = false,
            Width = width - 30,
            Height = 44,
            Location = new Point(8, 32)
        };

        var btnOpen = new Button
        {
            Text = "Aç",
            Width = 100,
            Height = 26,
            Location = new Point(width - 125, 6),
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold),
            Tag = url
        };
        btnOpen.FlatAppearance.BorderColor = GreenText;
        btnOpen.Click += (s, _) =>
        {
            if (s is Button b && b.Tag is string link)
                Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
        };

        card.Controls.AddRange([lblTitle, lblSnippet, btnOpen]);
        return card;
    }
}
