namespace Assist.Forms.DeveloperTools;

using System.Globalization;
using System.Text;
using Assist.Models;
using Assist.Services;

/// <summary>
/// Sprint Holiday Risk Analyzer — analyzes sprint date ranges against
/// Turkey + Germany public holidays and calculates capacity loss risk.
/// Supports TR / EN / DE UI language.
/// </summary>
internal sealed class SprintHolidayAnalyzerForm : Form
{
    private static readonly Color HighColor = Color.FromArgb(220, 50, 50);
    private static readonly Color MediumColor = Color.FromArgb(230, 160, 0);
    private static readonly Color LowColor = Color.FromArgb(0, 180, 80);

    private readonly HolidayAnalyzerService _service = new();
    private Lang _lang = Lang.TR;

    // Static labels — updated on language change
    private readonly Label _lblStart;
    private readonly Label _lblEnd;
    private readonly Button _btnAnalyze;
    private readonly Label _lblSummaryTitle;
    private readonly Label _lblTotalHolidaysDim;
    private readonly Label _lblWeekdayHolidaysDim;
    private readonly Label _lblWeekendHolidaysDim;
    private readonly Label _lblSprintDaysDim;
    private readonly Label _lblCapacityLossDim;
    private readonly Label _lblRiskTitle;
    private readonly Label _lblFilterTitle;
    private readonly CheckBox _chkTR;
    private readonly CheckBox _chkDE;
    private readonly Button _btnLangTR;
    private readonly Button _btnLangEN;
    private readonly Button _btnLangDE;

    // Dynamic output labels
    private readonly DateTimePicker _dtpStart;
    private readonly DateTimePicker _dtpEnd;
    private readonly DataGridView _dgv;
    private readonly Label _lblRisk;
    private readonly Label _lblTotalHolidays;
    private readonly Label _lblWeekdayHolidays;
    private readonly Label _lblWeekendHolidays;
    private readonly Label _lblSprintDays;
    private readonly Label _lblCapacityLoss;
    private readonly Panel _summaryPanel;

    private SprintAnalysisResult? _lastResult;

    public SprintHolidayAnalyzerForm()
    {
        var s = GetStr(_lang);
        Text = s.Title;
        ClientSize = new Size(1060, 660);
        BackColor = Color.FromArgb(24, 24, 28);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);

