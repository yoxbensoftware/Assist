namespace Assist.Forms.SystemTools.Monitoring;

using System.Diagnostics;
using System.Drawing.Drawing2D;

/// <summary>
/// Next-gen performance monitor with animated neon arc gauges for CPU, RAM, Disk, and GPU.
/// Custom-painted borderless-style panel — smoother and more stylish than Windows Task Manager.
/// </summary>
internal sealed class PerformanceMonitorForm : Form
{
    // ── Gauge panels ──
    private readonly GaugePanel _cpuGauge;
    private readonly GaugePanel _ramGauge;
    private readonly GaugePanel _diskGauge;
    private readonly GaugePanel _gpuGauge;

    // ── Details text (double-buffered to prevent flicker) ──
    private readonly FlickerFreeLabel _lblDetails;

    // ── Timer ──
    private readonly System.Windows.Forms.Timer _timer;

    // ── Performance counters (cached as fields — not re-created every tick) ──
    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _diskCounter;
    private readonly List<PerformanceCounter> _gpuCounters = [];
    private readonly Microsoft.VisualBasic.Devices.ComputerInfo _sysInfo = new();

    // ── Details refresh throttling ──
    // The details panel renders a large multi-line block. Rebuilding it on every
    // gauge tick (every ~700 ms) is expensive and wastes paint cycles, so we
    // refresh it on a longer cadence and cache the drive enumeration too.
    private DateTime _lastDetailsRefresh = DateTime.MinValue;
    private string _cachedDrivesBlock = string.Empty;
    private DateTime _lastDrivesRefresh = DateTime.MinValue;
    private static readonly TimeSpan DetailsRefreshInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DrivesRefreshInterval = TimeSpan.FromSeconds(10);

    // Track Windows modal move/size loop so we don't fight with the drag thread
    private const int WM_ENTERSIZEMOVE = 0x0231;
    private const int WM_EXITSIZEMOVE = 0x0232;
    private bool _isInSizeMove;
    private bool _timerWasRunning;

    // ── Colors ──
    private static readonly Color BgColor   = Color.FromArgb(8,  10,  20);
    private static readonly Color CpuColor  = Color.FromArgb(0,  210, 255);
    private static readonly Color RamColor  = Color.FromArgb(255,140,   0);
    private static readonly Color DiskColor = Color.FromArgb(180, 60, 255);
    private static readonly Color GpuColor  = Color.FromArgb(0, 255, 140);
    private static readonly Color HeaderFg  = Color.FromArgb(200, 220, 255);
    private static readonly Color DetailsFg = Color.FromArgb(120, 160, 200);

    // ── Fonts ──
    private static readonly Font HeaderFont = new("Consolas", 13, FontStyle.Bold);
    private static readonly Font SysFont    = new("Consolas",  8);

