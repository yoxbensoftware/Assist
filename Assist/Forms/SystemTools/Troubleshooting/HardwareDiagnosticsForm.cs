namespace Assist.Forms.SystemTools.Troubleshooting;

using System.Diagnostics;
using System.Net;

/// <summary>
/// Detects common hardware/system issues and provides per-issue fix buttons.
/// </summary>
internal sealed class HardwareDiagnosticsForm : Form
{
    private static readonly Color WarningColor = Color.FromArgb(255, 165, 0);
    private static readonly Color ErrorColor = Color.FromArgb(255, 60, 60);
    private static readonly Color OkColor = Color.FromArgb(0, 200, 0);
    private static readonly Color InfoColor = Color.Cyan;

    private readonly Panel _resultsPanel;
    private readonly Label _lblStatus;
    private readonly Button _btnScan;

    public HardwareDiagnosticsForm()
    {
        Text = "Donanım Sorun Giderici";
        ClientSize = new Size(950, 700);
        BackColor = Color.Black;
        ForeColor = AppConstants.AccentText;
        Font = new Font("Consolas", 10);

        var lblTitle = new Label
        {
            Text = "=== DONANIM SORUN GİDERİCİ ===",
            Location = new Point(20, 12),
            AutoSize = true,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 14, FontStyle.Bold)
        };

