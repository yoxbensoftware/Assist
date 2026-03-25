namespace Assist;

using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using Assist.Forms.ClipboardTools;
using Assist.Forms.Core;
using Assist.Forms.DeveloperTools;
using Assist.Forms.Games;
using Assist.Forms.Online;
using Assist.Forms.Passwords;
using Assist.Forms.SystemTools;
using Assist.Models;
using Assist.Services;

internal partial class MainMDIForm : Form
{

    private ClipboardHistoryService? _clipboardHistory;

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
    private System.Windows.Forms.Timer? _fastTimer;   // 1s — clock, CPU/RAM
    private System.Windows.Forms.Timer? _mediumTimer;  // 30s — disk, battery, uptime, ping, app stats
    private System.Windows.Forms.Timer? _slowTimer;    // 5min — weather, currency, crypto, IP

    // Current-process monitor
    private readonly Process _selfProcess = Process.GetCurrentProcess();
    private TimeSpan _lastCpuTime = TimeSpan.Zero;
    private DateTime _lastCpuCheck = DateTime.MinValue;
    private long _lastNetRx;
    private long _lastNetTx;
    private Label? _lblProcBar;

    // Dashboard panel refs for theme refresh
    private Panel? _topBorderPanel;
    private Panel? _procBarPanel;
    private Label? _lblVersion;

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
        menuStrip.Items.Add(CreateThemeMenu());
        menuStrip.Items.Add(CreateDeveloperToolsMenu());
        menuStrip.Items.Add(CreateClipboardMenu());
        menuStrip.Items.Add(CreateGamesMenu());
        menuStrip.Items.Add(CreateWindowMenu());

        // Right-aligned items
        menuStrip.Items.Add(new ToolStripLabel("Oz") { Alignment = ToolStripItemAlignment.Right, ForeColor = UITheme.Palette.Accent });
        menuStrip.Items.Add(CreateMenuItem("Hakkında", ShowAbout, ToolStripItemAlignment.Right));

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

        // System information
        menu.DropDownItems.Add(CreateMenuItem("Sistem Bilgisi", () => ShowMdiChild(new SystemInfoForm())));
        menu.DropDownItems.Add(CreateMenuItem("Performance Monitor", () => ShowMdiChild(new PerformanceMonitorForm())));
        menu.DropDownItems.Add(CreateMenuItem("Speed Test", () => ShowMdiChild(new SpeedTestForm())));

        menu.DropDownItems.Add(new ToolStripSeparator());

        // Management tools
        menu.DropDownItems.Add(CreateMenuItem("Startup Manager", () => ShowMdiChild(new StartupManagerForm())));
        menu.DropDownItems.Add(CreateMenuItem("Disk Temizleyici", () => ShowMdiChild(new DiskCleanerForm())));
        menu.DropDownItems.Add(CreateMenuItem("Hosts File Editor", () => ShowMdiChild(new HostsFileEditorForm())));
        menu.DropDownItems.Add(CreateMenuItem("Donanım Sorun Giderici", () => ShowMdiChild(new HardwareDiagnosticsForm())));
        menu.DropDownItems.Add(CreateMenuItem("Wi-Fi Şifreleri", () => ShowMdiChild(new WifiPasswordForm())));

        menu.DropDownItems.Add(new ToolStripSeparator());

        // Network tools
        menu.DropDownItems.Add(CreateAsyncMenuItem("DNS Reset", RunDnsResetAsync));
        menu.DropDownItems.Add(CreateMenuItem("Ağ Bağlantı Tarayıcı", () => ShowMdiChild(new NetworkScannerForm())));

        menu.DropDownItems.Add(new ToolStripSeparator());

        // Security
        menu.DropDownItems.Add(CreateMenuItem("Tehdit Tarayıcı", () => ShowMdiChild(new ThreatScannerForm())));

        menu.DropDownItems.Add(new ToolStripSeparator());

        // EV Charger Finder
        menu.DropDownItems.Add(CreateMenuItem("EV Şarj İstasyonu Bulucu", () => ShowMdiChild(new EvChargerFinderForm())));

        menu.DropDownItems.Add(new ToolStripSeparator());

        // Wiggle Mouse
        menu.DropDownItems.Add(CreateMenuItem("Wiggle Mouse", () => ShowMdiChild(new WiggleMouseForm())));

