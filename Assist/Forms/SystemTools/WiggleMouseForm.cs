namespace Assist.Forms.SystemTools;

using System.Runtime.InteropServices;

internal sealed class WiggleMouseForm : Form
{
    // ── Win32 drag support ──
    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;

    private readonly Label _countdownLabel = new();
    private readonly NumericUpDown _nudHours = new();
    private readonly NumericUpDown _nudMinutes = new();
    private readonly NumericUpDown _nudSeconds = new();
    private readonly Button _btnSet = new();
    private readonly Button _btnStartStop = new();
    private readonly Label _statusLabel = new();
    private readonly System.Windows.Forms.Timer _countdownTimer = new();
    private readonly System.Windows.Forms.Timer _wiggleTimer = new();

    private int _remainingSeconds;
    private bool _isRunning;

    public WiggleMouseForm()
    {
        InitializeComponent();
        WireEvents();
    }

    private void InitializeComponent()
    {
        Text = "Wiggle Mouse";
        Size = new Size(400, 380);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        ForeColor = Color.FromArgb(0, 255, 0);
        Opacity = 0.92;

        var configPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = Color.FromArgb(15, 15, 15),
            Padding = new Padding(6, 4, 6, 4)
        };
        MakeDraggable(configPanel);

        var lblTitle = new Label
        {
            Text = "Wiggle Mouse",
            AutoSize = true,
            ForeColor = Color.FromArgb(0, 255, 0),
            Font = new Font("Consolas", 9f, FontStyle.Bold),
            Location = new Point(6, 6)
        };

        var opacityLabel = new Label
        {
            Text = "Opaklık",
            AutoSize = true,
            ForeColor = Color.FromArgb(130, 130, 130),
            Font = new Font("Consolas", 8f),
            Location = new Point(160, 7)
        };

        var opacitySlider = new TrackBar
        {
            Minimum = 70,
            Maximum = 100,
            Value = 92,
            TickStyle = TickStyle.None,
            Size = new Size(90, 18),
            Location = new Point(210, 3),
            BackColor = Color.FromArgb(15, 15, 15)
        };
        opacitySlider.ValueChanged += (_, _) => Opacity = opacitySlider.Value / 100d;

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
        btnClose.Location = new Point(configPanel.Width - btnClose.Width - 4, 4);
        btnClose.Click += (_, _) => Close();
        btnClose.MouseEnter += (_, _) => btnClose.ForeColor = Color.FromArgb(255, 50, 50);
        btnClose.MouseLeave += (_, _) => btnClose.ForeColor = Color.FromArgb(160, 160, 160);
        configPanel.SizeChanged += (_, _) => btnClose.Location = new Point(configPanel.Width - btnClose.Width - 4, 4);

