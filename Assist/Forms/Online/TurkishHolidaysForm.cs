namespace Assist.Forms.Online;

using Assist.Models;
using Assist.Services;

internal sealed class TurkishHolidaysForm : Form
{
    // ─── Row colours (dark-theme compatible) ────────────────────────────────────

    // Holiday type backgrounds (low-brightness tints)
    private static readonly Color COfficial  = Color.FromArgb(18,  42,  90);
    private static readonly Color CReligious = Color.FromArgb(16,  62,  28);
    private static readonly Color CHalfDay   = Color.FromArgb(82,  50,   8);

    // Bridge quality backgrounds (override type colour)
    private static readonly Color CBridgeBest  = Color.FromArgb(82,  68,   4);
    private static readonly Color CBridgeGood  = Color.FromArgb(58,  18,  88);
    private static readonly Color CBridgeWeak  = Color.FromArgb(44,  44,  44);
    private static readonly Color CAlready     = Color.FromArgb( 8,  62,  72);

    // Foreground accents
    private static readonly Color TOfficialFg   = Color.FromArgb(110, 165, 255);
    private static readonly Color TReligiousFg  = Color.FromArgb( 90, 215, 130);
    private static readonly Color THalfDayFg    = Color.FromArgb(240, 155,  55);
    private static readonly Color TBridgeBestFg = Color.FromArgb(255, 215,   0);
    private static readonly Color TBridgeGoodFg = Color.FromArgb(200, 130, 255);
    private static readonly Color TAlreadyFg    = Color.FromArgb( 80, 220, 235);
    private static readonly Color TWeakFg       = Color.FromArgb(150, 150, 150);

    // UI chrome
    private static readonly Color CBack    = Color.FromArgb(10,  10,  10);
    private static readonly Color CSurface = Color.FromArgb(20,  20,  20);
    private static readonly Color CBorder  = Color.FromArgb(48,  48,  48);
    private static readonly Color CText    = Color.FromArgb(200, 200, 200);
    private static readonly Color CMuted   = Color.FromArgb(120, 120, 120);

    // ─── Controls ───────────────────────────────────────────────────────────────

    private readonly NumericUpDown _nudYear;
    private readonly Button        _btnAll;
    private readonly Button        _btnOfficial;
    private readonly Button        _btnReligious;
    private readonly Button        _btnBridge;
    private readonly Button        _btnWeekday;
    private readonly Button        _btnRefresh;
    private readonly DataGridView  _grid;
    private readonly Label         _lblStatus;

    // Summary panel labels
    private readonly Label _lblTotalDays;
    private readonly Label _lblWeekdayCount;
    private readonly Label _lblWeekendCount;
    private readonly Label _lblBridgeCount;
    private readonly Label _lblBestOpportunity;

    // State
    private IReadOnlyList<HolidayViewModel>? _allRows;
    private string _activeFilter = "all";
    private CancellationTokenSource _cts = new();

    // ─── Constructor ────────────────────────────────────────────────────────────

    public TurkishHolidaysForm()
    {
        Text        = "📅 Türkiye Tatil Takvimi";
        ClientSize  = new Size(1150, 650);
        MinimumSize = new Size(900,  520);
        BackColor   = CBack;
        ForeColor   = CText;
        Font        = new Font("Consolas", 10);

        // ── Toolbar ─────────────────────────────────────────────────────────────
        var toolbar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 46,
            BackColor = CSurface,
        };
        toolbar.Paint += (_, e) =>
        {
            using var p = new Pen(CBorder);
            e.Graphics.DrawLine(p, 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);
        };

        var lblYear = new Label
        {
            Text      = "Yıl:",
            Location  = new Point(10, 14),
            AutoSize  = true,
            ForeColor = CMuted,
        };

        _nudYear = new NumericUpDown
        {
            Location  = new Point(42, 10),
            Width     = 72,
            Minimum   = 2020,
            Maximum   = 2030,
            Value     = DateTime.Today.Year,
            BackColor = CSurface,
            ForeColor = CText,
            Font      = new Font("Consolas", 10, FontStyle.Bold),
            TextAlign = HorizontalAlignment.Center,
        };

