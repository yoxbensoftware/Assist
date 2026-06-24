namespace Assist.Forms.SystemTools.Monitoring;

using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;

internal sealed class PerformanceMonitorForm : Form
{
    private static readonly Color BgColor = Color.FromArgb(8, 10, 20);
    private static readonly Color HeaderColor = Color.FromArgb(12, 15, 28);
    private static readonly Color DetailsBackColor = Color.FromArgb(6, 8, 16);
    private static readonly Color DetailsForeColor = Color.FromArgb(130, 180, 235);
    private static readonly Color CpuColor = Color.FromArgb(0, 210, 255);
    private static readonly Color RamColor = Color.FromArgb(255, 140, 0);
    private static readonly Color DiskColor = Color.FromArgb(180, 60, 255);
    private static readonly Color GpuColor = Color.FromArgb(0, 255, 140);

    private static readonly Font HeaderFont = new("Consolas", 15, FontStyle.Bold);
    private static readonly Font HeaderMetaFont = new("Consolas", 9, FontStyle.Bold);
    private static readonly Font DetailsFont = new("Consolas", 9);

    private const int RefreshIntervalMs = 1000;
    private const int DetailsRefreshEveryTicks = 2;
    private const int DrivesRefreshEveryTicks = 12;
    private const int WM_ENTERSIZEMOVE = 0x0231;
    private const int WM_EXITSIZEMOVE = 0x0232;

    private MetricGauge _cpuGauge = null!;
    private MetricGauge _ramGauge = null!;
    private MetricGauge _diskGauge = null!;
    private MetricGauge _gpuGauge = null!;
    private readonly TextBox _detailsBox;
    private readonly MetricSampler _sampler = new();
    private readonly CancellationTokenSource _refreshCts = new();

    private Task? _refreshTask;
    private int _tickCount;
    private bool _isInSizeMove;

    public PerformanceMonitorForm()
    {
        Text = "⚡ Performance Monitor";
        ClientSize = new Size(980, 640);
        MinimumSize = new Size(900, 580);
        BackColor = BgColor;
        ForeColor = Color.FromArgb(200, 220, 255);
        Font = new Font("Consolas", 10);
        DoubleBuffered = true;

        var header = CreateHeader();
        var gaugeGrid = CreateGaugeGrid();
        var separator = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(0, 90, 160) };

