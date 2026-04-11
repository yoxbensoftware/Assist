namespace Assist.Forms.SystemTools.Monitoring;

using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

/// <summary>
/// Compact borderless Matrix-style popup for real-time internet connectivity monitoring.
/// Always-on-top, draggable from anywhere, pinned to the bottom-right corner.
/// </summary>
internal sealed class ConnectionMonitorForm : Form
{
    // ── Win32 drag support ──
    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;

    // ── Matrix rain ──
    private readonly System.Windows.Forms.Timer _rainTimer;
    private readonly System.Windows.Forms.Timer _pingTimer;
    private readonly Random _rng = new();
    private int[]? _drops;
    private Bitmap? _buffer;

    private const string MatrixChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789@#$%&*ﾊﾐﾋｰｳｼﾅﾓﾆｻﾜﾂｵﾘｱﾎﾃﾏｹﾒｴｶｷﾑﾕﾗｾﾈｽﾀﾇﾍ";
    private const int CellSize = 14;

    // ── Fonts ──
    private static readonly Font RainFont = new("Consolas", 9);
    private static readonly Font StatusFont = new("Consolas", 16, FontStyle.Bold);
    private static readonly Font PingFont = new("Consolas", 10);
    private static readonly Font StatsFont = new("Consolas", 7.5f);
    private static readonly Font TargetFont = new("Consolas", 8);

    // ── Colors ──
    private static readonly Color ConnectedColor = Color.FromArgb(0, 255, 0);
    private static readonly Color DisconnectedColor = Color.FromArgb(255, 50, 50);
    private static readonly Color BorderColor = Color.FromArgb(0, 80, 0);

    // ── Pre-allocated GDI brushes ──
    private readonly SolidBrush _fadeBrush = new(Color.FromArgb(30, 0, 0, 0));
    private readonly SolidBrush _greenBrush = new(ConnectedColor);
    private readonly SolidBrush _redBrush = new(DisconnectedColor);
    private readonly SolidBrush _whiteBrush = new(Color.White);

    // ── State ──
    private bool _isConnected = true;
    private long _lastPingMs;
    private string _pingTarget = "8.8.8.8";
    private int _totalChecks;
    private int _failedChecks;
    private readonly DateTime _startTime;

    // ── Controls ──
    private readonly Label _lblStatus;
    private readonly Label _lblPing;
    private readonly Label _lblStats;
    private readonly TextBox _txtTarget;
    private readonly Panel _matrixPanel;

    public ConnectionMonitorForm()
    {
        Text = "Bağlantı Monitörü";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = true;
        ClientSize = new Size(320, 280);
        BackColor = Color.Black;
        ForeColor = ConnectedColor;
        Font = new Font("Consolas", 9);
        Opacity = 0.92;
        _startTime = DateTime.Now;

        // Position at bottom-right corner of the primary screen
        var workArea = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(workArea.Right - Width - 12, workArea.Bottom - Height - 12);

        // ── Top config bar (compact) ──
        var configPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 26,
            BackColor = Color.FromArgb(15, 15, 15),
            Padding = new Padding(4, 3, 4, 3)
        };
        MakeDraggable(configPanel);

        var lblTarget = new Label
        {
            Text = "🎯",
            AutoSize = true,
            ForeColor = ConnectedColor,
            Font = TargetFont,
            Location = new Point(4, 5)
        };

        _txtTarget = new TextBox
        {
            Text = _pingTarget,
            Width = 130,
            Location = new Point(24, 3),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = ConnectedColor,
            Font = TargetFont,
            BorderStyle = BorderStyle.FixedSingle
        };

