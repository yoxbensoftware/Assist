namespace Assist;

using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using Assist.Forms.ClipboardTools;
using Assist.Forms.Core;
using Assist.Forms.DeveloperTools;
using Assist.Forms.DeveloperTools.Converters;
using Assist.Forms.DeveloperTools.Formatters;
using Assist.Forms.DeveloperTools.Generators;
using Assist.Forms.DeveloperTools.Testing;
using Assist.Forms.Games;
using Assist.Forms.Online;
using Assist.Forms.Online.Finance;
using Assist.Forms.Online.News;
using Assist.Forms.Online.Queries;
using Assist.Forms.Online.Reference;
using Assist.Forms.Passwords;
using Assist.Forms.Productivity;
using Assist.Forms.SystemTools;
using Assist.Forms.SystemTools.Maintenance;
using Assist.Forms.SystemTools.Monitoring;
using Assist.Forms.SystemTools.Network;
using Assist.Forms.SystemTools.Security;
using Assist.Forms.SystemTools.Troubleshooting;
using Assist.Models;
using Assist.Services;

internal partial class MainMDIForm : Form
{
    private ClipboardHistoryService? _clipboardHistory;

    private readonly Dictionary<string, Func<Form>> _sessionFormFactories;
    private IReadOnlyList<QuickLaunchItem> _quickLaunchItems = [];
    private ToolStripMenuItem? _lowPowerMenuItem;
    private bool _sessionRestored;

    // Dashboard fields
    private Panel? _dashboardPanel;
    private Label? _lblClock;
    private Label? _lblCpuRam;
    private Label? _lblDisk;
    private Label? _lblBattery;
    private Label? _lblUptime;
    private Label? _lblAppStats;
    private Label? _lblWeather;
    private Label? _lblCurrency;
    private Label? _lblCrypto;
    private Label? _lblIpInfo;
    private Label? _lblPing;
    private System.Windows.Forms.Timer? _fastTimer;   // 2s — clock, CPU/RAM
    private System.Windows.Forms.Timer? _mediumTimer;  // 30s — disk, battery, uptime, ping, app stats
    private System.Windows.Forms.Timer? _slowTimer;    // 5min — weather, currency, crypto, IP
    private bool _isClosing;
    private int _fastRefreshInProgress;
    private int _mediumRefreshInProgress;
    private int _slowRefreshInProgress;
    private bool _ozLauncherInProgress;

    // Current-process monitor
    private readonly Process _selfProcess = Process.GetCurrentProcess();
    private TimeSpan _lastCpuTime = TimeSpan.Zero;
    private DateTime _lastCpuCheck = DateTime.MinValue;
    private long _lastNetRx;
    private long _lastNetTx;
    private DateTime _lastNetCheck = DateTime.MinValue;
    private double _cachedRxKbPerSec;
    private double _cachedTxKbPerSec;
    private int _cachedThreadCount;
    private DateTime _lastThreadCountCheck = DateTime.MinValue;
    private Label? _lblProcBar;

    // Dashboard panel refs for theme refresh
    private Panel? _topBorderPanel;
    private Panel? _procBarPanel;
    private Label? _lblVersion;

    // DWM dark title bar
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // Watermark fields
    private MdiClient? _mdiClient;
    private Rectangle _rcAssist, _rcBy, _rcOz;
    private Color _watermarkAccent;
    private Color _watermarkMuted;
    private Size _watermarkLayoutForClientSize = Size.Empty;
    private Size _sAssistCached;
    private Size _sByCached;
    private Size _sOzCached;
    private static readonly Font WatermarkLargeFont = new("Consolas", 60, FontStyle.Bold);
    private static readonly Font WatermarkSmallFont = new("Consolas", 22);

    // Move/size loop tracking — used to throttle UI work while the window is being dragged or resized
    private const int WM_ENTERSIZEMOVE = 0x0231;
    private const int WM_EXITSIZEMOVE = 0x0232;
    private bool _isInSizeMove;
    private bool _fastTimerWasRunning;
    private bool _mediumTimerWasRunning;

    public MainMDIForm()
    {
        InitializeComponent();
        _sessionFormFactories = CreateSessionFormFactories();
        _quickLaunchItems = CreateQuickLaunchItems();
        IsMdiContainer = true;
        ThemeService.ThemeChanged += OnThemeChanged;
        AppSettingsService.SettingsChanged += OnAppSettingsChanged;
        FormClosing += OnFormClosing;
        FormClosed += OnFormClosed;
        InitializeMenu();
        ApplyTheme();
        EnsureClipboardHistory();
        LoadIcon();
        if (AppSettingsService.Current.DashboardEnabled)
            InitializeDashboardPanel();
        InitializeWatermark();
        HandleCreated += (_, _) => ApplyDarkTitleBar(this);
        Shown += async (_, _) =>
        {
            RestoreSessionIfNeeded();
            await CheckForUpdateAsync(silent: true);
        };
    }

    /// <summary>
    /// Workaround for a known WinForms MDI bug where ToolStripManager's
    /// internal WeakRefCollection is modified while being enumerated
    /// during keyboard processing (e.g. ComboBox key events in child forms).
    /// </summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        try
        {
            if (keyData == (Keys.Control | Keys.K))
            {
                ShowQuickLauncher();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.D))
            {
                ShowMdiChild(new DiagnosticsForm(this));
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Pause dashboard UI-thread timers while Windows is in its modal move/size loop.
    /// WinForms timers keep firing inside that loop and would otherwise interleave with
    /// window dragging, causing visible stutter.
    /// </summary>
    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_ENTERSIZEMOVE:
                if (!_isInSizeMove)
                {
                    _isInSizeMove = true;
                    _fastTimerWasRunning = _fastTimer?.Enabled == true;
                    _mediumTimerWasRunning = _mediumTimer?.Enabled == true;
                    _fastTimer?.Stop();
                    _mediumTimer?.Stop();
                }
                break;

            case WM_EXITSIZEMOVE:
                if (_isInSizeMove)
                {
                    _isInSizeMove = false;
                    if (_fastTimerWasRunning) _fastTimer?.Start();
                    if (_mediumTimerWasRunning) _mediumTimer?.Start();
                    // Single catch-up refresh so the dashboard looks current immediately after the drag ends
                    if (!_isClosing && !IsDisposed && AppSettingsService.Current.DashboardEnabled)
                    {
                        try { _ = RunFastRefreshAsync(); }
                        catch { /* refresh on resume is best-effort */ }
                    }
                }
                break;
        }