        _btnAll      = MakeFilterBtn("Tümü",      new Point(128, 8));
        _btnOfficial = MakeFilterBtn("Resmi",     new Point(204, 8));
        _btnReligious= MakeFilterBtn("Dini",      new Point(280, 8));
        _btnBridge   = MakeFilterBtn("Köprü",     new Point(356, 8));
        _btnWeekday  = MakeFilterBtn("Hafta İçi", new Point(432, 8));

        _btnRefresh = new Button
        {
            Text      = "🔄 Yenile",
            Location  = new Point(540, 8),
            Width     = 95,
            Height    = 30,
            BackColor = CSurface,
            ForeColor = CText,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Consolas", 9, FontStyle.Bold),
        };
        _btnRefresh.FlatAppearance.BorderColor = CBorder;

        toolbar.Controls.AddRange(
            [lblYear, _nudYear, _btnAll, _btnOfficial,
             _btnReligious, _btnBridge, _btnWeekday, _btnRefresh]);

        // ── Status bar ───────────────────────────────────────────────────────────
        _lblStatus = new Label
        {
            Dock      = DockStyle.Bottom,
            Height    = 22,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = CSurface,
            ForeColor = CMuted,
            Font      = new Font("Consolas", 9),
            Padding   = new Padding(6, 0, 0, 0),
        };

        // ── Split container ──────────────────────────────────────────────────────
        var split = new SplitContainer
        {
            Dock        = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            BackColor   = CBack,
        };

        // ── Grid ─────────────────────────────────────────────────────────────────
        _grid = new DataGridView
        {
            Dock                        = DockStyle.Fill,
            ReadOnly                    = true,
            AllowUserToAddRows          = false,
            AllowUserToResizeRows       = false,
            RowHeadersVisible           = false,
            SelectionMode               = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect                 = false,
            AutoSizeRowsMode            = DataGridViewAutoSizeRowsMode.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight         = 32,
            BackgroundColor             = CBack,
            GridColor                   = Color.FromArgb(35, 35, 35),
            EnableHeadersVisualStyles   = false,
            CellBorderStyle             = DataGridViewCellBorderStyle.SingleHorizontal,
        };
        _grid.RowTemplate.Height = 26;

        UITheme.Apply(_grid);
        AddGridColumns();
        _grid.CellFormatting += OnCellFormatting;

        split.Panel1.Controls.Add(_grid);

        // ── Summary panel ────────────────────────────────────────────────────────
        split.Panel2.Controls.Add(BuildSummaryPanel(
            out _lblTotalDays,
            out _lblWeekdayCount,
            out _lblWeekendCount,
            out _lblBridgeCount,
            out _lblBestOpportunity));

        Controls.Add(split);
        Controls.Add(_lblStatus);
        Controls.Add(toolbar);

        // ── Events ───────────────────────────────────────────────────────────────
        _nudYear.ValueChanged += async (_, _) => await LoadAsync();
        _btnRefresh.Click     += async (_, _) => await RefreshAsync();
        _btnAll.Click         += (_, _) => SetFilter("all");
        _btnOfficial.Click    += (_, _) => SetFilter("official");
        _btnReligious.Click   += (_, _) => SetFilter("religious");
        _btnBridge.Click      += (_, _) => SetFilter("bridge");
        _btnWeekday.Click     += (_, _) => SetFilter("weekday");

        Shown += async (_, _) =>
        {
            split.Panel1MinSize    = 500;
            split.Panel2MinSize    = 210;
            split.SplitterDistance = Math.Max(ClientSize.Width - 270, 500);
            await LoadAsync();
        };
        FormClosed += (_, _) => { _cts.Cancel(); _cts.Dispose(); };