        var btnApply = new Button
        {
            Text = "✓",
            Width = 28,
            Height = 20,
            Location = new Point(158, 3),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = ConnectedColor,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnApply.FlatAppearance.BorderColor = BorderColor;
        btnApply.Click += (_, _) =>
        {
            var target = _txtTarget.Text.Trim();
            if (!string.IsNullOrEmpty(target))
            {
                _pingTarget = target;
                _totalChecks = 0;
                _failedChecks = 0;
            }
        };

        var btnClose = new Label
        {
            Text = "✕",
            AutoSize = false,
            Size = new Size(22, 20),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(160, 160, 160),
            BackColor = Color.Transparent,
            Font = new Font("Consolas", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnClose.Location = new Point(configPanel.Width - btnClose.Width - 2, 3);
        btnClose.Click += (_, _) => Close();
        btnClose.MouseEnter += (_, _) => btnClose.ForeColor = DisconnectedColor;
        btnClose.MouseLeave += (_, _) => btnClose.ForeColor = Color.FromArgb(160, 160, 160);

        configPanel.Controls.AddRange(new Control[] { lblTarget, _txtTarget, btnApply, btnClose });
        configPanel.Controls.Add(new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = BorderColor
        });

        // ── Matrix rain panel (fills remaining space) ──
        _matrixPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black
        };
        typeof(Panel).InvokeMember("DoubleBuffered",
            System.Reflection.BindingFlags.SetProperty |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic,
            null, _matrixPanel, [true]);

        _matrixPanel.Paint += MatrixPanel_Paint;
        _matrixPanel.Resize += (_, _) =>
        {
            InitDrops();
            CenterStatusLabels();
        };
        MakeDraggable(_matrixPanel);

        // ── Overlaid status labels (on top of matrix rain) ──
        _lblStatus = new Label
        {
            Text = "● BAĞLI",
            Font = StatusFont,
            ForeColor = ConnectedColor,
            BackColor = Color.FromArgb(180, 0, 0, 0),
            AutoSize = false,
            Size = new Size(260, 35),
            TextAlign = ContentAlignment.MiddleCenter
        };
        MakeDraggable(_lblStatus);

        _lblPing = new Label
        {
            Text = "Ping: -- ms",
            Font = PingFont,
            ForeColor = ConnectedColor,
            BackColor = Color.FromArgb(180, 0, 0, 0),
            AutoSize = false,
            Size = new Size(260, 22),
            TextAlign = ContentAlignment.MiddleCenter
        };
        MakeDraggable(_lblPing);

        _lblStats = new Label
        {
            Text = "00:00:00 | 0% | K:0",
            Font = StatsFont,
            ForeColor = ConnectedColor,
            BackColor = Color.FromArgb(180, 0, 0, 0),
            AutoSize = false,
            Size = new Size(260, 18),
            TextAlign = ContentAlignment.MiddleCenter
        };
        MakeDraggable(_lblStats);

        _matrixPanel.Controls.AddRange(new Control[] { _lblStatus, _lblPing, _lblStats });

        // ── Add to form (order matters for docking) ──
        Controls.Add(_matrixPanel);
        Controls.Add(configPanel);

        // ── 1px border around the form ──
        var borderPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            BackColor = BorderColor
        };

        // Re-parent: move existing controls into borderPanel
        Controls.Remove(_matrixPanel);
        Controls.Remove(configPanel);
        borderPanel.Controls.Add(_matrixPanel);
        borderPanel.Controls.Add(configPanel);
        Controls.Add(borderPanel);

        // ── Timers ──
        _rainTimer = new System.Windows.Forms.Timer { Interval = 80 };
        _rainTimer.Tick += RainTick;
        _rainTimer.Start();

        _pingTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _pingTimer.Tick += async (_, _) => await CheckConnectivityAsync();
        _pingTimer.Start();

        _ = CheckConnectivityAsync();