        _btnScan = new Button
        {
            Text = "Taramayı Başlat",
            Location = new Point(20, 48),
            Size = new Size(220, 35),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 11, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnScan.FlatAppearance.BorderColor = AppConstants.AccentText;
        _btnScan.Click += async (_, _) => await RunDiagnosticsAsync();

        _resultsPanel = new Panel
        {
            Location = new Point(20, 95),
            Size = new Size(910, 560),
            AutoScroll = true,
            BackColor = Color.Black,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        _lblStatus = new Label
        {
            Text = "Taramayı başlatmak için butona tıklayın.",
            Location = new Point(20, 665),
            Size = new Size(910, 25),
            ForeColor = InfoColor,
            Font = new Font("Consolas", 9),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        Controls.AddRange([lblTitle, _btnScan, _resultsPanel, _lblStatus]);
    }

    private async Task RunDiagnosticsAsync()
    {
        _btnScan.Enabled = false;
        _btnScan.Text = "Taranıyor...";
        _resultsPanel.Controls.Clear();
        _lblStatus.Text = "Sistem taranıyor, lütfen bekleyin...";
        _lblStatus.ForeColor = InfoColor;

        var results = await Task.Run(CollectDiagnostics);

        int yPos = 0;
        foreach (var result in results)
        {
            var card = CreateIssueCard(result, yPos);
            _resultsPanel.Controls.Add(card);
            yPos += card.Height + 4;
        }

        var problemCount = results.Count(r => r.Severity is IssueSeverity.Warning or IssueSeverity.Error);
        _lblStatus.Text = problemCount > 0
            ? $"Tarama tamamlandı — {problemCount} sorun tespit edildi, {results.Count - problemCount} test başarılı."
            : "Tarama tamamlandı — tüm testler başarılı!";
        _lblStatus.ForeColor = problemCount > 0 ? WarningColor : OkColor;
        _btnScan.Text = "Tekrar Tara";
        _btnScan.Enabled = true;
    }

    private static List<DiagnosticResult> CollectDiagnostics()
    {
        return
        [
            CheckDiskSpace(),
            CheckTempFiles(),
            CheckRamUsage(),
            CheckDnsResolution(),
            CheckDiskHealth(),
            CheckNetworkConnectivity(),
            CheckPowerPlan(),
            CheckStartupPrograms(),
            CheckSystemFileIntegrity(),
            CheckEventLog()
        ];
    }

    private Panel CreateIssueCard(DiagnosticResult result, int yPos)
    {
        var severityColor = result.Severity switch
        {
            IssueSeverity.Ok => OkColor,
            IssueSeverity.Warning => WarningColor,
            IssueSeverity.Error => ErrorColor,
            _ => InfoColor
        };

        var icon = result.Severity switch
        {
            IssueSeverity.Ok => "[OK]",
            IssueSeverity.Warning => "[!!]",
            IssueSeverity.Error => "[XX]",
            _ => "[ii]"
        };

        bool hasFix = result.FixAction is not null && result.Severity is not IssueSeverity.Ok;
        int descWidth = hasFix ? 630 : 830;

        var card = new Panel
        {
            Location = new Point(0, yPos),
            Size = new Size(888, 56),
            BackColor = Color.FromArgb(15, 15, 15),
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblTitle = new Label
        {
            Text = $"{icon}  {result.Title}",
            Location = new Point(10, 5),
            Size = new Size(600, 20),
            ForeColor = severityColor,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        var lblDesc = new Label
        {
            Text = result.Description,
            Location = new Point(10, 28),
            Size = new Size(descWidth, 20),
            ForeColor = Color.FromArgb(170, 170, 170),
            Font = new Font("Consolas", 8)
        };

        card.Controls.AddRange([lblTitle, lblDesc]);

        if (hasFix)
        {
            var btnFix = new Button
            {
                Text = result.FixLabel ?? "Düzelt",
                Location = new Point(700, 10),
                Size = new Size(175, 34),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = severityColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Consolas", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnFix.FlatAppearance.BorderColor = severityColor;
            btnFix.Click += async (_, _) =>
            {
                btnFix.Enabled = false;
                btnFix.Text = "İşleniyor...";
                try
                {
                    await Task.Run(() => result.FixAction!());
                    btnFix.Text = "Tamamlandı";
                    btnFix.ForeColor = OkColor;
                    btnFix.FlatAppearance.BorderColor = OkColor;
                }
                catch (Exception ex)
                {
                    btnFix.Text = "Başarısız";
                    btnFix.ForeColor = ErrorColor;
                    btnFix.FlatAppearance.BorderColor = ErrorColor;
                    MessageBox.Show($"Düzeltme hatası: {ex.Message}", "Hata",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnFix.Enabled = true;
                }
            };
            card.Controls.Add(btnFix);
        }

        return card;
    }

    #region Diagnostic Checks

    private static DiagnosticResult CheckDiskSpace()
    {
        try
        {
            var issues = new List<string>();
            var allDrives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .ToList();

            foreach (var drive in allDrives)
            {
                var freePercent = (double)drive.TotalFreeSpace / drive.TotalSize * 100;
                var freeGb = drive.TotalFreeSpace / (1024.0 * 1024 * 1024);
                if (freePercent < 10)
                    issues.Add($"{drive.Name} {freeGb:F1} GB boş ({freePercent:F0}%)");
            }

            if (issues.Count > 0)
            {
                return new("Disk Alanı Düşük",
                    string.Join(", ", issues),
                    IssueSeverity.Warning,
                    "Disk Temizliği",
                    () => Process.Start(new ProcessStartInfo("cleanmgr") { UseShellExecute = true }));
            }

            var summary = string.Join(", ", allDrives.Select(d =>
                $"{d.Name} {d.TotalFreeSpace / (1024.0 * 1024 * 1024):F1} GB boş"));
            return new("Disk Alanı", summary, IssueSeverity.Ok, null, null);
        }
        catch (Exception ex)
        {
            return new("Disk Alanı", $"Kontrol edilemedi: {ex.Message}", IssueSeverity.Info, null, null);
        }
    }

    private static DiagnosticResult CheckTempFiles()
    {
        try
        {
            var tempPath = Path.GetTempPath();
            long totalSize = 0;
            int fileCount = 0;

            foreach (var file in Directory.EnumerateFiles(tempPath, "*",
                new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
            {
                try { totalSize += new FileInfo(file).Length; fileCount++; }
                catch { /* skip inaccessible */ }
            }

            var sizeMb = totalSize / (1024.0 * 1024);
            if (sizeMb > 500)
            {
                return new("Geçici Dosyalar Fazla",
                    $"{fileCount:N0} dosya, {sizeMb:F0} MB — temizlik önerilir",
                    IssueSeverity.Warning,
                    "Temp Temizle",
                    () => CleanTempFiles(tempPath));
            }

            return new("Geçici Dosyalar",
                $"{fileCount:N0} dosya, {sizeMb:F0} MB — normal",
                IssueSeverity.Ok, null, null);
        }
        catch (Exception ex)
        {
            return new("Geçici Dosyalar", $"Kontrol edilemedi: {ex.Message}", IssueSeverity.Info, null, null);
        }
    }

    private static void CleanTempFiles(string tempPath)
    {
        foreach (var file in Directory.EnumerateFiles(tempPath, "*", SearchOption.TopDirectoryOnly))
        {
            try { File.Delete(file); } catch { }
        }
        foreach (var dir in Directory.EnumerateDirectories(tempPath, "*", SearchOption.TopDirectoryOnly))
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static DiagnosticResult CheckRamUsage()
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");

            foreach (var obj in searcher.Get())
            {
                var freeKb = Convert.ToInt64(obj["FreePhysicalMemory"]);
                var totalKb = Convert.ToInt64(obj["TotalVisibleMemorySize"]);
                var usedPercent = (int)((1.0 - (double)freeKb / totalKb) * 100);
                var usedGb = (totalKb - freeKb) / (1024.0 * 1024);
                var totalGb = totalKb / (1024.0 * 1024);

                if (usedPercent > 90)
                {
                    return new("RAM Kullanımı Kritik",
                        $"{usedGb:F1}/{totalGb:F1} GB ({usedPercent}%)",
                        IssueSeverity.Error,
                        "Görev Yöneticisi",
                        () => Process.Start(new ProcessStartInfo("taskmgr") { UseShellExecute = true }));
                }

                if (usedPercent > 80)
                {
                    return new("RAM Kullanımı Yüksek",
                        $"{usedGb:F1}/{totalGb:F1} GB ({usedPercent}%)",
                        IssueSeverity.Warning,
                        "Görev Yöneticisi",
                        () => Process.Start(new ProcessStartInfo("taskmgr") { UseShellExecute = true }));
                }

                return new("RAM Durumu",
                    $"{usedGb:F1}/{totalGb:F1} GB ({usedPercent}%) — normal",
                    IssueSeverity.Ok, null, null);
            }
        }
        catch (Exception ex)
        {
            return new("RAM Durumu", $"Kontrol edilemedi: {ex.Message}", IssueSeverity.Info, null, null);
        }

        return new("RAM Durumu", "Bilgi alınamadı", IssueSeverity.Info, null, null);
    }

    private static DiagnosticResult CheckDnsResolution()
    {
        try
        {
            var sw = Stopwatch.StartNew();
            Dns.GetHostEntry("google.com");
            sw.Stop();

            if (sw.ElapsedMilliseconds > 2000)
            {
                return new("DNS Yavaş",
                    $"Çözümleme {sw.ElapsedMilliseconds} ms sürdü",
                    IssueSeverity.Warning,
                    "DNS Temizle",
                    RunFlushDns);
            }

            return new("DNS Çözümleme",
                $"Çalışıyor — {sw.ElapsedMilliseconds} ms",
                IssueSeverity.Ok, null, null);
        }
        catch
        {
            return new("DNS Çözümleme Hatası",
                "DNS çözümleme başarısız — internet veya DNS sorunu",
                IssueSeverity.Error,
                "DNS Sıfırla",
                RunFlushDns);
        }
    }

    private static void RunFlushDns()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "ipconfig",
            Arguments = "/flushdns",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process?.WaitForExit(10_000);
    }

    private static DiagnosticResult CheckDiskHealth()
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT Model, Status FROM Win32_DiskDrive");

            var issues = new List<string>();
            var ok = new List<string>();

            foreach (var obj in searcher.Get())
            {
                var model = obj["Model"]?.ToString() ?? "Bilinmeyen";
                var status = obj["Status"]?.ToString() ?? "Unknown";

                if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
                    issues.Add($"{model}: {status}");
                else
                    ok.Add(model);
            }

            if (issues.Count > 0)
            {
                return new("Disk Sağlığı Sorunu",
                    string.Join("; ", issues),
                    IssueSeverity.Error, null, null);
            }

            var desc = ok.Count > 0
                ? $"{ok.Count} disk sağlıklı — {string.Join(", ", ok.Select(n => n.Length > 30 ? string.Concat(n.AsSpan(0, 27), "...") : n))}"
                : "Disk bilgisi yok";
            return new("Disk Sağlığı (SMART)", desc, IssueSeverity.Ok, null, null);
        }
        catch (Exception ex)
        {
            return new("Disk Sağlığı", $"SMART okunamadı: {ex.Message}", IssueSeverity.Info, null, null);
        }
    }

    private static DiagnosticResult CheckNetworkConnectivity()
    {
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = ping.Send("8.8.8.8", 5000);

            if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
            {
                if (reply.RoundtripTime > 200)
                {
                    return new("Ağ Gecikmesi Yüksek",
                        $"Ping: {reply.RoundtripTime} ms",
                        IssueSeverity.Warning,
                        "Ağı Sıfırla",
                        RunNetworkReset);
                }

                return new("Ağ Bağlantısı",
                    $"Çevrimiçi — Ping: {reply.RoundtripTime} ms",
                    IssueSeverity.Ok, null, null);
            }

            return new("Ağ Bağlantısı Yok",
                "İnternete erişilemiyor",
                IssueSeverity.Error,
                "Ağı Sıfırla",
                RunNetworkReset);
        }
        catch
        {
            return new("Ağ Bağlantısı Hatası",
                "Bağlantı kontrolü başarısız",
                IssueSeverity.Error,
                "Ağı Sıfırla",
                RunNetworkReset);
        }
    }

    private static void RunNetworkReset()
    {
        foreach (var (file, args) in new[] { ("netsh", "winsock reset"), ("ipconfig", "/flushdns"), ("ipconfig", "/renew") })
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                p?.WaitForExit(15_000);
            }
            catch { }
        }
    }

    private static DiagnosticResult CheckPowerPlan()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = "/getactivescheme",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            var output = process?.StandardOutput.ReadToEnd() ?? "";
            process?.WaitForExit(5000);

            var planName = "Bilinmeyen";
            var parenStart = output.LastIndexOf('(');
            var parenEnd = output.LastIndexOf(')');
            if (parenStart >= 0 && parenEnd > parenStart)
                planName = output[(parenStart + 1)..parenEnd];

            if (planName.Contains("tasarruf", StringComparison.OrdinalIgnoreCase) ||
                planName.Contains("saver", StringComparison.OrdinalIgnoreCase))
            {
                return new("Güç Planı: Tasarruf Modu",
                    $"'{planName}' — performansı düşürebilir",
                    IssueSeverity.Warning,
                    "Dengeli Yap",
                    () =>
                    {
                        using var p = Process.Start(new ProcessStartInfo
                        {
                            FileName = "powercfg",
                            Arguments = "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        p?.WaitForExit(5000);
                    });
            }

            return new("Güç Planı", $"Aktif: {planName}", IssueSeverity.Ok, null, null);
        }
        catch (Exception ex)
        {
            return new("Güç Planı", $"Kontrol edilemedi: {ex.Message}", IssueSeverity.Info, null, null);
        }
    }

    private static DiagnosticResult CheckStartupPrograms()
    {
        try
        {
            var count = 0;

            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
                count += key?.GetValueNames().Length ?? 0;

            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
                count += key?.GetValueNames().Length ?? 0;

            if (count > 15)
            {
                return new("Çok Fazla Başlangıç Programı",
                    $"{count} program — yavaşlamaya neden olabilir",
                    IssueSeverity.Warning,
                    "Başlangıç Yönet",
                    () => Process.Start(new ProcessStartInfo("taskmgr", "/7") { UseShellExecute = true }));
            }

            return new("Başlangıç Programları",
                $"{count} program — normal",
                IssueSeverity.Ok, null, null);
        }
        catch (Exception ex)
        {
            return new("Başlangıç Programları", $"Kontrol edilemedi: {ex.Message}", IssueSeverity.Info, null, null);
        }
    }

    private static DiagnosticResult CheckSystemFileIntegrity()
    {
        return new("Sistem Dosya Bütünlüğü",
            "SFC ile bozuk sistem dosyaları taranabilir (yönetici gerekir)",
            IssueSeverity.Info,
            "SFC Tara",
            () => Process.Start(new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = "/c sfc /scannow & pause",
                UseShellExecute = true,
                Verb = "runas"
            }));
    }

    private static DiagnosticResult CheckEventLog()
    {
        return new("Sistem Olay Günlüğü",
            "Son hataları incelemek için Olay Görüntüleyici açılabilir",
            IssueSeverity.Info,
            "Olayları Aç",
            () => Process.Start(new ProcessStartInfo("eventvwr.msc") { UseShellExecute = true }));
    }

    #endregion

    private enum IssueSeverity { Ok, Warning, Error, Info }

    private sealed record DiagnosticResult(
        string Title,
        string Description,
        IssueSeverity Severity,
        string? FixLabel,
        Action? FixAction);
}
