namespace Assist.Forms.Productivity;

using System.Text;
using Assist.Models;
using Assist.Services;

/// <summary>
/// MDI child form for managing to-do tasks with colour-coded deadline rows.
/// </summary>
internal sealed class TodoForm : Form
{
    // ── Deadline colour palette ────────────────────────────────────────────
    private static readonly Color ClrOverdue   = Color.FromArgb(140, 28, 28);
    private static readonly Color ClrToday     = Color.FromArgb(155, 70,  0);
    private static readonly Color ClrTomorrow  = Color.FromArgb(120, 95,  5);
    private static readonly Color ClrThisWeek  = Color.FromArgb(  0, 72,108);
    private static readonly Color ClrLater     = Color.FromArgb( 28, 28, 52);
    private static readonly Color ClrDone      = Color.FromArgb( 36, 36, 40);

    private static readonly Color FgOverdue    = Color.FromArgb(255,100,100);
    private static readonly Color FgDone       = Color.FromArgb( 80, 80, 90);
    private static readonly Color FgNormal     = Color.FromArgb(220,220,240);

    private static readonly Font StrikeFont    = new("Consolas", 10, FontStyle.Strikeout);

    // ── Controls ──────────────────────────────────────────────────────────
    private readonly DataGridView _dgv;
    private readonly TextBox      _txtSearch;
    private readonly Label        _lblStatus;

    // Filter state
    private Button? _activeFilterBtn;
    private string  _currentFilter = "Tümü";
    private List<TodoItem> _view   = [];

    public TodoForm()
    {
        Text        = "Görev Takibi";
        Size        = new Size(980, 660);
        MinimumSize = new Size(780, 480);
        Font        = new Font("Consolas", 10);

        // ── Toolbar ───────────────────────────────────────────────────────
        // 3 columns: [200px search] | [Fill filter buttons] | [130px new btn]
        var toolbar = new TableLayoutPanel
        {
            Dock        = DockStyle.Top,
            Height      = 52,
            ColumnCount = 3,
            RowCount    = 1,
            BackColor   = Color.FromArgb(22, 22, 34),
            Padding     = new Padding(8, 10, 8, 10),
            Margin      = Padding.Empty,
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _txtSearch = new TextBox
        {
            Dock            = DockStyle.Fill,
            PlaceholderText = "Ara...",
            BorderStyle     = BorderStyle.FixedSingle,
            Font            = new Font("Consolas", 10),
            Margin          = new Padding(0, 0, 6, 0)
        };
        _txtSearch.TextChanged += (_, _) => ApplyView();

        // Filter buttons inside a FlowLayoutPanel so they never overlap
        var filterFlow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            BackColor     = Color.Transparent,
            Margin        = Padding.Empty,
            Padding       = Padding.Empty,
        };

        string[] filters = ["Tümü", "Bugün", "Bu Hafta", "Gecikmiş", "✅ Bitti"];
        Button? firstBtn = null;
        foreach (var f in filters)
        {
            var btn = MakeFilterBtn(f);
            btn.Margin = new Padding(0, 0, 4, 0);
            var cap = f;
            btn.Click += (_, _) => SetFilter(cap, btn);
            filterFlow.Controls.Add(btn);
            firstBtn ??= btn;
        }

        var btnNew = new Button
        {
            Dock      = DockStyle.Fill,
            Text      = "+ Yeni Görev",
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Consolas", 9),
            Cursor    = Cursors.Hand,
            Margin    = new Padding(4, 0, 0, 0)
        };
        btnNew.FlatAppearance.BorderSize = 1;
        btnNew.Click += OnNewTask;

        toolbar.Controls.Add(_txtSearch, 0, 0);
        toolbar.Controls.Add(filterFlow,  1, 0);
        toolbar.Controls.Add(btnNew,      2, 0);

        // ── DataGridView ─────────────────────────────────────────────────
        _dgv = new DataGridView
        {
            Dock                     = DockStyle.Fill,
            ReadOnly                 = true,
            AllowUserToAddRows       = false,
            AllowUserToDeleteRows    = false,
            AllowUserToResizeRows    = false,
            SelectionMode            = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect              = false,
            CellBorderStyle          = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
            RowTemplate              = { Height = 30 },
            GridColor                = Color.FromArgb(48, 48, 68),
            BorderStyle              = BorderStyle.None,
            EnableHeadersVisualStyles = false,
            AutoSizeColumnsMode      = DataGridViewAutoSizeColumnsMode.Fill,
            ScrollBars               = ScrollBars.Vertical,
            RowHeadersVisible        = false,
        };
        UITheme.Apply(_dgv);
        _dgv.ColumnHeadersDefaultCellStyle.Font           = new Font("Consolas", 9, FontStyle.Bold);
        _dgv.ColumnHeadersDefaultCellStyle.BackColor      = Color.FromArgb(28, 28, 44);
        _dgv.ColumnHeadersDefaultCellStyle.ForeColor      = Color.FromArgb(170, 170, 220);
        _dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(28, 28, 44);
        _dgv.ColumnHeadersHeight = 30;

        BuildColumns();

        _dgv.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditRow(e.RowIndex); };
        _dgv.KeyDown         += OnGridKey;
        _dgv.MouseDown       += OnMouseDown;

