namespace Assist.Forms.Productivity;

using Assist.Models;
using Assist.Services;

/// <summary>
/// Modal dialog for adding or editing a to-do item.
/// Uses manual absolute layout to avoid any overlap issues.
/// </summary>
internal sealed class TodoEditForm : Form
{
    private readonly TodoItem _item;
    private readonly bool     _isNew;

    private TextBox        _txtTitle    = null!;
    private TextBox        _txtDesc     = null!;
    private TextBox        _txtCategory = null!;
    private ComboBox       _cmbPriority = null!;
    private CheckBox       _chkDue      = null!;
    private DateTimePicker _dtp         = null!;
    private CheckBox       _chkRecurring = null!;
    private NumericUpDown  _nudDay       = null!;

    public TodoEditForm(TodoItem? existing = null)
    {
        _isNew = existing is null;
        _item  = existing is not null ? Clone(existing) : new TodoItem();
        BuildUI();
        LoadToUI();
        UITheme.Apply(this);
    }

    private static TodoItem Clone(TodoItem s) => new()
    {
        Id = s.Id, Title = s.Title, Description = s.Description,
        Category = s.Category, Priority = s.Priority, DueDate = s.DueDate,
        IsCompleted = s.IsCompleted, CreatedAt = s.CreatedAt, CompletedAt = s.CompletedAt
    };

