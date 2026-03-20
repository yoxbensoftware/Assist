using System.Text;
using Assist.Models;
using Assist.Services;

namespace Assist;

/// <summary>
/// Sprint Holiday Risk Analyzer — analyzes sprint date ranges against
/// Turkey + Germany public holidays and calculates capacity loss risk.
/// </summary>
public sealed class SprintHolidayAnalyzerForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);
    private static readonly Color HighColor = Color.FromArgb(220, 50, 50);
    private static readonly Color MediumColor = Color.FromArgb(230, 160, 0);
    private static readonly Color LowColor = Color.FromArgb(0, 180, 80);

    private readonly HolidayAnalyzerService _service = new();
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
        Text = "Sprint Holiday Risk Analyzer";
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

        var lblStart = MakeLabel("Sprint Başlangıç:", 14, 18);
        _dtpStart = MakeDatePicker(160, 14, new DateTime(2026, 5, 25));

        var lblEnd = MakeLabel("Sprint Bitiş:", 380, 18);
        _dtpEnd = MakeDatePicker(500, 14, new DateTime(2026, 6, 7));

        var btnAnalyze = new Button
        {
            Text = "🔍  Analiz Et",
            Location = new Point(700, 12),
            Size = new Size(140, 34),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnAnalyze.FlatAppearance.BorderSize = 0;
        btnAnalyze.Click += (_, _) => RunAnalysis();

        var btnExport = new Button
        {
            Text = "📥  Export CSV",
            Location = new Point(852, 12),
            Size = new Size(140, 34),
            BackColor = Color.FromArgb(50, 50, 56),
            ForeColor = Color.LightGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnExport.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 86);
        btnExport.Click += (_, _) => ExportCsv();

        inputPanel.Controls.AddRange([lblStart, _dtpStart, lblEnd, _dtpEnd, btnAnalyze, btnExport]);

        // ── Summary panel (right side) ──
        _summaryPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 280,
            BackColor = Color.FromArgb(30, 30, 36),
            Padding = new Padding(16, 12, 16, 12)
        };

        int sy = 14;
        _summaryPanel.Controls.Add(MakeSectionLabel("📊  ÖZET", 12, sy));

        sy += 40;
        _summaryPanel.Controls.Add(MakeDimLabel("Toplam Tatil:", 12, sy));
        _lblTotalHolidays = MakeValueLabel("—", 170, sy);
        _summaryPanel.Controls.Add(_lblTotalHolidays);

        sy += 30;
        _summaryPanel.Controls.Add(MakeDimLabel("İş Günü Tatili:", 12, sy));
        _lblWeekdayHolidays = MakeValueLabel("—", 170, sy);
        _summaryPanel.Controls.Add(_lblWeekdayHolidays);

        sy += 30;
        _summaryPanel.Controls.Add(MakeDimLabel("Hafta Sonu Tatili:", 12, sy));
        _lblWeekendHolidays = MakeValueLabel("—", 170, sy);
        _summaryPanel.Controls.Add(_lblWeekendHolidays);

        sy += 40;
        _summaryPanel.Controls.Add(MakeSeparator(sy));

        sy += 14;
        _summaryPanel.Controls.Add(MakeDimLabel("Sprint Toplam Gün:", 12, sy));
        _lblSprintDays = MakeValueLabel("—", 170, sy);
        _summaryPanel.Controls.Add(_lblSprintDays);

        sy += 30;
        _summaryPanel.Controls.Add(MakeDimLabel("Kapasite Kaybı:", 12, sy));
        _lblCapacityLoss = MakeValueLabel("—", 170, sy);
        _summaryPanel.Controls.Add(_lblCapacityLoss);

        sy += 50;
        _summaryPanel.Controls.Add(MakeSeparator(sy));

        sy += 14;
        _summaryPanel.Controls.Add(MakeSectionLabel("🎯  RİSK SEVİYESİ", 12, sy));

        sy += 36;
        _lblRisk = new Label
        {
            Text = "—",
            Location = new Point(12, sy),
            Size = new Size(240, 60),
            Font = new Font("Segoe UI", 11),
            ForeColor = Color.Gray,
            TextAlign = ContentAlignment.TopLeft
        };
        _summaryPanel.Controls.Add(_lblRisk);

        // ── Country filter checkboxes ──
        sy += 80;
        _summaryPanel.Controls.Add(MakeSeparator(sy));
        sy += 14;
        _summaryPanel.Controls.Add(MakeSectionLabel("🌍  FİLTRE", 12, sy));

        sy += 36;
        var chkTR = new CheckBox
        {
            Text = "🇹🇷 Türkiye",
            Location = new Point(12, sy),
            AutoSize = true,
            Checked = true,
            ForeColor = Color.LightGray,
            Font = new Font("Segoe UI", 9),
            Tag = "TR"
        };

        var chkDE = new CheckBox
        {
            Text = "🇩🇪 Almanya",
            Location = new Point(130, sy),
            AutoSize = true,
            Checked = true,
            ForeColor = Color.LightGray,
            Font = new Font("Segoe UI", 9),
            Tag = "DE"
        };

        chkTR.CheckedChanged += (_, _) => RunAnalysis();
        chkDE.CheckedChanged += (_, _) => RunAnalysis();

        _summaryPanel.Controls.AddRange([chkTR, chkDE]);

        // ── Data grid (center) ──
        _dgv = CreateGrid();

        // ── Layout assembly (order matters for Dock) ──
        Controls.Add(_dgv);
        Controls.Add(_summaryPanel);
        Controls.Add(inputPanel);

        // Initial analysis
        Load += (_, _) => RunAnalysis();
    }

    private void RunAnalysis()
    {
        if (_dtpStart.Value.Date > _dtpEnd.Value.Date)
        {
            MessageBox.Show("Başlangıç tarihi bitiş tarihinden sonra olamaz.",
                "Geçersiz Tarih", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        // Recalculate filtered counts
        int weekdayFiltered = filtered.Count(a => !a.Holiday.IsWeekend);
        int weekendFiltered = filtered.Count(a => a.Holiday.IsWeekend);

        _lastResult = result;

        // ── Populate grid ──
        _dgv.Rows.Clear();
        foreach (var a in filtered)
        {
            var rowIndex = _dgv.Rows.Add(
                a.Holiday.Date.ToString("dd.MM.yyyy"),
                a.Holiday.DayName,
                a.Holiday.Country,
                a.Holiday.Name,
                a.Holiday.IsWeekend ? "Evet" : "Hayır",
                a.Impact.ToString());

            var row = _dgv.Rows[rowIndex];
            row.DefaultCellStyle.ForeColor = a.Impact switch
            {
                ImpactLevel.High => HighColor,
                ImpactLevel.Medium => MediumColor,
                _ => LowColor
            };
        }

        // ── Update summary ──
        _lblTotalHolidays.Text = filtered.Count.ToString();
        _lblWeekdayHolidays.Text = weekdayFiltered.ToString();
        _lblWeekdayHolidays.ForeColor = weekdayFiltered > 0 ? HighColor : LowColor;
        _lblWeekendHolidays.Text = weekendFiltered.ToString();
        _lblSprintDays.Text = $"{result.SprintWorkDays} iş / {result.SprintTotalDays} toplam";
        _lblCapacityLoss.Text = $"%{result.CapacityLossPercent:F1}";
        _lblCapacityLoss.ForeColor = result.CapacityLossPercent > 15 ? HighColor
            : result.CapacityLossPercent > 5 ? MediumColor
            : LowColor;

        _lblRisk.Text = result.RiskMessage;
        _lblRisk.ForeColor = result.Risk switch
        {
            RiskLevel.High => HighColor,
            RiskLevel.Medium => MediumColor,
            _ => LowColor
        };
    }

    private void ExportCsv()
    {
        if (_lastResult is null || _lastResult.Holidays.Count == 0)
        {
            MessageBox.Show("Dışa aktarılacak veri yok. Önce analiz çalıştırın.",
                "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = new SaveFileDialog
        {
            Filter = "CSV Dosyası|*.csv",
            FileName = $"sprint_holidays_{_dtpStart.Value:yyyyMMdd}_{_dtpEnd.Value:yyyyMMdd}.csv"
        };

        if (dlg.ShowDialog() != DialogResult.OK) return;

        var sb = new StringBuilder();
        sb.AppendLine("Tarih;Gün;Ülke;Tatil Adı;Hafta Sonu;Etki Seviyesi");

        foreach (var a in _lastResult.Holidays)
        {
            sb.AppendLine(string.Join(";",
                a.Holiday.Date.ToString("dd.MM.yyyy"),
                a.Holiday.DayName,
                a.Holiday.Country,
                a.Holiday.Name,
                a.Holiday.IsWeekend ? "Evet" : "Hayır",
                a.Impact));
        }

        sb.AppendLine();
        sb.AppendLine($"Risk Seviyesi;{_lastResult.Risk}");
        sb.AppendLine($"Toplam Tatil;{_lastResult.TotalHolidayCount}");
        sb.AppendLine($"İş Günü Tatili;{_lastResult.WeekdayHolidayCount}");
        sb.AppendLine($"Kapasite Kaybı;%{_lastResult.CapacityLossPercent:F1}");

        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        MessageBox.Show($"Dosya kaydedildi:\n{dlg.FileName}", "Başarılı",
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
            new DataGridViewTextBoxColumn { Name = "Day", HeaderText = "Gün", FillWeight = 14 },
            new DataGridViewTextBoxColumn { Name = "Country", HeaderText = "Ülke", FillWeight = 8 },
            new DataGridViewTextBoxColumn { Name = "Holiday", HeaderText = "Tatil Adı", FillWeight = 30 },
            new DataGridViewTextBoxColumn { Name = "Weekend", HeaderText = "Hafta Sonu", FillWeight = 10 },
            new DataGridViewTextBoxColumn { Name = "Impact", HeaderText = "Etki Seviyesi", FillWeight = 12 }
        ]);

        // Tooltip for Impact column
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

        dgv.CellToolTipTextNeeded += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex == 5)
            {
                var val = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
                e.ToolTipText = val switch
                {
                    "High" => "Yüksek Etki: İş günü tatili — sprint kapasitesini doğrudan düşürür",
                    "Medium" => "Orta Etki: Hafta sonuna bitişik tatil — uzun hafta sonu etkisi",
                    _ => "Düşük Etki: Hafta sonuna denk — sprint kapasitesini etkilemez"
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
}
