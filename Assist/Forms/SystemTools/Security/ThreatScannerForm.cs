namespace Assist.Forms.SystemTools.Security;

using System.Diagnostics;
using System.Text.RegularExpressions;
using Assist.Services;

internal sealed partial class ThreatScannerForm : Form
{

    private static readonly HashSet<int> SuspiciousPorts =
        [4444, 5555, 6666, 6667, 12345, 31337, 1337, 9999, 3127, 27374, 20000, 65535];

    private static readonly string[] SuspiciousDirs =
        ["\\temp\\", "\\tmp\\", "\\downloads\\", "\\public\\", "\\appdata\\local\\temp\\"];

    private readonly FlowLayoutPanel _resultsPanel;
    private readonly Label _lblSummary;
    private readonly CheckBox _chkShowAll;

    public ThreatScannerForm()
    {
        Text = "Tehdit Tarayıcı — Güvenlik Analizi";
        ClientSize = new Size(960, 680);
        BackColor = Color.Black;
        ForeColor = AppConstants.AccentText;
        Font = new Font("Consolas", 10);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.Black };

        var btnScan = new Button
        {
            Text = "🔍 Tara",
            Location = new Point(8, 10),
            Width = 140,
            Height = 32,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };
        btnScan.FlatAppearance.BorderColor = AppConstants.AccentText;
        btnScan.Click += async (_, _) => await ScanAsync();

        _chkShowAll = new CheckBox
        {
            Text = "Tümünü göster",
            Location = new Point(164, 16),
            AutoSize = true,
            ForeColor = AppConstants.AccentText,
            Checked = false
        };

        _lblSummary = new Label
        {
            Text = "",
            Location = new Point(340, 16),
            AutoSize = true,
            ForeColor = Color.Cyan
        };

        topPanel.Controls.AddRange([btnScan, _chkShowAll, _lblSummary]);

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