        return menu;
    }

    private ToolStripMenuItem CreateOnlineMenu()
    {
        var menu = new ToolStripMenuItem("Online İşlemler");

        // News
        menu.DropDownItems.Add(CreateAsyncMenuItem("TR - Top 30", () => ShowNewsAsync(() => new NewsService().GetTopTrAsync(30), "TR - En Önemli Haberler (Top 30)")));
        menu.DropDownItems.Add(CreateAsyncMenuItem("Global - Top 30 (Türkçe)", () => ShowNewsAsync(() => new NewsService().GetTopGlobalAsync(30), "Global - Top 30 (Türkçe)")));
        menu.DropDownItems.Add(CreateAsyncMenuItem("Teknoloji - Top 30", () => ShowNewsAsync(() => new NewsService().GetTopTechAsync(30), "Teknoloji - Top 30 (Türkçe)")));

        menu.DropDownItems.Add(new ToolStripSeparator());

        // Online tools
        menu.DropDownItems.Add(CreateMenuItem("Wikipedia Arama", () => ShowMdiChild(new WikipediaSearchForm())));
        menu.DropDownItems.Add(CreateMenuItem("Sözlük (EN)", () => ShowMdiChild(new DictionaryForm())));
        menu.DropDownItems.Add(CreateMenuItem("IP / Domain Sorgula", () => ShowMdiChild(new IpDomainQueryForm())));
        menu.DropDownItems.Add(CreateMenuItem("WHOIS / Alan Adı", () => ShowMdiChild(new WhoisForm())));
        menu.DropDownItems.Add(CreateMenuItem("Deprem Takibi", () => ShowMdiChild(new EarthquakeForm())));
        menu.DropDownItems.Add(CreateMenuItem("Döviz Çevirici", () => ShowMdiChild(new CurrencyConverterForm())));
        menu.DropDownItems.Add(CreateMenuItem("Piyasa 20", () => ShowMdiChild(new ExchangeRatesForm())));

        return menu;
    }

    private ToolStripMenuItem CreateDeveloperToolsMenu()
    {
        var menu = new ToolStripMenuItem("Geliştirici Araçları");

        // Data formatters
        menu.DropDownItems.Add(CreateMenuItem("JSON Formatter/Validator", () => ShowMdiChild(new JsonFormatterForm())));
        menu.DropDownItems.Add(CreateMenuItem("XML Formatter", () => ShowMdiChild(new XmlFormatterForm())));
        menu.DropDownItems.Add(CreateMenuItem("Pretty XML", () => ShowMdiChild(new PrettyXmlForm())));

        menu.DropDownItems.Add(new ToolStripSeparator());

        // Testing tools
        menu.DropDownItems.Add(CreateMenuItem("Regex Tester", () => ShowMdiChild(new RegexTesterForm())));
        menu.DropDownItems.Add(CreateMenuItem("Text Diff Tool", () => ShowMdiChild(new TextDiffForm())));

        menu.DropDownItems.Add(new ToolStripSeparator());

        // Generators and utilities
        menu.DropDownItems.Add(CreateMenuItem("Base64 Encoder/Decoder", () => ShowMdiChild(new Base64ConverterForm())));
        menu.DropDownItems.Add(CreateMenuItem("Color Picker", () => ShowMdiChild(new ColorPickerForm())));
        menu.DropDownItems.Add(CreateMenuItem("QR Code Generator", () => ShowMdiChild(new QrCodeForm())));
        menu.DropDownItems.Add(CreateMenuItem("UUID/GUID Generator", () => ShowMdiChild(new UuidGeneratorForm())));
        menu.DropDownItems.Add(CreateMenuItem("Lorem Ipsum Generator", () => ShowMdiChild(new LoremIpsumForm())));
        menu.DropDownItems.Add(CreateMenuItem("Hash Generator", () => ShowMdiChild(new HashGeneratorForm())));

        menu.DropDownItems.Add(new ToolStripSeparator());

        // Utilities
        menu.DropDownItems.Add(CreateMenuItem("Birim Çevirici", () => ShowMdiChild(new UnitConverterForm())));
        menu.DropDownItems.Add(CreateMenuItem("Sprint Holiday Analyzer", () => ShowMdiChild(new SprintHolidayAnalyzerForm())));

        return menu;
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
                break;
            }
        }
    }

    private void ShowMdiChild(Form form)
    {
        form.MdiParent = this;
        form.WindowState = FormWindowState.Maximized;
        UITheme.Apply(form);
        form.Show();
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

            var newsForm = new NewsForm(title);
            ShowMdiChild(newsForm);
            newsForm.SetNews(items);
        }, "Haberler yükleniyor...");
    }

    private async Task RunDnsResetAsync()
    {
        var confirm = MessageBox.Show(
            "Bu işlem ağ bağlantınızı geçici olarak kesebilir ve yönetici izni gerektirebilir. Devam etmek istiyor musunuz?",
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

        var outputForm = new CommandOutputForm();
        ShowMdiChild(outputForm);
        outputForm.SetOutput(output);
    }

    private static void ShowAbout()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetEntryAssembly()
                ?? System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "1.0.0";

            var message = $"""
                {AppConstants.AppTitle}
                Sürüm: {version}
                © 2026 Assist

                Bu uygulama şifre yönetimi ve sistem bilgisi özellikleri sağlar.
                Geliştirici: Oz
                """;

            MessageBox.Show(message, "Hakkında", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch
        {
            MessageBox.Show("Assist - Hakkında", "Hakkında", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        typeof(Panel).InvokeMember("DoubleBuffered",
            System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, _dashboardPanel, [true]);

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
            Font = new Font("Consolas", 8, FontStyle.Regular),
            ForeColor = Color.FromArgb(80, 180, 255),
            BackColor = p.Surface2,
            TextAlign = ContentAlignment.MiddleLeft
        };
        typeof(Panel).InvokeMember("DoubleBuffered",
            System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, _procBarPanel, [true]);
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
        _lblClock = CreateDashboardLabel("⏰ --:--:--", 10, FontStyle.Bold);
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
            Font = new Font("Consolas", 8, FontStyle.Regular),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleRight
        };
        table.Controls.Add(_lblVersion, 1, 5);

        typeof(Control).InvokeMember("DoubleBuffered",
            System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, table, [true]);

        // Set non-transparent background on fast-updating labels to prevent flicker
        _lblClock!.BackColor = p.Surface;
        _lblCpuRam!.BackColor = p.Surface;

        _dashboardPanel.Controls.Add(table);
        Controls.Add(_dashboardPanel);

        // ── Timers ──

        // Fast: every 1 second — clock, CPU/RAM
        _fastTimer = new System.Windows.Forms.Timer { Interval = 1_000 };
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

    private static Label CreateDashboardLabel(string text, int fontSize = 9, FontStyle style = FontStyle.Regular)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            AutoSize = false,
            ForeColor = UITheme.Palette.Text,
            Font = new Font("Consolas", fontSize, style),
            BackColor = UITheme.Palette.Surface,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    /// <summary>Clock + CPU/RAM — every 1 second.</summary>
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

            // System-wide network delta (KB/s)
            long rx = 0, tx = 0;
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                var stats = ni.GetIPv4Statistics();
                rx += stats.BytesReceived;
                tx += stats.BytesSent;
            }
            var rxKb = _lastNetRx > 0 ? (rx - _lastNetRx) / 1024.0 : 0;
            var txKb = _lastNetTx > 0 ? (tx - _lastNetTx) / 1024.0 : 0;
            _lastNetRx = rx;
            _lastNetTx = tx;

            var procText =
                $"  ► ASSIST  |  💾 RAM: {ram} MB" +
                $"  |  🖥 CPU: {cpu:F1}%" +
                $"  |  🔀 Threads: {_selfProcess.Threads.Count}" +
                $"  |  🌐 ↓ {rxKb:F0} KB/s  ↑ {txKb:F0} KB/s";
            if (_lblProcBar.Text != procText)
                _lblProcBar.Text = procText;
        }
        catch { }
    }

    /// <summary>Disk, battery, uptime, ping, app stats — every 30 seconds.</summary>
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
        catch { }
    }

    /// <summary>Weather, currency, crypto, IP — every 5 minutes.</summary>
    private async Task RefreshSlowAsync()
    {
        try
        {
            // Fetch IP first so detected city is available for weather
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
        catch { }
    }

    #endregion
}
