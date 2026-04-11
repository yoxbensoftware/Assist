namespace Assist.Forms.Online.Queries;

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Assist.Services;

internal sealed class IpDomainQueryForm : Form
{
    private static readonly HttpClient Http = new();

    private readonly TextBox _txtQuery;
    private readonly Button _btnQuery;
    private readonly RichTextBox _rtbResult;

    public IpDomainQueryForm()
    {
        Text = "IP / Domain Sorgula";
        ClientSize = new Size(850, 560);
        BackColor = Color.Black;
        ForeColor = AppConstants.AccentText;
        Font = new Font("Consolas", 10);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.Black };

        _txtQuery = new TextBox
        {
            Location = new Point(6, 8),
            Width = 660,
            Height = 28,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = AppConstants.AccentText,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 11),
            PlaceholderText = "IP adresi veya domain girin (ör: 8.8.8.8 veya google.com)",
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _btnQuery = new Button
        {
            Text = "Sorgula",
            Location = new Point(674, 7),
            Width = 160,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 10, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _btnQuery.FlatAppearance.BorderColor = AppConstants.AccentText;

        topPanel.Controls.AddRange([_txtQuery, _btnQuery]);

        _rtbResult = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(15, 15, 15),
            ForeColor = AppConstants.AccentText,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 10)
        };

        Controls.Add(_rtbResult);
        Controls.Add(topPanel);

        _btnQuery.Click += async (_, _) => await QueryAsync();
        _txtQuery.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await QueryAsync();
            }
        };
    }

    private async Task QueryAsync()
    {
        var query = _txtQuery.Text.Trim();
        if (string.IsNullOrEmpty(query)) return;

        await Loading.RunAsync(this, async () =>
        {
            try
            {
                var url = $"http://ip-api.com/json/{Uri.EscapeDataString(query)}?fields=status,message,query,country,countryCode,region,regionName,city,zip,lat,lon,timezone,isp,org,as,reverse";
                var json = await Http.GetFromJsonAsync<JsonElement>(url);

                var status = json.GetProperty("status").GetString();
                if (status != "success")
                {
                    var msg = json.TryGetProperty("message", out var m) ? m.GetString() : "Bilinmeyen hata";
                    _rtbResult.Text = $"Sorgu başarısız: {msg}";
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine("══════════════════════════════════════════");
                sb.AppendLine($"  🌐  IP / Domain Sorgu Sonucu");
                sb.AppendLine("══════════════════════════════════════════");
                sb.AppendLine();

                AppendField(sb, "Sorgu", json, "query");
                AppendField(sb, "Ülke", json, "country");
                AppendField(sb, "Ülke Kodu", json, "countryCode");
                AppendField(sb, "Bölge", json, "regionName");
                AppendField(sb, "Bölge Kodu", json, "region");
                AppendField(sb, "Şehir", json, "city");
                AppendField(sb, "Posta Kodu", json, "zip");
                sb.AppendLine();
                sb.AppendLine("  ── Konum ──");
                AppendField(sb, "Enlem", json, "lat");
                AppendField(sb, "Boylam", json, "lon");
                AppendField(sb, "Zaman Dilimi", json, "timezone");
                sb.AppendLine();
                sb.AppendLine("  ── Ağ Bilgisi ──");
                AppendField(sb, "ISP", json, "isp");
                AppendField(sb, "Organizasyon", json, "org");
                AppendField(sb, "AS", json, "as");
                AppendField(sb, "Reverse DNS", json, "reverse");

                _rtbResult.Text = sb.ToString();
            }
            catch (HttpRequestException ex)
            {
                _rtbResult.Text = $"Sorgu hatası: {ex.Message}";
            }
        }, "IP/Domain sorgulanıyor...");
    }

    private static void AppendField(StringBuilder sb, string label, JsonElement json, string prop)
    {
        if (json.TryGetProperty(prop, out var val))
        {
            var text = val.ValueKind == JsonValueKind.Number
                ? val.GetDouble().ToString("F4")
                : val.GetString() ?? "-";
            sb.AppendLine($"  {label,-16}: {text}");
        }
    }
}
