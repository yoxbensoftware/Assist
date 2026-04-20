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

    // Layout constants — all relative to Panel client area
    private const int PL   = 24;   // padding left
    private const int CW   = 452;  // content width  (ClientSize.Width 500 - PL*2)
    private const int HW   = 218;  // half width for two-column rows
    private const int GAP  = 16;   // gap between two-column items
    private const int LH   = 18;   // label height
    private const int IH   = 27;   // input height
    private const int RGAP = 12;   // vertical gap between rows

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
        ClientSize      = new Size(500, 340);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        Font            = new Font("Consolas", 10);

        var main = new Panel { Dock = DockStyle.Fill };

        int y = 18; // top padding

        // ── Başlık ────────────────────────────────────────────────────────
        main.Controls.Add(SmallLbl("Başlık *", PL, y));
        y += LH + 3;
        _txtTitle = MakeTxt(PL, y, CW);
        main.Controls.Add(_txtTitle);
        y += IH + RGAP;

        // ── Açıklama ──────────────────────────────────────────────────────
        main.Controls.Add(SmallLbl("Açıklama", PL, y));
        y += LH + 3;
        _txtDesc = MakeTxt(PL, y, CW);
        main.Controls.Add(_txtDesc);
        y += IH + RGAP;

        // ── Kategori  |  Öncelik ──────────────────────────────────────────
        main.Controls.Add(SmallLbl("Kategori", PL, y));
        main.Controls.Add(SmallLbl("Öncelik",  PL + HW + GAP, y));
        y += LH + 3;

        _txtCategory = MakeTxt(PL, y, HW);
        main.Controls.Add(_txtCategory);

        _cmbPriority = new ComboBox
        {
            Location      = new Point(PL + HW + GAP, y),
            Width         = HW,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font          = new Font("Consolas", 10)
        };
        _cmbPriority.Items.AddRange(["  Düşük", "  Normal", "  Yüksek", "  Kritik"]);
        main.Controls.Add(_cmbPriority);
        y += IH + RGAP;

        // ── Bitiş Tarihi ──────────────────────────────────────────────────
        main.Controls.Add(SmallLbl("Bitiş Tarihi", PL, y));
        y += LH + 3;

        _chkDue = new CheckBox
        {
            Text      = "Tarih belirle",
            Location  = new Point(PL, y + 3),
            AutoSize  = true,
            Font      = new Font("Consolas", 10),
            FlatStyle = FlatStyle.Flat
        };
        _dtp = new DateTimePicker
        {
            Format   = DateTimePickerFormat.Short,
            Location = new Point(PL + 155, y + 1),
            Width    = 130,
            Value    = DateTime.Today.AddDays(1),
            Enabled  = false,
            Font     = new Font("Consolas", 10)
        };
        _chkDue.CheckedChanged += (_, _) => _dtp.Enabled = _chkDue.Checked;
        main.Controls.AddRange([_chkDue, _dtp]);
        y += IH + RGAP + 6;

        // ── Ayırıcı ───────────────────────────────────────────────────────
        main.Controls.Add(new Panel
        {
            Location  = new Point(PL, y),
            Size      = new Size(CW, 1),
            BackColor = Color.FromArgb(55, 55, 75)
        });
        y += 1 + 10;

        // ── Butonlar (sağa hizalı) ────────────────────────────────────────
        var btnSave   = MakeBtn("Kaydet");
        btnSave.Click += OnSave;

        var btnCancel = MakeBtn("İptal");
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        int rightEdge  = PL + CW;
        btnSave.Location   = new Point(rightEdge - btnSave.PreferredSize.Width - 2, y);
        btnCancel.Location = new Point(rightEdge - btnSave.PreferredSize.Width - btnCancel.PreferredSize.Width - 14, y);
        main.Controls.AddRange([btnCancel, btnSave]);

        Controls.Add(main);
        AcceptButton = btnSave;
        CancelButton = btnCancel;
    }

    private void LoadToUI()
    {
        _txtTitle.Text    = _item.Title;
        _txtDesc.Text     = _item.Description;
        _txtCategory.Text = _item.Category;
        _cmbPriority.SelectedIndex = (int)_item.Priority;
        if (_item.DueDate.HasValue)
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
        _item.DueDate     = _chkDue.Checked ? _dtp.Value.Date : (DateTime?)null;

        if (_isNew) TodoStore.Add(_item);
        else        TodoStore.Update(_item);

        DialogResult = DialogResult.OK;
        Close();
    }

    // ── Factory helpers ───────────────────────────────────────────────────
    private static Label SmallLbl(string text, int x, int y) => new()
    {
        Text      = text,
        Location  = new Point(x, y),
        AutoSize  = true,
        Font      = new Font("Consolas", 9),
        ForeColor = Color.FromArgb(150, 150, 195)
    };

    private static TextBox MakeTxt(int x, int y, int w) => new()
    {
        Location    = new Point(x, y),
        Width       = w,
        Height      = 27,
        Font        = new Font("Consolas", 10),
        BorderStyle = BorderStyle.FixedSingle
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