        var ctx = new ContextMenuStrip { Font = new Font("Consolas", 10) };
        ctx.Items.Add("✏️  Düzenle",            null, (_, _) => EditSelected());
        ctx.Items.Add("✅  Tamamla / Geri Al", null, (_, _) => ToggleSelected());
        ctx.Items.Add(new ToolStripSeparator());
        ctx.Items.Add("🗑️  Sil",               null, (_, _) => DeleteSelected());
        _dgv.ContextMenuStrip = ctx;

        // ── Legend bar ────────────────────────────────────────────────────
        var legend = BuildLegend();

        // ── Status bar ────────────────────────────────────────────────────
        _lblStatus = new Label
        {
            Dock      = DockStyle.Bottom,
            Height    = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(10, 0, 0, 0),
            BackColor = Color.FromArgb(22, 22, 34),
            Font      = new Font("Consolas", 9)
        };

        Controls.Add(_dgv);
        Controls.Add(legend);
        Controls.Add(_lblStatus);
        Controls.Add(toolbar);

        // Re-apply active-filter styling after UITheme.Apply (which runs before Load)
        Load += (_, _) =>
        {
            TodoStore.Load();
            if (_activeFilterBtn is not null)
                ApplyActiveStyle(_activeFilterBtn);
            ApplyView();
        };