    private void BuildUI()
    {
        Text            = _isNew ? "Yeni Görev" : "Görevi Düzenle";
        ClientSize      = new Size(500, 362);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        Font            = new Font("Consolas", 10);

        // ── Ana grid (1 sütun, sabit satır yükseklikleri) ─────────────────
        var grid = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 10,
            Padding     = new Padding(20, 14, 20, 10),
            Margin      = Padding.Empty,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));  // 0 lbl Başlık
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // 1 txt Başlık
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));  // 2 lbl Açıklama
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // 3 txt Açıklama
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));  // 4 Kategori + Öncelik
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));  // 5 lbl Bitiş
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));  // 6 chk + dtp
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));  // 7 periyodik satır
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 14));  // 8 ayırıcı
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));  // 9 butonlar

        // 0 – Başlık label
        grid.Controls.Add(SmallLbl("Başlık *"), 0, 0);

        // 1 – Başlık input
        _txtTitle = new TextBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 10), BorderStyle = BorderStyle.FixedSingle };
        grid.Controls.Add(_txtTitle, 0, 1);

        // 2 – Açıklama label
        grid.Controls.Add(SmallLbl("Açıklama"), 0, 2);

        // 3 – Açıklama input
        _txtDesc = new TextBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 10), BorderStyle = BorderStyle.FixedSingle };
        grid.Controls.Add(_txtDesc, 0, 3);

        // 4 – Kategori | Öncelik (iç içe 2-sütun TLP)
        var catPri = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 2,
            RowCount    = 2,
            Margin      = Padding.Empty,
            Padding     = Padding.Empty,
        };
        catPri.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        catPri.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        catPri.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); // labels
        catPri.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // inputs

        catPri.Controls.Add(SmallLbl("Kategori"), 0, 0);
        catPri.Controls.Add(SmallLbl("Öncelik"),  1, 0);

        _txtCategory = new TextBox
        {
            Dock        = DockStyle.Fill,
            Font        = new Font("Consolas", 10),
            BorderStyle = BorderStyle.FixedSingle,
            Margin      = new Padding(0, 0, 10, 0)
        };
        _cmbPriority = new ComboBox
        {
            Dock          = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font          = new Font("Consolas", 10),
            Margin        = Padding.Empty
        };
        _cmbPriority.Items.AddRange(["  Düşük", "  Normal", "  Yüksek", "  Kritik"]);

        catPri.Controls.Add(_txtCategory, 0, 1);
        catPri.Controls.Add(_cmbPriority, 1, 1);
        grid.Controls.Add(catPri, 0, 4);

        // 5 – Bitiş Tarihi label
        grid.Controls.Add(SmallLbl("Bitiş Tarihi"), 0, 5);

        // 6 – Checkbox + DateTimePicker (FlowLayoutPanel — asla üst üste binmez)
        var dueRow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            Margin        = Padding.Empty,
            Padding       = Padding.Empty,
        };
        _chkDue = new CheckBox
        {
            Text      = "Tarih belirle",
            AutoSize  = true,
            Font      = new Font("Consolas", 10),
            FlatStyle = FlatStyle.Flat,
            Margin    = new Padding(0, 4, 14, 0)
        };
        _dtp = new DateTimePicker
        {
            Format  = DateTimePickerFormat.Short,
            Width   = 145,
            Value   = DateTime.Today.AddDays(1),
            Enabled = false,
            Font    = new Font("Consolas", 10),
            Margin  = new Padding(0, 2, 0, 0)
        };
        _chkDue.CheckedChanged += (_, _) => _dtp.Enabled = _chkDue.Checked;
        dueRow.Controls.AddRange([_chkDue, _dtp]);
        grid.Controls.Add(dueRow, 0, 6);

        // 7 – Periyodik satır
        var recurRow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            Margin        = Padding.Empty,
            Padding       = Padding.Empty,
        };
        _chkRecurring = new CheckBox
        {
            Text      = "Periyodik",
            AutoSize  = true,
            Font      = new Font("Consolas", 10),
            FlatStyle = FlatStyle.Flat,
            Margin    = new Padding(0, 4, 14, 0)
        };
        var lblPre = new Label { Text = "Her ayın", AutoSize = true, Font = new Font("Consolas", 10), Margin = new Padding(0, 6, 6, 0), Enabled = false };
        _nudDay    = new NumericUpDown { Minimum = 1, Maximum = 28, Value = 1, Width = 56, Font = new Font("Consolas", 10), Margin = new Padding(0, 3, 6, 0), Enabled = false };
        var lblSuf = new Label { Text = ". günü (aylık)", AutoSize = true, Font = new Font("Consolas", 10), Margin = new Padding(0, 6, 0, 0), Enabled = false };
        _chkRecurring.CheckedChanged += (_, _) =>
        {
            bool on = _chkRecurring.Checked;
            lblPre.Enabled  = on;
            _nudDay.Enabled = on;
            lblSuf.Enabled  = on;
            _chkDue.Enabled = !on;          // periyodik ise manuel tarih kilitle
            _dtp.Enabled    = !on && _chkDue.Checked;
        };
        recurRow.Controls.AddRange([_chkRecurring, lblPre, _nudDay, lblSuf]);
        grid.Controls.Add(recurRow, 0, 7);

        // 8 – Ayırıcı
        grid.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(55, 55, 75) }, 0, 8);

        // 9 – Butonlar
        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = Padding.Empty, Padding = Padding.Empty };
        var btnSave   = MakeBtn("  Kaydet  ");
        var btnCancel = MakeBtn("  İptal  ");
        btnSave.Click   += OnSave;
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        btnRow.Controls.Add(btnSave);
        btnRow.Controls.Add(btnCancel);
        grid.Controls.Add(btnRow, 0, 9);

        Controls.Add(grid);
        AcceptButton = btnSave;
        CancelButton = btnCancel;
    }

    private void LoadToUI()
    {
        _txtTitle.Text    = _item.Title;
        _txtDesc.Text     = _item.Description;
        _txtCategory.Text = _item.Category;
        _cmbPriority.SelectedIndex = (int)_item.Priority;

        if (_item.IsRecurring)
        {
            _chkRecurring.Checked = true;
            _nudDay.Value         = Math.Clamp(_item.RecurrenceDay, 1, 28);
        }
        else if (_item.DueDate.HasValue)
        {
            _chkDue.Checked = true;
            _dtp.Value      = _item.DueDate.Value;
            _dtp.Enabled    = true;
        }
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtTitle.Text))
        {
            MessageBox.Show("Başlık boş olamaz.", "Uyarı",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtTitle.Focus();
            return;
        }
        _item.Title       = _txtTitle.Text.Trim();
        _item.Description = _txtDesc.Text.Trim();
        _item.Category    = _txtCategory.Text.Trim();
        _item.Priority    = (TodoPriority)_cmbPriority.SelectedIndex;

        if (_chkRecurring.Checked)
        {
            _item.IsRecurring    = true;
            _item.RecurrenceType = RecurrenceType.Monthly;
            _item.RecurrenceDay  = (int)_nudDay.Value;
            // DueDate = bu ayki ödeme günü (geçmişse Load otomatik ilerletir)
            var today = DateTime.Today;
            int day   = (int)_nudDay.Value;
            _item.DueDate = day >= today.Day
                ? new DateTime(today.Year, today.Month, day)
                : new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(day - 1);
        }
        else
        {
            _item.IsRecurring    = false;
            _item.RecurrenceType = RecurrenceType.None;
            _item.DueDate        = _chkDue.Checked ? _dtp.Value.Date : (DateTime?)null;
        }

        if (_isNew) TodoStore.Add(_item);
        else        TodoStore.Update(_item);

        DialogResult = DialogResult.OK;
        Close();
    }

    // ── Factory helpers ───────────────────────────────────────────────────
    private static Label SmallLbl(string text) => new()
    {
        Text      = text,
        AutoSize  = true,
        Font      = new Font("Consolas", 9),
        ForeColor = Color.FromArgb(150, 150, 195),
        Margin    = new Padding(0, 2, 0, 1)
    };

    private static Button MakeBtn(string text)
    {
        var btn = new Button
        {
            Text      = text,
            AutoSize  = true,
            Padding   = new Padding(16, 4, 16, 4),
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Consolas", 10),
            Cursor    = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 1;
        return btn;
    }
}