        _detailsBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = ScrollBars.Vertical,
            BackColor = DetailsBackColor,
            ForeColor = DetailsForeColor,
            Font = DetailsFont,
            Margin = Padding.Empty,
            WordWrap = false
        };

        var detailsHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 12, 18, 12),
            BackColor = DetailsBackColor
        };
        detailsHost.Controls.Add(_detailsBox);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = BgColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 66f));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 1f));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 34f));
        body.Controls.Add(gaugeGrid, 0, 0);
        body.Controls.Add(separator, 0, 1);
        body.Controls.Add(detailsHost, 0, 2);

        Controls.Add(body);
        Controls.Add(header);

        Shown += (_, _) => _refreshTask ??= RunRefreshLoopAsync(_refreshCts.Token);
    }

    private Panel CreateHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 88,
            BackColor = HeaderColor
        };

        header.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var bg = new LinearGradientBrush(
                header.ClientRectangle,
                Color.FromArgb(9, 12, 24),
                Color.FromArgb(18, 8, 34),
                LinearGradientMode.Horizontal);
            g.FillRectangle(bg, header.ClientRectangle);

            using var scanPen = new Pen(Color.FromArgb(22, 0, 220, 255), 1f);
            for (var x = 0; x < header.Width; x += 28)
                g.DrawLine(scanPen, x, 0, x + 44, header.Height);

            var boltRect = new Rectangle(16, 10, 28, 36);
            using var boltBrush = new SolidBrush(Color.FromArgb(0, 220, 255));
            TextRenderer.DrawText(g, "⚡", HeaderFont, boltRect, Color.FromArgb(0, 230, 255),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            var statusRect = new Rectangle(Math.Max(0, header.Width - 236), 13, 198, 28);
            var titleWidth = Math.Max(120, statusRect.Left - 58);
            var titleRect = new Rectangle(54, 10, titleWidth, 34);
            TextRenderer.DrawText(
                g,
                "PERFORMANCE MONITOR",
                HeaderFont,
                titleRect,
                Color.FromArgb(0, 235, 255),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            var metaRect = new Rectangle(18, 52, Math.Max(120, header.Width - 36), 24);
            using var metaBrush = new SolidBrush(Color.FromArgb(68, 3, 12, 26));
            using var metaPen = new Pen(Color.FromArgb(55, 0, 210, 255), 1f);
            g.FillRectangle(metaBrush, metaRect);
            g.DrawRectangle(metaPen, metaRect);
            TextRenderer.DrawText(
                g,
                $"Build {AppConstants.BuildVersion}  |  {Environment.MachineName}  |  {Environment.ProcessorCount} cores",
                HeaderMetaFont,
                metaRect,
                Color.FromArgb(150, 205, 255),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            using var statusBrush = new SolidBrush(Color.FromArgb(28, 0, 255, 140));
            using var statusPen = new Pen(Color.FromArgb(0, 255, 140), 1f);
            g.FillRectangle(statusBrush, statusRect);
            g.DrawRectangle(statusPen, statusRect);
            TextRenderer.DrawText(
                g,
                "TELEMETRY ONLINE",
                HeaderMetaFont,
                statusRect,
                Color.FromArgb(0, 255, 140),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            using var accentPen = new Pen(Color.FromArgb(0, 220, 255), 2f);
            using var magentaPen = new Pen(Color.FromArgb(190, 70, 255), 2f);
            g.DrawLine(accentPen, 0, header.Height - 3, header.Width, header.Height - 3);
            g.DrawLine(magentaPen, 0, header.Height - 1, header.Width / 3, header.Height - 1);
        };

        return header;
    }

    private TableLayoutPanel CreateGaugeGrid()
    {
        var gaugeGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = BgColor,
            Padding = new Padding(14, 12, 14, 8),
            Margin = Padding.Empty
        };

        for (var i = 0; i < 4; i++)
            gaugeGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        gaugeGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _cpuGauge = new MetricGauge("CPU", CpuColor) { Dock = DockStyle.Fill, Margin = new Padding(8) };
        _ramGauge = new MetricGauge("RAM", RamColor) { Dock = DockStyle.Fill, Margin = new Padding(8) };
        _diskGauge = new MetricGauge("DISK", DiskColor) { Dock = DockStyle.Fill, Margin = new Padding(8) };
        _gpuGauge = new MetricGauge("GPU", GpuColor) { Dock = DockStyle.Fill, Margin = new Padding(8) };

        gaugeGrid.Controls.Add(_cpuGauge, 0, 0);
        gaugeGrid.Controls.Add(_ramGauge, 1, 0);
        gaugeGrid.Controls.Add(_diskGauge, 2, 0);
        gaugeGrid.Controls.Add(_gpuGauge, 3, 0);

        return gaugeGrid;
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_ENTERSIZEMOVE:
                _isInSizeMove = true;
                break;
            case WM_EXITSIZEMOVE:
                _isInSizeMove = false;
                break;
        }

        base.WndProc(ref m);
    }

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var options = new CaptureOptions(
                    IncludeDetails: _tickCount % DetailsRefreshEveryTicks == 0,
                    RefreshDrives: _tickCount % DrivesRefreshEveryTicks == 0);

                var snapshot = await Task.Run(() => _sampler.Capture(options), cancellationToken).ConfigureAwait(false);

                if (!_isInSizeMove)
                    PostToUi(() => ApplySnapshot(snapshot));

                _tickCount++;
                await Task.Delay(RefreshIntervalMs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                PostToUi(() => _detailsBox.Text = $"Hata: {ex.Message}");
                await Task.Delay(RefreshIntervalMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void ApplySnapshot(MetricSnapshot snapshot)
    {
        _cpuGauge.SetValue(snapshot.CpuPercent, $"{snapshot.CpuPercent:F1}%", "Processor Time");
        _ramGauge.SetValue(snapshot.RamPercent, $"{snapshot.RamPercent:F1}%", $"{snapshot.UsedMemoryGb:F1} / {snapshot.TotalMemoryGb:F1} GB");
        _diskGauge.SetValue(snapshot.DiskPercent, $"{snapshot.DiskPercent:F1}%", "Disk Activity");
        _gpuGauge.SetValue(snapshot.GpuPercent, $"{snapshot.GpuPercent:F1}%", "GPU Usage");

        if (!string.IsNullOrWhiteSpace(snapshot.DetailsText) && _detailsBox.Text != snapshot.DetailsText)
            _detailsBox.Text = snapshot.DetailsText;
    }

    private void PostToUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        try
        {
            if (InvokeRequired)
                BeginInvoke(action);
            else
                action();
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _refreshCts.Cancel();
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _refreshCts.Cancel();
        _refreshCts.Dispose();
        _sampler.Dispose();
        base.OnFormClosed(e);
    }

    private readonly record struct CaptureOptions(bool IncludeDetails, bool RefreshDrives);

    private readonly record struct MetricSnapshot(
        float CpuPercent,
        float RamPercent,
        float DiskPercent,
        float GpuPercent,
        double UsedMemoryGb,
        double TotalMemoryGb,
        string? DetailsText);

    private sealed class MetricSampler : IDisposable
    {
        private PerformanceCounter? _diskCounter;
        private readonly List<PerformanceCounter> _gpuCounters = [];
        private readonly object _sync = new();
        private bool _countersInitialized;
        private string _cachedDrivesBlock = string.Empty;
        private CpuTimes? _lastCpuTimes;

        public MetricSnapshot Capture(CaptureOptions options)
        {
            lock (_sync)
            {
                EnsureCountersInitialized();

                var cpu = ReadCpuPercent();
                var memory = ReadMemory();
                var disk = ReadDiskPercent();
                var gpu = ReadGpuPercent();
                var details = options.IncludeDetails
                    ? BuildDetailsText(cpu, memory, disk, gpu, options.RefreshDrives)
                    : null;

                return new MetricSnapshot(
                    cpu,
                    memory.Percent,
                    disk,
                    gpu,
                    memory.UsedGb,
                    memory.TotalGb,
                    details);
            }
        }

        private void EnsureCountersInitialized()
        {
            if (_countersInitialized)
                return;

            _countersInitialized = true;

            try
            {
                _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total", readOnly: true);
                _diskCounter.NextValue();
            }
            catch
            {
                _diskCounter?.Dispose();
                _diskCounter = null;
            }

            try
            {
                var category = new PerformanceCounterCategory("GPU Engine");
                var counterName = category.CounterExists("Utilization Percentage")
                    ? "Utilization Percentage"
                    : "% Utilization";

                foreach (var instance in category.GetInstanceNames())
                {
                    try
                    {
                        var counter = new PerformanceCounter("GPU Engine", counterName, instance, readOnly: true);
                        counter.NextValue();
                        _gpuCounters.Add(counter);
                    }
                    catch
                    {
                        // Unsupported or stale GPU engine instance.
                    }
                }
            }
            catch
            {
                DisposeGpuCounters();
            }
        }

        private float ReadCpuPercent()
        {
            if (!GetSystemTimes(out var idleFileTime, out var kernelFileTime, out var userFileTime))
                return 0f;

            var current = new CpuTimes(
                ToUInt64(idleFileTime),
                ToUInt64(kernelFileTime),
                ToUInt64(userFileTime));

            if (_lastCpuTimes is null)
            {
                _lastCpuTimes = current;
                return 0f;
            }

            var previous = _lastCpuTimes.Value;
            _lastCpuTimes = current;

            var idleDelta = current.Idle - previous.Idle;
            var kernelDelta = current.Kernel - previous.Kernel;
            var userDelta = current.User - previous.User;
            var totalDelta = kernelDelta + userDelta;

            if (totalDelta == 0 || totalDelta < idleDelta)
                return 0f;

            var busyDelta = totalDelta - idleDelta;
            return ClampPercent((float)(busyDelta * 100.0 / totalDelta));
        }

        private static MemorySnapshot ReadMemory()
        {
            var status = new MemoryStatusEx();
            if (!GlobalMemoryStatusEx(status))
                return new MemorySnapshot(0, 0, 0, 0);

            var total = status.ullTotalPhys;
            var available = status.ullAvailPhys;
            var used = total > available ? total - available : 0;
            var usedGb = used / 1073741824.0;
            var totalGb = total / 1073741824.0;
            var availableGb = available / 1073741824.0;
            var percent = total > 0 ? (float)(used * 100.0 / total) : 0f;

            return new MemorySnapshot(usedGb, totalGb, availableGb, ClampPercent(percent));
        }

        private float ReadDiskPercent()
        {
            try
            {
                return ClampPercent(_diskCounter?.NextValue() ?? 0f);
            }
            catch
            {
                return 0f;
            }
        }

        private float ReadGpuPercent()
        {
            if (_gpuCounters.Count == 0)
                return 0f;

            var total = 0f;
            foreach (var counter in _gpuCounters)
            {
                try
                {
                    total += Math.Max(0f, counter.NextValue());
                }
                catch
                {
                    // GPU engine instances can disappear while apps close.
                }
            }

            return ClampPercent(total);
        }

        private string BuildDetailsText(float cpu, MemorySnapshot memory, float disk, float gpu, bool refreshDrives)
        {
            if (refreshDrives || string.IsNullOrEmpty(_cachedDrivesBlock))
                _cachedDrivesBlock = BuildDrivesBlock();

            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            var builder = new StringBuilder(768);
            builder.AppendLine("  ┌─────────────────────────── SİSTEM DETAYLARI ─────────────────────────────┐");
            builder.AppendLine($"  │  CPU Kullanımı    :  {cpu,6:F1}%  ({Environment.ProcessorCount} çekirdek)");
            builder.AppendLine($"  │  RAM Kullanımı    :  {memory.Percent,6:F1}%  ({memory.UsedGb:F2} GB / {memory.TotalGb:F2} GB)");
            builder.AppendLine($"  │  Kullanılabilir   :  {memory.AvailableGb:F2} GB");
            builder.AppendLine($"  │  Disk Aktivitesi  :  {disk,6:F1}%");
            builder.AppendLine($"  │  GPU Kullanımı    :  {gpu,6:F1}%");
            builder.AppendLine("  ├────────────────────────────────────────────────────────────────────────────┤");
            builder.AppendLine($"  │  OS               :  {Environment.OSVersion}");
            builder.AppendLine($"  │  Makine           :  {Environment.MachineName}");
            builder.AppendLine($"  │  Sistem Uptime    :  {uptime.Days}g {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}");
            builder.AppendLine($"  │  .NET Sürümü      :  {Environment.Version}");
            builder.AppendLine("  ├────────────────────────────────────────────────────────────────────────────┤");
            builder.AppendLine("  │  Sürücüler:");
            builder.Append(_cachedDrivesBlock);
            builder.AppendLine("  └────────────────────────────────────────────────────────────────────────────┘");
            builder.Append($"    Güncelleme: {DateTime.Now:HH:mm:ss}");
            return builder.ToString();
        }

        private static string BuildDrivesBlock()
        {
            var builder = new StringBuilder(256);

            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady)
                        continue;

                    var freeGb = drive.TotalFreeSpace / 1073741824.0;
                    var totalGb = drive.TotalSize / 1073741824.0;
                    var usedPercent = drive.TotalSize > 0
                        ? (int)((1.0 - (double)drive.TotalFreeSpace / drive.TotalSize) * 100)
                        : 0;

                    builder.AppendLine($"  │    {drive.Name,-6} {usedPercent,3}%  [{freeGb:F0} GB boş / {totalGb:F0} GB]");
                }
            }
            catch
            {
                builder.AppendLine("  │    Sürücü bilgisi alınamadı");
            }

            if (builder.Length == 0)
                builder.AppendLine("  │    Hazır sürücü bulunamadı");

            return builder.ToString();
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _diskCounter?.Dispose();
                _diskCounter = null;
                DisposeGpuCounters();
            }
        }

        private void DisposeGpuCounters()
        {
            foreach (var counter in _gpuCounters)
                counter.Dispose();
            _gpuCounters.Clear();
        }

        private static float ClampPercent(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;

            return Math.Clamp(value, 0f, 100f);
        }

        private static ulong ToUInt64(FileTime fileTime) =>
            ((ulong)fileTime.HighDateTime << 32) | fileTime.LowDateTime;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx status);

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct FileTime
        {
            public readonly uint LowDateTime;
            public readonly uint HighDateTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private sealed class MemoryStatusEx
        {
            public uint dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>();
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        private readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);

        private readonly record struct MemorySnapshot(double UsedGb, double TotalGb, double AvailableGb, float Percent);
    }

    private sealed class MetricGauge : Control
    {
        private const float StartAngle = 150f;
        private const float SweepAngle = 240f;
        private const int DotSpacing = 22;

        private static readonly Font LabelFont = new("Consolas", 11, FontStyle.Bold);
        private static readonly Font SubFont = new("Consolas", 8);
        private static readonly Font TitleFont = new("Consolas", 9, FontStyle.Bold);
        private static readonly Font TickFont = new("Consolas", 7);

        private readonly string _label;
        private readonly Color _accent;
        private Bitmap? _backgroundCache;
        private Size _backgroundCacheSize = Size.Empty;
        private float _value;
        private string _mainText = "0%";
        private string _subText = string.Empty;

        public MetricGauge(string label, Color accent)
        {
            _label = label;
            _accent = accent;
            BackColor = Color.FromArgb(10, 12, 22);
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);
            UpdateStyles();
        }

        public void SetValue(float value, string mainText, string subText)
        {
            value = Math.Clamp(value, 0f, 100f);
            if (Math.Abs(value - _value) < 0.05f &&
                string.Equals(mainText, _mainText, StringComparison.Ordinal) &&
                string.Equals(subText, _subText, StringComparison.Ordinal))
            {
                return;
            }

            _value = value;
            _mainText = mainText;
            _subText = subText;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _backgroundCache?.Dispose();
            _backgroundCache = null;
            _backgroundCacheSize = Size.Empty;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _backgroundCache?.Dispose();
                _backgroundCache = null;
            }

            base.Dispose(disposing);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0)
            {
                base.OnPaintBackground(e);
                return;
            }

            if (_backgroundCache is null || _backgroundCacheSize != Size)
            {
                _backgroundCache?.Dispose();
                _backgroundCache = new Bitmap(Width, Height);
                using var graphics = Graphics.FromImage(_backgroundCache);
                graphics.Clear(BackColor);

                using var cardBrush = new LinearGradientBrush(
                    new Rectangle(0, 0, Width, Height),
                    Color.FromArgb(12, 15, 30),
                    Color.FromArgb(6, 8, 18),
                    LinearGradientMode.Vertical);
                graphics.FillRectangle(cardBrush, 0, 0, Width, Height);

                using var dotBrush = new SolidBrush(Color.FromArgb(20, 140, 200, 255));
                for (var x = DotSpacing / 2; x < Width; x += DotSpacing)
                {
                    for (var y = DotSpacing / 2; y < Height; y += DotSpacing)
                        graphics.FillRectangle(dotBrush, x, y, 1, 1);
                }

                using var topPen = new Pen(Color.FromArgb(130, _accent.R, _accent.G, _accent.B), 2f);
                using var edgePen = new Pen(Color.FromArgb(46, _accent.R, _accent.G, _accent.B), 1f);
                graphics.DrawLine(topPen, 12, 10, Width - 12, 10);
                graphics.DrawRectangle(edgePen, 0, 0, Width - 1, Height - 1);

                using var cornerPen = new Pen(Color.FromArgb(160, _accent.R, _accent.G, _accent.B), 2f);
                const int corner = 18;
                graphics.DrawLine(cornerPen, 0, 0, corner, 0);
                graphics.DrawLine(cornerPen, 0, 0, 0, corner);
                graphics.DrawLine(cornerPen, Width - corner - 1, Height - 1, Width - 1, Height - 1);
                graphics.DrawLine(cornerPen, Width - 1, Height - corner - 1, Width - 1, Height - 1);

                _backgroundCacheSize = Size;
            }

            e.Graphics.DrawImageUnscaled(_backgroundCache, 0, 0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            const float titleBandHeight = 76f;
            const float bottomBandHeight = 56f;
            var centerX = Width / 2f;
            var radius = Math.Max(26f, Math.Min((Width - 42f) / 2f, (Height - titleBandHeight - bottomBandHeight) / 2f));
            var centerY = titleBandHeight + radius;
            var arcRect = new RectangleF(centerX - radius, centerY - radius, radius * 2f, radius * 2f);
            var fillSweep = SweepAngle * _value / 100f;

            DrawTrack(graphics, arcRect);
            DrawTicks(graphics, centerX, centerY, radius);
            DrawFill(graphics, arcRect, fillSweep);
            DrawTexts(graphics, centerX, centerY, radius);
            DrawProgressBar(graphics, centerX, centerY, radius);
        }

        private void DrawTrack(Graphics graphics, RectangleF arcRect)
        {
            using var trackPen = new Pen(Color.FromArgb(24, 38, 58), 10f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawArc(trackPen, arcRect, StartAngle, SweepAngle);
        }

        private void DrawTicks(Graphics graphics, float centerX, float centerY, float radius)
        {
            for (var i = 0; i <= 20; i++)
            {
                var angle = (StartAngle + i * SweepAngle / 20f) * Math.PI / 180.0;
                var major = i % 5 == 0;
                var inner = radius + (major ? 8 : 5);
                var outer = radius + (major ? 16 : 11);
                using var tickPen = new Pen(
                    major
                        ? Color.FromArgb(72, _accent.R, _accent.G, _accent.B)
                        : Color.FromArgb(34, _accent.R, _accent.G, _accent.B),
                    major ? 1.4f : 1f);

                graphics.DrawLine(
                    tickPen,
                    centerX + (float)(inner * Math.Cos(angle)),
                    centerY + (float)(inner * Math.Sin(angle)),
                    centerX + (float)(outer * Math.Cos(angle)),
                    centerY + (float)(outer * Math.Sin(angle)));
            }

            DrawTickLabel(graphics, "0", StartAngle, centerX, centerY, radius + 24);
            DrawTickLabel(graphics, "50", StartAngle + SweepAngle / 2f, centerX, centerY, radius + 24);
            DrawTickLabel(graphics, "100", StartAngle + SweepAngle, centerX, centerY, radius + 24);
        }

        private void DrawFill(Graphics graphics, RectangleF arcRect, float fillSweep)
        {
            if (fillSweep <= 0.5f)
                return;

            using var fillPen = new Pen(_accent, 9f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawArc(fillPen, arcRect, StartAngle, fillSweep);

            var angle = (StartAngle + fillSweep) * Math.PI / 180.0;
            var radius = arcRect.Width / 2f;
            var centerX = arcRect.Left + radius;
            var centerY = arcRect.Top + radius;
            var dotX = centerX + (float)(radius * Math.Cos(angle));
            var dotY = centerY + (float)(radius * Math.Sin(angle));
            using var dotBrush = new SolidBrush(Color.White);
            graphics.FillEllipse(dotBrush, dotX - 4f, dotY - 4f, 8f, 8f);
        }

        private void DrawTexts(Graphics graphics, float centerX, float centerY, float radius)
        {
            var mainSize = TextRenderer.MeasureText(_mainText, LabelFont);
            TextRenderer.DrawText(
                graphics,
                _mainText,
                LabelFont,
                new Point((int)(centerX - mainSize.Width / 2f), (int)(centerY - mainSize.Height / 2f - 8)),
                _accent);

            var subSize = TextRenderer.MeasureText(_subText, SubFont);
            TextRenderer.DrawText(
                graphics,
                _subText,
                SubFont,
                new Point((int)(centerX - subSize.Width / 2f), (int)(centerY + mainSize.Height / 2f - 4)),
                Color.FromArgb(170, _accent.R, _accent.G, _accent.B));

            var titleSize = TextRenderer.MeasureText(_label, TitleFont);
            var titleRect = new Rectangle(
                (int)(centerX - titleSize.Width / 2f - 12),
                20,
                titleSize.Width + 24,
                24);

            using var titleBrush = new SolidBrush(Color.FromArgb(30, _accent.R, _accent.G, _accent.B));
            graphics.FillRectangle(titleBrush, titleRect);
            TextRenderer.DrawText(
                graphics,
                _label,
                TitleFont,
                titleRect,
                _accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawProgressBar(Graphics graphics, float centerX, float centerY, float radius)
        {
            var barWidth = Math.Max(40, (int)(Width * 0.72f));
            var barX = (int)(centerX - barWidth / 2f);
            var barY = Math.Min(Height - 32, (int)(centerY + radius + 22));
            var fillWidth = (int)(barWidth * _value / 100f);

            using var backBrush = new SolidBrush(Color.FromArgb(24, 38, 58));
            graphics.FillRectangle(backBrush, barX, barY, barWidth, 5);

            if (fillWidth <= 0)
                return;

            using var fillBrush = new SolidBrush(_accent);
            graphics.FillRectangle(fillBrush, barX, barY, fillWidth, 5);
        }

        private void DrawTickLabel(Graphics graphics, string text, float angleDeg, float centerX, float centerY, float radius)
        {
            var angle = angleDeg * Math.PI / 180.0;
            var x = (int)(centerX + radius * Math.Cos(angle));
            var y = (int)(centerY + radius * Math.Sin(angle));
            var size = TextRenderer.MeasureText(text, TickFont);

            TextRenderer.DrawText(
                graphics,
                text,
                TickFont,
                new Point(x - size.Width / 2, y - size.Height / 2),
                Color.FromArgb(110, _accent.R, _accent.G, _accent.B));
        }
    }
}