        configPanel.Controls.AddRange([lblTitle, opacityLabel, opacitySlider, btnClose]);

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            Padding = new Padding(12, 10, 12, 12)
        };

        var lblHeader = new Label
        {
            Text = "Fare hareketi için bekleme süresi:",
            Font = new Font("Consolas", 10f),
            ForeColor = Color.FromArgb(0, 255, 0),
            AutoSize = true,
            Location = new Point(8, 12)
        };

        _countdownLabel.Text = "0h:0m:0s";
        _countdownLabel.Font = new Font("Consolas", 28f, FontStyle.Bold);
        _countdownLabel.ForeColor = Color.FromArgb(0, 255, 0);
        _countdownLabel.AutoSize = true;
        _countdownLabel.Location = new Point(52, 48);

        var lblH = new Label { Text = "S", Font = new Font("Consolas", 9f), ForeColor = Color.Gray, Location = new Point(16, 122), AutoSize = true };
        var lblM = new Label { Text = "D", Font = new Font("Consolas", 9f), ForeColor = Color.Gray, Location = new Point(76, 122), AutoSize = true };
        var lblS = new Label { Text = "SN", Font = new Font("Consolas", 9f), ForeColor = Color.Gray, Location = new Point(133, 122), AutoSize = true };

        ConfigureNumericUpDown(_nudHours, new Point(8, 140), 23);
        ConfigureNumericUpDown(_nudMinutes, new Point(68, 140), 59);
        ConfigureNumericUpDown(_nudSeconds, new Point(128, 140), 59);
        _nudSeconds.Value = 1;

        _btnSet.Text = "Süre Ayarla";
        _btnSet.Font = new Font("Consolas", 10f);
        _btnSet.Size = new Size(120, 34);
        _btnSet.Location = new Point(212, 138);
        _btnSet.FlatStyle = FlatStyle.Flat;
        _btnSet.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 0);
        _btnSet.BackColor = Color.FromArgb(30, 30, 30);
        _btnSet.ForeColor = Color.FromArgb(0, 255, 0);
        _btnSet.Cursor = Cursors.Hand;

        _btnStartStop.Text = "▶  Başlat";
        _btnStartStop.Font = new Font("Consolas", 12f, FontStyle.Bold);
        _btnStartStop.Size = new Size(340, 42);
        _btnStartStop.Location = new Point(8, 194);
        _btnStartStop.FlatStyle = FlatStyle.Flat;
        _btnStartStop.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 0);
        _btnStartStop.BackColor = Color.FromArgb(20, 60, 20);
        _btnStartStop.ForeColor = Color.FromArgb(0, 255, 0);
        _btnStartStop.Cursor = Cursors.Hand;

        _statusLabel.Text = "● Beklemede";
        _statusLabel.Font = new Font("Consolas", 10f);
        _statusLabel.ForeColor = Color.Gray;
        _statusLabel.AutoSize = true;
        _statusLabel.Location = new Point(8, 250);

        _countdownTimer.Interval = 1000;
        _wiggleTimer.Interval = 180;  // shorter steps = more reliable idle reset

        body.Controls.AddRange([lblHeader, _countdownLabel, lblH, lblM, lblS, _nudHours, _nudMinutes, _nudSeconds, _btnSet, _btnStartStop, _statusLabel]);
        Controls.Add(body);
        Controls.Add(configPanel);

        // Allow drag from anywhere
        MakeDraggableRecursive(this);
    }

    private void MakeDraggableRecursive(Control control)
    {
        MakeDraggable(control);
        foreach (Control child in control.Controls)
            MakeDraggableRecursive(child);
    }

    private void MakeDraggable(Control control)
    {
        control.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        };
    }

    private static void ConfigureNumericUpDown(NumericUpDown nud, Point location, int max)
    {
        nud.Minimum = 0;
        nud.Maximum = max;
        nud.Value = 0;
        nud.Font = new Font("Consolas", 14f, FontStyle.Bold);
        nud.Size = new Size(52, 30);
        nud.Location = location;
        nud.BackColor = Color.FromArgb(30, 30, 30);
        nud.ForeColor = Color.FromArgb(0, 255, 0);
        nud.BorderStyle = BorderStyle.FixedSingle;
        nud.TextAlign = HorizontalAlignment.Center;
    }

    private void WireEvents()
    {
        _btnSet.Click += (_, _) => ApplyTime();
        _btnStartStop.Click += (_, _) => ToggleStartStop();
        _countdownTimer.Tick += (_, _) => OnCountdownTick();
        _wiggleTimer.Tick += (_, _) => OnWiggleTick();
        FormClosing += (_, _) => StopAll();
    }

    private void ApplyTime()
    {
        var total = (int)(_nudHours.Value * 3600 + _nudMinutes.Value * 60 + _nudSeconds.Value);
        if (total <= 0)
        {
            _statusLabel.Text = "● Süre en az 1 saniye olmalı";
            _statusLabel.ForeColor = Color.OrangeRed;
            return;
        }

        _remainingSeconds = total;
        UpdateCountdownDisplay();
        _statusLabel.Text = "● Süre ayarlandı";
        _statusLabel.ForeColor = Color.FromArgb(0, 255, 0);
    }

    private void ToggleStartStop()
    {
        if (_isRunning)
        {
            StopAll();
            return;
        }

        if (_remainingSeconds <= 0)
            ApplyTime();

        if (_remainingSeconds <= 0)
            return;

        _isRunning = true;
        SetInputsEnabled(false);

        _btnStartStop.Text = "■  Durdur";
        _btnStartStop.BackColor = Color.FromArgb(80, 20, 20);
        _btnStartStop.FlatAppearance.BorderColor = Color.OrangeRed;

        _statusLabel.Text = "● Hareket gönderiliyor...";
        _statusLabel.ForeColor = Color.Yellow;

        // İlk inputu hemen gönder; aksi halde uzun bekleme süresinde Teams AFK/offline görünebilir.
        PerformWiggleCycle();
    }

    private void StopAll()
    {
        _isRunning = false;
        _countdownTimer.Stop();
        _wiggleTimer.Stop();
        SetInputsEnabled(true);

        _btnStartStop.Text = "▶  Başlat";
        _btnStartStop.BackColor = Color.FromArgb(20, 60, 20);
        _btnStartStop.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 0);

        _statusLabel.Text = "● Durduruldu";
        _statusLabel.ForeColor = Color.Gray;
    }

    private void SetInputsEnabled(bool enabled)
    {
        _nudHours.Enabled = enabled;
        _nudMinutes.Enabled = enabled;
        _nudSeconds.Enabled = enabled;
        _btnSet.Enabled = enabled;
    }

    private void OnCountdownTick()
    {
        if (_remainingSeconds > 0)
        {
            _remainingSeconds--;
            UpdateCountdownDisplay();
            return;
        }

        _countdownTimer.Stop();
        PerformWiggleCycle();
    }

    private void PerformWiggleCycle()
    {
        _statusLabel.Text = "● Fare hareket ettiriliyor!";
        _statusLabel.ForeColor = Color.FromArgb(0, 255, 0);

        _wiggleCount = 0;
        _wiggleTimer.Start();
    }

    private int _wiggleCount;
    private Point _savedCursorPos;

    // ── Win32 SendInput P/Invoke ─────────────────────────────────────────────
    // IMPORTANT: Cursor.Position uses SetCursorPos which does NOT update
    // GetLastInputInfo. Teams uses GetLastInputInfo for idle detection.
    // SendInput is the only API that updates it, so online status is preserved.

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type; // 0 = INPUT_MOUSE
        public MOUSEINPUT mi;
    }

    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000; // multi-monitor support
    private const uint MOUSEEVENTF_MOVE_NOCOALESCE = 0x2000;

    // ── Wiggle logic ─────────────────────────────────────────────────────────

    private void OnWiggleTick()
    {
        if (_wiggleCount == 0)
            _savedCursorPos = Cursor.Position;

        // Pattern: right 120 → center → down 120 → center (net displacement = 0)
        switch (_wiggleCount)
        {
            case 0: SendMouseAbsolute(_savedCursorPos.X + 120, _savedCursorPos.Y); break;
            case 1: SendMouseAbsolute(_savedCursorPos.X, _savedCursorPos.Y); break;
            case 2: SendMouseAbsolute(_savedCursorPos.X, _savedCursorPos.Y + 120); break;
            case 3: SendMouseAbsolute(_savedCursorPos.X, _savedCursorPos.Y); break;
            default:
                _wiggleTimer.Stop();
                if (!_isRunning) return;
                _remainingSeconds = (int)(_nudHours.Value * 3600 + _nudMinutes.Value * 60 + _nudSeconds.Value);
                UpdateCountdownDisplay();
                _countdownTimer.Start();
                _statusLabel.Text = "● Geri sayım...";
                _statusLabel.ForeColor = Color.Yellow;
                return;
        }
        _wiggleCount++;
    }

    /// <summary>
    /// Moves the cursor via SendInput (updates GetLastInputInfo → Teams stays online).
    /// Supports multi-monitor via MOUSEEVENTF_VIRTUALDESK.
    /// </summary>
    private static void SendMouseAbsolute(int x, int y)
    {
        // Virtual desktop spans all monitors
        var vd = SystemInformation.VirtualScreen;
        x = Math.Clamp(x, vd.Left, vd.Right - 1);
        y = Math.Clamp(y, vd.Top, vd.Bottom - 1);

        // Normalize pixel coords → 0–65535 range required by MOUSEEVENTF_ABSOLUTE
        var nx = (int)((long)(x - vd.Left) * 65535 / Math.Max(vd.Width - 1, 1));
        var ny = (int)((long)(y - vd.Top) * 65535 / Math.Max(vd.Height - 1, 1));

        var input = new INPUT
        {
            type = 0,
            mi = new MOUSEINPUT
            {
                dx = nx,
                dy = ny,
                mouseData = 0,
                dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK | MOUSEEVENTF_MOVE_NOCOALESCE,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        };
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    private void UpdateCountdownDisplay()
    {
        var h = _remainingSeconds / 3600;
        var m = (_remainingSeconds % 3600) / 60;
        var s = _remainingSeconds % 60;
        _countdownLabel.Text = $"{h}h:{m}m:{s}s";
    }
}
