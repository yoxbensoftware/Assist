using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Assist.Services;

namespace Assist.Forms.Online;

internal sealed class DictionaryForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);
    private static readonly HttpClient Http = new();

    private readonly TextBox _txtWord;
    private readonly Button _btnSearch;
    private readonly RichTextBox _rtbResult;

    public DictionaryForm()
    {
        Text = "Sözlük (EN)";
        ClientSize = new Size(850, 580);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.Black };

        _txtWord = new TextBox
        {
            Location = new Point(6, 8),
            Width = 660,
            Height = 28,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = GreenText,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 11)
        };

        _btnSearch = new Button
        {
            Text = "Ara",
            Location = new Point(674, 7),
            Width = 160,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };
        _btnSearch.FlatAppearance.BorderColor = GreenText;

        topPanel.Controls.AddRange([_txtWord, _btnSearch]);

        _rtbResult = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(15, 15, 15),
            ForeColor = GreenText,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 10)
        };

        Controls.Add(_rtbResult);
        Controls.Add(topPanel);

        _btnSearch.Click += async (_, _) => await SearchAsync();
        _txtWord.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SearchAsync();
            }
        };
    }

    private async Task SearchAsync()
    {
        var word = _txtWord.Text.Trim();
        if (string.IsNullOrEmpty(word)) return;

        await Loading.RunAsync(this, async () =>
        {
            try
            {
                var url = $"https://api.dictionaryapi.dev/api/v2/entries/en/{Uri.EscapeDataString(word)}";
                var json = await Http.GetFromJsonAsync<JsonElement[]>(url);
                if (json is null || json.Length == 0)
                {
                    _rtbResult.Text = "Sonuç bulunamadı.";
                    return;
                }

                var sb = new StringBuilder();
                foreach (var entry in json)
                {
                    var w = entry.GetProperty("word").GetString();
                    sb.AppendLine($"══════════════════════════════════════");
                    sb.AppendLine($"  📖  {w?.ToUpperInvariant()}");
                    sb.AppendLine($"══════════════════════════════════════");

                    if (entry.TryGetProperty("phonetic", out var phonetic))
                        sb.AppendLine($"  Telaffuz: {phonetic.GetString()}");

                    sb.AppendLine();

                    if (entry.TryGetProperty("meanings", out var meanings))
                    {
                        foreach (var meaning in meanings.EnumerateArray())
                        {
                            var pos = meaning.GetProperty("partOfSpeech").GetString();
                            sb.AppendLine($"  ── {pos?.ToUpperInvariant()} ──");

                            if (meaning.TryGetProperty("definitions", out var defs))
                            {
                                int i = 1;
                                foreach (var def in defs.EnumerateArray())
                                {
                                    var definition = def.GetProperty("definition").GetString();
                                    sb.AppendLine($"    {i}. {definition}");

                                    if (def.TryGetProperty("example", out var example))
                                        sb.AppendLine($"       ➤ \"{example.GetString()}\"");

                                    i++;
                                    if (i > 5) break;
                                }
                            }

                            if (meaning.TryGetProperty("synonyms", out var syns) && syns.GetArrayLength() > 0)
                            {
                                var synList = new List<string>();
                                foreach (var s in syns.EnumerateArray())
                                {
                                    synList.Add(s.GetString() ?? "");
                                    if (synList.Count >= 5) break;
                                }
                                sb.AppendLine($"    Eş anlamlı: {string.Join(", ", synList)}");
                            }

                            sb.AppendLine();
                        }
                    }
                }

                _rtbResult.Text = sb.ToString();
            }
            catch (HttpRequestException)
            {
                _rtbResult.Text = $"'{word}' kelimesi bulunamadı veya API'ye erişilemedi.";
            }
        }, "Sözlük aranıyor...");
    }
}