        SetFilterButtonState("all");
    }

    // ─── Grid columns ────────────────────────────────────────────────────────────

    private void AddGridColumns()
    {
        _grid.Columns.AddRange(
        [
            Col("Name",      "Ad",              220, DataGridViewAutoSizeColumnMode.Fill),
            Col("Type",      "Tür",              58, DataGridViewAutoSizeColumnMode.None),
            Col("Start",     "Başlangıç",       100, DataGridViewAutoSizeColumnMode.None),
            Col("End",       "Bitiş",           100, DataGridViewAutoSizeColumnMode.None),
            Col("Days",      "Gün",              42, DataGridViewAutoSizeColumnMode.None),
            Col("WeekDay",   "Haftanın Günü",    96, DataGridViewAutoSizeColumnMode.None),
            Col("Weekend",   "H.Sonu",           62, DataGridViewAutoSizeColumnMode.None),
            Col("Bridge",    "Köprü Analizi",   220, DataGridViewAutoSizeColumnMode.Fill),
        ]);

        foreach (DataGridViewColumn col in _grid.Columns)
        {
            col.SortMode  = DataGridViewColumnSortMode.NotSortable;
            col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        _grid.Columns["Name"]!.DefaultCellStyle.Alignment   = DataGridViewContentAlignment.MiddleLeft;
        _grid.Columns["Bridge"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
    }

    private static DataGridViewTextBoxColumn Col(
        string name, string header, int width, DataGridViewAutoSizeColumnMode mode)
        => new()
        {
            Name            = name,
            HeaderText      = header,
            Width           = width,
            AutoSizeMode    = mode,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter },
        };

    // ─── Summary panel ───────────────────────────────────────────────────────────

    private Panel BuildSummaryPanel(
        out Label lblTotal,
        out Label lblWeekday,
        out Label lblWeekend,
        out Label lblBridge,
        out Label lblBest)
    {
        var panel = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = CSurface,
            Padding   = new Padding(14, 16, 10, 16),
        };

        int y = 10;

        var title = SummaryLabel("📊 ÖZET", ref y, bold: true);
        title.ForeColor = CText;
        Separator(panel, ref y);

        lblTotal   = SummaryRow(panel, "Toplam Tatil",    "—", ref y, TOfficialFg);
        lblWeekday = SummaryRow(panel, "Hafta İçi",       "—", ref y, CText);
        lblWeekend = SummaryRow(panel, "Hafta Sonu",      "—", ref y, CText);
        lblBridge  = SummaryRow(panel, "Köprü Fırsatı",  "—", ref y, TBridgeGoodFg);
        Separator(panel, ref y);

        var bestTitle = SummaryLabel("🌟 EN İYİ FIRSAT", ref y, bold: true);
        bestTitle.ForeColor = TBridgeBestFg;

        lblBest = new Label
        {
            Left      = 8,
            Top       = y,
            Width     = panel.Width - 16,
            Height    = 80,
            ForeColor = TBridgeBestFg,
            BackColor = Color.Transparent,
            Font      = new Font("Consolas", 9),
            AutoSize  = false,
            Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        y += 85;

        Separator(panel, ref y);
        BuildLegend(panel, ref y);

        panel.Controls.Add(title);
        panel.Controls.Add(bestTitle);
        panel.Controls.Add(lblBest);
        return panel;
    }

    private static Label SummaryLabel(string text, ref int y, bool bold = false)
    {
        var lbl = new Label
        {
            Text      = text,
            Left      = 8,
            Top       = y,
            AutoSize  = true,
            Font      = new Font("Consolas", bold ? 10 : 9, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = CText,
            BackColor = Color.Transparent,
        };
        y += 22;
        return lbl;
    }

    private static Label SummaryRow(
        Panel panel, string labelText, string value, ref int y, Color valueColor)
    {
        var key = new Label
        {
            Text      = labelText + ":",
            Left      = 8,
            Top       = y,
            Width     = 110,
            AutoSize  = false,
            ForeColor = CMuted,
            BackColor = Color.Transparent,
            Font      = new Font("Consolas", 9),
        };
        var val = new Label
        {
            Text      = value,
            Left      = 122,
            Top       = y,
            AutoSize  = true,
            ForeColor = valueColor,
            BackColor = Color.Transparent,
            Font      = new Font("Consolas", 9, FontStyle.Bold),
        };
        panel.Controls.Add(key);
        panel.Controls.Add(val);
        y += 20;
        return val;
    }

    private static void Separator(Panel panel, ref int y)
    {
        var sep = new Panel
        {
            Left      = 8,
            Top       = y,
            Width     = 180,
            Height    = 1,
            BackColor = CBorder,
            Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        panel.Controls.Add(sep);
        y += 10;
    }

    private static void BuildLegend(Panel panel, ref int y)
    {
        var title = new Label
        {
            Text      = "RENK AÇIKLAMASI",
            Left      = 8,
            Top       = y,
            AutoSize  = true,
            ForeColor = CMuted,
            BackColor = Color.Transparent,
            Font      = new Font("Consolas", 8, FontStyle.Bold),
        };
        panel.Controls.Add(title);
        y += 20;

        (string Text, Color Fg)[] items =
        [
            ("█ Resmi Tatil",     TOfficialFg),
            ("█ Dini Tatil",      TReligiousFg),
            ("█ Yarım Gün",       THalfDayFg),
            ("█ Köprü (İyi)",     TBridgeGoodFg),
            ("█ En İyi Fırsat",   TBridgeBestFg),
            ("█ Zaten Birleşiyor",TAlreadyFg),
        ];

        foreach (var (text, fg) in items)
        {
            var lbl = new Label
            {
                Text      = text,
                Left      = 8,
                Top       = y,
                AutoSize  = true,
                ForeColor = fg,
                BackColor = Color.Transparent,
                Font      = new Font("Consolas", 8),
            };
            panel.Controls.Add(lbl);
            y += 17;
        }
    }

    // ─── Filter buttons ──────────────────────────────────────────────────────────

    private Button MakeFilterBtn(string text, Point location)
    {
        var btn = new Button
        {
            Text      = text,
            Location  = location,
            Width     = 68,
            Height    = 30,
            BackColor = CSurface,
            ForeColor = CMuted,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Consolas", 9),
        };
        btn.FlatAppearance.BorderColor = CBorder;
        return btn;
    }

    private void SetFilter(string filter)
    {
        _activeFilter = filter;
        SetFilterButtonState(filter);
        PopulateGrid();
    }

    private void SetFilterButtonState(string active)
    {
        var map = new Dictionary<string, Button>
        {
            ["all"]      = _btnAll,
            ["official"] = _btnOfficial,
            ["religious"]= _btnReligious,
            ["bridge"]   = _btnBridge,
            ["weekday"]  = _btnWeekday,
        };
        foreach (var (key, btn) in map)
        {
            bool isActive = key == active;
            btn.ForeColor = isActive ? TBridgeBestFg : CMuted;
            btn.FlatAppearance.BorderColor = isActive ? TBridgeBestFg : CBorder;
        }
    }

    // ─── Data loading ────────────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        var year = (int)_nudYear.Value;
        SetStatus("Yükleniyor...");

        try
        {
            await Loading.RunAsync(this, async () =>
            {
                var holidays = await TurkishHolidayProvider
                    .GetHolidaysAsync(year, ct)
                    .ConfigureAwait(false);

                _allRows = BridgeAnalyzer.Analyze(holidays, year);
            });

            if (ct.IsCancellationRequested) return;

            PopulateGrid();
            SetStatus($"{year} yılı tatil takvimi yüklendi — {_allRows?.Count ?? 0} tatil");
        }
        catch (OperationCanceledException) { /* year changed quickly */ }
        catch (Exception ex)
        {
            SetStatus($"Hata: {ex.Message}");
        }
    }

    private async Task RefreshAsync()
    {
        TurkishHolidayProvider.InvalidateCache((int)_nudYear.Value);
        await LoadAsync();
    }

    // ─── Grid population ─────────────────────────────────────────────────────────

    private void PopulateGrid()
    {
        if (_allRows is null) return;

        var rows = _activeFilter switch
        {
            "official" => _allRows.Where(r => r.Type == HolidayType.Official),
            "religious"=> _allRows.Where(r => r.Type == HolidayType.Religious),
            "bridge"   => _allRows.Where(r => r.HasBridge),
            "weekday"  => _allRows.Where(r => !r.IsWeekend),
            _          => (IEnumerable<HolidayViewModel>)_allRows,
        };

        _grid.SuspendLayout();
        _grid.Rows.Clear();

        foreach (var vm in rows)
        {
            var typeLabel = vm.Type switch
            {
                HolidayType.Official  => "Resmi",
                HolidayType.Religious => "Dini",
                HolidayType.HalfDay   => "Yarım",
                _                     => "—",
            };

            int idx = _grid.Rows.Add(
                vm.Name,
                typeLabel,
                vm.StartDate.ToString("dd.MM.yyyy"),
                vm.EndDate.ToString("dd.MM.yyyy"),
                vm.DayCount,
                vm.WeekDay,
                vm.IsWeekend ? "Evet" : "Hayır",
                vm.BridgeSummary);

            ApplyRowStyle(_grid.Rows[idx], vm);
        }

        _grid.ResumeLayout();
        UpdateSummary();
    }

    private static void ApplyRowStyle(DataGridViewRow row, HolidayViewModel vm)
    {
        var (bg, fg) = vm.BridgeQuality switch
        {
            BridgeQuality.Best           => (CBridgeBest,  TBridgeBestFg),
            BridgeQuality.Good           => (CBridgeGood,  TBridgeGoodFg),
            BridgeQuality.Weak           => (CBridgeWeak,  TWeakFg),
            BridgeQuality.AlreadyConnected => (CAlready,   TAlreadyFg),
            _ => vm.Type switch
            {
                HolidayType.Religious => (CReligious, TReligiousFg),
                HolidayType.HalfDay   => (CHalfDay,   THalfDayFg),
                _                     => (COfficial,  TOfficialFg),
            },
        };

        row.DefaultCellStyle.BackColor = bg;
        row.DefaultCellStyle.ForeColor = fg;
        row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(
            Math.Min(bg.R + 40, 255),
            Math.Min(bg.G + 40, 255),
            Math.Min(bg.B + 40, 255));
        row.DefaultCellStyle.SelectionForeColor = Color.White;
    }

    // CellFormatting is intentionally left minimal — row style already set in ApplyRowStyle
    private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e) { }

    // ─── Summary update ──────────────────────────────────────────────────────────

    private void UpdateSummary()
    {
        if (_allRows is null) return;

        int total    = _allRows.Sum(r => r.DayCount);
        int weekday  = _allRows.Count(r => !r.IsWeekend);
        int weekend  = _allRows.Count(r => r.IsWeekend);
        int bridges  = _allRows.Count(r => r.HasBridge);

        _lblTotalDays.Text   = $"{total} gün";
        _lblWeekdayCount.Text = $"{weekday} tatil";
        _lblWeekendCount.Text = $"{weekend} tatil";
        _lblBridgeCount.Text  = $"{bridges} fırsat";

        var best = _allRows
            .Where(r => r.BridgeQuality is BridgeQuality.Best or BridgeQuality.Good)
            .OrderBy(r => r.BridgeQuality)
            .FirstOrDefault();

        _lblBestOpportunity.Text = best is not null
            ? $"{best.Name}\n{best.StartDate:dd.MM.yyyy}\n{best.BridgeSummary}"
            : "—";
    }

    // ─── Helpers

    private void SetStatus(string text)
    {
        if (InvokeRequired) { Invoke(() => SetStatus(text)); return; }
        _lblStatus.Text = "  " + text;
    }
}
