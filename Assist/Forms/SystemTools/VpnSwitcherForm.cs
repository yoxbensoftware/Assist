namespace Assist.Forms.SystemTools;

using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.Json;
using Assist.Services;

/// <summary>
/// Lists all Windows VPN profiles and ProtonVPN desktop app state with one-click
/// connect / disconnect per profile and auto-refresh every 5 seconds.
/// Chrome browser VPN extensions run in a sandbox and cannot be controlled here.
/// </summary>
internal sealed class VpnSwitcherForm : Form
{
    private readonly FlowLayoutPanel _flow;
    private readonly Label _lblIpStatus;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private bool _refreshing;

    private static readonly Color ColGreen  = Color.LimeGreen;
    private static readonly Color ColRed    = Color.OrangeRed;
    private static readonly Color ColAccent = AppConstants.AccentText;
    private static readonly Color ColSurf   = Color.FromArgb(8, 16, 8);

    /// <summary>Initializes the VPN Switcher form and starts auto-refresh.</summary>
    public VpnSwitcherForm()
    {
        Text       = "VPN Yöneticisi";
        ClientSize = new Size(900, 660);
        BackColor  = Color.Black;
        ForeColor  = ColAccent;
        Font       = new Font("Consolas", 10);

        // ── Header ─────────────────────────────────────────────────────────────
        var header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = ColSurf };

        var lblTitle = new Label
        {
            Text      = "VPN YÖNETİCİSİ",
            Location  = new Point(12, 10),
            AutoSize  = true,
            ForeColor = ColAccent,
            Font      = new Font("Consolas", 14, FontStyle.Bold)
        };

        var btnRefresh = new Button
        {
            Text      = "↻ Yenile",
            Location  = new Point(775, 11),
            Size      = new Size(105, 30),
            BackColor = Color.Black,
            ForeColor = ColAccent,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Consolas", 9, FontStyle.Bold),
            Cursor    = Cursors.Hand
        };
        btnRefresh.FlatAppearance.BorderColor = ColAccent;
        btnRefresh.Click += async (_, _) => await RefreshAllAsync();

        _lblIpStatus = new Label
        {
            Text      = "Genel IP: Yükleniyor...",
            Location  = new Point(14, 35),
            AutoSize  = true,
            ForeColor = Color.Gray,
            Font      = new Font("Consolas", 8)
        };

        header.Controls.AddRange([lblTitle, btnRefresh, _lblIpStatus]);

