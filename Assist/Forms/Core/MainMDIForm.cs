namespace Assist;

using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using Assist.Forms.ClipboardTools;
using Assist.Forms.Core;
using Assist.Forms.DeveloperTools;
using Assist.SDLC.Domain;
using Assist.SDLC.Forms;
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

    // SDLC agent detail forms — keyed by AgentRole for deduplication
    private readonly Dictionary<AgentRole, AgentDetailForm> _agentForms = [];

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

    // Current-process monitor
    private readonly Process _selfProcess = Process.GetCurrentProcess();
    private TimeSpan _lastCpuTime = TimeSpan.Zero;
    private DateTime _lastCpuCheck = DateTime.MinValue;
    private long _lastNetRx;
    private long _lastNetTx;
    private DateTime _lastNetCheck = DateTime.MinValue;
    private double _cachedRxKbPerSec;
    private double _cachedTxKbPerSec;
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
    private static readonly Font WatermarkLargeFont = new("Consolas", 60, FontStyle.Bold);
    private static readonly Font WatermarkSmallFont = new("Consolas", 22);

    public MainMDIForm()
    {
        InitializeComponent();
        IsMdiContainer = true;
        ThemeService.ThemeChanged += OnThemeChanged;
        FormClosed += OnFormClosed;
        InitializeMenu();
        ApplyTheme();
        EnsureClipboardHistory();
        LoadIcon();
        InitializeDashboardPanel();
        InitializeWatermark();
        HandleCreated += (_, _) => ApplyDarkTitleBar(this);
        Shown += async (_, _) => await CheckForUpdateAsync(silent: true);
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
            return base.ProcessCmdKey(ref msg, keyData);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
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

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        ThemeService.ThemeChanged -= OnThemeChanged;
        _fastTimer?.Stop();
        _fastTimer?.Dispose();
        _mediumTimer?.Stop();
        _mediumTimer?.Dispose();
        _slowTimer?.Stop();
        _slowTimer?.Dispose();
        _selfProcess.Dispose();
    }

    private void InitializeMenu()
    {
        var menuStrip = new MenuStrip
        {
            RenderMode = ToolStripRenderMode.System
        };

        // Main menus
        menuStrip.Items.Add(CreatePasswordMenu());
        menuStrip.Items.Add(CreateSystemToolsMenu());
        menuStrip.Items.Add(CreateOnlineMenu());
        menuStrip.Items.Add(CreateTodoMenuItem());
        menuStrip.Items.Add(CreateThemeMenu());
        menuStrip.Items.Add(CreateDeveloperToolsMenu());
        menuStrip.Items.Add(CreateClipboardMenu());
        menuStrip.Items.Add(CreateSdlcMenu());
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
        news.DropDownItems.Add(CreateAsyncMenuItem("Global - Top 30 (Türkçe)", () => ShowNewsAsync(() => new NewsService().GetTopGlobalAsync(30), "Global - Top 30 (Türkçe)")));
        news.DropDownItems.Add(CreateAsyncMenuItem("Teknoloji - Top 30", () => ShowNewsAsync(() => new NewsService().GetTopTechAsync(30), "Teknoloji - Top 30 (Türkçe)")));
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

    private ToolStripMenuItem CreateSdlcMenu()
    {
        var menu = new ToolStripMenuItem("AI SDLC Orchestrator");

        // ── 🎯 Yönetim ───────────────────────────────
        var mgmt = new ToolStripMenuItem("🎯 Yönetim");
        mgmt.DropDownItems.Add(CreateMenuItem("Dashboard", () => ShowMdiChild(new SdlcDashboardForm())));
        mgmt.DropDownItems.Add(CreateMenuItem("Task Intake", () => ShowMdiChild(new TaskIntakeForm())));
        mgmt.DropDownItems.Add(CreateMenuItem("Session / IDE Manager", () => ShowMdiChild(new SessionManagerForm())));
        menu.DropDownItems.Add(mgmt);

        // ── 🤖 Agent'lar ─────────────────────────────
        var agents = new ToolStripMenuItem("🤖 Agent'lar");
        agents.DropDownItems.Add(CreateMenuItem("Agent Console Hub", () => ShowMdiChild(new AgentConsoleHubForm())));
        agents.DropDownItems.Add(new ToolStripSeparator());
        agents.DropDownItems.Add(CreateMenuItem("Product Owner Agent", () => ShowAgentDetail(AgentRole.ProductOwner)));
        agents.DropDownItems.Add(CreateMenuItem("Analyst Agent", () => ShowAgentDetail(AgentRole.Analyst)));
        agents.DropDownItems.Add(CreateMenuItem("Architect Agent", () => ShowAgentDetail(AgentRole.Architect)));
        agents.DropDownItems.Add(CreateMenuItem("Developer Agent", () => ShowAgentDetail(AgentRole.Developer)));
        agents.DropDownItems.Add(CreateMenuItem("Tester Agent", () => ShowAgentDetail(AgentRole.Tester)));
        agents.DropDownItems.Add(CreateMenuItem("Reviewer Agent", () => ShowAgentDetail(AgentRole.Reviewer)));
        agents.DropDownItems.Add(CreateMenuItem("Documentation Agent", () => ShowAgentDetail(AgentRole.Documentation)));
        menu.DropDownItems.Add(agents);

        // ── 🧑‍💼 İnsan Kontrol ──────────────────────
        var human = new ToolStripMenuItem("🧑‍💼 İnsan Kontrol");
        human.DropDownItems.Add(CreateMenuItem("Human Decision Console", () => ShowMdiChild(new HumanDecisionConsoleForm())));
        menu.DropDownItems.Add(human);

        // ── 📡 İzleme ────────────────────────────────
        var monitoring = new ToolStripMenuItem("📡 İzleme");
        monitoring.DropDownItems.Add(CreateMenuItem("Console Runner", () => ShowMdiChild(new ConsoleRunnerForm())));
        monitoring.DropDownItems.Add(CreateMenuItem("Notifications Center", () => ShowMdiChild(new NotificationsCenterForm())));
        monitoring.DropDownItems.Add(CreateMenuItem("Waiting Queue Monitor", () => ShowMdiChild(new WaitingQueueForm())));
        monitoring.DropDownItems.Add(CreateMenuItem("Timeline / Iteration Monitor", () => ShowMdiChild(new TimelineForm())));
        menu.DropDownItems.Add(monitoring);

        // ── 📋 Raporlama ─────────────────────────────
        var reports = new ToolStripMenuItem("📋 Raporlama");
        reports.DropDownItems.Add(CreateMenuItem("Reports & Outputs", () => ShowMdiChild(new ReportsForm())));
        menu.DropDownItems.Add(reports);

        menu.DropDownItems.Add(new ToolStripSeparator());

        menu.DropDownItems.Add(CreateMenuItem("⚙️ Settings", () => ShowMdiChild(new SdlcSettingsForm())));

        return menu;
    }

    /// <summary>
    /// Opens (or activates) an <see cref="AgentDetailForm"/> for the given role.
    /// Uses a role-keyed dictionary instead of the standard type-based deduplication
    /// because all agent detail forms share the same <see cref="Type"/>.
    /// </summary>
    private void ShowAgentDetail(AgentRole role)
    {
        if (_agentForms.TryGetValue(role, out var existing) && !existing.IsDisposed)
        {
            existing.Activate();
            return;
        }

        var form = new AgentDetailForm(role)
        {
            MdiParent = this,
            WindowState = FormWindowState.Maximized
        };

        UITheme.Apply(form);
        EnsureDarkTitleBar(form);
        form.FormClosed += (_, _) => _agentForms.Remove(role);
        _agentForms[role] = form;
        form.Show();
    }

    private ToolStripMenuItem CreateClipboardMenu()
    {
        var menu = new ToolStripMenuItem("Pano");

        var historyItem = new ToolStripMenuItem("Pano Geçmişi");
        historyItem.Click += (_, _) =>
        {
            EnsureClipboardHistory();
            ShowMdiChild(new ClipboardHistoryForm(_clipboardHistory!));
        };

        var clearItem = new ToolStripMenuItem("Pano Temizle");
        clearItem.Click += (_, _) =>
        {
            if (_clipboardHistory is null) return;

            var result = MessageBox.Show(
                "Tüm pano geçmişini silmek istediğinize emin misiniz?",
                "Onay",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                _clipboardHistory.Clear();
                MessageBox.Show("Pano geçmişi temizlendi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        menu.DropDownItems.Add(historyItem);

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

    private void ApplyThemeSelection(AppTheme theme)
    {
        ThemeService.SetTheme(theme);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        BeginInvoke(new Action(() =>
        {
            UITheme.ApplyToOpenForms();
            ApplyDashboardTheme();
            ApplyWatermarkTheme();
        }));
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
        if (_clipboardHistory is not null) return;

        _clipboardHistory = new ClipboardHistoryService(50, filterSensitive: true);
        _clipboardHistory.Start(1000);
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

    private async Task ShowNewsAsync(Func<Task<List<NewsItem>>> fetcher, string title)
    {
        await Loading.RunAsync(this, async () =>
        {
            var items = await fetcher();

            foreach (var item in items)
            {
                item.Title = await TranslationService.TranslateAsync(item.Title, "tr");
                if (!string.IsNullOrEmpty(item.Summary))
                {
                    item.Summary = await TranslationService.TranslateAsync(item.Summary, "tr");
                }
            }

            // Reuse existing NewsForm if open
            var existing = MdiChildren.OfType<NewsForm>().FirstOrDefault();
            if (existing is not null)
            {
                existing.Text = title;
                existing.SetNews(items);
                existing.Activate();
                return;
            }

            var newsForm = new NewsForm(title);
            newsForm.SetNews(items); // set BEFORE ShowMdiChild to avoid disposed-form exception
            ShowMdiChild(newsForm);
        }, "Haberler yükleniyor...");
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

        // ── Timers ──

        // Fast: every 2 seconds — clock, CPU/RAM (reduced from 1s to lower GC pressure)
        _fastTimer = new System.Windows.Forms.Timer { Interval = 2_000 };
        _fastTimer.Tick += (_, _) => RefreshFast();
        _fastTimer.Start();

        // Medium: every 30 seconds — disk, battery, uptime, ping, app stats
        _mediumTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _mediumTimer.Tick += async (_, _) => await RefreshMediumAsync();
        _mediumTimer.Start();

        // Slow: every 5 minutes — weather, currency, crypto, IP
        _slowTimer = new System.Windows.Forms.Timer { Interval = 300_000 };
        _slowTimer.Tick += async (_, _) => await RefreshSlowAsync();
        _slowTimer.Start();

        // Initial load
        RefreshFast();
        _ = RefreshMediumAsync();
        _ = RefreshSlowAsync();
        ApplyDashboardTheme();
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

    /// <summary>Clock + CPU/RAM — every 2 seconds.</summary>
    private void RefreshFast()
    {
        if (_lblClock is not null)
        {
            var clockText = $"⏰ {DateTime.Now:HH:mm:ss}  📅 {DateTime.Now:dddd, dd MMMM yyyy}";
            if (_lblClock.Text != clockText)
                _lblClock.Text = clockText;
        }

        if (_lblCpuRam is not null)
        {
            var cpuRamText = DashboardService.GetCpuRam();
            if (_lblCpuRam.Text != cpuRamText)
                _lblCpuRam.Text = cpuRamText;
        }

        RefreshProcessBar();
    }

    private void RefreshProcessBar()
    {
        if (_lblProcBar is null) return;
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
                $"  |  \ud83d\udd00 Threads: {_selfProcess.Threads.Count}" +
                $"  |  \ud83c\udf10 \u2193 {_cachedRxKbPerSec:F0} KB/s  \u2191 {_cachedTxKbPerSec:F0} KB/s";
            if (_lblProcBar.Text != procText)
                _lblProcBar.Text = procText;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProcBar] Refresh failed: {ex.Message}");
        }
    }
    private async Task RefreshMediumAsync()
    {
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

            if (InvokeRequired) Invoke(Update); else Update();
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

            void Update()
            {
                if (_lblIpInfo is not null) _lblIpInfo.Text = ipResult;
                if (_lblWeather is not null) _lblWeather.Text = weatherTask.Result;
                if (_lblCurrency is not null) _lblCurrency.Text = currencyTask.Result;
                if (_lblCrypto is not null) _lblCrypto.Text = cryptoTask.Result;
            }

                if (InvokeRequired) Invoke(Update); else Update();
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

        _mdiClient.Paint      += MdiClient_Paint;
        _mdiClient.Resize     += (_, _) => _mdiClient.Invalidate();
        _mdiClient.MouseClick += MdiClient_MouseClick;
    }

    private void MdiClient_Paint(object? sender, PaintEventArgs e)
    {
        if (_mdiClient is null) return;
        var g = e.Graphics;

        var cw = _mdiClient.ClientSize.Width;
        var ch = _mdiClient.ClientSize.Height;

        var sAssist = TextRenderer.MeasureText(g, "Assist", WatermarkLargeFont);
        var sBy     = TextRenderer.MeasureText(g, "By",     WatermarkSmallFont);
        var sOz     = TextRenderer.MeasureText(g, "Oz",     WatermarkLargeFont);

        var totalWidth = sAssist.Width + sBy.Width + sOz.Width + 8;
        var maxHeight  = Math.Max(sAssist.Height, Math.Max(sBy.Height, sOz.Height));

        var startX  = (cw - totalWidth) / 2;
        var centerY = (ch - maxHeight)  / 2;

        _rcAssist = new Rectangle(startX, centerY, sAssist.Width, sAssist.Height);
        _rcBy     = new Rectangle(startX + sAssist.Width + 4, centerY + sAssist.Height - sBy.Height, sBy.Width, sBy.Height);
        _rcOz     = new Rectangle(startX + sAssist.Width + sBy.Width + 8, centerY, sOz.Width, sOz.Height);

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

    private void OnOzClick()
    {
        _ = ShowNewsAsync(() => new NewsService().GetTopTrAsync(30), "TR - En Önemli Haberler (Top 30)");
        if (!Application.OpenForms.OfType<ConnectionMonitorForm>().Any())
            new ConnectionMonitorForm().Show();
        ShowMdiChild(new PerformanceMonitorForm());
    }

    #endregion
}
