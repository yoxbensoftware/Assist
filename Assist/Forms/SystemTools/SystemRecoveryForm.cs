namespace Assist.Forms.SystemTools;

using Assist.Services;

/// <summary>
/// MDI child form providing safe hardware diagnostics and recovery tools.
/// Displays system info (displays, Bluetooth audio, default audio, power state)
/// and offers Audio Fix, Display Fix, and Power Refresh operations with a
/// color-coded, timestamped log console.
/// </summary>
internal sealed class SystemRecoveryForm : Form
{
    // ── Info labels ───────────────────────────────────────────
    private readonly Label _lblDisplayCount;
    private readonly Label _lblBluetoothAudio;
    private readonly Label _lblDefaultAudio;
    private readonly Label _lblPowerStatus;

    // ── Action buttons ────────────────────────────────────────
    private readonly Button _btnAudioFix;
    private readonly Button _btnDisplayFix;
    private readonly Button _btnPowerRefresh;
    private readonly Button _btnRefreshInfo;
    private readonly Button _btnClearLog;

    // ── Log console ───────────────────────────────────────────
    private readonly RichTextBox _rtbLog;

    public SystemRecoveryForm()
    {
        Text = "System Recovery Tools";
        ClientSize = new Size(1050, 720);

        var p = UITheme.Palette;

        // ── Title ─────────────────────────────────────────────
        var lblTitle = new Label
        {
            Text = "=== SYSTEM RECOVERY TOOLS ===",
            Dock = DockStyle.Top,
            Height = 38,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0),
            Font = new Font("Consolas", 14, FontStyle.Bold),
            ForeColor = p.Accent,
            BackColor = p.Back
        };