        // ── Chrome extension warning bar ────────────────────────────────────────
        var warnBar = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = Color.FromArgb(22, 15, 0) };
        warnBar.Controls.Add(new Label
        {
            Text      = "  ⚠  Chrome ProtonVPN eklentisi tarayıcı sandbox'ı nedeniyle buradan kontrol edilemez — yalnızca Windows VPN profilleri ve ProtonVPN masaüstü uygulaması yönetilebilir.",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.Goldenrod,
            Font      = new Font("Consolas", 8)
        });

        // ── Scrollable content ──────────────────────────────────────────────────
        _flow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            AutoScroll    = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            BackColor     = Color.Black,
            Padding       = new Padding(12, 10, 20, 10)
        };

        Controls.Add(_flow);
        Controls.Add(header);
        Controls.Add(warnBar);

        // ── Auto-refresh ────────────────────────────────────────────────────────
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 5_000 };
        _refreshTimer.Tick += async (_, _) => await RefreshAllAsync();
        _refreshTimer.Start();
        FormClosed += (_, _) => { _refreshTimer.Stop(); _refreshTimer.Dispose(); };

        Load += async (_, _) => await RefreshAllAsync();
    }

    // ─── Main refresh ──────────────────────────────────────────────────────────

    /// <summary>Fetches all VPN data concurrently and rebuilds the UI sections.</summary>
    private async Task RefreshAllAsync()
    {
        if (_refreshing || IsDisposed) return;
        _refreshing = true;
        try
        {
            var profilesTask = GetWindowsVpnProfilesAsync();
            var activeTask   = GetActiveRasConnectionsAsync();
            var adaptersTask = Task.Run(GetVpnAdapters);
            var ipTask       = GetPublicIpAsync();

            await Task.WhenAll(profilesTask, activeTask, adaptersTask, ipTask).ConfigureAwait(true);

            if (IsDisposed) return;

            var profiles = profilesTask.Result;
            var active   = activeTask.Result;
            var adapters = adaptersTask.Result;
            var publicIp = ipTask.Result;
            var proton   = GetProtonVpnStatus();

            var activeTunnel = adapters.FirstOrDefault(a => a.IsUp);
            _lblIpStatus.Text      = $"Genel IP: {publicIp}" + (activeTunnel is not null ? $"  |  Aktif tünel: {activeTunnel.Name}" : "  |  VPN tüneli yok");
            _lblIpStatus.ForeColor = activeTunnel is not null ? ColGreen : Color.Gray;

            _flow.SuspendLayout();
            _flow.Controls.Clear();
            _flow.Controls.Add(BuildWindowsVpnSection(profiles, active));
            _flow.Controls.Add(BuildProtonSection(proton));
            _flow.Controls.Add(BuildAdaptersSection(adapters));
            _flow.ResumeLayout();
        }
        finally
        {
            _refreshing = false;
        }
    }

    // ─── Section builders ──────────────────────────────────────────────────────

    /// <summary>Builds the Windows VPN profiles section with connect/disconnect cards.</summary>
    private FlowLayoutPanel BuildWindowsVpnSection(List<VpnProfile> profiles, HashSet<string> active)
    {
        var wrap = MakeWrapper();
        wrap.Controls.Add(SectionHeader("── WINDOWS VPN PROFİLLERİ ──────────────────────────────────────────────"));

        if (profiles.Count == 0)
        {
            wrap.Controls.Add(InfoLabel("Windows'ta kayıtlı VPN profili bulunamadı. Sisteme bir VPN bağlantısı ekleyin."));
        }
        else
        {
            foreach (var p in profiles)
                wrap.Controls.Add(BuildProfileCard(p, active.Contains(p.Name)));
        }

        var btnSettings = new Button
        {
            Text      = "+ Windows VPN Ayarlarını Aç",
            AutoSize  = true,
            BackColor = Color.Black,
            ForeColor = Color.DimGray,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Consolas", 8),
            Margin    = new Padding(0, 4, 0, 8),
            Cursor    = Cursors.Hand
        };
        btnSettings.FlatAppearance.BorderColor = Color.FromArgb(40, 40, 40);
        btnSettings.Click += (_, _) => Process.Start(new ProcessStartInfo("ms-settings:network-vpn") { UseShellExecute = true });
        wrap.Controls.Add(btnSettings);

        return wrap;
    }

    /// <summary>Builds the ProtonVPN desktop application status card.</summary>
    private FlowLayoutPanel BuildProtonSection(ProtonStatus status)
    {
        var wrap = MakeWrapper();
        wrap.Controls.Add(SectionHeader("── PROTONVPN MASAÜSTÜ UYGULAMASI ───────────────────────────────────────"));

        var card = new Panel
        {
            BackColor = ColSurf,
            Size      = new Size(856, 72),
            Margin    = new Padding(0, 2, 0, 6)
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(status.TunnelActive ? ColGreen
                                  : status.Running     ? Color.Gold
                                  : Color.FromArgb(50, 50, 50));
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        // Status dot
        card.Controls.Add(new Label
        {
            Text      = status.TunnelActive ? "●" : status.Running ? "◑" : "○",
            Location  = new Point(10, 12),
            AutoSize  = true,
            ForeColor = status.TunnelActive ? ColGreen : status.Running ? Color.Gold : ColRed,
            Font      = new Font("Consolas", 18, FontStyle.Bold)
        });

        var tunnelText = status.TunnelActive  ? $"Tünel AKTİF  ({status.AdapterName})"
                       : status.Running        ? "Uygulama çalışıyor — tünel kapalı"
                       : "ProtonVPN çalışmıyor";

        card.Controls.Add(new Label
        {
            Text      = $"ProtonVPN  |  {tunnelText}",
            Location  = new Point(40, 14),
            Size      = new Size(580, 22),
            ForeColor = ColAccent,
            Font      = new Font("Consolas", 10)
        });
        card.Controls.Add(new Label
        {
            Text      = status.Running ? $"PID: {status.Pid}" : "İşlem: Yok",
            Location  = new Point(40, 38),
            Size      = new Size(400, 18),
            ForeColor = Color.Gray,
            Font      = new Font("Consolas", 8)
        });

        var btn = new Button
        {
            Text      = status.Running ? "✕ Uygulamayı Kapat" : "▶ Uygulamayı Başlat",
            Location  = new Point(648, 18),
            Size      = new Size(192, 34),
            BackColor = Color.Black,
            ForeColor = status.Running ? ColRed : ColGreen,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Consolas", 9, FontStyle.Bold),
            Cursor    = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = status.Running ? ColRed : ColGreen;

        var wasRunning = status.Running;
        btn.Click += async (_, _) =>
        {
            btn.Enabled = false;
            btn.Text    = "Lütfen bekleyin...";
            if (wasRunning) StopProtonVpn();
            else            StartProtonVpn();
            await Task.Delay(2_000);
            await RefreshAllAsync();
        };

        card.Controls.Add(btn);
        wrap.Controls.Add(card);

        // Chrome extension informational note
        wrap.Controls.Add(new Label
        {
            Text      = "  ℹ  Chrome tarayıcı eklentisi yalnızca o tarayıcının trafiğini yönlendirir ve bu araçla kontrol edilemez.",
            AutoSize  = true,
            ForeColor = Color.FromArgb(110, 95, 30),
            Font      = new Font("Consolas", 8),
            Margin    = new Padding(0, 2, 0, 8)
        });

        return wrap;
    }

    /// <summary>Builds the active VPN network adapters list section.</summary>
    private static FlowLayoutPanel BuildAdaptersSection(List<VpnAdapter> adapters)
    {
        var wrap = MakeWrapper();
        wrap.Controls.Add(SectionHeader("── AKTİF VPN AĞ ADAPTÖRLER ──────────────────────────────────────────────"));

        var lv = new ListView
        {
            FullRowSelect = true,
            GridLines     = false,
            HeaderStyle   = ColumnHeaderStyle.Nonclickable,
            View          = View.Details,
            BackColor     = Color.FromArgb(5, 10, 5),
            ForeColor     = ColAccent,
            Font          = new Font("Consolas", 9),
            BorderStyle   = BorderStyle.FixedSingle,
            Size          = new Size(856, adapters.Count > 0 ? Math.Min(adapters.Count * 22 + 28, 160) : 44),
            Margin        = new Padding(0, 2, 0, 8)
        };
        lv.Columns.Add("Ad",        185);
        lv.Columns.Add("Açıklama",  250);
        lv.Columns.Add("Durum",      72);
        lv.Columns.Add("IP Adresi", 160);
        lv.Columns.Add("Protokol",   90);

        if (adapters.Count == 0)
        {
            lv.Items.Add(new ListViewItem(["(Aktif VPN adaptörü bulunamadı)", "", "", "", ""])
                { ForeColor = Color.Gray });
        }
        else
        {
            foreach (var a in adapters)
            {
                var desc = a.Description.Length > 42 ? a.Description[..42] + "…" : a.Description;
                var item = new ListViewItem([a.Name, desc, a.IsUp ? "UP" : "DOWN", a.Ip, a.Protocol]);
                item.ForeColor = a.IsUp ? ColGreen : Color.Gray;
                lv.Items.Add(item);
            }
        }

        wrap.Controls.Add(lv);
        return wrap;
    }

    // ─── Profile card ──────────────────────────────────────────────────────────

    /// <summary>Builds a single VPN profile card with connect / disconnect button.</summary>
    private Panel BuildProfileCard(VpnProfile profile, bool connected)
    {
        var card = new Panel
        {
            BackColor = ColSurf,
            Size      = new Size(856, 62),
            Margin    = new Padding(0, 3, 0, 3)
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(connected ? ColGreen : Color.FromArgb(40, 40, 40));
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        card.Controls.Add(new Label
        {
            Text      = connected ? "●" : "○",
            Location  = new Point(10, 14),
            AutoSize  = true,
            ForeColor = connected ? ColGreen : ColRed,
            Font      = new Font("Consolas", 14, FontStyle.Bold)
        });
        card.Controls.Add(new Label
        {
            Text      = profile.Name,
            Location  = new Point(36, 10),
            Size      = new Size(440, 22),
            ForeColor = ColAccent,
            Font      = new Font("Consolas", 10, FontStyle.Bold)
        });
        card.Controls.Add(new Label
        {
            Text      = string.IsNullOrWhiteSpace(profile.Server) ? "Sunucu: —" : $"Sunucu: {profile.Server}",
            Location  = new Point(36, 34),
            Size      = new Size(440, 18),
            ForeColor = Color.Gray,
            Font      = new Font("Consolas", 8)
        });
        card.Controls.Add(new Label
        {
            Text      = connected ? "● BAĞLI" : "○ BAĞLI DEĞİL",
            Location  = new Point(504, 22),
            AutoSize  = true,
            ForeColor = connected ? ColGreen : ColRed,
            Font      = new Font("Consolas", 9, FontStyle.Bold)
        });

        var btn = new Button
        {
            Text      = connected ? "✕ Bağlantıyı Kes" : "▶ Bağlan",
            Location  = new Point(660, 14),
            Size      = new Size(180, 34),
            BackColor = Color.Black,
            ForeColor = connected ? ColRed : ColGreen,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Consolas", 9, FontStyle.Bold),
            Cursor    = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = connected ? ColRed : ColGreen;

        btn.Click += async (_, _) =>
        {
            btn.Enabled = false;
            btn.Text    = "Bekleniyor...";
            if (connected)
                await DisconnectVpnAsync(profile.Name);
            else
                await ConnectVpnAsync(profile.Name);
            await Task.Delay(2_000);
            await RefreshAllAsync();
        };

        card.Controls.Add(btn);
        return card;
    }

    // ─── VPN data operations ───────────────────────────────────────────────────

    /// <summary>
    /// Reads Windows VPN profiles via PowerShell Get-VpnConnection.
    /// Falls back to parsing rasphone.pbk if PowerShell returns no data.
    /// </summary>
    private static async Task<List<VpnProfile>> GetWindowsVpnProfilesAsync()
    {
        try
        {
            // Try PowerShell first (most reliable)
            var raw = await RunAsync("powershell",
                "-NoProfile -NonInteractive -Command " +
                "\"$v = Get-VpnConnection -ErrorAction SilentlyContinue; " +
                "if ($v) { $v | Select-Object Name,ServerAddress | ConvertTo-Json -Compress } else { '[]' }\"")
                .ConfigureAwait(false);

            var trimmed = raw.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && (trimmed[0] == '[' || trimmed[0] == '{'))
            {
                using var doc  = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;
                var items = root.ValueKind == JsonValueKind.Array
                    ? root.EnumerateArray().ToList()
                    : [root];

                var list = items
                    .Select(e => new VpnProfile(
                        e.GetProperty("Name").GetString() ?? "",
                        e.TryGetProperty("ServerAddress", out var s) ? s.GetString() ?? "" : ""))
                    .Where(p => !string.IsNullOrEmpty(p.Name))
                    .ToList();

                if (list.Count > 0) return list;
            }
        }
        catch { /* fall through to phonebook */ }

        return ParsePhonebook();
    }

    /// <summary>Reads VPN entry names from the Windows phonebook (rasphone.pbk) as fallback.</summary>
    private static List<VpnProfile> ParsePhonebook()
    {
        var paths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Network\Connections\Pbk\rasphone.pbk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"ras\rasphone.pbk")
        };

        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;

            var profiles = File.ReadAllLines(path)
                .Where(l => l.StartsWith('[') && l.EndsWith(']'))
                .Select(l => new VpnProfile(l[1..^1], ""))
                .ToList();

            if (profiles.Count > 0) return profiles;
        }

        return [];
    }

    /// <summary>Returns a set of currently active VPN connection names via rasdial.</summary>
    private static async Task<HashSet<string>> GetActiveRasConnectionsAsync()
    {
        try
        {
            var output = await RunAsync("rasdial", "").ConfigureAwait(false);
            var names = output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0
                         && !l.StartsWith("Connected",       StringComparison.OrdinalIgnoreCase)
                         && !l.StartsWith("Command",         StringComparison.OrdinalIgnoreCase)
                         && !l.StartsWith("No connections",  StringComparison.OrdinalIgnoreCase));

            return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        }
        catch { return []; }
    }

    /// <summary>Connects to a Windows VPN profile by name using rasdial.</summary>
    private static async Task ConnectVpnAsync(string name) =>
        await RunAsync("rasdial", $"\"{name}\"").ConfigureAwait(false);

    /// <summary>Disconnects a Windows VPN profile by name using rasdial.</summary>
    private static async Task DisconnectVpnAsync(string name) =>
        await RunAsync("rasdial", $"\"{name}\" /disconnect").ConfigureAwait(false);

    /// <summary>
    /// Scans network interfaces for adapters that resemble VPN tunnels
    /// (WireGuard, TAP, ProtonVPN, OpenVPN, PPTP, L2TP, etc.).
    /// </summary>
    private static List<VpnAdapter> GetVpnAdapters()
    {
        var keywords = new[] { "vpn", "tap", "wireguard", "proton", "openvpn",
                               "tunnel", "nordvpn", "tun", "ivacy", "pptp", "l2tp" };

        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni =>
            {
                var n = ni.Name.ToLowerInvariant();
                var d = ni.Description.ToLowerInvariant();
                return keywords.Any(k => n.Contains(k) || d.Contains(k));
            })
            .Select(ni =>
            {
                var ip = ni.GetIPProperties()
                           .UnicastAddresses
                           .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                           ?.Address.ToString() ?? "—";

                var descL    = ni.Description.ToLowerInvariant();
                var protocol = descL.Contains("wireguard") ? "WireGuard"
                             : descL.Contains("tap")       ? "TAP"
                             : descL.Contains("proton")    ? "ProtonVPN"
                             : descL.Contains("openvpn")   ? "OpenVPN"
                             : descL.Contains("pptp")      ? "PPTP"
                             : descL.Contains("l2tp")      ? "L2TP"
                             : "VPN";

                return new VpnAdapter(ni.Name, ni.Description,
                    ni.OperationalStatus == OperationalStatus.Up, ip, protocol);
            })
            .OrderByDescending(a => a.IsUp)
            .ToList();
    }

    /// <summary>Detects whether ProtonVPN is running and whether its VPN tunnel is active.</summary>
    private static ProtonStatus GetProtonVpnStatus()
    {
        var procs   = Process.GetProcessesByName("ProtonVPN");
        var running = procs.Length > 0;
        var pid     = running ? procs[0].Id : 0;

        var adapters = GetVpnAdapters();
        var protonAdapter = adapters.FirstOrDefault(a =>
            a.Description.Contains("proton", StringComparison.OrdinalIgnoreCase) ||
            (a.Protocol == "WireGuard" && a.IsUp));

        return new ProtonStatus(running, pid, protonAdapter?.IsUp ?? false, protonAdapter?.Name ?? "—");
    }

    /// <summary>Launches the ProtonVPN desktop application from known install paths.</summary>
    private static void StartProtonVpn()
    {
        string[] paths =
        [
            @"C:\Program Files\Proton\VPN\ProtonVPN.exe",
            @"C:\Program Files (x86)\Proton\VPN\ProtonVPN.exe",
            @"C:\Program Files\ProtonVPN\ProtonVPN.exe"
        ];

        foreach (var p in paths.Where(File.Exists))
        {
            Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
            return;
        }
    }

    /// <summary>Kills all running ProtonVPN processes.</summary>
    private static void StopProtonVpn()
    {
        foreach (var p in Process.GetProcessesByName("ProtonVPN"))
        {
            try { p.Kill(); } catch { /* process may have already exited */ }
        }
    }

    /// <summary>Fetches the current public IP address to reflect active routing.</summary>
    private static async Task<string> GetPublicIpAsync()
    {
        try
        {
            var json = await AppConstants.SharedHttpClient
                .GetStringAsync("https://api.ipify.org?format=json")
                .ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("ip").GetString() ?? "—";
        }
        catch { return "—"; }
    }

    /// <summary>Runs an executable and returns its standard output.</summary>
    private static async Task<string> RunAsync(string exe, string args)
    {
        using var proc = new Process();
        proc.StartInfo = new ProcessStartInfo(exe, args)
        {
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8
        };
        proc.Start();
        var output = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        await proc.WaitForExitAsync().ConfigureAwait(false);
        return output;
    }

    // ─── UI factory helpers ────────────────────────────────────────────────────

    /// <summary>Creates a vertically stacking flow wrapper panel for a section.</summary>
    private static FlowLayoutPanel MakeWrapper() => new()
    {
        AutoSize      = true,
        AutoSizeMode  = AutoSizeMode.GrowAndShrink,
        FlowDirection = FlowDirection.TopDown,
        WrapContents  = false,
        BackColor     = Color.Black,
        Margin        = new Padding(0, 4, 0, 10)
    };

    /// <summary>Creates a styled section header label.</summary>
    private static Label SectionHeader(string text) => new()
    {
        Text      = text,
        AutoSize  = true,
        ForeColor = ColAccent,
        Font      = new Font("Consolas", 9, FontStyle.Bold),
        Margin    = new Padding(0, 4, 0, 6)
    };

    /// <summary>Creates a muted informational label.</summary>
    private static Label InfoLabel(string text) => new()
    {
        Text      = $"  {text}",
        AutoSize  = true,
        ForeColor = Color.Gray,
        Font      = new Font("Consolas", 9),
        Margin    = new Padding(0, 4, 0, 4)
    };

    // ─── Data records ──────────────────────────────────────────────────────────

    private sealed record VpnProfile(string Name, string Server);
    private sealed record VpnAdapter(string Name, string Description, bool IsUp, string Ip, string Protocol);
    private sealed record ProtonStatus(bool Running, int Pid, bool TunnelActive, string AdapterName);
}
