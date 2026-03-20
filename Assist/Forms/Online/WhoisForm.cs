using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Assist.Services;

namespace Assist;

public sealed class WhoisForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);
    private static readonly HttpClient Http = new();

    private readonly TextBox _txtDomain;
    private readonly Button _btnQuery;
    private readonly RichTextBox _rtbResult;

    public WhoisForm()
    {
        Text = "WHOIS / Alan Adı Sorgula";
        ClientSize = new Size(850, 580);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.Black };

        _txtDomain = new TextBox
        {
            Location = new Point(6, 8),
            Width = 660,
            Height = 28,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = GreenText,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 11),
            PlaceholderText = "Alan adı girin (ör: google.com)"
        };

        _btnQuery = new Button
        {
            Text = "Sorgula",
            Location = new Point(674, 7),
            Width = 160,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };
        _btnQuery.FlatAppearance.BorderColor = GreenText;

        topPanel.Controls.AddRange([_txtDomain, _btnQuery]);

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

        _btnQuery.Click += async (_, _) => await QueryAsync();
        _txtDomain.KeyDown += async (_, e) =>
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
        var domain = _txtDomain.Text.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(domain)) return;

        // Strip protocol/path if user pastes a URL
        if (domain.Contains("://"))
            domain = new Uri(domain).Host;
        domain = domain.TrimEnd('/');

        await Loading.RunAsync(this, async () =>
        {
            try
            {
                var url = $"https://rdap.org/domain/{Uri.EscapeDataString(domain)}";
                var json = await Http.GetFromJsonAsync<JsonElement>(url);

                var sb = new StringBuilder();
                sb.AppendLine("══════════════════════════════════════════════");
                sb.AppendLine($"  🔍  WHOIS / RDAP — {domain}");
                sb.AppendLine("══════════════════════════════════════════════");
                sb.AppendLine();

                // Domain name
                if (json.TryGetProperty("ldhName", out var ldh))
                    sb.AppendLine($"  Alan Adı       : {ldh.GetString()}");

                // Handle / objectClassName
                if (json.TryGetProperty("handle", out var handle))
                    sb.AppendLine($"  Handle         : {handle.GetString()}");

                // Status
                if (json.TryGetProperty("status", out var statusArr))
                {
                    var statuses = new List<string>();
                    foreach (var s in statusArr.EnumerateArray())
                        statuses.Add(s.GetString() ?? "");
                    sb.AppendLine($"  Durum          : {string.Join(", ", statuses)}");
                }

                sb.AppendLine();

                // Events (registration, expiration, last update)
                if (json.TryGetProperty("events", out var events))
                {
                    sb.AppendLine("  ── Tarihler ──");
                    foreach (var ev in events.EnumerateArray())
                    {
                        var action = ev.GetProperty("eventAction").GetString();
                        var date = ev.GetProperty("eventDate").GetString();
                        var label = action switch
                        {
                            "registration" => "Kayıt Tarihi   ",
                            "expiration" => "Son Kullanma   ",
                            "last changed" => "Son Güncelleme ",
                            "last update of RDAP database" => "RDAP Güncelleme",
                            _ => action?.PadRight(15) ?? "Bilinmiyor     "
                        };
                        sb.AppendLine($"  {label}: {date}");
                    }
                    sb.AppendLine();
                }

                // Nameservers
                if (json.TryGetProperty("nameservers", out var nameservers))
                {
                    sb.AppendLine("  ── DNS Sunucuları ──");
                    foreach (var ns in nameservers.EnumerateArray())
                    {
                        if (ns.TryGetProperty("ldhName", out var nsName))
                            sb.AppendLine($"    • {nsName.GetString()}");
                    }
                    sb.AppendLine();
                }

                // Entities (registrar, registrant)
                if (json.TryGetProperty("entities", out var entities))
                {
                    sb.AppendLine("  ── Kayıt Bilgileri ──");
                    foreach (var entity in entities.EnumerateArray())
                    {
                        var roles = new List<string>();
                        if (entity.TryGetProperty("roles", out var rolesArr))
                            foreach (var r in rolesArr.EnumerateArray())
                                roles.Add(r.GetString() ?? "");

                        var roleStr = string.Join(", ", roles);

                        if (entity.TryGetProperty("vcardArray", out var vcard) &&
                            vcard.GetArrayLength() > 1)
                        {
                            var fields = vcard[1];
                            foreach (var field in fields.EnumerateArray())
                            {
                                if (field.GetArrayLength() >= 4 && field[0].GetString() == "fn")
                                    sb.AppendLine($"  [{roleStr}] {field[3].GetString()}");
                            }
                        }
                        else if (entity.TryGetProperty("handle", out var eHandle))
                        {
                            sb.AppendLine($"  [{roleStr}] {eHandle.GetString()}");
                        }
                    }
                    sb.AppendLine();
                }

                // Links
                if (json.TryGetProperty("links", out var links))
                {
                    sb.AppendLine("  ── Bağlantılar ──");
                    foreach (var link in links.EnumerateArray())
                    {
                        if (link.TryGetProperty("href", out var href))
                            sb.AppendLine($"    {href.GetString()}");
                    }
                }

                _rtbResult.Text = sb.ToString();
            }
            catch (HttpRequestException ex)
            {
                _rtbResult.Text = $"RDAP sorgu hatası: {ex.Message}\n\nDomain bulunamadı veya RDAP servisi erişilemez.";
            }
        }, "WHOIS sorgulanıyor...");
    }
}