        // Prime the active filter button in the constructor
        if (firstBtn is not null) SetFilter("Tümü", firstBtn);
    }

    // ── Column layout ──────────────────────────────────────────────────────
    private void BuildColumns()
    {
        AddFill("Title",    "Görev",      40, 200);
        AddFill("Category", "Kategori",   15, 100);
        AddFill("DueDate",  "Bitiş",      13, 100);
        AddFill("TimeLeft", "Kalan Süre", 18, 120);
        AddFill("Priority", "Öncelik",    14,  80);
    }

    private void AddFill(string name, string header, int weight, int minWidth) =>
        _dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name         = name,
            HeaderText   = header,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight   = weight,
            MinimumWidth = minWidth,
            ReadOnly     = true
        });

    // ── Legend bar ─────────────────────────────────────────────────────────
    private static Panel BuildLegend()
    {
        var bar = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 24,
            BackColor = Color.FromArgb(18, 18, 28),
        };

        (Color bg, string label)[] entries =
        [
            (ClrOverdue,  "Gecikmiş"), (ClrToday,   "Bugün"),
            (ClrTomorrow, "Yarın"),    (ClrThisWeek, "Bu Hafta"),
            (ClrLater,    "İleride"),  (ClrDone,     "Bitti"),
        ];

        int x = 10;
        foreach (var (bg, label) in entries)
        {
            bar.Controls.Add(new Panel
            {
                Location  = new Point(x, 5),
                Size      = new Size(13, 13),
                BackColor = bg
            });
            bar.Controls.Add(new Label
            {
                Text      = label,
                Location  = new Point(x + 16, 4),
                AutoSize  = true,
                ForeColor = Color.FromArgb(155, 155, 195),
                BackColor = Color.Transparent,
                Font      = new Font("Consolas", 8)
            });
            x += 82;
        }
        return bar;
    }

    // ── Grid population ────────────────────────────────────────────────────
    private void ApplyView()
    {
        var all    = TodoStore.Items.ToList();
        var search = _txtSearch.Text.Trim();

        IEnumerable<TodoItem> filtered = _currentFilter switch
        {
            "Bugün"    => all.Where(x => x.IsDueToday),
            "Bu Hafta" => all.Where(x => x.IsDueToday || x.IsDueTomorrow || x.IsDueThisWeek),
            "Gecikmiş" => all.Where(x => x.IsOverdue),
            "✅ Bitti" => all.Where(x => x.IsCompleted),
            _          => all
        };

        if (!string.IsNullOrWhiteSpace(search))
            filtered = filtered.Where(x =>
                x.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Category.Contains(search, StringComparison.OrdinalIgnoreCase));

        _view = [.. filtered
            .OrderBy(x => x.IsCompleted)
            .ThenBy(x => !x.IsOverdue)
            .ThenBy(x => x.DueDate.HasValue ? 0 : 1)
            .ThenBy(x => x.DueDate)
            .ThenByDescending(x => (int)x.Priority)];

        _dgv.SuspendLayout();
        _dgv.Rows.Clear();

        foreach (var item in _view)
        {
            int ri = _dgv.Rows.Add(
                item.Title,
                string.IsNullOrEmpty(item.Category) ? "—" : item.Category,
                item.DueDate?.ToString("dd.MM.yyyy") ?? "—",
                item.TimeLeftText,
                item.PriorityText);

            var row = _dgv.Rows[ri];
            row.Tag = item.Id;
            StyleRow(row, item);
        }

        _dgv.ResumeLayout();
        UpdateStatus(all);
    }

    private static void StyleRow(DataGridViewRow row, TodoItem item)
    {
        (Color bg, Color fg) = item switch
        {
            { IsCompleted: true }  => (ClrDone,    FgDone),
            { IsOverdue: true }    => (ClrOverdue,  FgOverdue),
            { IsDueToday: true }   => (ClrToday,    FgNormal),
            { IsDueTomorrow: true }=> (ClrTomorrow, FgNormal),
            { IsDueThisWeek: true }=> (ClrThisWeek, FgNormal),
            _                      => (ClrLater,    FgNormal),
        };

        var sel = Color.FromArgb(
            Math.Min(bg.R + 28, 255),
            Math.Min(bg.G + 28, 255),
            Math.Min(bg.B + 28, 255));

        row.DefaultCellStyle.BackColor          = bg;
        row.DefaultCellStyle.ForeColor          = fg;
        row.DefaultCellStyle.SelectionBackColor = sel;
        row.DefaultCellStyle.SelectionForeColor = fg;

        if (item.IsCompleted && row.Cells["Title"] is { } cell)
            cell.Style.Font = StrikeFont;
    }

    private void UpdateStatus(List<TodoItem> all)
    {
        int total   = all.Count;
        int done    = all.Count(x => x.IsCompleted);
        int pending = all.Count(x => !x.IsCompleted);
        int overdue = all.Count(x => x.IsOverdue);
        int today   = all.Count(x => x.IsDueToday);

        var sb = new StringBuilder();
        sb.Append($"  📊 Toplam: {total}   ✅ Tamamlandı: {done}   ⏳ Bekliyor: {pending}");
        if (overdue > 0) sb.Append($"   ⚠ Gecikmiş: {overdue}");
        if (today   > 0) sb.Append($"   🔥 Bugün: {today}");
        _lblStatus.Text = sb.ToString();
    }

    // ── Filter ─────────────────────────────────────────────────────────────
    private void SetFilter(string filter, Button btn)
    {
        _currentFilter = filter;

        // De-activate previous button (UITheme has already overridden colors to Surface2
        // so we just leave any previously active button as-is; only the new active one gets accent)
        if (_activeFilterBtn is not null && _activeFilterBtn != btn)
            _activeFilterBtn.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 85);

        ApplyActiveStyle(btn);
        _activeFilterBtn = btn;
        ApplyView();
    }

    private static void ApplyActiveStyle(Button btn)
    {
        var accent = UITheme.Palette.Accent;
        btn.BackColor = Color.FromArgb(
            Math.Max(accent.R - 70, 0),
            Math.Max(accent.G - 70, 0),
            Math.Max(accent.B - 70, 0));
        btn.ForeColor = accent;
        btn.FlatAppearance.BorderColor = accent;
    }

    // ── Actions ────────────────────────────────────────────────────────────
    private void OnNewTask(object? sender, EventArgs e)
    {
        using var dlg = new TodoEditForm();
        if (dlg.ShowDialog(this) == DialogResult.OK)
            ApplyView();
    }

    private void EditRow(int rowIdx)
    {
        if (rowIdx < 0 || rowIdx >= _view.Count) return;
        using var dlg = new TodoEditForm(_view[rowIdx]);
        if (dlg.ShowDialog(this) == DialogResult.OK)
            ApplyView();
    }

    private void EditSelected()   => EditRow(SelectedIndex());
    private void ToggleSelected()
    {
        var idx = SelectedIndex();
        if (idx < 0) return;
        TodoStore.ToggleComplete(_view[idx].Id);
        ApplyView();
    }

    private void DeleteSelected()
    {
        var idx = SelectedIndex();
        if (idx < 0) return;
        var item = _view[idx];
        if (MessageBox.Show($"'{item.Title}' silinsin mi?", "Sil",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            TodoStore.Delete(item.Id);
            ApplyView();
        }
    }

    private int SelectedIndex() =>
        _dgv.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault()?.Index ?? -1;

    // ── Events ─────────────────────────────────────────────────────────────
    private void OnGridKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
        else if (e.KeyCode is Keys.Enter or Keys.Space) { ToggleSelected(); e.Handled = true; }
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        var hit = _dgv.HitTest(e.X, e.Y);
        if (hit.RowIndex >= 0) _dgv.Rows[hit.RowIndex].Selected = true;
    }

    // ── Button factories ───────────────────────────────────────────────────
    private static Button MakeFilterBtn(string text)
    {
        var font = new Font("Consolas", 9);
        int w    = TextRenderer.MeasureText(text, font).Width + 22;
        var btn  = new Button
        {
            Text      = text,
            Width     = w,
            Height    = 28,
            FlatStyle = FlatStyle.Flat,
            Font      = font,
            Cursor    = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 1;
        return btn;
    }
}