        base.WndProc(ref m);
    }

    private void LoadIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "assist_icon.ico");
            if (File.Exists(iconPath))
                Icon = new Icon(iconPath);
        }
        catch
        {
            // Icon load failed, continue without icon
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _isClosing = true;
        SaveSession();
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        _isClosing = true;
        ThemeService.ThemeChanged -= OnThemeChanged;
        AppSettingsService.SettingsChanged -= OnAppSettingsChanged;
        _fastTimer?.Stop();
        _fastTimer?.Dispose();
        _mediumTimer?.Stop();
        _mediumTimer?.Dispose();
        _slowTimer?.Stop();
        _slowTimer?.Dispose();
        _clipboardHistory?.Dispose();
        _clipboardHistory = null;
        _selfProcess.Dispose();
    }

    private void InitializeMenu()
    {
        var menuStrip = new MenuStrip
        {
            RenderMode = ToolStripRenderMode.System
        };

        // Main menus
        menuStrip.Items.Add(CreateAssistMenu());
        menuStrip.Items.Add(CreatePasswordMenu());
        menuStrip.Items.Add(CreateSystemToolsMenu());
        menuStrip.Items.Add(CreateOnlineMenu());
        menuStrip.Items.Add(CreateTodoMenuItem());
        menuStrip.Items.Add(CreateThemeMenu());
        menuStrip.Items.Add(CreateDeveloperToolsMenu());
        menuStrip.Items.Add(CreateClipboardMenu());
        menuStrip.Items.Add(CreateGamesMenu());
        menuStrip.Items.Add(CreateWindowMenu());

        // Right-aligned items
        menuStrip.Items.Add(new ToolStripLabel("Oz") { Alignment = ToolStripItemAlignment.Right, ForeColor = UITheme.Palette.Accent });
        menuStrip.Items.Add(CreateMenuItem("Hakkında", ShowAbout, ToolStripItemAlignment.Right));
        var updateItem = CreateAsyncMenuItem("Güncelleme Kontrol", () => CheckForUpdateAsync(silent: false));
        updateItem.Alignment = ToolStripItemAlignment.Right;
        menuStrip.Items.Add(updateItem);

        MainMenuStrip = menuStrip;
        Controls.Add(menuStrip);
    }

    private ToolStripMenuItem CreateAssistMenu()
    {
        var menu = new ToolStripMenuItem("Assist");

        menu.DropDownItems.Add(CreateMenuItem("Hızlı Başlatıcı\tCtrl+K", ShowQuickLauncher));
        menu.DropDownItems.Add(CreateMenuItem("Diagnostics\tCtrl+Shift+D", () => ShowMdiChild(new DiagnosticsForm(this))));
        menu.DropDownItems.Add(CreateMenuItem("Genel Ayarlar", ShowAppSettings));
        menu.DropDownItems.Add(new ToolStripSeparator());

        _lowPowerMenuItem = CreateMenuItem("Low Power Mode", ToggleLowPowerMode);
        _lowPowerMenuItem.Checked = AppSettingsService.Current.LowPowerMode;
        menu.DropDownItems.Add(_lowPowerMenuItem);

        return menu;
    }

    private ToolStripMenuItem CreatePasswordMenu()
    {
        var menu = new ToolStripMenuItem("Şifreler");

        // Password management
        menu.DropDownItems.Add(CreateMenuItem("Şifre Ekle", () => ShowMdiChild(new PasswordEntryForm())));
        menu.DropDownItems.Add(CreateMenuItem("Şifreleri Gör", () => ShowMdiChild(new PasswordListForm())));

        menu.DropDownItems.Add(new ToolStripSeparator());

        // Password tools
        menu.DropDownItems.Add(CreateMenuItem("Şifre Üret", () => ShowMdiChild(new PasswordGeneratorForm())));

        return menu;
    }

    private ToolStripMenuItem CreateThemeMenu()
    {
        var menu = new ToolStripMenuItem("Tema");
        menu.DropDownItems.Add(CreateMenuItem("Tema Seçimi...", ShowThemeSelection));
        menu.DropDownItems.Add(new ToolStripSeparator());

        foreach (var (theme, name) in ThemeService.GetThemeOptions())
        {
            var capturedTheme = theme;
            menu.DropDownItems.Add(CreateMenuItem(name, () => ApplyThemeSelection(capturedTheme)));
        }

        return menu;
    }

    private ToolStripMenuItem CreateSystemToolsMenu()
    {
        var menu = new ToolStripMenuItem("Sistem Araçları");

        // ── 📊 İzleme ────────────────────────────────
        var monitoring = new ToolStripMenuItem("📊 İzleme");
        monitoring.DropDownItems.Add(CreateMenuItem("Sistem Bilgisi", () => ShowMdiChild(new SystemInfoForm())));
        monitoring.DropDownItems.Add(CreateMenuItem("Performance Monitor", () => ShowMdiChild(new PerformanceMonitorForm())));
        monitoring.DropDownItems.Add(CreateMenuItem("NoSleep Guardian", () => ShowMdiChild(new NoSleepGuardianForm())));
        monitoring.DropDownItems.Add(CreateMenuItem("Bağlantı Monitörü", () =>
        {
            var existing = Application.OpenForms.OfType<ConnectionMonitorForm>().FirstOrDefault();
            if (existing is not null) { existing.BringToFront(); return; }
            new ConnectionMonitorForm().Show();
        }));
        menu.DropDownItems.Add(monitoring);

        // ── 🔧 Bakım & Yönetim ──────────────────────
        var maintenance = new ToolStripMenuItem("🔧 Bakım & Yönetim");
        maintenance.DropDownItems.Add(CreateMenuItem("Startup Manager", () => ShowMdiChild(new StartupManagerForm())));
        maintenance.DropDownItems.Add(CreateMenuItem("Disk Temizleyici", () => ShowMdiChild(new DiskCleanerForm())));
        maintenance.DropDownItems.Add(CreateMenuItem("Hosts File Editor", () => ShowMdiChild(new HostsFileEditorForm())));
        menu.DropDownItems.Add(maintenance);

        // ── 🛠 Sorun Giderme ─────────────────────────
        var troubleshoot = new ToolStripMenuItem("🛠 Sorun Giderme");
        troubleshoot.DropDownItems.Add(CreateMenuItem("Donanım Sorun Giderici", () => ShowMdiChild(new HardwareDiagnosticsForm())));
        troubleshoot.DropDownItems.Add(CreateMenuItem("Sistem Kurtarma", () => ShowMdiChild(new SystemRecoveryForm())));
        menu.DropDownItems.Add(troubleshoot);

        // ── 🌐 Ağ Araçları ───────────────────────────
        var network = new ToolStripMenuItem("🌐 Ağ Araçları");
        network.DropDownItems.Add(CreateMenuItem("Speed Test", () => ShowMdiChild(new SpeedTestForm())));
        network.DropDownItems.Add(CreateMenuItem("Ağ Bağlantı Tarayıcı", () => ShowMdiChild(new NetworkScannerForm())));
        network.DropDownItems.Add(CreateAsyncMenuItem("DNS Reset", RunDnsResetAsync));
        network.DropDownItems.Add(CreateMenuItem("Wi-Fi Şifreleri", () => ShowMdiChild(new WifiPasswordForm())));
        menu.DropDownItems.Add(network);

        // ── 🔒 Güvenlik ──────────────────────────────
        var security = new ToolStripMenuItem("🔒 Güvenlik");
        security.DropDownItems.Add(CreateMenuItem("Tehdit Tarayıcı", () => ShowMdiChild(new ThreatScannerForm())));
        menu.DropDownItems.Add(security);

        menu.DropDownItems.Add(new ToolStripSeparator());

        menu.DropDownItems.Add(CreateMenuItem("🖱 Wiggle Mouse", () => ShowMdiChild(new WiggleMouseForm())));

        return menu;
    }

    private ToolStripMenuItem CreateOnlineMenu()
    {
        var menu = new ToolStripMenuItem("Online İşlemler");

        // ── 📰 Haberler ──────────────────────────────
        var news = new ToolStripMenuItem("📰 Haberler");
        news.DropDownItems.Add(CreateAsyncMenuItem("TR - Top 30", () => ShowNewsAsync(() => new NewsService().GetTopTrAsync(30), "TR - En Önemli Haberler (Top 30)")));
        news.DropDownItems.Add(CreateAsyncMenuItem("Global - Top 30 (Türkçe)", () => ShowNewsAsync(() => new NewsService().GetTopGlobalAsync(30), "Global - Top 30 (Türkçe)", translateToTurkish: true)));
        news.DropDownItems.Add(CreateAsyncMenuItem("Teknoloji - Top 30", () => ShowNewsAsync(() => new NewsService().GetTopTechAsync(30), "Teknoloji - Top 30 (Türkçe)", translateToTurkish: true)));
        menu.DropDownItems.Add(news);

        // ── 📚 Sözlükler & Referans ──────────────────
        var reference = new ToolStripMenuItem("📚 Sözlükler & Referans");
        reference.DropDownItems.Add(CreateMenuItem("Wikipedia Arama", () => ShowMdiChild(new WikipediaSearchForm())));
        reference.DropDownItems.Add(CreateMenuItem("Sözlük (EN)", () => ShowMdiChild(new DictionaryForm())));
        reference.DropDownItems.Add(CreateMenuItem("Sözlük (EN ↔ TR)", () => ShowMdiChild(new TranslationDictionaryForm())));
        menu.DropDownItems.Add(reference);

        // ── 🔎 Sorgulamalar ──────────────────────────
        var queries = new ToolStripMenuItem("🔎 Sorgulamalar");
        queries.DropDownItems.Add(CreateMenuItem("IP / Domain Sorgula", () => ShowMdiChild(new IpDomainQueryForm())));
        queries.DropDownItems.Add(CreateMenuItem("WHOIS / Alan Adı", () => ShowMdiChild(new WhoisForm())));
        menu.DropDownItems.Add(queries);

        menu.DropDownItems.Add(new ToolStripSeparator());
        menu.DropDownItems.Add(CreateMenuItem("📈 Usage Center", ShowUsageCenter));

        // ── 💰 Finans ────────────────────────────────
        var finance = new ToolStripMenuItem("💰 Finans");
        finance.DropDownItems.Add(CreateMenuItem("Döviz Çevirici", () => ShowMdiChild(new CurrencyConverterForm())));
        finance.DropDownItems.Add(CreateMenuItem("Piyasa 20", () => ShowMdiChild(new ExchangeRatesForm())));
        menu.DropDownItems.Add(finance);

        menu.DropDownItems.Add(new ToolStripSeparator());

        menu.DropDownItems.Add(CreateMenuItem("🌍 Deprem Takibi", () => ShowMdiChild(new EarthquakeForm())));
        menu.DropDownItems.Add(CreateMenuItem("📅 Tatil Takvimi (TR)", () => ShowMdiChild(new TurkishHolidaysForm())));

        return menu;
    }

    private ToolStripMenuItem CreateTodoMenuItem()
    {
        var item = new ToolStripMenuItem("Görevler");
        item.Click += (_, _) => ShowMdiChild(new TodoForm());
        return item;
    }

    private void ShowUsageCenter()
    {
        var existing = Application.OpenForms.OfType<UsageViewerForm>().FirstOrDefault();
        if (existing is not null)
        {
            existing.WindowState = FormWindowState.Normal;
            existing.Activate();
            existing.BringToFront();
            return;
        }

        new UsageViewerForm().Show();
    }

    private ToolStripMenuItem CreateDeveloperToolsMenu()
    {
        var menu = new ToolStripMenuItem("Geliştirici Araçları");

        // ── 📝 Formatlayıcılar ────────────────────────
        var formatters = new ToolStripMenuItem("📝 Formatlayıcılar");
        formatters.DropDownItems.Add(CreateMenuItem("JSON Formatter/Validator", () => ShowMdiChild(new JsonFormatterForm())));
        formatters.DropDownItems.Add(CreateMenuItem("XML Formatter", () => ShowMdiChild(new XmlFormatterForm())));
        formatters.DropDownItems.Add(CreateMenuItem("Pretty XML", () => ShowMdiChild(new PrettyXmlForm())));
        menu.DropDownItems.Add(formatters);

        // ── 🔍 Test Araçları ──────────────────────────
        var testing = new ToolStripMenuItem("🔍 Test Araçları");
        testing.DropDownItems.Add(CreateMenuItem("Regex Tester", () => ShowMdiChild(new RegexTesterForm())));
        testing.DropDownItems.Add(CreateMenuItem("Text Diff Tool", () => ShowMdiChild(new TextDiffForm())));
        menu.DropDownItems.Add(testing);

        // ── 🔄 Dönüştürücüler ─────────────────────────
        var converters = new ToolStripMenuItem("🔄 Dönüştürücüler");
        converters.DropDownItems.Add(CreateMenuItem("Base64 Encoder/Decoder", () => ShowMdiChild(new Base64ConverterForm())));
        converters.DropDownItems.Add(CreateMenuItem("Hash Generator", () => ShowMdiChild(new HashGeneratorForm())));
        converters.DropDownItems.Add(CreateMenuItem("Birim Çevirici", () => ShowMdiChild(new UnitConverterForm())));
        menu.DropDownItems.Add(converters);

        // ── 🎲 Üreticiler ─────────────────────────────
        var generators = new ToolStripMenuItem("🎲 Üreticiler");
        generators.DropDownItems.Add(CreateMenuItem("UUID/GUID Generator", () => ShowMdiChild(new UuidGeneratorForm())));
        generators.DropDownItems.Add(CreateMenuItem("Lorem Ipsum Generator", () => ShowMdiChild(new LoremIpsumForm())));
        generators.DropDownItems.Add(CreateMenuItem("QR Code Generator", () => ShowMdiChild(new QrCodeForm())));
        menu.DropDownItems.Add(generators);

        menu.DropDownItems.Add(new ToolStripSeparator());

        menu.DropDownItems.Add(CreateMenuItem("🎨 Color Picker", () => ShowMdiChild(new ColorPickerForm())));

        return menu;
    }

    private ToolStripMenuItem CreateClipboardMenu()
    {
        var menu = new ToolStripMenuItem("Pano");

        var historyItem = new ToolStripMenuItem("Pano Geçmişi");
        historyItem.Click += (_, _) => ShowClipboardHistory();

        var settingsItem = new ToolStripMenuItem("Pano Ayarları");
        settingsItem.Click += (_, _) =>
        {
            if (!AppSettingsService.Current.ClipboardHistoryEnabled)
            {
                MessageBox.Show(
                    "Pano geçmişi genel ayarlardan kapalı. Önce Assist > Genel Ayarlar ekranından açın.",
                    "Pano Geçmişi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            EnsureClipboardHistory();
            using var settingsForm = new ClipboardSettingsForm(_clipboardHistory!);
            UITheme.Apply(settingsForm);
            settingsForm.ShowDialog(this);
        };

        var clearItem = new ToolStripMenuItem("Pano Temizle");
        clearItem.Click += (_, _) =>
        {
            if (!AppSettingsService.Current.ClipboardHistoryEnabled)
                return;

            EnsureClipboardHistory();

            var result = MessageBox.Show(
                "Tüm pano geçmişini silmek istediğinize emin misiniz?",
                "Onay",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                _clipboardHistory!.Clear();
                MessageBox.Show("Pano geçmişi temizlendi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        menu.DropDownItems.Add(historyItem);
        menu.DropDownItems.Add(settingsItem);

        menu.DropDownItems.Add(new ToolStripSeparator());

        menu.DropDownItems.Add(clearItem);

        return menu;
    }

    private ToolStripMenuItem CreateGamesMenu()
    {
        var menu = new ToolStripMenuItem("Oyunlar");
        menu.DropDownItems.Add(CreateMenuItem("Tetris", () => ShowMdiChild(new TetrisGame())));
        return menu;
    }

    private ToolStripMenuItem CreateWindowMenu()
    {
        var menu = new ToolStripMenuItem("Pencereler");

        // Layout options
        menu.DropDownItems.Add(CreateMenuItem("Basamaklı Yerleştir", () => LayoutMdi(MdiLayout.Cascade)));
        menu.DropDownItems.Add(CreateMenuItem("Yatay Döşe", () => LayoutMdi(MdiLayout.TileHorizontal)));
        menu.DropDownItems.Add(CreateMenuItem("Dikey Döşe", () => LayoutMdi(MdiLayout.TileVertical)));

        menu.DropDownItems.Add(new ToolStripSeparator());

        // Close all
        menu.DropDownItems.Add(CreateMenuItem("Tümünü Kapat", CloseAllMdiChildren));

        menu.DropDownItems.Add(new ToolStripSeparator());

        // Detach
        menu.DropDownItems.Add(CreateMenuItem("📌 Pencereyi Ayır", DetachActiveChild));

        return menu;
    }

    private void CloseAllMdiChildren()
    {
        if (MdiChildren.Length == 0) return;

        var result = MessageBox.Show(
            $"Tüm açık pencereleri ({MdiChildren.Length} adet) kapatmak istediğinize emin misiniz?",
            "Onay",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            foreach (var child in MdiChildren)
            {
                child.Close();
            }
        }
    }

    private void ShowThemeSelection()
    {
        using var form = new ThemeSelectionForm();
        form.ShowDialog(this);
    }

    private void ShowAppSettings()
    {
        using var form = new AppSettingsForm();
        form.ShowDialog(this);
    }

    private void ShowQuickLauncher()
    {
        if (!AppSettingsService.Current.QuickLauncherEnabled)
            return;

        var existing = Application.OpenForms.OfType<QuickLauncherForm>().FirstOrDefault();
        if (existing is not null)
        {
            existing.Activate();
            existing.BringToFront();
            return;
        }

        using var launcher = new QuickLauncherForm(_quickLaunchItems);
        UITheme.Apply(launcher);
        launcher.ShowDialog(this);
    }

    private void ShowClipboardHistory()
    {
        if (!AppSettingsService.Current.ClipboardHistoryEnabled)
        {
            MessageBox.Show(
                "Pano geçmişi genel ayarlardan kapalı.",
                "Pano Geçmişi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        EnsureClipboardHistory();
        if (_clipboardHistory is not null)
            ShowMdiChild(new ClipboardHistoryForm(_clipboardHistory));
    }

    private void ToggleLowPowerMode()
    {
        AppSettingsService.Update(settings => settings.LowPowerMode = !settings.LowPowerMode);
    }

    private void OnAppSettingsChanged(object? sender, EventArgs e)
    {
        if (_isClosing || IsDisposed)
            return;

        if (_lowPowerMenuItem is not null)
            _lowPowerMenuItem.Checked = AppSettingsService.Current.LowPowerMode;

        ApplyClipboardSettings();
        ApplyDashboardSettings();
    }

    private void ApplyClipboardSettings()
    {
        if (!AppSettingsService.Current.ClipboardHistoryEnabled)
        {
            _clipboardHistory?.Dispose();
            _clipboardHistory = null;
            return;
        }

        EnsureClipboardHistory();
        _clipboardHistory?.Start(AppSettingsService.EffectiveClipboardIntervalMs);
    }

    private void ApplyDashboardSettings()
    {
        if (!AppSettingsService.Current.DashboardEnabled)
        {
            StopDashboardTimers();
            if (_dashboardPanel is not null)
                _dashboardPanel.Visible = false;
            return;
        }

        if (_dashboardPanel is null || _dashboardPanel.IsDisposed)
        {
            InitializeDashboardPanel();
            return;
        }

        _dashboardPanel.Visible = true;
        StartDashboardTimers();
        _ = RunFastRefreshAsync();
        _ = RunMediumRefreshAsync();
        _ = RunSlowRefreshAsync();
    }

    private void OpenSessionForm(string key)
    {
        if (_sessionFormFactories.TryGetValue(key, out var factory))
            ShowMdiChild(factory());
    }

    private Dictionary<string, Func<Form>> CreateSessionFormFactories() => new(StringComparer.Ordinal)
    {
        [nameof(PasswordListForm)] = () => new PasswordListForm(),
        [nameof(PasswordGeneratorForm)] = () => new PasswordGeneratorForm(),
        [nameof(TodoForm)] = () => new TodoForm(),
        [nameof(SystemInfoForm)] = () => new SystemInfoForm(),
        [nameof(PerformanceMonitorForm)] = () => new PerformanceMonitorForm(),
        [nameof(NoSleepGuardianForm)] = () => new NoSleepGuardianForm(),
        [nameof(StartupManagerForm)] = () => new StartupManagerForm(),
        [nameof(DiskCleanerForm)] = () => new DiskCleanerForm(),
        [nameof(HostsFileEditorForm)] = () => new HostsFileEditorForm(),
        [nameof(HardwareDiagnosticsForm)] = () => new HardwareDiagnosticsForm(),
        [nameof(SystemRecoveryForm)] = () => new SystemRecoveryForm(),
        [nameof(SpeedTestForm)] = () => new SpeedTestForm(),
        [nameof(NetworkScannerForm)] = () => new NetworkScannerForm(),
        [nameof(WifiPasswordForm)] = () => new WifiPasswordForm(),
        [nameof(ThreatScannerForm)] = () => new ThreatScannerForm(),
        [nameof(WikipediaSearchForm)] = () => new WikipediaSearchForm(),
        [nameof(DictionaryForm)] = () => new DictionaryForm(),
        [nameof(TranslationDictionaryForm)] = () => new TranslationDictionaryForm(),
        [nameof(IpDomainQueryForm)] = () => new IpDomainQueryForm(),
        [nameof(WhoisForm)] = () => new WhoisForm(),
        [nameof(CurrencyConverterForm)] = () => new CurrencyConverterForm(),
        [nameof(ExchangeRatesForm)] = () => new ExchangeRatesForm(),
        [nameof(EarthquakeForm)] = () => new EarthquakeForm(),
        [nameof(TurkishHolidaysForm)] = () => new TurkishHolidaysForm(),
        [nameof(JsonFormatterForm)] = () => new JsonFormatterForm(),
        [nameof(XmlFormatterForm)] = () => new XmlFormatterForm(),
        [nameof(PrettyXmlForm)] = () => new PrettyXmlForm(),
        [nameof(RegexTesterForm)] = () => new RegexTesterForm(),
        [nameof(TextDiffForm)] = () => new TextDiffForm(),
        [nameof(Base64ConverterForm)] = () => new Base64ConverterForm(),
        [nameof(HashGeneratorForm)] = () => new HashGeneratorForm(),
        [nameof(UnitConverterForm)] = () => new UnitConverterForm(),
        [nameof(UuidGeneratorForm)] = () => new UuidGeneratorForm(),
        [nameof(LoremIpsumForm)] = () => new LoremIpsumForm(),
        [nameof(QrCodeForm)] = () => new QrCodeForm(),
        [nameof(ColorPickerForm)] = () => new ColorPickerForm(),
        [nameof(DiagnosticsForm)] = () => new DiagnosticsForm(this),
    };

    private IReadOnlyList<QuickLaunchItem> CreateQuickLaunchItems()
    {
        QuickLaunchItem Open(string title, string category, string keywords, string key) =>
            new(title, category, keywords, () => OpenSessionForm(key));

        return
        [
            new("Assist Ayarları", "Assist", "settings ayar low power dashboard clipboard", ShowAppSettings),
            new("Assist Diagnostics", "Assist", "ram cpu memory diagnostics performans kaynak", () => ShowMdiChild(new DiagnosticsForm(this))),
            new("Low Power Mode", "Assist", "battery ram low power tasarruf", ToggleLowPowerMode),
            Open("Şifreleri Gör", "Şifreler", "password vault şifre kasa", nameof(PasswordListForm)),
            Open("Şifre Üret", "Şifreler", "password generator şifre üret", nameof(PasswordGeneratorForm)),
            Open("Görevler", "Verimlilik", "todo görev yapılacak", nameof(TodoForm)),
            new("Pano Geçmişi", "Pano", "clipboard pano geçmiş", ShowClipboardHistory),
            Open("Sistem Bilgisi", "Sistem", "system info hardware", nameof(SystemInfoForm)),
            Open("Performance Monitor", "Sistem", "cpu ram disk monitor performans", nameof(PerformanceMonitorForm)),
            Open("NoSleep Guardian", "Sistem", "nosleep guardian sleep power heartbeat bildirim uyku güç", nameof(NoSleepGuardianForm)),
            Open("Startup Manager", "Sistem", "başlangıç startup process", nameof(StartupManagerForm)),
            Open("Disk Temizleyici", "Sistem", "disk cleaner temp temizle", nameof(DiskCleanerForm)),
            Open("Speed Test", "Ağ", "internet speed hız ping", nameof(SpeedTestForm)),
            Open("Ağ Tarayıcı", "Ağ", "network scanner netstat bağlantı", nameof(NetworkScannerForm)),
            Open("Wi-Fi Şifreleri", "Ağ", "wifi password kablosuz", nameof(WifiPasswordForm)),
            Open("Tehdit Tarayıcı", "Güvenlik", "threat scanner security malware", nameof(ThreatScannerForm)),
            Open("JSON Formatter", "Geliştirici", "json format validate", nameof(JsonFormatterForm)),
            Open("XML Formatter", "Geliştirici", "xml format validate", nameof(XmlFormatterForm)),
            Open("Regex Tester", "Geliştirici", "regex test replace", nameof(RegexTesterForm)),
            Open("Text Diff", "Geliştirici", "diff compare text", nameof(TextDiffForm)),
            Open("Base64 Converter", "Geliştirici", "base64 encode decode", nameof(Base64ConverterForm)),
            Open("Hash Generator", "Geliştirici", "hash md5 sha", nameof(HashGeneratorForm)),
            Open("UUID Generator", "Geliştirici", "uuid guid generator", nameof(UuidGeneratorForm)),
            Open("QR Code Generator", "Geliştirici", "qr qrcode barcode", nameof(QrCodeForm)),
            Open("Wikipedia Arama", "Online", "wiki wikipedia search", nameof(WikipediaSearchForm)),
            Open("Sözlük", "Online", "dictionary sözlük", nameof(DictionaryForm)),
            Open("IP / Domain Sorgula", "Online", "ip domain dns geo", nameof(IpDomainQueryForm)),
            Open("WHOIS", "Online", "whois domain rdap", nameof(WhoisForm)),
            Open("Döviz Çevirici", "Finans", "currency exchange döviz", nameof(CurrencyConverterForm)),
            Open("Piyasa 20", "Finans", "borsa piyasa crypto gold", nameof(ExchangeRatesForm)),
            Open("Deprem Takibi", "Online", "earthquake deprem afad", nameof(EarthquakeForm)),
            Open("Tatil Takvimi", "Online", "tatil holiday calendar", nameof(TurkishHolidaysForm)),
        ];
    }

    private void RestoreSessionIfNeeded()
    {
        if (_sessionRestored || !AppSettingsService.Current.RestoreLastSession)
            return;

        _sessionRestored = true;
        foreach (var key in SessionStateService.LoadOpenForms().Take(8))
        {
            if (_sessionFormFactories.TryGetValue(key, out var factory))
            {
                try { ShowMdiChild(factory()); }
                catch { /* Skip forms that cannot be restored in this session. */ }
            }
        }
    }

    private void SaveSession()
    {
        if (!AppSettingsService.Current.RestoreLastSession)
            return;

        var keys = MdiChildren
            .Select(form => form.GetType().Name)
            .Where(key => _sessionFormFactories.ContainsKey(key));

        SessionStateService.SaveOpenForms(keys);
    }

    private void ApplyThemeSelection(AppTheme theme)
    {
        ThemeService.SetTheme(theme);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        PostToUi(() =>
        {
            UITheme.ApplyToOpenForms();
            ApplyDashboardTheme();
            ApplyWatermarkTheme();
        });
    }

    private static ToolStripMenuItem CreateMenuItem(string text, Action action, ToolStripItemAlignment alignment = ToolStripItemAlignment.Left)
    {
        var item = new ToolStripMenuItem(text) { Alignment = alignment };
        item.Click += (_, _) => action();
        return item;
    }

    private static ToolStripMenuItem CreateAsyncMenuItem(string text, Func<Task> action)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += async (_, _) => await action();
        return item;
    }

    private void EnsureClipboardHistory()
    {
        if (!AppSettingsService.Current.ClipboardHistoryEnabled)
            return;

        if (_clipboardHistory is not null) return;

        _clipboardHistory = new ClipboardHistoryService(50, filterSensitive: true);
        _clipboardHistory.Start(AppSettingsService.EffectiveClipboardIntervalMs);
    }

    private void ApplyTheme()
    {
        Text = AppConstants.AppTitle;
        UITheme.Apply(this);
        ShowIcon = false;

        var p = UITheme.Palette;
        foreach (Control control in Controls)
        {
            if (control is MdiClient mdiClient)
            {
                mdiClient.BackColor = p.Back;
                _mdiClient = mdiClient;
                break;
            }
        }
    }

    private void ShowMdiChild(Form form)
    {
        // Activate the existing instance instead of opening a duplicate
        var existing = MdiChildren.FirstOrDefault(c => c.GetType() == form.GetType());
        if (existing is not null)
        {
            form.Dispose();
            existing.Activate();
            return;
        }

        form.MdiParent = this;
        form.WindowState = FormWindowState.Maximized;
        UITheme.Apply(form);
        EnsureDarkTitleBar(form);
        form.Show();
    }

    private static void EnsureDarkTitleBar(Form form)
    {
        if (form.IsHandleCreated)
        {
            ApplyDarkTitleBar(form);
        }
        else
        {
            form.HandleCreated += (_, _) => ApplyDarkTitleBar(form);
        }
    }

    private static void ApplyDarkTitleBar(Form form)
    {
        if (form.IsDisposed || !form.IsHandleCreated) return;
        var val = 1;
        // Attr 20 = Win11+, attr 19 = Win10 fallback
        if (DwmSetWindowAttribute(form.Handle, 20, ref val, sizeof(int)) != 0)
            DwmSetWindowAttribute(form.Handle, 19, ref val, sizeof(int));
    }

    /// <summary>
    /// Enables double-buffering on a control via the protected property to reduce flicker.
    /// </summary>
    private static void SetDoubleBuffered(Control control) =>
        typeof(Control).InvokeMember(
            "DoubleBuffered",
            System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, control, [true]);

    private void DetachActiveChild()
    {
        var child = ActiveMdiChild;
        if (child is null)
        {
            MessageBox.Show("Ayırılacak aktif pencere yok.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var bounds = child.RectangleToScreen(child.ClientRectangle);
        child.Hide();
        child.MdiParent = null!;
        child.FormBorderStyle = FormBorderStyle.Sizable;
        child.StartPosition = FormStartPosition.Manual;
        child.Location = bounds.Location;
        child.Size = bounds.Size;
        child.WindowState = FormWindowState.Normal;
        EnsureDarkTitleBar(child);
        child.Show();
    }

    private async Task ShowNewsAsync(
        Func<Task<List<NewsItem>>> fetcher,
        string title,
        bool openNormal = false,
        bool translateToTurkish = false)
    {
        await Loading.RunAsync(this, async () =>
        {
            var items = await fetcher();

            if (translateToTurkish)
                await TranslateNewsTitlesAsync(items);

            // Reuse existing NewsForm if open
            var existing = MdiChildren.OfType<NewsForm>().FirstOrDefault();
            if (existing is not null)
            {
                existing.Text = title;
                existing.SetNews(items);
                if (openNormal && existing.WindowState == FormWindowState.Maximized)
                    existing.WindowState = FormWindowState.Normal;
                existing.Activate();
                return;
            }

            var newsForm = new NewsForm(title);
            newsForm.SetNews(items); // set BEFORE ShowMdiChild to avoid disposed-form exception
            if (openNormal) ShowMdiChildNormal(newsForm);
            else ShowMdiChild(newsForm);
        }, "Haberler yükleniyor...");
    }

    private static async Task TranslateNewsTitlesAsync(IReadOnlyList<NewsItem> items)
    {
        using var throttler = new SemaphoreSlim(3);
        var tasks = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .Select(async item =>
            {
                await throttler.WaitAsync().ConfigureAwait(false);
                try
                {
                    item.Title = await TranslationService.TranslateAsync(item.Title, "tr").ConfigureAwait(false);
                }
                catch
                {
                    // Keep the original title when translation fails.
                }
                finally
                {
                    throttler.Release();
                }
            });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task RunDnsResetAsync()
    {
        var confirm = MessageBox.Show(
            "Bu işlem ağ bağlantınızı geçici olarak kesebilir. Devam etmek istiyor musunuz?",
            "DNS Reset Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        var output = await Loading.RunAsync(this, async () =>
        {
            var commands = new[]
            {
                ("ipconfig", "/release"),
                ("ipconfig", "/flushdns"),
                ("ipconfig", "/renew")
            };

            var result = new StringBuilder();

            await Task.Run(() =>
            {
                foreach (var (fileName, args) in commands)
                {
                    try
                    {
                        using var process = Process.Start(new ProcessStartInfo
                        {
                            FileName = fileName,
                            Arguments = args,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        });

                        if (process is not null)
                        {
                            var stdout = process.StandardOutput.ReadToEnd();
                            var stderr = process.StandardError.ReadToEnd();
                            process.WaitForExit(30_000);

                            result.AppendLine($"> {fileName} {args}");
                            result.AppendLine(stdout);

                            if (!string.IsNullOrWhiteSpace(stderr))
                            {
                                result.AppendLine("ERR:");
                                result.AppendLine(stderr);
                            }

                            result.AppendLine(new string('-', 60));
                        }
                    }
                    catch (Exception ex)
                    {
                        result.AppendLine($"Komut çalıştırılamadı: {fileName} {args} -> {ex.Message}");
                    }
                }
            });

            return result.ToString();
        }, "DNS sıfırlanıyor...");

        var existingOutput = MdiChildren.OfType<CommandOutputForm>().FirstOrDefault();
        existingOutput?.Close();
        var outputForm = new CommandOutputForm();
        outputForm.SetOutput(output); // set BEFORE ShowMdiChild
        ShowMdiChild(outputForm);
    }

    private static void ShowAbout()
    {
        var message = $"""
            {AppConstants.AppTitle}
            Sürüm: {AppConstants.BuildVersion}
            © 2026 Assist

            Bu uygulama şifre yönetimi ve sistem bilgisi özellikleri sağlar.
            Geliştirici: Oz
            """;

        MessageBox.Show(message, "Hakkında", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>
    /// Checks GitHub Releases for a newer version. If silent, only notifies when an update exists.
    /// </summary>
    private async Task CheckForUpdateAsync(bool silent)
    {
        try
        {
            var update = await AutoUpdateService.CheckForUpdateAsync().ConfigureAwait(true);

            if (update is null)
            {
                if (!silent)
                    MessageBox.Show(
                        $"Assist güncel! (Mevcut sürüm: {AppConstants.BuildVersion})",
                        "Güncelleme",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                return;
            }

            var releaseNotes = string.IsNullOrWhiteSpace(update.Body)
                ? ""
                : $"\n\nDeğişiklikler:\n{update.Body}";

            var result = MessageBox.Show(
                $"Yeni sürüm mevcut: {update.TagName}\nMevcut sürüm: {AppConstants.BuildVersion}{releaseNotes}\n\nŞimdi güncellemek ister misiniz?",
                "Güncelleme Mevcut",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result != DialogResult.Yes)
                return;

            var success = await Loading.RunAsync(this, async () =>
            {
                return await AutoUpdateService.DownloadAndApplyAsync(update).ConfigureAwait(false);
            }, "Güncelleme indiriliyor...");

            if (success)
            {
                MessageBox.Show(
                    "Güncelleme indirildi. Uygulama yeniden başlatılacak.",
                    "Güncelleme",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                Application.Exit();
            }
            else
            {
                MessageBox.Show(
                    "Güncelleme indirilemedi. Lütfen daha sonra tekrar deneyin.",
                    "Hata",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        catch
        {
            if (!silent)
                MessageBox.Show(
                    "Güncelleme kontrolü sırasında bir hata oluştu.",
                    "Hata",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
        }
    }

    #region Dashboard Panel

    private void InitializeDashboardPanel()
    {
        if (_dashboardPanel is not null && !_dashboardPanel.IsDisposed)
        {
            _dashboardPanel.Visible = true;
            StartDashboardTimers();
            return;
        }

        var p = UITheme.Palette;
        _dashboardPanel = new Panel
        {
            Height = 148,
            Dock = DockStyle.Bottom,
            BackColor = p.Surface,
            BorderStyle = BorderStyle.None
        };
        SetDoubleBuffered(_dashboardPanel);

        _topBorderPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = p.Accent
        };
        _dashboardPanel.Controls.Add(_topBorderPanel);

        // ── Process monitor bar ──
        _procBarPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 24,
            BackColor = p.Surface2
        };
        _lblProcBar = new Label
        {
            Dock = DockStyle.Fill,
            Text = "  \u25ba ASSIST  |  Monitoring...",
            Font = ProcBarFont,
            ForeColor = Color.FromArgb(80, 180, 255),
            BackColor = p.Surface2,
            TextAlign = ContentAlignment.MiddleLeft
        };
        SetDoubleBuffered(_procBarPanel);
        _procBarPanel.Controls.Add(_lblProcBar);
        _procBarPanel.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = p.Grid });
        _dashboardPanel.Controls.Add(_procBarPanel);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 6,
            Margin = Padding.Empty,
            Padding = new Padding(12, 0, 12, 0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        for (var i = 0; i < 6; i++)
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 16.67F));

        // ── Left column labels ──
        _lblClock = CreateDashboardLabel("⏰ --:--:--", FontStyle.Bold);
        _lblWeather = CreateDashboardLabel("🌤 Yükleniyor...");
        _lblCpuRam = CreateDashboardLabel("💻 CPU: --%  RAM: --/-- MB");
        _lblDisk = CreateDashboardLabel("💾 C: -- GB boş / -- GB");
        _lblBattery = CreateDashboardLabel("🔋 --");
        _lblUptime = CreateDashboardLabel("⬆ Uptime: --");

        // ── Right column labels ──
        _lblIpInfo = CreateDashboardLabel("🌐 Yükleniyor...");
        _lblPing = CreateDashboardLabel("📶 Kontrol ediliyor...");
        _lblCurrency = CreateDashboardLabel("💱 Yükleniyor...");
        _lblCrypto = CreateDashboardLabel("₿ Yükleniyor...");
        _lblAppStats = CreateDashboardLabel("🔑 Şifre: -  📋 Pano: -");

        // Row 0: Clock | IP Info
        table.Controls.Add(_lblClock, 0, 0);
        table.Controls.Add(_lblIpInfo, 1, 0);

        // Row 1: Weather | Ping
        table.Controls.Add(_lblWeather, 0, 1);
        table.Controls.Add(_lblPing, 1, 1);

        // Row 2: CPU/RAM | Currency
        table.Controls.Add(_lblCpuRam, 0, 2);
        table.Controls.Add(_lblCurrency, 1, 2);

        // Row 3: Disk | Crypto
        table.Controls.Add(_lblDisk, 0, 3);
        table.Controls.Add(_lblCrypto, 1, 3);

        // Row 4: Battery | App Stats
        table.Controls.Add(_lblBattery, 0, 4);
        table.Controls.Add(_lblAppStats, 1, 4);

        // Row 5: Uptime | Version
        table.Controls.Add(_lblUptime, 0, 5);

        _lblVersion = new Label
        {
            Text = AppConstants.BuildVersion,
            Dock = DockStyle.Fill,
            AutoSize = false,
            ForeColor = p.Muted,
            Font = VersionFont,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleRight
        };
        table.Controls.Add(_lblVersion, 1, 5);

        SetDoubleBuffered(table);

        // Set non-transparent background on fast-updating labels to prevent flicker
        _lblClock!.BackColor = p.Surface;
        _lblCpuRam!.BackColor = p.Surface;

        _dashboardPanel.Controls.Add(table);
        Controls.Add(_dashboardPanel);

        _fastTimer = new System.Windows.Forms.Timer { Interval = (int)AppSettingsService.FastDashboardInterval.TotalMilliseconds };
        _fastTimer.Tick += async (_, _) => await RunFastRefreshAsync();

        _mediumTimer = new System.Windows.Forms.Timer { Interval = (int)AppSettingsService.MediumDashboardInterval.TotalMilliseconds };
        _mediumTimer.Tick += async (_, _) => await RunMediumRefreshAsync();

        _slowTimer = new System.Windows.Forms.Timer { Interval = (int)AppSettingsService.SlowDashboardInterval.TotalMilliseconds };
        _slowTimer.Tick += async (_, _) => await RunSlowRefreshAsync();

        StartDashboardTimers();

        // Initial load
        _ = RunFastRefreshAsync();
        _ = RunMediumRefreshAsync();
        _ = RunSlowRefreshAsync();
        ApplyDashboardTheme();
    }

    private void StartDashboardTimers()
    {
        ApplyDashboardTimerIntervals();

        if (!AppSettingsService.Current.DashboardEnabled)
            return;

        _fastTimer?.Start();
        _mediumTimer?.Start();
        _slowTimer?.Start();
    }

    private void StopDashboardTimers()
    {
        _fastTimer?.Stop();
        _mediumTimer?.Stop();
        _slowTimer?.Stop();
    }

    private void ApplyDashboardTimerIntervals()
    {
        if (_fastTimer is not null)
            _fastTimer.Interval = (int)AppSettingsService.FastDashboardInterval.TotalMilliseconds;
        if (_mediumTimer is not null)
            _mediumTimer.Interval = (int)AppSettingsService.MediumDashboardInterval.TotalMilliseconds;
        if (_slowTimer is not null)
            _slowTimer.Interval = (int)AppSettingsService.SlowDashboardInterval.TotalMilliseconds;
    }

    private void ApplyDashboardTheme()
    {
        if (_dashboardPanel is null) return;
        var p = UITheme.Palette;
        _dashboardPanel.BackColor = p.Surface;
        if (_topBorderPanel is not null) _topBorderPanel.BackColor = p.Accent;
        if (_procBarPanel is not null) _procBarPanel.BackColor = p.Surface2;
        if (_lblProcBar is not null) _lblProcBar.BackColor = p.Surface2;
        if (_lblVersion is not null) { _lblVersion.ForeColor = p.Muted; _lblVersion.BackColor = p.Surface; }

        // Sync all dashboard label BackColors
        foreach (var lbl in new[] { _lblClock, _lblCpuRam, _lblWeather, _lblDisk, _lblBattery, _lblUptime,
                                     _lblIpInfo, _lblPing, _lblCurrency, _lblCrypto, _lblAppStats })
        {
            if (lbl is not null) lbl.BackColor = p.Surface;
        }
    }

    // Cached fonts for dashboard labels to prevent repeated allocations
    private static readonly Font DashboardFont = new("Consolas", 9);
    private static readonly Font DashboardFontBold = new("Consolas", 10, FontStyle.Bold);
    private static readonly Font ProcBarFont = new("Consolas", 8);
    private static readonly Font VersionFont = new("Consolas", 8);

    private static Label CreateDashboardLabel(string text, FontStyle style = FontStyle.Regular)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            AutoSize = false,
            ForeColor = UITheme.Palette.Text,
            Font = style == FontStyle.Bold ? DashboardFontBold : DashboardFont,
            BackColor = UITheme.Palette.Surface,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private void PostToUi(Action action)
    {
        if (_isClosing || IsDisposed || !IsHandleCreated)
            return;

        void GuardedAction()
        {
            if (_isClosing || IsDisposed)
                return;

            action();
        }

        try
        {
            if (InvokeRequired)
                BeginInvoke((Action)GuardedAction);
            else
                GuardedAction();
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    private async Task RunMediumRefreshAsync()
    {
        if (_isClosing || !AppSettingsService.Current.DashboardEnabled || Interlocked.Exchange(ref _mediumRefreshInProgress, 1) == 1)
            return;

        try
        {
            await RefreshMediumAsync().ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _mediumRefreshInProgress, 0);
        }
    }

    private async Task RunSlowRefreshAsync()
    {
        if (_isClosing || !AppSettingsService.Current.DashboardEnabled || Interlocked.Exchange(ref _slowRefreshInProgress, 1) == 1)
            return;

        try
        {
            await RefreshSlowAsync().ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _slowRefreshInProgress, 0);
        }
    }

    private async Task RunFastRefreshAsync()
    {
        if (_isClosing || IsDisposed || !AppSettingsService.Current.DashboardEnabled) return;
        if (_isInSizeMove) return;
        if (Interlocked.Exchange(ref _fastRefreshInProgress, 1) == 1)
            return;

        try
        {
            UpdateClockLabel();

            var includeProcessBar = _lblProcBar is not null;
            var snapshot = await Task.Run(() => CaptureFastDashboardSnapshot(includeProcessBar)).ConfigureAwait(false);

            PostToUi(() =>
            {
                if (_lblCpuRam is not null && _lblCpuRam.Text != snapshot.CpuRamText)
                    _lblCpuRam.Text = snapshot.CpuRamText;

                if (_lblProcBar is not null &&
                    !string.IsNullOrWhiteSpace(snapshot.ProcessBarText) &&
                    _lblProcBar.Text != snapshot.ProcessBarText)
                {
                    _lblProcBar.Text = snapshot.ProcessBarText;
                }
            });
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Fast refresh failed: {ex.Message}");
        }
        finally
        {
            Volatile.Write(ref _fastRefreshInProgress, 0);
        }
    }

    private void UpdateClockLabel()
    {
        if (_lblClock is null)
            return;

        var clockText = $"⏰ {DateTime.Now:HH:mm:ss}  📅 {DateTime.Now:dddd, dd MMMM yyyy}";
        if (_lblClock.Text != clockText)
            _lblClock.Text = clockText;
    }

    private FastDashboardSnapshot CaptureFastDashboardSnapshot(bool includeProcessBar)
    {
        var cpuRamText = DashboardService.GetCpuRam();
        var processBarText = includeProcessBar ? CaptureProcessBarText() : string.Empty;
        return new FastDashboardSnapshot(cpuRamText, processBarText);
    }

    private string CaptureProcessBarText()
    {
        try
        {
            _selfProcess.Refresh();
            var ram = _selfProcess.WorkingSet64 / 1024 / 1024;

            var now = DateTime.UtcNow;
            double cpu = 0;
            if (_lastCpuCheck != DateTime.MinValue)
            {
                var cpuDelta = (_selfProcess.TotalProcessorTime - _lastCpuTime).TotalSeconds;
                var elapsed = (now - _lastCpuCheck).TotalSeconds;
                cpu = elapsed > 0 ? cpuDelta / (elapsed * Environment.ProcessorCount) * 100.0 : 0;
            }
            _lastCpuTime = _selfProcess.TotalProcessorTime;
            _lastCpuCheck = now;

            // Thread count is cached for ~5s — Process.Threads materialises a full ProcessThreadCollection
            // which is surprisingly expensive when called every couple of seconds.
            if ((now - _lastThreadCountCheck).TotalSeconds >= 5)
            {
                try { _cachedThreadCount = _selfProcess.Threads.Count; }
                catch { /* keep the previous cached value */ }
                _lastThreadCountCheck = now;
            }

            // System-wide network delta — only update every ~10 seconds to reduce allocations
            var secondsSinceNetCheck = (now - _lastNetCheck).TotalSeconds;
            if (secondsSinceNetCheck >= 10)
            {
                long rx = 0, tx = 0;
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    var stats = ni.GetIPv4Statistics();
                    rx += stats.BytesReceived;
                    tx += stats.BytesSent;
                }
                if (_lastNetRx > 0)
                {
                    _cachedRxKbPerSec = (rx - _lastNetRx) / 1024.0 / secondsSinceNetCheck;
                    _cachedTxKbPerSec = (tx - _lastNetTx) / 1024.0 / secondsSinceNetCheck;
                }
                _lastNetRx = rx;
                _lastNetTx = tx;
                _lastNetCheck = now;
            }

            var procText =
                $"  \u25ba ASSIST  |  \ud83d\udcbe RAM: {ram} MB" +
                $"  |  \ud83d\udda5 CPU: {cpu:F1}%" +
                $"  |  \ud83d\udd00 Threads: {_cachedThreadCount}" +
                $"  |  \ud83c\udf10 \u2193 {_cachedRxKbPerSec:F0} KB/s  \u2191 {_cachedTxKbPerSec:F0} KB/s";
            return procText;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProcBar] Refresh failed: {ex.Message}");
            return string.Empty;
        }
    }

    private readonly record struct FastDashboardSnapshot(string CpuRamText, string ProcessBarText);

    private async Task RefreshMediumAsync()
    {
        if (_isClosing || IsDisposed || !AppSettingsService.Current.DashboardEnabled) return;

        try
        {
            var pingTask = DashboardService.GetPingAsync();

            var disk = DashboardService.GetDiskUsage();
            var battery = DashboardService.GetBatteryStatus();
            var uptime = DashboardService.GetUptime();
            var appStats = DashboardService.GetAppStats();
            var ping = await pingTask.ConfigureAwait(false);

            void Update()
            {
                if (_lblDisk is not null) _lblDisk.Text = disk;
                if (_lblBattery is not null) _lblBattery.Text = battery;
                if (_lblUptime is not null) _lblUptime.Text = uptime;
                if (_lblPing is not null) _lblPing.Text = ping;
                if (_lblAppStats is not null) _lblAppStats.Text = appStats;
            }

            PostToUi(Update);
        }
        catch (ObjectDisposedException) { /* form closed during refresh — expected */ }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Medium refresh failed: {ex.Message}");
        }
    }

    /// <summary>Weather, currency, crypto, IP — every 5 minutes.</summary>
    private async Task RefreshSlowAsync()
    {
        if (_isClosing || IsDisposed || !AppSettingsService.Current.DashboardEnabled) return;

        try
        {
            // Detect physical location (WiFi/GPS) for accurate weather city
            var detectTask = DashboardService.DetectPhysicalCityAsync();
            await Task.WhenAny(detectTask, Task.Delay(15000)).ConfigureAwait(false);

            // Fetch IP info (city used as fallback if physical location unavailable)
            var ipResult = await DashboardService.GetIpInfoAsync().ConfigureAwait(false);

            var weatherTask = DashboardService.GetWeatherAsync();
            var currencyTask = DashboardService.GetCurrencyAsync();
            var cryptoTask = DashboardService.GetCryptoAsync();

            await Task.WhenAll(weatherTask, currencyTask, cryptoTask).ConfigureAwait(false);
            var weather = await weatherTask.ConfigureAwait(false);
            var currency = await currencyTask.ConfigureAwait(false);
            var crypto = await cryptoTask.ConfigureAwait(false);

            void Update()
            {
                if (_lblIpInfo is not null) _lblIpInfo.Text = ipResult;
                if (_lblWeather is not null) _lblWeather.Text = weather;
                if (_lblCurrency is not null) _lblCurrency.Text = currency;
                if (_lblCrypto is not null) _lblCrypto.Text = crypto;
            }

            PostToUi(Update);
        }
        catch (ObjectDisposedException) { /* form closed during refresh — expected */ }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Slow refresh failed: {ex.Message}");
        }
    }

    #endregion

    #region Watermark

    private void InitializeWatermark()
    {
        // _mdiClient is already assigned by ApplyTheme() which runs before this
        if (_mdiClient is null) return;

        var p = UITheme.Palette;
        _watermarkAccent = BlendColor(p.Accent, p.Back, 0.12);
        _watermarkMuted  = BlendColor(p.Muted,  p.Back, 0.08);

        // Double-buffer the MDI client so watermark painting does not flicker while the window moves
        SetDoubleBuffered(_mdiClient);

        _mdiClient.Paint      += MdiClient_Paint;
        // Recompute layout only when the size actually changes, and only invalidate then
        _mdiClient.SizeChanged += MdiClient_SizeChanged;
        _mdiClient.MouseClick += MdiClient_MouseClick;
    }

    private void MdiClient_SizeChanged(object? sender, EventArgs e)
    {
        if (_mdiClient is null) return;
        // Invalidating the watermark is only needed when the client area really changed.
        // The previous Resize handler fired on every move-driven layout cycle and caused dragging stutter.
        if (_mdiClient.ClientSize != _watermarkLayoutForClientSize)
            _mdiClient.Invalidate();
    }

    private void MdiClient_Paint(object? sender, PaintEventArgs e)
    {
        if (_mdiClient is null) return;
        var g = e.Graphics;

        var clientSize = _mdiClient.ClientSize;
        if (clientSize != _watermarkLayoutForClientSize)
        {
            // Recompute the (expensive) text metrics only when the client size actually changes.
            _sAssistCached = TextRenderer.MeasureText(g, "Assist", WatermarkLargeFont);
            _sByCached     = TextRenderer.MeasureText(g, "By",     WatermarkSmallFont);
            _sOzCached     = TextRenderer.MeasureText(g, "Oz",     WatermarkLargeFont);

            var totalWidth = _sAssistCached.Width + _sByCached.Width + _sOzCached.Width + 8;
            var maxHeight  = Math.Max(_sAssistCached.Height, Math.Max(_sByCached.Height, _sOzCached.Height));

            var startX  = (clientSize.Width  - totalWidth) / 2;
            var centerY = (clientSize.Height - maxHeight)  / 2;

            _rcAssist = new Rectangle(startX, centerY, _sAssistCached.Width, _sAssistCached.Height);
            _rcBy     = new Rectangle(startX + _sAssistCached.Width + 4,
                                      centerY + _sAssistCached.Height - _sByCached.Height,
                                      _sByCached.Width, _sByCached.Height);
            _rcOz     = new Rectangle(startX + _sAssistCached.Width + _sByCached.Width + 8,
                                      centerY, _sOzCached.Width, _sOzCached.Height);

            _watermarkLayoutForClientSize = clientSize;
        }

        TextRenderer.DrawText(g, "Assist", WatermarkLargeFont, _rcAssist.Location, _watermarkAccent);
        TextRenderer.DrawText(g, "By",     WatermarkSmallFont, _rcBy.Location,     _watermarkMuted);
        TextRenderer.DrawText(g, "Oz",     WatermarkLargeFont, _rcOz.Location,     _watermarkAccent);
    }

    private void MdiClient_MouseClick(object? sender, MouseEventArgs e)
    {
        if (_rcAssist.Contains(e.Location)) OnAssistClick();
        else if (_rcOz.Contains(e.Location)) OnOzClick();
    }

    private void ApplyWatermarkTheme()
    {
        if (_mdiClient is null) return;
        var p = UITheme.Palette;
        _mdiClient.BackColor = p.Back;
        _watermarkAccent = BlendColor(p.Accent, p.Back, 0.12);
        _watermarkMuted  = BlendColor(p.Muted,  p.Back, 0.08);
        // Force a layout recompute on next paint in case the palette implies different metrics
        _watermarkLayoutForClientSize = Size.Empty;
        _mdiClient.Invalidate();
    }

    private static Color BlendColor(Color fg, Color bg, double factor)
    {
        return Color.FromArgb(
            (int)(fg.R * factor + bg.R * (1 - factor)),
            (int)(fg.G * factor + bg.G * (1 - factor)),
            (int)(fg.B * factor + bg.B * (1 - factor)));
    }

    private static void OnAssistClick() => ShowAbout();

    private async void OnOzClick()
    {
        if (_ozLauncherInProgress)
            return;

        _ozLauncherInProgress = true;
        try
        {
            ShowMdiChildNormal(new PerformanceMonitorForm());
            ShowMdiChildNormal(new TodoForm());
            TileMdiChildrenSoon();

            await ShowNewsAsync(
                () => new NewsService().GetTopTrAsync(15),
                "TR - En Önemli Haberler",
                openNormal: true);

            TileMdiChildrenSoon();

            if (!Application.OpenForms.OfType<ConnectionMonitorForm>().Any())
                new ConnectionMonitorForm().Show();

            if (!Application.OpenForms.OfType<WiggleMouseForm>().Any())
                new WiggleMouseForm().Show();
        }
        finally
        {
            _ozLauncherInProgress = false;
        }
    }

    private void TileMdiChildrenSoon()
    {
        BeginInvoke(() =>
        {
            foreach (var child in MdiChildren)
                child.WindowState = FormWindowState.Normal;

            var tileTimer = new System.Windows.Forms.Timer { Interval = 160 };
            tileTimer.Tick += (_, _) =>
            {
                tileTimer.Stop();
                tileTimer.Dispose();
                LayoutMdi(MdiLayout.TileVertical);
            };
            tileTimer.Start();
        });
    }

    /// <summary>MDI child'ı Normal (tile'a uygun) boyutta açar, zaten açıksa aktive eder.</summary>
    private void ShowMdiChildNormal(Form form)
    {
        var existing = MdiChildren.FirstOrDefault(c => c.GetType() == form.GetType());
        if (existing is not null)
        {
            form.Dispose();
            if (existing.WindowState == FormWindowState.Maximized)
                existing.WindowState = FormWindowState.Normal;
            existing.Activate();
            return;
        }

        form.MdiParent   = this;
        form.WindowState = FormWindowState.Normal;
        UITheme.Apply(form);
        EnsureDarkTitleBar(form);
        form.Show();
    }

    #endregion
}