        Shown += async (_, _) => await ScanAsync();
    }

    [GeneratedRegex(@"\s+TCP\s+\S+:(\d+)\s+(\S+)\s+(LISTENING|ESTABLISHED)\s+(\d+)")]
    private static partial Regex NetstatRegex();

    private async Task ScanAsync()
    {
        await Loading.RunAsync(this, async () =>
        {
            // 1. Parse netstat for network connections per PID
            var netConnections = await GetNetworkConnectionsAsync();

            // 2. Analyze all processes
            var results = new List<ThreatResult>();
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    var result = AnalyzeProcess(proc, netConnections);
                    if (_chkShowAll.Checked || result.Score > 0)
                        results.Add(result);
                }
                catch
                {
                    // Access denied for system processes — skip
                }
                finally
                {
                    proc.Dispose();
                }
            }

            results.Sort((a, b) => b.Score.CompareTo(a.Score));

            // 3. Render
            _resultsPanel.Controls.Clear();
            var cardWidth = _resultsPanel.ClientSize.Width - 30;

            var highCount = results.Count(r => r.Score >= 60);
            var medCount = results.Count(r => r.Score is >= 30 and < 60);
            var lowCount = results.Count(r => r.Score is > 0 and < 30);
            _lblSummary.Text = $"🔴 Yüksek: {highCount}  🟠 Orta: {medCount}  🟡 Düşük: {lowCount}  |  Toplam: {results.Count}";

            foreach (var r in results)
                _resultsPanel.Controls.Add(CreateCard(cardWidth, r));

        }, "Sistem taranıyor...");
    }

    private static ThreatResult AnalyzeProcess(Process proc, Dictionary<int, List<NetConnection>> netMap)
    {
        var result = new ThreatResult { ProcessName = proc.ProcessName, Pid = proc.Id };
        var reasons = new List<string>();
        int score = 0;

        // Path analysis
        try
        {
            var path = proc.MainModule?.FileName;
            if (path is not null)
            {
                result.Path = path;
                var lower = path.ToLowerInvariant();

                if (SuspiciousDirs.Any(d => lower.Contains(d)))
                {
                    score += 25;
                    reasons.Add("⚠ Şüpheli dizinden çalışıyor");
                }

                // svchost outside System32
                if (proc.ProcessName.Equals("svchost", StringComparison.OrdinalIgnoreCase) &&
                    !lower.Contains("\\system32\\"))
                {
                    score += 40;
                    reasons.Add("🚨 svchost System32 dışından çalışıyor!");
                }
            }
        }
        catch
        {
            result.Path = "(erişim engellendi)";
        }

        // File description / company
        try
        {
            var info = proc.MainModule?.FileVersionInfo;
            if (info is not null)
            {
                result.Company = info.CompanyName ?? "";
                result.Description = info.FileDescription ?? "";

                if (string.IsNullOrWhiteSpace(info.FileDescription))
                {
                    score += 10;
                    reasons.Add("⚠ Dosya açıklaması yok");
                }
                if (string.IsNullOrWhiteSpace(info.CompanyName))
                {
                    score += 10;
                    reasons.Add("⚠ Yayıncı bilgisi yok");
                }
            }
        }
        catch { /* access denied */ }

        // Network connections
        if (netMap.TryGetValue(proc.Id, out var connections))
        {
            result.Connections = connections;

            var listeners = connections.Where(c => c.State == "LISTENING").ToList();
            var established = connections.Where(c => c.State == "ESTABLISHED").ToList();

            if (listeners.Count > 0)
            {
                score += 10;
                reasons.Add($"📡 {listeners.Count} portta dinliyor");

                // Check for suspicious ports
                var suspPorts = listeners.Where(c => SuspiciousPorts.Contains(c.LocalPort)).ToList();
                if (suspPorts.Count > 0)
                {
                    score += 30;
                    reasons.Add($"🚨 Şüpheli port: {string.Join(", ", suspPorts.Select(p => p.LocalPort))}");
                }
            }

            if (established.Count > 10)
            {
                score += 15;
                reasons.Add($"⚠ Çok sayıda bağlantı ({established.Count})");
            }
            else if (established.Count > 0)
            {
                score += 5;
                reasons.Add($"🌐 {established.Count} aktif bağlantı");
            }
        }

        result.Score = Math.Min(score, 100);
        result.Reasons = reasons;
        return result;
    }

    private static async Task<Dictionary<int, List<NetConnection>>> GetNetworkConnectionsAsync()
    {
        using var proc = new Process();
        proc.StartInfo = new ProcessStartInfo("netstat", "-ano")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        proc.Start();
        var output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();

        var map = new Dictionary<int, List<NetConnection>>();
        foreach (var line in output.Split('\n'))
        {
            var match = NetstatRegex().Match(line);
            if (!match.Success) continue;

            var localPort = int.Parse(match.Groups[1].Value);
            var remote = match.Groups[2].Value;
            var state = match.Groups[3].Value;
            var pid = int.Parse(match.Groups[4].Value);

            if (!map.ContainsKey(pid))
                map[pid] = [];

            map[pid].Add(new NetConnection(localPort, remote, state));
        }

        return map;
    }

    private static Panel CreateCard(int width, ThreatResult r)
    {
        var (bgColor, badgeColor, levelText) = r.Score switch
        {
            >= 60 => (Color.FromArgb(40, 10, 10), Color.Red, "YÜKSEK"),
            >= 30 => (Color.FromArgb(40, 30, 5), Color.Orange, "ORTA"),
            > 0 => (Color.FromArgb(30, 30, 10), Color.Yellow, "DÜŞÜK"),
            _ => (Color.FromArgb(15, 20, 15), Color.FromArgb(0, 160, 0), "TEMİZ")
        };

        var reasonCount = r.Reasons.Count;
        var cardHeight = 56 + reasonCount * 18;

        var card = new Panel
        {
            Width = width,
            Height = Math.Max(cardHeight, 62),
            BackColor = bgColor,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(3)
        };

        // Score badge
        var lblScore = new Label
        {
            Text = $" {r.Score} ",
            Font = new Font("Consolas", 14, FontStyle.Bold),
            ForeColor = Color.Black,
            BackColor = badgeColor,
            AutoSize = false,
            Width = 52,
            Height = 34,
            Location = new Point(8, 8),
            TextAlign = ContentAlignment.MiddleCenter
        };

        // Process name + PID
        var lblName = new Label
        {
            Text = $"{r.ProcessName}  (PID: {r.Pid})   [{levelText}]",
            ForeColor = badgeColor,
            Font = new Font("Consolas", 10, FontStyle.Bold),
            AutoSize = false,
            Width = width - 200,
            Height = 20,
            Location = new Point(70, 8)
        };

        // Path
        var lblPath = new Label
        {
            Text = TruncatePath(r.Path, 90),
            ForeColor = Color.Gray,
            Font = new Font("Consolas", 8),
            AutoSize = false,
            Width = width - 200,
            Height = 16,
            Location = new Point(70, 28)
        };

        // Kill button
        var btnKill = new Button
        {
            Text = "Sonlandır",
            Width = 100,
            Height = 26,
            Location = new Point(width - 122, 8),
            BackColor = Color.FromArgb(60, 0, 0),
            ForeColor = Color.Red,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 8, FontStyle.Bold),
            Tag = r.Pid
        };
        btnKill.FlatAppearance.BorderColor = Color.Red;
        btnKill.Click += (s, _) =>
        {
            if (s is not Button b || b.Tag is not int pid) return;
            var confirm = MessageBox.Show(
                $"PID {pid} sonlandırılsın mı?",
                "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;
            try
            {
                using var killProc = new Process();
                killProc.StartInfo = new ProcessStartInfo("taskkill", $"/F /PID {pid}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                killProc.Start();
                killProc.WaitForExit(5000);

                if (killProc.ExitCode == 0)
                {
                    b.Text = "Kapatıldı";
                    b.Enabled = false;
                    b.ForeColor = Color.Gray;
                }
                else
                {
                    MessageBox.Show("İşlem sonlandırılamadı. Sistem korumalı bir işlem olabilir.",
                        "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        card.Controls.AddRange([lblScore, lblName, lblPath, btnKill]);

        // Reasons
        int ry = 48;
        foreach (var reason in r.Reasons)
        {
            card.Controls.Add(new Label
            {
                Text = reason,
                ForeColor = Color.LightGray,
                Font = new Font("Consolas", 8),
                AutoSize = false,
                Width = width - 100,
                Height = 16,
                Location = new Point(70, ry)
            });
            ry += 18;
        }

        return card;
    }

    private static string TruncatePath(string path, int max)
    {
        if (string.IsNullOrEmpty(path) || path.Length <= max) return path;
        return "..." + path[^(max - 3)..];
    }

    private sealed record NetConnection(int LocalPort, string Remote, string State);

    private sealed class ThreatResult
    {
        public string ProcessName { get; set; } = "";
        public int Pid { get; set; }
        public string Path { get; set; } = "";
        public string Company { get; set; } = "";
        public string Description { get; set; } = "";
        public int Score { get; set; }
        public List<string> Reasons { get; set; } = [];
        public List<NetConnection> Connections { get; set; } = [];
    }
}