        FormClosed += OnFormClosed;
        InitDrops();
    }

    /// <summary>
    /// Makes a control act as a drag handle for the borderless form.
    /// </summary>
    private void MakeDraggable(Control control)
    {
        control.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        };
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        _rainTimer.Stop();
        _rainTimer.Dispose();
        _pingTimer.Stop();
        _pingTimer.Dispose();
        _buffer?.Dispose();
        _fadeBrush.Dispose();
        _greenBrush.Dispose();
        _redBrush.Dispose();
        _whiteBrush.Dispose();
    }

    #region Layout

    private void CenterStatusLabels()
    {
        var cx = _matrixPanel.ClientSize.Width / 2;
        var cy = _matrixPanel.ClientSize.Height / 2;
        _lblStatus.Location = new Point(cx - _lblStatus.Width / 2, cy - 38);
        _lblPing.Location = new Point(cx - _lblPing.Width / 2, cy + 2);
        _lblStats.Location = new Point(cx - _lblStats.Width / 2, cy + 28);
    }

    #endregion

    #region Matrix Rain

    private void InitDrops()
    {
        var cols = Math.Max(1, _matrixPanel.ClientSize.Width / CellSize);
        var rows = Math.Max(1, _matrixPanel.ClientSize.Height / CellSize);
        _drops = new int[cols];
        for (var i = 0; i < cols; i++)
            _drops[i] = _rng.Next(-rows, rows);

        _buffer?.Dispose();
        _buffer = null;
    }

    private void EnsureBuffer()
    {
        var w = _matrixPanel.ClientSize.Width;
        var h = _matrixPanel.ClientSize.Height;
        if (w <= 0 || h <= 0) return;

        if (_buffer is null || _buffer.Width != w || _buffer.Height != h)
        {
            _buffer?.Dispose();
            _buffer = new Bitmap(w, h);
            using var g = Graphics.FromImage(_buffer);
            g.Clear(Color.Black);
        }
    }

    private void RainTick(object? sender, EventArgs e)
    {
        if (_drops is null) return;

        var w = _matrixPanel.ClientSize.Width;
        var h = _matrixPanel.ClientSize.Height;
        if (w <= 0 || h <= 0) return;

        EnsureBuffer();
        if (_buffer is null) return;

        using var g = Graphics.FromImage(_buffer);

        // Fade previous frame for the trail effect
        g.FillRectangle(_fadeBrush, 0, 0, w, h);

        var headBrush = _isConnected ? _greenBrush : _redBrush;

        for (var i = 0; i < _drops.Length; i++)
        {
            var ch = MatrixChars[_rng.Next(MatrixChars.Length)].ToString();
            var x = i * CellSize;
            var y = _drops[i] * CellSize;

            if (y >= 0 && y < h)
            {
                var brush = _rng.NextDouble() < 0.12 ? _whiteBrush : headBrush;
                g.DrawString(ch, RainFont, brush, x, y);
            }

            _drops[i]++;
            if (_drops[i] * CellSize > h && _rng.NextDouble() > 0.975)
                _drops[i] = _rng.Next(-15, 0);
        }

        _matrixPanel.Invalidate();
    }

    private void MatrixPanel_Paint(object? sender, PaintEventArgs e)
    {
        if (_buffer is not null)
            e.Graphics.DrawImageUnscaled(_buffer, 0, 0);
    }

    #endregion

    #region Connectivity

    private async Task CheckConnectivityAsync()
    {
        _totalChecks++;
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(_pingTarget, 3000);

            if (reply.Status == IPStatus.Success)
            {
                _isConnected = true;
                _lastPingMs = reply.RoundtripTime;
            }
            else
            {
                _failedChecks++;
                _isConnected = false;
            }
        }
        catch
        {
            _failedChecks++;
            _isConnected = false;
        }

        UpdateStatusUi();
        UpdateStatsUi();
    }

    private void UpdateStatusUi()
    {
        void Apply()
        {
            if (_isConnected)
            {
                _lblStatus.Text = "● BAĞLI";
                _lblStatus.ForeColor = ConnectedColor;
                _lblPing.Text = $"Ping: {_lastPingMs} ms";
                _lblPing.ForeColor = ConnectedColor;
            }
            else
            {
                _lblStatus.Text = "✖ BAĞLANTI KOPUK";
                _lblStatus.ForeColor = DisconnectedColor;
                _lblPing.Text = "Ping: TIMEOUT";
                _lblPing.ForeColor = DisconnectedColor;
            }
        }

        if (InvokeRequired) Invoke(Apply); else Apply();
    }

    private void UpdateStatsUi()
    {
        var elapsed = DateTime.Now - _startTime;
        var rate = _totalChecks > 0
            ? (_totalChecks - _failedChecks) * 100.0 / _totalChecks
            : 0;

        void Apply()
        {
            _lblStats.Text =
                $"{elapsed:hh\\:mm\\:ss} | %{rate:F0} | Kontrol:{_totalChecks} Kopukluk:{_failedChecks}";
            _lblStats.ForeColor = _isConnected ? ConnectedColor : DisconnectedColor;
        }

        if (InvokeRequired) Invoke(Apply); else Apply();
    }

    #endregion
}