        // ── Info Panel (2×2 grid) ─────────────────────────────
        var infoPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 80,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(10, 4, 10, 4),
            BackColor = p.Surface
        };
        infoPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        infoPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        infoPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        infoPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        _lblDisplayCount = MakeInfoLabel("🖥 Ekran Sayısı: —");
        _lblBluetoothAudio = MakeInfoLabel("🎧 Bluetooth Audio: —");
        _lblDefaultAudio = MakeInfoLabel("🔊 Varsayılan Ses: —");
        _lblPowerStatus = MakeInfoLabel("⚡ Güç Durumu: —");

        infoPanel.Controls.Add(_lblDisplayCount, 0, 0);
        infoPanel.Controls.Add(_lblBluetoothAudio, 1, 0);
        infoPanel.Controls.Add(_lblDefaultAudio, 0, 1);
        infoPanel.Controls.Add(_lblPowerStatus, 1, 1);

        // ── Button Panel ──────────────────────────────────────
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(10, 6, 10, 4),
            WrapContents = false,
            BackColor = p.Back
        };

        _btnAudioFix = MakeButton("🔊 Audio Fix", 170);
        _btnDisplayFix = MakeButton("🖥 Display Fix", 170);
        _btnPowerRefresh = MakeButton("⚡ Power Refresh", 180);
        _btnRefreshInfo = MakeButton("🔄 Refresh Info", 160);
        _btnClearLog = MakeButton("🗑 Clear Log", 140);

        _btnAudioFix.Click += async (_, _) => await RunSafeAsync(_btnAudioFix, SystemRecoveryService.RunAudioFixAsync);
        _btnDisplayFix.Click += async (_, _) => await RunSafeAsync(_btnDisplayFix, SystemRecoveryService.RunDisplayFixAsync);
        _btnPowerRefresh.Click += async (_, _) => await RunSafeAsync(_btnPowerRefresh, SystemRecoveryService.RunPowerRefreshAsync);
        _btnRefreshInfo.Click += async (_, _) => await LoadSystemInfoAsync();
        _btnClearLog.Click += (_, _) => _rtbLog.Clear();

        btnPanel.Controls.AddRange([_btnAudioFix, _btnDisplayFix, _btnPowerRefresh, _btnRefreshInfo, _btnClearLog]);

        // ── Separator ─────────────────────────────────────────
        var separator = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = p.Accent
        };

        // ── Log Console ───────────────────────────────────────
        _rtbLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = p.Back,
            ForeColor = p.Text,
            Font = new Font("Consolas", 9.5f),
            BorderStyle = BorderStyle.None,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both
        };

        // Add controls — reverse dock order: Fill first, then Top items last-to-first
        Controls.Add(_rtbLog);
        Controls.Add(separator);
        Controls.Add(btnPanel);
        Controls.Add(infoPanel);
        Controls.Add(lblTitle);

        // Auto-load system info on open; restore log background after UITheme.Apply
        Shown += async (_, _) =>
        {
            _rtbLog.BackColor = UITheme.Palette.Back;
            await LoadSystemInfoAsync();
        };
    }

    // ── UI Helpers ────────────────────────────────────────────

    private static Label MakeInfoLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font("Consolas", 10),
        ForeColor = UITheme.Palette.Text,
        Padding = new Padding(8, 0, 0, 0)
    };

    private static Button MakeButton(string text, int width)
    {
        var p = UITheme.Palette;
        var btn = new Button
        {
            Text = text,
            Size = new Size(width, 34),
            Margin = new Padding(3, 0, 3, 0),
            BackColor = p.Surface2,
            ForeColor = p.Text,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = p.Accent;
        btn.FlatAppearance.BorderSize = 1;
        return btn;
    }

    // ── Operations ────────────────────────────────────────────

    private async Task LoadSystemInfoAsync()
    {
        AppendLog("Sistem bilgileri yükleniyor...", RecoveryLogLevel.Info);
        try
        {
            var info = await SystemRecoveryService.GetSystemInfoAsync();

            _lblDisplayCount.Text = $"🖥 Ekran Sayısı: {info.DisplayCount}";
            _lblBluetoothAudio.Text = info.BluetoothAudioDevices.Count > 0
                ? $"🎧 BT Audio: {string.Join(", ", info.BluetoothAudioDevices)}"
                : "🎧 BT Audio: Yok";
            _lblDefaultAudio.Text = $"🔊 Varsayılan Ses: {info.DefaultAudioDevice}";
            _lblPowerStatus.Text = $"⚡ Güç: {info.PowerStatus}";

            if (!info.IsAdmin)
                AppendLog("Uygulama admin olarak çalışmıyor — bazı işlemler kısıtlı olabilir.", RecoveryLogLevel.Warning);

            AppendLog("Sistem bilgileri başarıyla yüklendi.", RecoveryLogLevel.Success);
        }
        catch (Exception ex)
        {
            AppendLog($"Sistem bilgileri yüklenemedi: {ex.Message}", RecoveryLogLevel.Error);
        }
    }

    private async Task RunSafeAsync(Button btn, Func<Action<string, RecoveryLogLevel>, Task> operation)
    {
        btn.Enabled = false;
        try
        {
            await operation(AppendLog);
        }
        catch (Exception ex)
        {
            AppendLog($"İşlem sırasında beklenmeyen hata: {ex.Message}", RecoveryLogLevel.Error);
        }
        finally
        {
            btn.Enabled = true;
        }
    }

    // ── Log Console ───────────────────────────────────────────

    private void AppendLog(string message, RecoveryLogLevel level)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { Invoke(() => AppendLog(message, level)); }
            catch (ObjectDisposedException) { }
            return;
        }

        string prefix = level switch
        {
            RecoveryLogLevel.Success => "[SUCCESS]",
            RecoveryLogLevel.Warning => "[WARNING]",
            RecoveryLogLevel.Error   => "[ERROR]  ",
            _                        => "[INFO]   "
        };

        Color color = level switch
        {
            RecoveryLogLevel.Success => Color.FromArgb(0, 200, 0),
            RecoveryLogLevel.Warning => Color.FromArgb(255, 200, 0),
            RecoveryLogLevel.Error   => Color.FromArgb(255, 60, 60),
            _                        => UITheme.Palette.Text
        };

        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string line = $"{timestamp}  {prefix}  {message}{Environment.NewLine}";

        _rtbLog.SelectionStart = _rtbLog.TextLength;
        _rtbLog.SelectionLength = 0;
        _rtbLog.SelectionColor = color;
        _rtbLog.AppendText(line);
        _rtbLog.ScrollToCaret();
    }
}