        // ── Top input panel ──
        var inputPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.FromArgb(30, 30, 36),
            Padding = new Padding(12, 10, 12, 6)
        };

        _lblStart = MakeLabel(s.LblStart, 14, 18);
        _dtpStart = MakeDatePicker(152, 14, new DateTime(2026, 5, 25));

        _lblEnd = MakeLabel(s.LblEnd, 368, 18);
        _dtpEnd = MakeDatePicker(468, 14, new DateTime(2026, 6, 7));

        _btnAnalyze = new Button
        {
            Text = s.BtnAnalyze,
            Location = new Point(670, 12),
            Size = new Size(126, 34),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnAnalyze.FlatAppearance.BorderSize = 0;
        _btnAnalyze.Click += (_, _) => RunAnalysis();

        var btnExport = new Button
        {
            Text = s.BtnExport,
            Location = new Point(804, 12),
            Size = new Size(126, 34),
            BackColor = Color.FromArgb(50, 50, 56),
            ForeColor = Color.LightGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnExport.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 86);
        btnExport.Click += (_, _) => ExportCsv();

        // Language toggle buttons
        _btnLangTR = MakeLangButton("TR", 942, active: true);
        _btnLangEN = MakeLangButton("EN", 976, active: false);
        _btnLangDE = MakeLangButton("DE", 1010, active: false);
        _btnLangTR.Click += (_, _) => SetLanguage(Lang.TR);
        _btnLangEN.Click += (_, _) => SetLanguage(Lang.EN);
        _btnLangDE.Click += (_, _) => SetLanguage(Lang.DE);

        inputPanel.Controls.AddRange([_lblStart, _dtpStart, _lblEnd, _dtpEnd,
            _btnAnalyze, btnExport, _btnLangTR, _btnLangEN, _btnLangDE]);

        // ── Summary panel (right side) ──
        _summaryPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 280,
            BackColor = Color.FromArgb(30, 30, 36),
            Padding = new Padding(16, 12, 16, 12)
        };

        int sy = 14;
        _lblSummaryTitle = MakeSectionLabel(s.Summary, 12, sy);
        _summaryPanel.Controls.Add(_lblSummaryTitle);

        sy += 40;
        _lblTotalHolidaysDim = MakeDimLabel(s.TotalHol, 12, sy);
        _lblTotalHolidays = MakeValueLabel("\u2014", 170, sy);
        _summaryPanel.Controls.AddRange([_lblTotalHolidaysDim, _lblTotalHolidays]);

        sy += 30;
        _lblWeekdayHolidaysDim = MakeDimLabel(s.WeekdayHol, 12, sy);
        _lblWeekdayHolidays = MakeValueLabel("\u2014", 170, sy);
        _summaryPanel.Controls.AddRange([_lblWeekdayHolidaysDim, _lblWeekdayHolidays]);

        sy += 30;
        _lblWeekendHolidaysDim = MakeDimLabel(s.WeekendHol, 12, sy);
        _lblWeekendHolidays = MakeValueLabel("\u2014", 170, sy);
        _summaryPanel.Controls.AddRange([_lblWeekendHolidaysDim, _lblWeekendHolidays]);

        sy += 40;
        _summaryPanel.Controls.Add(MakeSeparator(sy));

        sy += 14;
        _lblSprintDaysDim = MakeDimLabel(s.SprintDays, 12, sy);
        _lblSprintDays = MakeValueLabel("\u2014", 170, sy);
        _summaryPanel.Controls.AddRange([_lblSprintDaysDim, _lblSprintDays]);

        sy += 30;
        _lblCapacityLossDim = MakeDimLabel(s.Capacity, 12, sy);
        _lblCapacityLoss = MakeValueLabel("\u2014", 170, sy);
        _summaryPanel.Controls.AddRange([_lblCapacityLossDim, _lblCapacityLoss]);

        sy += 50;
        _summaryPanel.Controls.Add(MakeSeparator(sy));

        sy += 14;
        _lblRiskTitle = MakeSectionLabel(s.RiskTitle, 12, sy);
        _summaryPanel.Controls.Add(_lblRiskTitle);

        sy += 36;
        _lblRisk = new Label
        {
            Text = "\u2014",
            Location = new Point(12, sy),
            Size = new Size(240, 60),
            Font = new Font("Segoe UI", 11),
            ForeColor = Color.Gray,
            TextAlign = ContentAlignment.TopLeft
        };
        _summaryPanel.Controls.Add(_lblRisk);

        // Country filter checkboxes
        sy += 80;
        _summaryPanel.Controls.Add(MakeSeparator(sy));
        sy += 14;
        _lblFilterTitle = MakeSectionLabel(s.Filter, 12, sy);
        _summaryPanel.Controls.Add(_lblFilterTitle);

        sy += 36;
        _chkTR = new CheckBox
        {
            Text = s.CtrTR,
            Location = new Point(12, sy),
            AutoSize = true,
            Checked = true,
            ForeColor = Color.LightGray,
            Font = new Font("Segoe UI", 9),
            Tag = "TR"
        };

        _chkDE = new CheckBox
        {
            Text = s.CtrDE,
            Location = new Point(130, sy),
            AutoSize = true,
            Checked = true,
            ForeColor = Color.LightGray,
            Font = new Font("Segoe UI", 9),
            Tag = "DE"
        };

        _chkTR.CheckedChanged += (_, _) => RunAnalysis();
        _chkDE.CheckedChanged += (_, _) => RunAnalysis();

        _summaryPanel.Controls.AddRange([_chkTR, _chkDE]);

        // ── Data grid (center) ──
        _dgv = CreateGrid();

        // Localized tooltips — registered here to access _lang at hover time
        _dgv.CellToolTipTextNeeded += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 5) return;
            var val = _dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
            var ts = GetStr(_lang);
            e.ToolTipText = val switch
            {
                "High" => ts.TipHigh,
                "Medium" => ts.TipMedium,
                _ => ts.TipLow
            };
        };

        // ── Layout assembly (order matters for Dock) ──
        Controls.Add(_dgv);
        Controls.Add(_summaryPanel);
        Controls.Add(inputPanel);

        Load += (_, _) => RunAnalysis();
    }

    // ── Language switching ──

    private void SetLanguage(Lang lang)
    {
        _lang = lang;
        ApplyLanguage();
        RunAnalysis();
    }

    private void ApplyLanguage()
    {
        var s = GetStr(_lang);
        Text = s.Title;
        _lblStart.Text = s.LblStart;
        _lblEnd.Text = s.LblEnd;
        _btnAnalyze.Text = s.BtnAnalyze;
        _lblSummaryTitle.Text = s.Summary;
        _lblTotalHolidaysDim.Text = s.TotalHol;
        _lblWeekdayHolidaysDim.Text = s.WeekdayHol;
        _lblWeekendHolidaysDim.Text = s.WeekendHol;
        _lblSprintDaysDim.Text = s.SprintDays;
        _lblCapacityLossDim.Text = s.Capacity;
        _lblRiskTitle.Text = s.RiskTitle;
        _lblFilterTitle.Text = s.Filter;
        _chkTR.Text = s.CtrTR;
        _chkDE.Text = s.CtrDE;

        _dgv.Columns["Date"]!.HeaderText = s.ColDate;
        _dgv.Columns["Day"]!.HeaderText = s.ColDay;
        _dgv.Columns["Country"]!.HeaderText = s.ColCountry;
        _dgv.Columns["Holiday"]!.HeaderText = s.ColName;
        _dgv.Columns["Weekend"]!.HeaderText = s.ColWeekend;
        _dgv.Columns["Impact"]!.HeaderText = s.ColImpact;

        _btnLangTR.BackColor = _lang == Lang.TR ? Color.FromArgb(0, 120, 215) : Color.FromArgb(50, 50, 56);
        _btnLangEN.BackColor = _lang == Lang.EN ? Color.FromArgb(0, 120, 215) : Color.FromArgb(50, 50, 56);
        _btnLangDE.BackColor = _lang == Lang.DE ? Color.FromArgb(0, 120, 215) : Color.FromArgb(50, 50, 56);
    }

    // ── Analysis ──

    private void RunAnalysis()
    {
        var s = GetStr(_lang);

        if (_dtpStart.Value.Date > _dtpEnd.Value.Date)
        {
            MessageBox.Show(s.ErrDate, "\u26A0", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var result = _service.Analyze(_dtpStart.Value, _dtpEnd.Value);

        // Apply country filter
        var activeCountries = _summaryPanel.Controls
            .OfType<CheckBox>()
            .Where(c => c.Checked && c.Tag is string)
            .Select(c => (string)c.Tag!)
            .ToHashSet();

        var filtered = result.Holidays
            .Where(a => activeCountries.Any(c => a.Holiday.Country.Contains(c)))
            .ToList();

        int weekdayFiltered = filtered.Count(a => !a.Holiday.IsWeekend);
        int weekendFiltered = filtered.Count(a => a.Holiday.IsWeekend);

        _lastResult = result;

        // Populate grid
        _dgv.Rows.Clear();
        foreach (var a in filtered)
        {
            var dayName = a.Holiday.Date.ToString("dddd", s.Locale);
            var rowIndex = _dgv.Rows.Add(
                a.Holiday.Date.ToString("dd.MM.yyyy"),
                dayName,
                a.Holiday.Country,
                a.Holiday.Name,
                a.Holiday.IsWeekend ? s.Yes : s.No,
                a.Impact.ToString());

            var row = _dgv.Rows[rowIndex];
            row.DefaultCellStyle.ForeColor = a.Impact switch
            {
                ImpactLevel.High => HighColor,
                ImpactLevel.Medium => MediumColor,
                _ => LowColor
            };
        }

        // Update summary
        _lblTotalHolidays.Text = filtered.Count.ToString();
        _lblWeekdayHolidays.Text = weekdayFiltered.ToString();
        _lblWeekdayHolidays.ForeColor = weekdayFiltered > 0 ? HighColor : LowColor;
        _lblWeekendHolidays.Text = weekendFiltered.ToString();
        _lblSprintDays.Text = string.Format(s.WorkDaysFmt, result.SprintWorkDays, result.SprintTotalDays);
        _lblCapacityLoss.Text = string.Format(s.Locale, s.CapacityFmt, result.CapacityLossPercent);
        _lblCapacityLoss.ForeColor = result.CapacityLossPercent > 15 ? HighColor
            : result.CapacityLossPercent > 5 ? MediumColor
            : LowColor;

        _lblRisk.Text = result.Risk switch
        {
            RiskLevel.High => string.Format(s.Locale, s.RiskHigh, result.WeekdayHolidayCount, result.CapacityLossPercent),
            RiskLevel.Medium => string.Format(s.Locale, s.RiskMedium, result.WeekdayHolidayCount, result.CapacityLossPercent),
            _ => s.RiskLow
        };
        _lblRisk.ForeColor = result.Risk switch
        {
            RiskLevel.High => HighColor,
            RiskLevel.Medium => MediumColor,
            _ => LowColor
        };
    }

    // ── CSV export ──

    private void ExportCsv()
    {
        var s = GetStr(_lang);

        if (_lastResult is null || _lastResult.Holidays.Count == 0)
        {
            MessageBox.Show(s.ErrNoData, "\u26A0", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = new SaveFileDialog
        {
            Filter = "CSV|*.csv",
            FileName = $"sprint_holidays_{_dtpStart.Value:yyyyMMdd}_{_dtpEnd.Value:yyyyMMdd}.csv"
        };

        if (dlg.ShowDialog() != DialogResult.OK) return;

        var sb = new StringBuilder();
        sb.AppendLine(s.CsvHeader);

        foreach (var a in _lastResult.Holidays)
        {
            var dayName = a.Holiday.Date.ToString("dddd", s.Locale);
            sb.AppendLine(string.Join(";",
                a.Holiday.Date.ToString("dd.MM.yyyy"),
                dayName,
                a.Holiday.Country,
                a.Holiday.Name,
                a.Holiday.IsWeekend ? s.Yes : s.No,
                a.Impact));
        }

        sb.AppendLine();
        sb.AppendLine($"{s.CsvRisk};{_lastResult.Risk}");
        sb.AppendLine($"{s.CsvTotalHol};{_lastResult.TotalHolidayCount}");
        sb.AppendLine($"{s.CsvWeekdayHol};{_lastResult.WeekdayHolidayCount}");
        sb.AppendLine(string.Format(s.Locale, "{0};{1:F1}%", s.CsvCapacity, _lastResult.CapacityLossPercent));

        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        MessageBox.Show(string.Format(s.Saved, dlg.FileName), "\u2705",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ── Grid factory ──

    private static DataGridView CreateGrid()
    {
        var dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.FromArgb(24, 24, 28),
            GridColor = Color.FromArgb(50, 50, 56),
            BorderStyle = BorderStyle.None,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 36,
            RowHeadersVisible = false,
            DefaultCellStyle =
            {
                BackColor = Color.FromArgb(24, 24, 28),
                ForeColor = Color.White,
                SelectionBackColor = Color.FromArgb(0, 80, 160),
                SelectionForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Padding = new Padding(4, 2, 4, 2)
            },
            ColumnHeadersDefaultCellStyle =
            {
                BackColor = Color.FromArgb(36, 36, 42),
                ForeColor = Color.FromArgb(180, 180, 190),
                Font = new Font("Segoe UI Semibold", 9),
                Alignment = DataGridViewContentAlignment.MiddleLeft
            },
            AlternatingRowsDefaultCellStyle =
            {
                BackColor = Color.FromArgb(30, 30, 36)
            }
        };

        dgv.RowTemplate.Height = 32;

        dgv.Columns.AddRange(
        [
            new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Tarih", FillWeight = 12 },
            new DataGridViewTextBoxColumn { Name = "Day", HeaderText = "G\u00FCn", FillWeight = 14 },
            new DataGridViewTextBoxColumn { Name = "Country", HeaderText = "\u00DClke", FillWeight = 8 },
            new DataGridViewTextBoxColumn { Name = "Holiday", HeaderText = "Tatil Ad\u0131", FillWeight = 30 },
            new DataGridViewTextBoxColumn { Name = "Weekend", HeaderText = "Hafta Sonu", FillWeight = 10 },
            new DataGridViewTextBoxColumn { Name = "Impact", HeaderText = "Etki Seviyesi", FillWeight = 12 }
        ]);

        dgv.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex == 5 && e.Value is string impact)
            {
                e.CellStyle!.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                e.CellStyle.ForeColor = impact switch
                {
                    "High" => Color.FromArgb(220, 50, 50),
                    "Medium" => Color.FromArgb(230, 160, 0),
                    _ => Color.FromArgb(0, 180, 80)
                };
            }
        };

        return dgv;
    }

    // ── UI helpers ──

    private static Label MakeLabel(string text, int x, int y) => new()
    {
        Text = text,
        Location = new Point(x, y),
        AutoSize = true,
        ForeColor = Color.FromArgb(180, 180, 190),
        Font = new Font("Segoe UI", 10)
    };

    private static Label MakeSectionLabel(string text, int x, int y) => new()
    {
        Text = text,
        Location = new Point(x, y),
        AutoSize = true,
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 11)
    };

    private static Label MakeDimLabel(string text, int x, int y) => new()
    {
        Text = text,
        Location = new Point(x, y),
        AutoSize = true,
        ForeColor = Color.FromArgb(140, 140, 150),
        Font = new Font("Segoe UI", 9)
    };

    private static Label MakeValueLabel(string text, int x, int y) => new()
    {
        Text = text,
        Location = new Point(x, y),
        AutoSize = true,
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 10)
    };

    private static Panel MakeSeparator(int y) => new()
    {
        Location = new Point(12, y),
        Size = new Size(240, 1),
        BackColor = Color.FromArgb(60, 60, 66)
    };

    private static DateTimePicker MakeDatePicker(int x, int y, DateTime value) => new()
    {
        Location = new Point(x, y),
        Width = 200,
        Format = DateTimePickerFormat.Short,
        Value = value,
        CalendarMonthBackground = Color.FromArgb(30, 30, 36),
        CalendarForeColor = Color.White
    };

    private static Button MakeLangButton(string text, int x, bool active) => new()
    {
        Text = text,
        Location = new Point(x, 13),
        Size = new Size(30, 34),
        BackColor = active ? Color.FromArgb(0, 120, 215) : Color.FromArgb(50, 50, 56),
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    // ── Localization ──

    private enum Lang { TR, EN, DE }

    private sealed class Str
    {
        public required string Title { get; init; }
        public required string LblStart { get; init; }
        public required string LblEnd { get; init; }
        public required string BtnAnalyze { get; init; }
        public required string BtnExport { get; init; }
        public required string Summary { get; init; }
        public required string TotalHol { get; init; }
        public required string WeekdayHol { get; init; }
        public required string WeekendHol { get; init; }
        public required string SprintDays { get; init; }
        public required string Capacity { get; init; }
        public required string RiskTitle { get; init; }
        public required string Filter { get; init; }
        public required string CtrTR { get; init; }
        public required string CtrDE { get; init; }
        public required string ColDate { get; init; }
        public required string ColDay { get; init; }
        public required string ColCountry { get; init; }
        public required string ColName { get; init; }
        public required string ColWeekend { get; init; }
        public required string ColImpact { get; init; }
        public required string Yes { get; init; }
        public required string No { get; init; }
        public required string WorkDaysFmt { get; init; }
        public required string CapacityFmt { get; init; }
        public required CultureInfo Locale { get; init; }
        public required string RiskHigh { get; init; }
        public required string RiskMedium { get; init; }
        public required string RiskLow { get; init; }
        public required string TipHigh { get; init; }
        public required string TipMedium { get; init; }
        public required string TipLow { get; init; }
        public required string ErrDate { get; init; }
        public required string ErrNoData { get; init; }
        public required string Saved { get; init; }
        public required string CsvHeader { get; init; }
        public required string CsvRisk { get; init; }
        public required string CsvTotalHol { get; init; }
        public required string CsvWeekdayHol { get; init; }
        public required string CsvCapacity { get; init; }
    }

    private static Str GetStr(Lang lang) => lang switch
    {
        Lang.EN => new()
        {
            Title = "Sprint Holiday Risk Analyzer",
            LblStart = "Sprint Start:",
            LblEnd = "Sprint End:",
            BtnAnalyze = "\u26A1  Analyze",
            BtnExport = "\uD83D\uDCE5  Export CSV",
            Summary = "\uD83D\uDCCB  SUMMARY",
            TotalHol = "Total Holidays:",
            WeekdayHol = "Weekday Holidays:",
            WeekendHol = "Weekend Holidays:",
            SprintDays = "Sprint Total Days:",
            Capacity = "Capacity Loss:",
            RiskTitle = "\u26A0  RISK LEVEL",
            Filter = "\uD83D\uDD0D  FILTER",
            CtrTR = "\uD83C\uDDF9\uD83C\uDDF7 Turkey",
            CtrDE = "\uD83C\uDDE9\uD83C\uDDEA Germany",
            ColDate = "Date",
            ColDay = "Day",
            ColCountry = "Country",
            ColName = "Holiday Name",
            ColWeekend = "Weekend",
            ColImpact = "Impact Level",
            Yes = "Yes",
            No = "No",
            WorkDaysFmt = "{0} work / {1} total",
            CapacityFmt = "{0:F1}%",
            Locale = CultureInfo.GetCultureInfo("en-US"),
            RiskHigh = "\u26A0\uFE0F High Risk: Sprint overlaps {0} weekday holiday(s) (capacity loss: {1:F1}%)",
            RiskMedium = "\u26A1 Medium Risk: {0} weekday holiday(s) in sprint (capacity loss: {1:F1}%)",
            RiskLow = "\u2705 Low Risk: No weekday holidays during sprint",
            TipHigh = "High Impact: Weekday holiday \u2014 directly reduces sprint capacity",
            TipMedium = "Medium Impact: Adjacent to weekend \u2014 extended weekend effect",
            TipLow = "Low Impact: Falls on weekend \u2014 does not affect sprint capacity",
            ErrDate = "Start date cannot be after end date.",
            ErrNoData = "No data to export. Run analysis first.",
            Saved = "File saved:\n{0}",
            CsvHeader = "Date;Day;Country;Holiday Name;Weekend;Impact Level",
            CsvRisk = "Risk Level",
            CsvTotalHol = "Total Holidays",
            CsvWeekdayHol = "Weekday Holidays",
            CsvCapacity = "Capacity Loss"
        },
        Lang.DE => new()
        {
            Title = "Sprint-Feiertagsrisikoanalyse",
            LblStart = "Sprint-Beginn:",
            LblEnd = "Sprint-Ende:",
            BtnAnalyze = "\u26A1  Analysieren",
            BtnExport = "\uD83D\uDCE5  CSV Export",
            Summary = "\uD83D\uDCCB  ZUSAMMENFASSUNG",
            TotalHol = "Gesamt Feiertage:",
            WeekdayHol = "Werktag-Feiertage:",
            WeekendHol = "Wochenend-Feiertage:",
            SprintDays = "Sprint Gesamttage:",
            Capacity = "Kapazit\u00E4tsverlust:",
            RiskTitle = "\u26A0  RISIKONIVEAU",
            Filter = "\uD83D\uDD0D  FILTER",
            CtrTR = "\uD83C\uDDF9\uD83C\uDDF7 T\u00FCrkei",
            CtrDE = "\uD83C\uDDE9\uD83C\uDDEA Deutschland",
            ColDate = "Datum",
            ColDay = "Tag",
            ColCountry = "Land",
            ColName = "Feiertagsname",
            ColWeekend = "Wochenende",
            ColImpact = "Auswirkung",
            Yes = "Ja",
            No = "Nein",
            WorkDaysFmt = "{0} Arbeit / {1} gesamt",
            CapacityFmt = "{0:F1} %",
            Locale = CultureInfo.GetCultureInfo("de-DE"),
            RiskHigh = "\u26A0\uFE0F Hohes Risiko: Sprint \u00FCberschneidet {0} Werktags-Feiertag(e) (Kapazit\u00E4tsverlust: {1:F1} %)",
            RiskMedium = "\u26A1 Mittleres Risiko: {0} Werktags-Feiertag(e) im Sprint (Kapazit\u00E4tsverlust: {1:F1} %)",
            RiskLow = "\u2705 Geringes Risiko: Keine Werktags-Feiertage w\u00E4hrend des Sprints",
            TipHigh = "Hohe Auswirkung: Werktags-Feiertag \u2014 reduziert Sprint-Kapazit\u00E4t direkt",
            TipMedium = "Mittlere Auswirkung: Neben Wochenende \u2014 verl\u00E4ngertes Wochenende",
            TipLow = "Geringe Auswirkung: F\u00E4llt auf Wochenende \u2014 beeinflusst Sprint-Kapazit\u00E4t nicht",
            ErrDate = "Startdatum darf nicht nach dem Enddatum liegen.",
            ErrNoData = "Keine Daten zum Exportieren. Bitte zuerst analysieren.",
            Saved = "Datei gespeichert:\n{0}",
            CsvHeader = "Datum;Tag;Land;Feiertagsname;Wochenende;Auswirkung",
            CsvRisk = "Risikoniveau",
            CsvTotalHol = "Gesamt Feiertage",
            CsvWeekdayHol = "Werktag-Feiertage",
            CsvCapacity = "Kapazit\u00E4tsverlust"
        },
        _ => new()
        {
            Title = "Sprint Tatil Risk Analizi",
            LblStart = "Sprint Ba\u015Flang\u0131\u00E7:",
            LblEnd = "Sprint Biti\u015F:",
            BtnAnalyze = "\u26A1  Analiz Et",
            BtnExport = "\uD83D\uDCE5  Export CSV",
            Summary = "\uD83D\uDCCB  \u00D6ZET",
            TotalHol = "Toplam Tatil:",
            WeekdayHol = "\u0130\u015F G\u00FCn\u00FC Tatili:",
            WeekendHol = "Hafta Sonu Tatili:",
            SprintDays = "Sprint Toplam G\u00FCn:",
            Capacity = "Kapasite Kayb\u0131:",
            RiskTitle = "\u26A0  R\u0130SK SEV\u0130YES\u0130",
            Filter = "\uD83D\uDD0D  F\u0130LTRE",
            CtrTR = "\uD83C\uDDF9\uD83C\uDDF7 T\u00FCrkiye",
            CtrDE = "\uD83C\uDDE9\uD83C\uDDEA Almanya",
            ColDate = "Tarih",
            ColDay = "G\u00FCn",
            ColCountry = "\u00DClke",
            ColName = "Tatil Ad\u0131",
            ColWeekend = "Hafta Sonu",
            ColImpact = "Etki Seviyesi",
            Yes = "Evet",
            No = "Hay\u0131r",
            WorkDaysFmt = "{0} i\u015F / {1} toplam",
            CapacityFmt = "%{0:F1}",
            Locale = CultureInfo.GetCultureInfo("tr-TR"),
            RiskHigh = "\u26A0\uFE0F Y\u00FCksek Risk: Sprint {0} i\u015F g\u00FCn\u00FC tatili ile \u00E7ak\u0131\u015F\u0131yor (kapasite kayb\u0131: %{1:F1})",
            RiskMedium = "\u26A1 Orta Risk: Sprint {0} i\u015F g\u00FCn\u00FC tatili i\u00E7eriyor (kapasite kayb\u0131: %{1:F1})",
            RiskLow = "\u2705 D\u00FC\u015F\u00FCk Risk: Sprint'te i\u015F g\u00FCn\u00FCne denk gelen tatil yok",
            TipHigh = "Y\u00FCksek Etki: \u0130\u015F g\u00FCn\u00FC tatili \u2014 sprint kapasitesini do\u011Frudan d\u00FC\u015F\u00FCr\u00FCr",
            TipMedium = "Orta Etki: Hafta sonuna biti\u015Fik tatil \u2014 uzun hafta sonu etkisi",
            TipLow = "D\u00FC\u015F\u00FCk Etki: Hafta sonuna denk \u2014 sprint kapasitesini etkilemez",
            ErrDate = "Ba\u015Flang\u0131\u00E7 tarihi biti\u015F tarihinden sonra olamaz.",
            ErrNoData = "D\u0131\u015Fa aktar\u0131lacak veri yok. \u00D6nce analiz \u00E7al\u0131\u015Ft\u0131r\u0131n.",
            Saved = "Dosya kaydedildi:\n{0}",
            CsvHeader = "Tarih;G\u00FCn;\u00DClke;Tatil Ad\u0131;Hafta Sonu;Etki Seviyesi",
            CsvRisk = "Risk Seviyesi",
            CsvTotalHol = "Toplam Tatil",
            CsvWeekdayHol = "\u0130\u015F G\u00FCn\u00FC Tatili",
            CsvCapacity = "Kapasite Kayb\u0131"
        }
    };
}
