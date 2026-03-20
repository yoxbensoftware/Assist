using Assist.Services;

namespace Assist;

/// <summary>
/// Safe disk cleaner — only targets temp and log files.
/// Skips active browser/app directories.
/// </summary>
public sealed class DiskCleanerForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);

    private readonly TextBox _txtLog = null!;
    private readonly Label _lblStatus = null!;
    private readonly CheckedListBox _chkTargets = null!;
    private readonly Button _btnScan = null!;
    private readonly Button _btnClean = null!;

    private long _totalBytesFound;
    private readonly List<string> _filesToDelete = [];

    // Directories to never touch
    private static readonly HashSet<string> ProtectedDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "google", "chrome", "firefox", "mozilla", "edge", "opera", "brave",
        "slack", "discord", "teams", "spotify", "steam",
        "vscode", "visual studio", "jetbrains", "rider"
    };

    public DiskCleanerForm()
    {
        Text = "Disk Temizleyici";
        ClientSize = new Size(800, 600);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        var lblTitle = new Label
        {
            Text = "=== DİSK TEMİZLEYİCİ ===",
            Location = new Point(20, 15),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };

        var lblWarning = new Label
        {
            Text = "⚠ Sadece temp ve log dosyaları hedeflenir. Tarayıcı/uygulama verileri korunur.",
            Location = new Point(20, 45),
            AutoSize = true,
            ForeColor = Color.Yellow,
            Font = new Font("Consolas", 9)
        };

        _chkTargets = new CheckedListBox
        {
            Location = new Point(20, 75),
            Size = new Size(760, 100),
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9),
            BorderStyle = BorderStyle.FixedSingle,
            CheckOnClick = true
        };

        _chkTargets.Items.Add("Windows Temp (%TEMP%)", true);
        _chkTargets.Items.Add("System Temp (C:\\Windows\\Temp)", true);
        _chkTargets.Items.Add("Log Dosyaları (*.log)", true);
        _chkTargets.Items.Add("Geri Dönüşüm Kutusu", false);
        _chkTargets.Items.Add("Thumbnail Cache", true);

        _btnScan = CreateButton("🔍 Tara", new Point(20, 185));
        _btnScan.Click += async (_, _) => await ScanAsync();

        _btnClean = CreateButton("🧹 Temizle", new Point(200, 185));
        _btnClean.Enabled = false;
        _btnClean.Click += async (_, _) => await CleanAsync();

        _txtLog = new TextBox
        {
            Location = new Point(20, 225),
            Size = new Size(760, 330),
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9),
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblStatus = new Label
        {
            Text = "Taramaya hazır.",
            Location = new Point(20, 565),
            Width = 760,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9)
        };

        Controls.AddRange([lblTitle, lblWarning, _chkTargets, _btnScan, _btnClean, _txtLog, _lblStatus]);
    }

    private async Task ScanAsync()
    {
        _btnScan.Enabled = false;
        _btnClean.Enabled = false;
        _filesToDelete.Clear();
        _totalBytesFound = 0;
        _txtLog.Clear();

        AppendLog("Tarama başlıyor...\r\n");

        await Task.Run(() =>
        {
            if (IsTargetChecked(0))
                ScanDirectory(Path.GetTempPath(), "Windows Temp");

            if (IsTargetChecked(1))
                ScanDirectory(@"C:\Windows\Temp", "System Temp");

            if (IsTargetChecked(2))
                ScanLogFiles();

            if (IsTargetChecked(4))
                ScanThumbnailCache();
        });

        AppendLog($"\r\n{'=',-60}");
        AppendLog($"Toplam: {_filesToDelete.Count} dosya, {FormatSize(_totalBytesFound)}");

        _lblStatus.Text = $"Tarama tamamlandı: {_filesToDelete.Count} dosya ({FormatSize(_totalBytesFound)})";
        _btnScan.Enabled = true;
        _btnClean.Enabled = _filesToDelete.Count > 0;
    }

    private async Task CleanAsync()
    {
        var confirm = MessageBox.Show(
            $"{_filesToDelete.Count} dosya ({FormatSize(_totalBytesFound)}) silinecek. Devam?",
            "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        _btnClean.Enabled = false;
        _btnScan.Enabled = false;

        var deleted = 0;
        long freedBytes = 0;

        await Task.Run(() =>
        {
            foreach (var file in _filesToDelete)
            {
                try
                {
                    var info = new FileInfo(file);
                    var size = info.Length;
                    info.Delete();
                    deleted++;
                    freedBytes += size;
                }
                catch
                {
                    // File in use or access denied — skip
                }
            }
        });

        if (IsTargetChecked(3))
        {
            try
            {
                EmptyRecycleBin();
                AppendLog("Geri dönüşüm kutusu boşaltıldı.");
            }
            catch
            {
                AppendLog("Geri dönüşüm kutusu boşaltılamadı.");
            }
        }

        AppendLog($"\r\nTemizlik tamamlandı: {deleted} dosya silindi, {FormatSize(freedBytes)} boşaltıldı.");
        _lblStatus.Text = $"Temizlik tamamlandı: {deleted}/{_filesToDelete.Count} dosya silindi ({FormatSize(freedBytes)})";

        _filesToDelete.Clear();
        _btnScan.Enabled = true;
    }

    private void ScanDirectory(string path, string label)
    {
        if (!Directory.Exists(path)) return;

        AppendLog($"[{label}] Taranıyor: {path}");

        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    if (IsProtectedPath(file)) continue;

                    var info = new FileInfo(file);
                    _filesToDelete.Add(file);
                    _totalBytesFound += info.Length;
                }
                catch
                {
                    // Access denied
                }
            }
        }
        catch
        {
            AppendLog($"  ⚠ Erişim hatası: {path}");
        }

        AppendLog($"  Bulunan: {_filesToDelete.Count} dosya");
    }

    private void ScanLogFiles()
    {
        AppendLog("[Log Dosyaları] Taranıyor...");
        var count = 0;

        var searchPaths = new[]
        {
            Path.GetTempPath(),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        };

        foreach (var basePath in searchPaths)
        {
            if (!Directory.Exists(basePath)) continue;

            try
            {
                foreach (var file in Directory.EnumerateFiles(basePath, "*.log", SearchOption.AllDirectories))
                {
                    try
                    {
                        if (IsProtectedPath(file)) continue;

                        var info = new FileInfo(file);
                        if (info.LastWriteTime < DateTime.Now.AddDays(-7))
                        {
                            _filesToDelete.Add(file);
                            _totalBytesFound += info.Length;
                            count++;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        AppendLog($"  Bulunan: {count} log dosyası (7+ gün eski)");
    }

    private void ScanThumbnailCache()
    {
        AppendLog("[Thumbnail Cache] Taranıyor...");
        var explorerCache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Explorer");

        if (!Directory.Exists(explorerCache)) return;

        var count = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(explorerCache, "thumbcache_*.db"))
            {
                try
                {
                    var info = new FileInfo(file);
                    _filesToDelete.Add(file);
                    _totalBytesFound += info.Length;
                    count++;
                }
                catch { }
            }
        }
        catch { }

        AppendLog($"  Bulunan: {count} thumbnail cache dosyası");
    }

    private static bool IsProtectedPath(string filePath)
    {
        var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => ProtectedDirNames.Contains(part));
    }

    private bool IsTargetChecked(int index)
    {
        var result = false;
        if (InvokeRequired)
            Invoke(() => result = _chkTargets.GetItemChecked(index));
        else
            result = _chkTargets.GetItemChecked(index);
        return result;
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
            Invoke(() => AppendLogCore(message));
        else
            AppendLogCore(message);
    }

    private void AppendLogCore(string message)
    {
        _txtLog.AppendText(message + "\r\n");
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F2} MB",
            >= 1_024 => $"{bytes / 1_024.0:F2} KB",
            _ => $"{bytes} B"
        };
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    private static void EmptyRecycleBin()
    {
        // SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND
        SHEmptyRecycleBin(IntPtr.Zero, null, 0x00000007);
    }

    private static Button CreateButton(string text, Point location)
    {
        var btn = new Button
        {
            Text = text,
            Location = location,
            Width = 160,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btn.FlatAppearance.BorderColor = GreenText;
        return btn;
    }
}
