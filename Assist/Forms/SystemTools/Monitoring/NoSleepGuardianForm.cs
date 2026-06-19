namespace Assist.Forms.SystemTools.Monitoring;

using System.Diagnostics;
using Assist.Models;
using Assist.Services;

internal sealed class NoSleepGuardianForm : Form
{
    private readonly IGuardianLogService _logService;
    private readonly IGuardianConfigService _configService;
    private readonly IPowerGuardService _powerGuardService;
    private readonly IPowerSettingsService _powerSettingsService;
    private readonly ISystemHealthMonitor _healthMonitor;
    private readonly INotificationService _notificationService;
    private readonly CancellationTokenSource _disposeCts = new();

    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 15_000 };
    private readonly System.Windows.Forms.Timer _heartbeatTimer = new();
    private readonly System.Windows.Forms.Timer _countdownTimer = new() { Interval = 1000 };

    private readonly Button _btnGuardToggle = new();
    private readonly Button _btnApplyPersistent = new();
    private readonly Button _btnRestore = new();
    private readonly Button _btnTestNotification = new();
    private readonly Button _btnRefresh = new();
    private readonly Button _btnDimScreens = new();
    private readonly NumericUpDown _numDurationDays = new();
    private readonly Label _lblCountdown = new();

    private readonly Label _lblGuard = new();
    private readonly Label _lblPower = new();
    private readonly Label _lblBattery = new();
    private readonly Label _lblPendingReboot = new();
    private readonly Label _lblNetwork = new();
    private readonly Label _lblDisk = new();
    private readonly Label _lblSleepTimeout = new();
    private readonly Label _lblHibernateTimeout = new();
    private readonly Label _lblHeartbeat = new();
    private readonly Label _lblRemoteConfig = new();
    private readonly Label _lblActionStatus = new();
    private readonly TextBox _txtEvents = new();

    private bool _refreshInProgress;
    private bool _heartbeatInProgress;
    private bool _isBusy;
    private DateTime? _guardEndsAtUtc;

    public NoSleepGuardianForm()
    {
        _logService = NoSleepGuardianServiceFactory.CreateLogService();
        _configService = NoSleepGuardianServiceFactory.CreateConfigService(_logService);
        _powerGuardService = NoSleepGuardianServiceFactory.CreatePowerGuardService(_logService);
        _powerSettingsService = NoSleepGuardianServiceFactory.CreatePowerSettingsService(_logService);
        _notificationService = NoSleepGuardianServiceFactory.CreateNotificationService(_configService, _logService);
        _healthMonitor = NoSleepGuardianServiceFactory.CreateHealthMonitor(_configService, _powerSettingsService, _logService);

        Text = "NoSleep Guardian";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 620);
        Size = new Size(980, 700);

        BuildUi();
        WireEvents();
        ConfigureHeartbeatTimer();
        ApplyTheme();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 5,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 304));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "NoSleep Guardian",
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 15, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var controls = BuildControlsPanel();
        var statusGrid = BuildStatusGrid();

        _lblActionStatus.Dock = DockStyle.Fill;
        _lblActionStatus.TextAlign = ContentAlignment.MiddleLeft;
        _lblActionStatus.Text = "Hazır.";

        _txtEvents.Dock = DockStyle.Fill;
        _txtEvents.Multiline = true;
        _txtEvents.ReadOnly = true;
        _txtEvents.ScrollBars = ScrollBars.Vertical;
        _txtEvents.BorderStyle = BorderStyle.FixedSingle;

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(controls, 0, 1);
        root.Controls.Add(statusGrid, 0, 2);
        root.Controls.Add(_lblActionStatus, 0, 3);
        root.Controls.Add(_txtEvents, 0, 4);
        Controls.Add(root);
    }

    private TableLayoutPanel BuildControlsPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
        };

        ConfigureButton(_btnGuardToggle, "Guard: OFF", 150);
        ConfigureButton(_btnApplyPersistent, "Kalıcı uykusuz mod uygula", 230);
        ConfigureButton(_btnRestore, "Ayarları geri al", 190);
        ConfigureButton(_btnTestNotification, "Test bildirimi gönder", 190);
        ConfigureButton(_btnRefresh, "Durumu yenile", 140);
        ConfigureButton(_btnDimScreens, "Ekranları karart", 160);
        _btnDimScreens.Visible = false;
        buttons.Controls.AddRange([
            _btnGuardToggle,
            _btnApplyPersistent,
            _btnRestore,
            _btnTestNotification,
            _btnRefresh,
            _btnDimScreens]);

        var durationPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        var lblDuration = new Label
        {
            Text = "Guard süresi (gün, 0 = sınırsız)",
            AutoSize = false,
            Width = 290,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _numDurationDays.Minimum = 0;
        _numDurationDays.Maximum = 3650;
        _numDurationDays.Value = 0;
        _numDurationDays.Width = 110;
        _numDurationDays.ThousandsSeparator = true;
        _lblCountdown.Text = "Kalan: sınırsız";
        _lblCountdown.AutoSize = false;
        _lblCountdown.Width = 340;
        _lblCountdown.TextAlign = ContentAlignment.MiddleLeft;
        _lblCountdown.Margin = new Padding(16, 7, 0, 0);
        durationPanel.Controls.Add(lblDuration);
        durationPanel.Controls.Add(_numDurationDays);
        durationPanel.Controls.Add(_lblCountdown);

        panel.Controls.Add(buttons, 0, 0);
        panel.Controls.Add(durationPanel, 0, 1);
        return panel;
    }

    private TableLayoutPanel BuildStatusGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 10,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddStatusRow(grid, 0, "Guard", _lblGuard);
        AddStatusRow(grid, 1, "Power", _lblPower);
        AddStatusRow(grid, 2, "Battery", _lblBattery);
        AddStatusRow(grid, 3, "Pending Reboot", _lblPendingReboot);
        AddStatusRow(grid, 4, "Network", _lblNetwork);
        AddStatusRow(grid, 5, "System Disk", _lblDisk);
        AddStatusRow(grid, 6, "Sleep Timeout", _lblSleepTimeout);
        AddStatusRow(grid, 7, "Hibernate Timeout", _lblHibernateTimeout);
        AddStatusRow(grid, 8, "Last Heartbeat", _lblHeartbeat);
        AddStatusRow(grid, 9, "Remote Config", _lblRemoteConfig);

        return grid;
    }

    private static void ConfigureButton(Button button, string text, int width)
    {
        button.Text = text;
        button.Width = width;
        button.Height = 34;
        button.Margin = new Padding(0, 4, 8, 4);
        button.Cursor = Cursors.Hand;
    }

    private static void AddStatusRow(TableLayoutPanel grid, int row, string name, Label valueLabel)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));

        var nameLabel = new Label
        {
            Text = name,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 4, 0),
            Font = new Font("Consolas", 10, FontStyle.Bold),
        };

        valueLabel.Text = "--";
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        valueLabel.Padding = new Padding(8, 0, 4, 0);
        valueLabel.AutoEllipsis = true;

        grid.Controls.Add(nameLabel, 0, row);
        grid.Controls.Add(valueLabel, 1, row);
    }

    private void WireEvents()
    {
        _btnGuardToggle.Click += async (_, _) => await ToggleGuardAsync();
        _btnApplyPersistent.Click += async (_, _) => await ApplyPersistentModeAsync();
        _btnRestore.Click += async (_, _) => await RestoreSettingsAsync();
        _btnTestNotification.Click += async (_, _) => await SendTestNotificationAsync();
        _btnRefresh.Click += async (_, _) => await RefreshSnapshotAsync(sendAlerts: false);
        _btnDimScreens.Click += (_, _) => DimScreensUntilUserInput();
        _refreshTimer.Tick += async (_, _) => await OnRefreshTimerTickAsync();
        _heartbeatTimer.Tick += async (_, _) => await SendHeartbeatNowAsync();
        _countdownTimer.Tick += async (_, _) => await OnCountdownTimerTickAsync();
        ThemeService.ThemeChanged += OnThemeChanged;
        Load += async (_, _) =>
        {
            AppendEvent("NoSleep Guardian açıldı.");
            await RefreshSnapshotAsync(sendAlerts: false);
        };
        FormClosing += OnFormClosing;
        FormClosed += OnFormClosed;
    }

    private void ConfigureHeartbeatTimer()
    {
        var minutes = Math.Max(1, _configService.Current.HeartbeatIntervalMinutes);
        _heartbeatTimer.Interval = checked(minutes * 60_000);
    }

    private async Task ToggleGuardAsync()
    {
        if (_powerGuardService.IsActive)
            await StopGuardAsync("Guard kullanıcı tarafından kapatıldı.");
        else
            await StartGuardAsync();
    }

    private async Task StartGuardAsync()
    {
        if (_powerGuardService.IsActive)
            return;

        if (!_powerGuardService.Start())
        {
            SetActionStatus("Guard başlatılamadı. Log dosyasını kontrol edin.", isError: true);
            return;
        }

        var durationDays = (int)_numDurationDays.Value;
        _guardEndsAtUtc = durationDays > 0 ? DateTime.UtcNow.AddDays(durationDays) : null;
        _refreshTimer.Start();
        _heartbeatTimer.Start();
        _countdownTimer.Start();
        UpdateCountdownDisplay();

        AppendEvent(durationDays > 0
            ? $"Guard başlatıldı. Süre: {durationDays} gün."
            : "Guard başlatıldı. Süre: sınırsız.");
        SetActionStatus("Guard aktif. Uygulama açık kaldığı sürece sistem uykusu engellenir.", isError: false);
        await RefreshSnapshotAsync(sendAlerts: false);
        await SendHeartbeatNowAsync();
    }

    private async Task StopGuardAsync(string reason)
    {
        if (!_powerGuardService.IsActive)
            return;

        _powerGuardService.Stop();
        _guardEndsAtUtc = null;
        _heartbeatTimer.Stop();
        _countdownTimer.Stop();
        UpdateCountdownDisplay();
        AppendEvent(reason);
        SetActionStatus("Guard durduruldu. Process bazlı sleep prevention kaldırıldı.", isError: false);
        await RefreshSnapshotAsync(sendAlerts: false);
    }

    private async Task ApplyPersistentModeAsync()
    {
        if (!EnsureAdministrator())
            return;

        var confirm = MessageBox.Show(
            "Bu işlem aktif Windows güç planında AC/prize takılı profilinin sleep ve hibernate timeout değerlerini kalıcı olarak kapatır.\n\nMevcut değerler önce yedeklenecek ve 'Ayarları geri al' ile geri yüklenebilecek.\n\nDevam edilsin mi?",
            "Kalıcı Uykusuz Mod",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
            return;

        var batteryChoice = MessageBox.Show(
            "Batarya profilini de değiştirmek ister misiniz?\n\nÖnerilen güvenli seçenek: Hayır. Batarya profilini değiştirmek pilin hızlı tükenmesine neden olabilir.",
            "Batarya Profili",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);

        if (batteryChoice == DialogResult.Cancel)
            return;

        SetBusy(true);
        try
        {
            var includeBattery = batteryChoice == DialogResult.Yes;
            var result = await _powerSettingsService.ApplyPersistentNoSleepAsync(includeBattery, _disposeCts.Token);
            if (result.Success && includeBattery)
                SetDimScreensButtonVisible(true);

            AppendEvent(result.Message);
            SetActionStatus(result.Message, !result.Success);
            await RefreshSnapshotAsync(sendAlerts: false);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RestoreSettingsAsync()
    {
        if (!EnsureAdministrator())
            return;

        var confirm = MessageBox.Show(
            "NoSleep Guardian tarafından alınan güç ayarı yedeği geri yüklenecek.\n\nDevam edilsin mi?",
            "Ayarları Geri Al",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
            return;

        SetBusy(true);
        try
        {
            var result = await _powerSettingsService.RestoreFromBackupAsync(_disposeCts.Token);
            if (result.Success)
                SetDimScreensButtonVisible(false);

            AppendEvent(result.Message);
            SetActionStatus(result.Message, !result.Success);
            await RefreshSnapshotAsync(sendAlerts: false);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task SendTestNotificationAsync()
    {
        SetBusy(true);
        try
        {
            var result = await _notificationService.SendTestAsync(_disposeCts.Token);
            AppendEvent($"Test bildirimi: {result.Message}");
            SetActionStatus(result.Message, !result.IsSuccessful);
            await RefreshSnapshotAsync(sendAlerts: false);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task OnRefreshTimerTickAsync()
    {
        if (_guardEndsAtUtc is not null && DateTime.UtcNow >= _guardEndsAtUtc.Value)
        {
            await StopGuardAsync("Guard süresi dolduğu için durduruldu.");
            return;
        }

        await RefreshSnapshotAsync(sendAlerts: _powerGuardService.IsActive);
    }

    private async Task OnCountdownTimerTickAsync()
    {
        if (!_powerGuardService.IsActive)
        {
            _countdownTimer.Stop();
            UpdateCountdownDisplay();
            return;
        }

        UpdateCountdownDisplay();

        if (_guardEndsAtUtc is not null && DateTime.UtcNow >= _guardEndsAtUtc.Value)
            await StopGuardAsync("Guard süresi dolduğu için kapatıldı.");
    }

    private async Task SendHeartbeatNowAsync()
    {
        if (_heartbeatInProgress || !_powerGuardService.IsActive)
            return;

        _heartbeatInProgress = true;
        try
        {
            var snapshot = await _healthMonitor.GetSnapshotAsync(
                _powerGuardService.IsActive,
                _notificationService.Heartbeat,
                _disposeCts.Token);

            var result = await _notificationService.SendHeartbeatAsync(snapshot, _disposeCts.Token);
            AppendEvent($"Heartbeat: {result.Message}");
            await RefreshSnapshotAsync(sendAlerts: false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _heartbeatInProgress = false;
        }
    }

    private async Task RefreshSnapshotAsync(bool sendAlerts)
    {
        if (_refreshInProgress)
            return;

        _refreshInProgress = true;
        try
        {
            var snapshot = await _healthMonitor.GetSnapshotAsync(
                _powerGuardService.IsActive,
                _notificationService.Heartbeat,
                _disposeCts.Token);
            UpdateSnapshotUi(snapshot);

            if (sendAlerts)
            {
                foreach (var issue in snapshot.Issues)
                {
                    var result = await _notificationService.SendAlertAsync(
                        issue.Key,
                        issue.Title,
                        issue.Message,
                        issue.Severity,
                        _disposeCts.Token);

                    if (!result.SkippedByCooldown)
                        AppendEvent($"Alert: {issue.Title} - {result.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logService.Error("NoSleep Guardian refresh failed.", ex);
            SetActionStatus("Durum bilgisi alınamadı. Log dosyasını kontrol edin.", isError: true);
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private void UpdateSnapshotUi(SystemHealthSnapshot snapshot)
    {
        var guardText = snapshot.GuardActive ? "ON" : "OFF";
        if (snapshot.GuardActive && _guardEndsAtUtc is not null)
        {
            var remaining = _guardEndsAtUtc.Value - DateTime.UtcNow;
            guardText += remaining > TimeSpan.Zero ? $" ({FormatRemaining(remaining)} kaldı)" : " (süre doluyor)";
        }

        SetValue(_lblGuard, guardText, snapshot.GuardActive ? ValueState.Good : ValueState.Neutral);
        SetValue(_lblPower, snapshot.AcPowerOnline ? "Online" : "Battery", snapshot.AcPowerOnline ? ValueState.Good : ValueState.Warning);
        SetValue(_lblBattery, FormatBattery(snapshot), snapshot.BatteryLow ? ValueState.Critical : ValueState.Neutral);
        SetValue(_lblPendingReboot, snapshot.PendingReboot ? "Yes" : "No", snapshot.PendingReboot ? ValueState.Warning : ValueState.Good);
        SetValue(_lblNetwork, snapshot.NetworkAvailable ? $"OK - {snapshot.NetworkMessage}" : $"Failed - {snapshot.NetworkMessage}", snapshot.NetworkAvailable ? ValueState.Good : ValueState.Warning);
        SetValue(_lblDisk, $"{snapshot.SystemDiskName} boş {snapshot.SystemDiskFreeGb:F1} GB (%{snapshot.SystemDiskFreePercent:F1})", snapshot.SystemDiskCritical ? ValueState.Critical : ValueState.Good);
        SetValue(_lblSleepTimeout, FormatSleep(snapshot.PowerSettings), ValueState.Neutral);
        SetValue(_lblHibernateTimeout, FormatHibernate(snapshot.PowerSettings), ValueState.Neutral);
        SetValue(_lblHeartbeat, FormatHeartbeat(snapshot.Heartbeat), snapshot.Heartbeat.LastHeartbeatSucceeded == false ? ValueState.Warning : ValueState.Neutral);
        SetValue(_lblRemoteConfig, _configService.GetMaskedRemoteSummary(), ValueState.Neutral);
        UpdateGuardToggleAppearance(snapshot.GuardActive);
        UpdateCountdownDisplay();
        SetDimScreensButtonVisible(IsAllProfilesNoSleep(snapshot.PowerSettings));
    }

    private bool EnsureAdministrator()
    {
        if (_powerSettingsService.IsAdministrator())
            return true;

        var result = MessageBox.Show(
            "Bu işlem Windows güç ayarlarını değiştireceği için yönetici yetkisi gerektirir.\n\nAssist yönetici olarak yeniden başlatılsın mı?",
            "Yönetici Yetkisi Gerekli",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
            return false;

        try
        {
            Process.Start(new ProcessStartInfo(Application.ExecutablePath)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            Close();
        }
        catch (Exception ex)
        {
            _logService.Error("Elevation request failed.", ex);
            SetActionStatus("Yönetici yetkisi istenemedi. Uygulamayı yönetici olarak açın.", isError: true);
        }

        return false;
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        _btnApplyPersistent.Enabled = !busy;
        _btnRestore.Enabled = !busy;
        _btnTestNotification.Enabled = !busy;
        _btnRefresh.Enabled = !busy;
        _btnGuardToggle.Enabled = !busy;
        _btnDimScreens.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void UpdateGuardToggleAppearance(bool active)
    {
        _btnGuardToggle.Text = active ? "Guard: ON" : "Guard: OFF";
        _btnGuardToggle.BackColor = active ? Color.FromArgb(70, 24, 24) : UITheme.Palette.Surface2;
        _btnGuardToggle.ForeColor = active ? Color.White : UITheme.Palette.Text;
        _btnGuardToggle.FlatAppearance.BorderColor = active ? UITheme.Palette.Negative : UITheme.Palette.Accent;
    }

    private void UpdateCountdownDisplay()
    {
        if (!_powerGuardService.IsActive)
        {
            _lblCountdown.Text = "Kalan: kapalı";
            return;
        }

        if (_guardEndsAtUtc is null)
        {
            _lblCountdown.Text = "Kalan: sınırsız";
            return;
        }

        var remaining = _guardEndsAtUtc.Value - DateTime.UtcNow;
        _lblCountdown.Text = remaining > TimeSpan.Zero
            ? $"Kalan: {FormatRemaining(remaining)}"
            : "Kalan: süre doluyor";
    }

    private void DimScreensUntilUserInput()
    {
        AppendEvent("Ekran karartma açıldı. İlk mouse hareketi veya tuş ile kapanır.");
        ScreenDimOverlayForm.ShowUntilUserInput();
    }

    private void SetDimScreensButtonVisible(bool visible)
    {
        _btnDimScreens.Visible = visible;
        _btnDimScreens.Enabled = visible && !_isBusy;
    }

    private void SetActionStatus(string message, bool isError)
    {
        _lblActionStatus.Text = message;
        _lblActionStatus.ForeColor = isError ? UITheme.Palette.Negative : UITheme.Palette.Positive;
    }

    private void AppendEvent(string message)
    {
        if (_txtEvents.IsDisposed)
            return;

        _txtEvents.AppendText($"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");
    }

    private void ApplyTheme()
    {
        UITheme.Apply(this);
        var p = UITheme.Palette;
        _txtEvents.BackColor = p.Surface;
        _txtEvents.ForeColor = p.Text;
        _lblActionStatus.ForeColor = p.Muted;
        foreach (var button in new[] { _btnGuardToggle, _btnApplyPersistent, _btnRestore, _btnTestNotification, _btnRefresh, _btnDimScreens })
            UITheme.Apply(button);
        UpdateGuardToggleAppearance(_powerGuardService.IsActive);
        _lblCountdown.ForeColor = p.Text;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (!IsDisposed)
            ApplyTheme();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_powerGuardService.IsActive)
            return;

        var result = MessageBox.Show(
            "NoSleep Guard aktif. Bu pencere kapatılırsa process bazlı sleep prevention kaldırılır.\n\nGuard durdurulup pencere kapatılsın mı?",
            "NoSleep Guardian",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        _powerGuardService.Stop();
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        ThemeService.ThemeChanged -= OnThemeChanged;
        _disposeCts.Cancel();
        _refreshTimer.Stop();
        _heartbeatTimer.Stop();
        _countdownTimer.Stop();
        _refreshTimer.Dispose();
        _heartbeatTimer.Dispose();
        _countdownTimer.Dispose();
        _disposeCts.Dispose();
        if (_notificationService is IDisposable disposable)
            disposable.Dispose();
    }

    private static string FormatBattery(SystemHealthSnapshot snapshot)
    {
        if (!snapshot.HasBattery)
            return "No battery";

        return snapshot.BatteryPercent is int percent
            ? $"{percent}%"
            : "Unknown";
    }

    private static string FormatSleep(PowerSettingsSnapshot? settings)
    {
        if (settings is null)
            return "Okunamadı";

        return $"AC {FormatTimeout(settings.SleepAcSeconds)} / Battery {FormatTimeout(settings.SleepDcSeconds)}";
    }

    private static string FormatHibernate(PowerSettingsSnapshot? settings)
    {
        if (settings is null)
            return "Okunamadı";

        return $"AC {FormatTimeout(settings.HibernateAcSeconds)} / Battery {FormatTimeout(settings.HibernateDcSeconds)}";
    }

    private static string FormatTimeout(uint seconds)
    {
        if (seconds == 0)
            return "Never";

        var time = TimeSpan.FromSeconds(seconds);
        if (time.TotalHours >= 1)
            return $"{(int)time.TotalHours}h {time.Minutes}m";

        return $"{Math.Max(1, (int)Math.Round(time.TotalMinutes))}m";
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        return remaining.Days > 0
            ? $"{remaining.Days}g {remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
            : $"{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    private static bool IsAllProfilesNoSleep(PowerSettingsSnapshot? settings) =>
        settings is not null &&
        settings.SleepAcSeconds == 0 &&
        settings.SleepDcSeconds == 0 &&
        settings.HibernateAcSeconds == 0 &&
        settings.HibernateDcSeconds == 0;

    private static string FormatHeartbeat(HeartbeatState state)
    {
        if (state.LastHeartbeatUtc is null)
            return state.Message;

        var local = state.LastHeartbeatUtc.Value.ToLocalTime();
        var status = state.LastHeartbeatSucceeded switch
        {
            true => "OK",
            false => "Failed",
            _ => "Not configured",
        };
        return $"{local:yyyy-MM-dd HH:mm:ss} - {status} - {state.Message}";
    }

    private enum ValueState
    {
        Neutral,
        Good,
        Warning,
        Critical,
    }

    private static void SetValue(Label label, string text, ValueState state)
    {
        label.Text = text;
        var p = UITheme.Palette;
        label.ForeColor = state switch
        {
            ValueState.Good => p.Positive,
            ValueState.Warning => Color.Orange,
            ValueState.Critical => p.Negative,
            _ => p.Text,
        };
    }
}
