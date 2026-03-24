using System.Diagnostics;

namespace Assist.Forms.SystemTools;

/// <summary>
/// Real-time performance monitor showing CPU, RAM, and Disk usage.
/// </summary>
internal sealed class PerformanceMonitorForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);

    private readonly Label _lblCpu = null!;
    private readonly Label _lblRam = null!;
    private readonly Label _lblDisk = null!;
    private readonly ProgressBar _barCpu = null!;
    private readonly ProgressBar _barRam = null!;
    private readonly ProgressBar _barDisk = null!;
    private readonly TextBox _txtDetails = null!;
    private readonly System.Windows.Forms.Timer _refreshTimer = null!;

    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _diskCounter;
    private long _totalMemory;

    public PerformanceMonitorForm()
    {
        Text = "Performance Monitor";
        ClientSize = new Size(700, 500);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        var lblTitle = new Label
        {
            Text = "=== PERFORMANCE MONITOR ===",
            Location = new Point(20, 20),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };

        // CPU
        var lblCpuTitle = new Label
        {
            Text = "CPU Kullanımı:",
            Location = new Point(20, 70),
            Width = 200,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _lblCpu = new Label
        {
            Text = "0%",
            Location = new Point(600, 70),
            Width = 80,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = GreenText
        };

        _barCpu = new ProgressBar
        {
            Location = new Point(20, 95),
            Width = 660,
            Height = 25,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // RAM
        var lblRamTitle = new Label
        {
            Text = "RAM Kullanımı:",
            Location = new Point(20, 140),
            Width = 200,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _lblRam = new Label
        {
            Text = "0%",
            Location = new Point(600, 140),
            Width = 80,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = GreenText
        };

        _barRam = new ProgressBar
        {
            Location = new Point(20, 165),
            Width = 660,
            Height = 25,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // Disk (simulated)
        var lblDiskTitle = new Label
        {
            Text = "Disk Kullanımı:",
            Location = new Point(20, 210),
            Width = 200,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _lblDisk = new Label
        {
            Text = "0%",
            Location = new Point(600, 210),
            Width = 80,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = GreenText
        };

        _barDisk = new ProgressBar
        {
            Location = new Point(20, 235),
            Width = 660,
            Height = 25,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // Details
        _txtDetails = new TextBox
        {
            Location = new Point(20, 280),
            Width = 660,
            Height = 200,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9),
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        Controls.AddRange(new Control[]
        {
            lblTitle,
            lblCpuTitle, _lblCpu, _barCpu,
            lblRamTitle, _lblRam, _barRam,
            lblDiskTitle, _lblDisk, _barDisk,
            _txtDetails
        });

        // Initialize performance counters
        InitializeCounters();

        // Refresh timer
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _refreshTimer.Tick += (_, _) => UpdatePerformance();
        _refreshTimer.Start();

        // Initial update
        UpdatePerformance();
    }

    private void InitializeCounters()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue(); // First call always returns 0
        }
        catch
        {
            _cpuCounter = null;
        }

        try
        {
            _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
            _diskCounter.NextValue(); // First call always returns 0
        }
        catch
        {
            _diskCounter = null;
        }

        try
        {
            var computerInfo = new Microsoft.VisualBasic.Devices.ComputerInfo();
            _totalMemory = (long)computerInfo.TotalPhysicalMemory;
        }
        catch
        {
            _totalMemory = 16L * 1024 * 1024 * 1024; // 16GB default
        }
    }

    private void UpdatePerformance()
    {
        try
        {
            // CPU
            var cpuUsage = _cpuCounter?.NextValue() ?? 0;
            _lblCpu.Text = $"{cpuUsage:F1}%";
            _barCpu.Value = Math.Min(100, (int)cpuUsage);

            // RAM - Total system memory usage
            var computerInfo = new Microsoft.VisualBasic.Devices.ComputerInfo();
            var totalMemory = (long)computerInfo.TotalPhysicalMemory;
            var availableMemory = (long)computerInfo.AvailablePhysicalMemory;
            var usedMemory = totalMemory - availableMemory;
            var ramPercent = (totalMemory > 0) ? (usedMemory * 100.0 / totalMemory) : 0;
            _lblRam.Text = $"{ramPercent:F1}%";
            _barRam.Value = Math.Min(100, (int)ramPercent);

            // Disk - Real system disk usage
            var diskUsage = _diskCounter?.NextValue() ?? 0;
            _lblDisk.Text = $"{diskUsage:F1}%";
            _barDisk.Value = Math.Min(100, (int)diskUsage);

            // Details
            UpdateDetails(cpuUsage, usedMemory, totalMemory, availableMemory, diskUsage);
        }
        catch (Exception ex)
        {
            _txtDetails.Text = $"Hata: {ex.Message}";
        }
    }

    private void UpdateDetails(float cpu, long usedMemory, long totalMemory, long availableMemory, float disk)
    {
        var usedGB = usedMemory / (1024.0 * 1024.0 * 1024.0);
        var totalGB = totalMemory / (1024.0 * 1024.0 * 1024.0);
        var availableGB = availableMemory / (1024.0 * 1024.0 * 1024.0);
        var ramPercent = (totalMemory > 0) ? (usedMemory * 100.0 / totalMemory) : 0;

        var details = $"""
            ╔═══════════════════════════════════════════════════════════════╗
            ║                     SİSTEM PERFORMANSI                         ║
            ╚═══════════════════════════════════════════════════════════════╝

            CPU Kullanımı:     {cpu,6:F1}%

            RAM Kullanımı:     {ramPercent,6:F1}% ({usedGB:F1}/{totalGB:F1} GB)
            Kullanılan:        {usedGB,10:F2} GB
            Kullanılabilir:    {availableGB,10:F2} GB
            Toplam:            {totalGB,10:F2} GB

            Disk Kullanımı:    {disk,6:F1}%

            ─────────────────────────────────────────────────────────────────

            Processor Sayısı:  {Environment.ProcessorCount}
            İşletim Sistemi:   {Environment.OSVersion.Platform}
            Sistem Uptime:     {TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"hh\:mm\:ss")}

            Güncelleme: {DateTime.Now:HH:mm:ss}
            """;

        _txtDetails.Text = details;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _cpuCounter?.Dispose();
        _diskCounter?.Dispose();
    }
}
