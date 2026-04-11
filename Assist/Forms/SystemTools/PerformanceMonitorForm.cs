namespace Assist.Forms.SystemTools;

using System.Diagnostics;
using System.Drawing.Drawing2D;

/// <summary>
/// Next-gen performance monitor with animated neon arc gauges for CPU, RAM, and Disk.
/// Custom-painted borderless-style panel — smoother and more stylish than Windows Task Manager.
/// </summary>
internal sealed class PerformanceMonitorForm : Form
{
    // ── Gauge panels ──
    private readonly GaugePanel _cpuGauge;
    private readonly GaugePanel _ramGauge;
    private readonly GaugePanel _diskGauge;

    // ── Details text ──
    private readonly Label _lblDetails;

    // ── Timer ──
    private readonly System.Windows.Forms.Timer _timer;

    // ── Performance counters (cached as fields — not re-created every tick) ──
    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _diskCounter;
    private readonly Microsoft.VisualBasic.Devices.ComputerInfo _sysInfo = new();

    // ── Colors ──
    private static readonly Color BgColor   = Color.FromArgb(8,  10,  20);
    private static readonly Color CpuColor  = Color.FromArgb(0,  210, 255);
    private static readonly Color RamColor  = Color.FromArgb(255,140,   0);
    private static readonly Color DiskColor = Color.FromArgb(180, 60, 255);
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

        // ── Gauge container ──
        var gaugePanel = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 292,
            BackColor = BgColor
        };

        _cpuGauge  = new GaugePanel("CPU",  CpuColor)  { Bounds = new Rectangle(14,  8, 256, 276) };
        _ramGauge  = new GaugePanel("RAM",  RamColor)  { Bounds = new Rectangle(277, 8, 256, 276) };
        _diskGauge = new GaugePanel("DISK", DiskColor) { Bounds = new Rectangle(540, 8, 256, 276) };
        gaugePanel.Controls.AddRange(new Control[] { _cpuGauge, _ramGauge, _diskGauge });

        // ── Separator ──
        var sep = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(0, 80, 140) };

        // ── Details label ──
        _lblDetails = new Label
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.FromArgb(6, 8, 16),
            ForeColor = DetailsFg,
            Font      = new Font("Consolas", 9),
            TextAlign = ContentAlignment.TopLeft,
            Padding   = new Padding(14, 10, 14, 10),
            AutoSize  = false
        };

        Controls.Add(_lblDetails);
        Controls.Add(sep);
        Controls.Add(gaugePanel);
        Controls.Add(header);

        InitCounters();

        _timer = new System.Windows.Forms.Timer { Interval = 450 };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();
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

            _cpuGauge .SetValue(cpu,    $"{cpu:F1}%",    "Processor Time");
            _ramGauge .SetValue(ramPct, $"{ramPct:F1}%", $"{usedGB:F1} / {totalGB:F1} GB");
            _diskGauge.SetValue(disk,   $"{disk:F1}%",   "Disk Activity");

            UpdateDetails(cpu, usedMem, totalMem, availMem, disk);
        }
        catch (Exception ex)
        {
            _lblDetails.Text = $"  Hata: {ex.Message}";
        }
    }

    private void UpdateDetails(float cpu, long usedMem, long totalMem, long availMem, float disk)
    {
        var usedGB  = usedMem  / 1073741824.0;
        var totalGB = totalMem / 1073741824.0;
        var availGB = availMem / 1073741824.0;
        var ramPct  = totalMem > 0 ? usedMem * 100.0 / totalMem : 0;
        var uptime  = TimeSpan.FromMilliseconds(Environment.TickCount64);

        var drives = new System.Text.StringBuilder();
        foreach (var d in DriveInfo.GetDrives())
        {
            if (!d.IsReady) continue;
            var free = d.TotalFreeSpace / 1073741824.0;
            var tot  = d.TotalSize      / 1073741824.0;
            var pct  = (int)((1.0 - (double)d.TotalFreeSpace / d.TotalSize) * 100);
            drives.AppendLine($"  {d.Name,-6} {pct,3}%  [{free:F0} GB boş / {tot:F0} GB]");
        }

        _lblDetails.Text =
            $"  ┌─────────────────────────── SİSTEM DETAYLARI ─────────────────────────────┐\r\n" +
            $"  │  CPU Kullanımı    :  {cpu,6:F1}%  ({Environment.ProcessorCount} çekirdek)\r\n" +
            $"  │  RAM Kullanımı    :  {ramPct,6:F1}%  ({usedGB:F2} GB / {totalGB:F2} GB)\r\n" +
            $"  │  Kullanılabilir   :  {availGB:F2} GB\r\n" +
            $"  │  Disk Aktivitesi  :  {disk,6:F1}%\r\n" +
            $"  ├────────────────────────────────────────────────────────────────────────────┤\r\n" +
            $"  │  OS              :  {Environment.OSVersion}\r\n" +
            $"  │  Makine          :  {Environment.MachineName}\r\n" +
            $"  │  Sistem Uptime   :  {uptime.Days}g {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}\r\n" +
            $"  │  .NET Sürümü     :  {Environment.Version}\r\n" +
            $"  ├────────────────────────────────────────────────────────────────────────────┤\r\n" +
            $"  │  Sürücüler:\r\n{drives}" +
            $"  └────────────────────────────────────────────────────────────────────────────┘\r\n" +
            $"    Güncelleme: {DateTime.Now:HH:mm:ss}";
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _timer.Stop();
        _timer.Dispose();
        _cpuCounter?.Dispose();
        _diskCounter?.Dispose();
        _cpuGauge.Dispose();
        _ramGauge.Dispose();
        _diskGauge.Dispose();
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
        private string _mainText = "0%";
        private string _subText  = "";

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
        }

        public void SetValue(float value, string mainText, string subText)
        {
            var clamped = Math.Max(0, Math.Min(100, value));
            _smooth    += (clamped - _smooth) * 0.35f;
            _mainText   = mainText;
            _subText    = subText;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            var g = e.Graphics;
            using var dotBrush = new SolidBrush(Color.FromArgb(22, 140, 200, 255));
            for (var x = DotSpacing / 2; x < Width; x += DotSpacing)
                for (var y = DotSpacing / 2; y < Height; y += DotSpacing)
                    g.FillEllipse(dotBrush, x - 1, y - 1, 2, 2);
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