    public PerformanceMonitorForm()
    {
        Text          = "⚡ Performance Monitor";
        ClientSize    = new Size(810, 570);
        MinimumSize   = new Size(810, 570);
        BackColor     = BgColor;
        ForeColor     = HeaderFg;
        Font          = new Font("Consolas", 10);

        // ── Header bar ──
        var header = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 50,
            BackColor = Color.FromArgb(12, 15, 28)
        };
        header.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(0, 150, 220), 2f);
            g.DrawLine(pen, 0, header.Height - 2, header.Width, header.Height - 2);
            TextRenderer.DrawText(g, "⚡  PERFORMANCE MONITOR",
                HeaderFont, new Point(18, 12), Color.FromArgb(0, 220, 255));
            TextRenderer.DrawText(g, $"  {AppConstants.BuildVersion}  •  {Environment.MachineName}  •  {Environment.ProcessorCount} Cores",
                SysFont, new Point(20, 34), Color.FromArgb(80, 130, 180));
        };

        // ── Gauge container (4 equal-width columns) ──
        var gaugePanel = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 4,
            RowCount    = 1,
            BackColor   = BgColor,
            Padding     = new Padding(10, 6, 10, 6),
            Margin      = Padding.Empty
        };
        gaugePanel.ColumnStyles.Clear();
        gaugePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        gaugePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        gaugePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        gaugePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        gaugePanel.RowStyles.Clear();
        gaugePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _cpuGauge  = new GaugePanel("CPU",  CpuColor)  { Dock = DockStyle.Fill, Margin = new Padding(4) };
        _ramGauge  = new GaugePanel("RAM",  RamColor)  { Dock = DockStyle.Fill, Margin = new Padding(4) };
        _diskGauge = new GaugePanel("DISK", DiskColor) { Dock = DockStyle.Fill, Margin = new Padding(4) };
        _gpuGauge  = new GaugePanel("GPU",  GpuColor)  { Dock = DockStyle.Fill, Margin = new Padding(4) };
        gaugePanel.Controls.Add(_cpuGauge,  0, 0);
        gaugePanel.Controls.Add(_ramGauge,  1, 0);
        gaugePanel.Controls.Add(_diskGauge, 2, 0);
        gaugePanel.Controls.Add(_gpuGauge,  3, 0);

        // ── Separator ──
        var sep = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(0, 80, 140) };

        // ── Details label (flicker-free) ──
        _lblDetails = new FlickerFreeLabel
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.FromArgb(6, 8, 16),
            ForeColor = DetailsFg,
            Font      = new Font("Consolas", 9),
            Padding   = new Padding(14, 10, 14, 10),
            Margin    = Padding.Empty
        };

        // ── Body: 70% gauges / 30% details ──
        var body = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 3,
            BackColor   = BgColor,
            Margin      = Padding.Empty,
            Padding     = Padding.Empty
        };
        body.RowStyles.Clear();
        body.RowStyles.Add(new RowStyle(SizeType.Percent,  70f)); // gauges
        body.RowStyles.Add(new RowStyle(SizeType.Absolute,  1f)); // separator
        body.RowStyles.Add(new RowStyle(SizeType.Percent,  30f)); // details
        body.Controls.Add(gaugePanel,  0, 0);
        body.Controls.Add(sep,         0, 1);
        body.Controls.Add(_lblDetails, 0, 2);

        Controls.Add(body);
        Controls.Add(header);

        InitCounters();

        // 700 ms keeps the gauges responsive without painting at near-15 fps. Custom-painted gauges
        // with glow/gradient passes are surprisingly expensive — at 450 ms they kept the UI thread
        // busy continuously while still smooth at 700 ms.
        _timer = new System.Windows.Forms.Timer { Interval = 700 };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();
    }

    /// <summary>
    /// Pause the gauge refresh timer while the window is being moved or resized; resume on exit.
    /// </summary>
    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_ENTERSIZEMOVE:
                if (!_isInSizeMove)
                {
                    _isInSizeMove = true;
                    _timerWasRunning = _timer is { Enabled: true };
                    _timer?.Stop();
                }
                break;

            case WM_EXITSIZEMOVE:
                if (_isInSizeMove)
                {
                    _isInSizeMove = false;
                    if (_timerWasRunning) _timer?.Start();
                }
                break;
        }

        base.WndProc(ref m);
    }

    private void InitCounters()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue();
        }
        catch { _cpuCounter = null; }

        try
        {
            _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
            _diskCounter.NextValue();
        }
        catch { _diskCounter = null; }

        try
        {
            _gpuCounters.Clear();
            var category = new PerformanceCounterCategory("GPU Engine");
            var counterName = category.CounterExists("Utilization Percentage")
                ? "Utilization Percentage"
                : "% Utilization";

            foreach (var instance in category.GetInstanceNames())
            {
                try
                {
                    if (!category.CounterExists(counterName))
                        continue;

                    var counter = new PerformanceCounter("GPU Engine", counterName, instance, readOnly: true);
                    counter.NextValue();
                    _gpuCounters.Add(counter);
                }
                catch
                {
                    // Skip unsupported GPU engine instances
                }
            }
        }
        catch
        {
            _gpuCounters.Clear();
        }
    }

    private void Tick()
    {
        try
        {
            var cpu      = _cpuCounter?.NextValue() ?? 0f;
            var totalMem = (long)_sysInfo.TotalPhysicalMemory;
            var availMem = (long)_sysInfo.AvailablePhysicalMemory;
            var usedMem  = totalMem - availMem;
            var ramPct   = totalMem > 0 ? (float)(usedMem * 100.0 / totalMem) : 0f;
            var usedGB   = usedMem  / 1073741824.0;
            var totalGB  = totalMem / 1073741824.0;
            var disk     = Math.Min(100f, _diskCounter?.NextValue() ?? 0f);
            var gpu     = ReadGpuUsage();

            _cpuGauge .SetValue(cpu,    $"{cpu:F1}%",    "Processor Time");
            _ramGauge .SetValue(ramPct, $"{ramPct:F1}%", $"{usedGB:F1} / {totalGB:F1} GB");
            _diskGauge.SetValue(disk,   $"{disk:F1}%",   "Disk Activity");
            _gpuGauge .SetValue(gpu,    $"{gpu:F1}%",    "GPU Usage");

            UpdateDetails(cpu, usedMem, totalMem, availMem, disk, gpu);
        }
        catch (Exception ex)
        {
            _lblDetails.Text = $"  Hata: {ex.Message}";
        }
    }

    private float ReadGpuUsage()
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
                // ignore stale counter instances
            }
        }

        return Math.Min(100f, total);
    }

    private void UpdateDetails(float cpu, long usedMem, long totalMem, long availMem, float disk, float gpu)
    {
        var now = DateTime.UtcNow;
        if (now - _lastDetailsRefresh < DetailsRefreshInterval)
            return;
        _lastDetailsRefresh = now;

        var usedGB  = usedMem  / 1073741824.0;
        var totalGB = totalMem / 1073741824.0;
        var availGB = availMem / 1073741824.0;
        var ramPct  = totalMem > 0 ? usedMem * 100.0 / totalMem : 0;
        var uptime  = TimeSpan.FromMilliseconds(Environment.TickCount64);

        // DriveInfo.GetDrives() touches every mounted volume (including spun-down or network ones)
        // which can take hundreds of milliseconds. Cache the rendered block for 10 s.
        if (now - _lastDrivesRefresh >= DrivesRefreshInterval || string.IsNullOrEmpty(_cachedDrivesBlock))
        {
            var drives = new System.Text.StringBuilder();
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    if (!d.IsReady) continue;
                    var free = d.TotalFreeSpace / 1073741824.0;
                    var tot  = d.TotalSize      / 1073741824.0;
                    var pct  = (int)((1.0 - (double)d.TotalFreeSpace / d.TotalSize) * 100);
                    drives.AppendLine($"  {d.Name,-6} {pct,3}%  [{free:F0} GB boş / {tot:F0} GB]");
                }
            }
            catch
            {
                // If a removable/network drive throws, keep whatever we have so far
            }
            _cachedDrivesBlock = drives.ToString();
            _lastDrivesRefresh = now;
        }

        var newText =
            $"  ┌─────────────────────────── SİSTEM DETAYLARI ─────────────────────────────┐\r\n" +
            $"  │  CPU Kullanımı    :  {cpu,6:F1}%  ({Environment.ProcessorCount} çekirdek)\r\n" +
            $"  │  RAM Kullanımı    :  {ramPct,6:F1}%  ({usedGB:F2} GB / {totalGB:F2} GB)\r\n" +
            $"  │  Kullanılabilir   :  {availGB:F2} GB\r\n" +
            $"  │  Disk Aktivitesi  :  {disk,6:F1}%\r\n" +
            $"  │  GPU Kullanımı    :  {gpu,6:F1}%\r\n" +
            $"  ├────────────────────────────────────────────────────────────────────────────┤\r\n" +
            $"  │  OS              :  {Environment.OSVersion}\r\n" +
            $"  │  Makine          :  {Environment.MachineName}\r\n" +
            $"  │  Sistem Uptime   :  {uptime.Days}g {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}\r\n" +
            $"  │  .NET Sürümü     :  {Environment.Version}\r\n" +
            $"  ├────────────────────────────────────────────────────────────────────────────┤\r\n" +
            $"  │  Sürücüler:\r\n{_cachedDrivesBlock}" +
            $"  └────────────────────────────────────────────────────────────────────────────┘\r\n" +
            $"    Güncelleme: {DateTime.Now:HH:mm:ss}";

        if (_lblDetails.Text != newText)
            _lblDetails.Text = newText;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _timer.Stop();
        _timer.Dispose();
        _cpuCounter?.Dispose();
        _diskCounter?.Dispose();
        foreach (var counter in _gpuCounters)
            counter.Dispose();
        _cpuGauge.Dispose();
        _ramGauge.Dispose();
        _diskGauge.Dispose();
        _gpuGauge.Dispose();
    }

    // ════════════════════════════════════════════════════════════════════
    //  FlickerFreeLabel — fully double-buffered text panel (no Label.Text repaint flicker)
    // ════════════════════════════════════════════════════════════════════
    private sealed class FlickerFreeLabel : Panel
    {
        private string _text = string.Empty;

        public FlickerFreeLabel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw, true);
            UpdateStyles();
        }

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string Text
        {
            get => _text;
            set
            {
                var v = value ?? string.Empty;
                if (_text == v) return;
                _text = v;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // We do NOT call base.OnPaint — Panel default paint can introduce flicker
            var g = e.Graphics;
            g.Clear(BackColor);
            var rect = new Rectangle(
                Padding.Left,
                Padding.Top,
                Width  - Padding.Horizontal,
                Height - Padding.Vertical);
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.Top
                                  | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(g, _text, Font, rect, ForeColor, BackColor, flags);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  GaugePanel — custom-painted neon arc gauge
    // ════════════════════════════════════════════════════════════════════
    private sealed class GaugePanel : Control
    {
        private const float StartAngle = 150f;
        private const float SweepAngle = 240f;
        private const int   DotSpacing = 22;

        private readonly Color  _color;
        private readonly string _label;
        private float  _smooth;
        private float  _lastDrawnSmooth = -1f;
        private string _mainText = "0%";
        private string _subText  = "";
        private string _lastDrawnMainText = string.Empty;
        private string _lastDrawnSubText = string.Empty;

        // Cached dot-grid background bitmap; rebuilt only when the panel size changes
        private Bitmap? _backgroundCache;
        private Size _backgroundCacheSize = Size.Empty;

        private static readonly Font LabelFont = new("Consolas", 11, FontStyle.Bold);
        private static readonly Font SubFont   = new("Consolas",  8);
        private static readonly Font TitleFont = new("Consolas",  9, FontStyle.Bold);
        private static readonly Font TickFont  = new("Consolas",  7);

        public GaugePanel(string label, Color color)
        {
            _label         = label;
            _color         = color;
            DoubleBuffered = true;
            BackColor      = Color.FromArgb(10, 12, 22);
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint, true);
            UpdateStyles();
        }

        public void SetValue(float value, string mainText, string subText)
        {
            var clamped = Math.Max(0, Math.Min(100, value));
            var newSmooth = _smooth + (clamped - _smooth) * 0.35f;

            // Skip the repaint entirely when nothing visible would change.
            // Snap to the final value once we're within rounding distance to stop the
            // perpetual easing animation that otherwise repaints forever.
            var diff = Math.Abs(newSmooth - _lastDrawnSmooth);
            if (diff < 0.05f) newSmooth = clamped;

            var willRepaint =
                Math.Abs(newSmooth - _lastDrawnSmooth) >= 0.05f
                || !string.Equals(mainText, _lastDrawnMainText, StringComparison.Ordinal)
                || !string.Equals(subText, _lastDrawnSubText, StringComparison.Ordinal);

            _smooth   = newSmooth;
            _mainText = mainText;
            _subText  = subText;

            if (willRepaint) Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0)
            {
                base.OnPaintBackground(e);
                return;
            }

            // Rebuild the dot-grid cache only when the panel size changes
            if (_backgroundCache is null || _backgroundCacheSize != Size)
            {
                _backgroundCache?.Dispose();
                _backgroundCache = new Bitmap(Width, Height);
                using (var bg = Graphics.FromImage(_backgroundCache))
                {
                    bg.Clear(BackColor);
                    using var dotBrush = new SolidBrush(Color.FromArgb(22, 140, 200, 255));
                    for (var x = DotSpacing / 2; x < Width; x += DotSpacing)
                        for (var y = DotSpacing / 2; y < Height; y += DotSpacing)
                            bg.FillEllipse(dotBrush, x - 1, y - 1, 2, 2);
                }
                _backgroundCacheSize = Size;
            }

            e.Graphics.DrawImageUnscaled(_backgroundCache, 0, 0);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // Invalidate the cached background so it is regenerated at the new size
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

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var cx = Width  / 2f;
            var cy = (int)(Height * 0.46f);

            var radius   = (int)(Math.Min(Width, Height * 0.85f) / 2f) - 22;
            var arcRect  = new RectangleF(cx - radius, cy - radius, radius * 2f, radius * 2f);
            var glowRect = new RectangleF(cx - radius - 4, cy - radius - 4,
                                          (radius + 4) * 2f, (radius + 4) * 2f);
            var fillSweep = SweepAngle * _smooth / 100f;

            // Track
            using (var tp = new Pen(Color.FromArgb(22, 36, 54), 10f)
                   { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(tp, arcRect, StartAngle, SweepAngle);

            // Tick marks
            for (var i = 0; i <= 20; i++)
            {
                var rad    = (StartAngle + i * SweepAngle / 20.0) * Math.PI / 180.0;
                var major  = (i % 5 == 0);
                var inner  = radius + (major ? 8 : 5);
                var outer  = radius + (major ? 16 : 11);
                using var tp = new Pen(major
                    ? Color.FromArgb(80, _color.R, _color.G, _color.B)
                    : Color.FromArgb(38, _color.R, _color.G, _color.B), major ? 1.5f : 1f);
                g.DrawLine(tp,
                    cx + (float)(inner * Math.Cos(rad)), cy + (float)(inner * Math.Sin(rad)),
                    cx + (float)(outer * Math.Cos(rad)), cy + (float)(outer * Math.Sin(rad)));
            }

            DrawTickLabel(g, "0",   StartAngle,                cx, cy, radius + 24);
            DrawTickLabel(g, "50",  StartAngle + SweepAngle / 2f, cx, cy, radius + 24);
            DrawTickLabel(g, "100", StartAngle + SweepAngle,   cx, cy, radius + 24);

            // Filled arc
            if (fillSweep > 0.8f)
            {
                using (var gp1 = new Pen(Color.FromArgb(20, _color.R, _color.G, _color.B), 28f)
                       { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawArc(gp1, glowRect, StartAngle, fillSweep);

                using (var gp2 = new Pen(Color.FromArgb(50, _color.R, _color.G, _color.B), 16f)
                       { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawArc(gp2, arcRect, StartAngle, fillSweep);

                using (var ap = new Pen(_color, 9f)
                       { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawArc(ap, arcRect, StartAngle, fillSweep);

                // Leading dot
                var er  = (StartAngle + fillSweep) * Math.PI / 180.0;
                var dx  = cx + (float)(radius * Math.Cos(er));
                var dy  = cy + (float)(radius * Math.Sin(er));
                using (var gb = new SolidBrush(Color.FromArgb(180, _color.R, _color.G, _color.B)))
                    g.FillEllipse(gb, dx - 7f, dy - 7f, 14f, 14f);
                using (var wb = new SolidBrush(Color.White))
                    g.FillEllipse(wb, dx - 4f, dy - 4f, 8f, 8f);
            }

            // Center value text
            var ms = TextRenderer.MeasureText(_mainText, LabelFont);
            TextRenderer.DrawText(g, _mainText, LabelFont,
                new Point((int)(cx - ms.Width / 2f), cy - ms.Height / 2 - 8), _color);

            var ss = TextRenderer.MeasureText(_subText, SubFont);
            TextRenderer.DrawText(g, _subText, SubFont,
                new Point((int)(cx - ss.Width / 2f), cy + ms.Height / 2 - 4),
                Color.FromArgb(160, _color.R, _color.G, _color.B));

            // Title
            var ts = TextRenderer.MeasureText(_label, TitleFont);
            using (var tb = new SolidBrush(Color.FromArgb(30, _color.R, _color.G, _color.B)))
                g.FillRectangle(tb, cx - ts.Width / 2f - 6, cy - radius - 34, ts.Width + 12, ts.Height + 6);
            TextRenderer.DrawText(g, _label, TitleFont,
                new Point((int)(cx - ts.Width / 2f), (int)(cy - radius - 32)), _color);

            // Mini gradient bar
            var barY = cy + (int)(radius * 0.72f);
            var barW = (int)(Width * 0.72f);
            var barX = (int)(cx - barW / 2f);
            using (var bb = new SolidBrush(Color.FromArgb(22, 36, 54)))
                g.FillRectangle(bb, barX, barY, barW, 5);
            var fw = (int)(barW * _smooth / 100f);
            if (fw > 1)
            {
                using var gr = new LinearGradientBrush(
                    new Rectangle(barX, barY, Math.Max(1, fw), 5),
                    Color.FromArgb(180, _color.R, _color.G, _color.B),
                    _color, LinearGradientMode.Horizontal);
                g.FillRectangle(gr, barX, barY, fw, 5);
            }

            // Record what was actually drawn so SetValue can skip redundant repaints
            _lastDrawnSmooth = _smooth;
            _lastDrawnMainText = _mainText;
            _lastDrawnSubText = _subText;
        }

        private void DrawTickLabel(Graphics g, string text, float angleDeg, float cx, float cy, float r)
        {
            var rad = angleDeg * Math.PI / 180.0;
            var lx  = (int)(cx + r * Math.Cos(rad));
            var ly  = (int)(cy + r * Math.Sin(rad));
            var sz  = TextRenderer.MeasureText(text, TickFont);
            TextRenderer.DrawText(g, text, TickFont,
                new Point(lx - sz.Width / 2, ly - sz.Height / 2),
                Color.FromArgb(100, _color.R, _color.G, _color.B));
        }
    }
}
